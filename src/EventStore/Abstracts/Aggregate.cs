using Leira.EventSourcing.Enums;
using Leira.EventSourcing.Interfaces;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Reflection;
using Leira.EventSourcing.Extensions;
using System.Net;

namespace Leira.EventSourcing.Abstracts
{
    public abstract class Aggregate<TError> : ICosmosDocument
    {
        public string Id { get; private set; }
        public string PartitionKey { get; private set; }

        private readonly ConsistencyRestriction consistencyOption;
        private readonly Container eventsContainer;
        private readonly Container snapshotsContainer;
        private readonly Container commandsContainer;
        private readonly string commandsPartitionKey;
        private readonly string eventsPartitionKey;
        private string lastSnapshotEtag;

        public long LastAppliedEventNumber { get; private set; }

        public Aggregate(string aggregateId, ConsistencyRestriction consistencyOption, Container eventsContainer, Container snapshotsContainer, Container commandsContainer)
        {
            this.Id = aggregateId;
            this.PartitionKey = aggregateId;
            this.commandsPartitionKey = $"{aggregateId}-commands";
            this.eventsPartitionKey = $"{aggregateId}-events";
            this.consistencyOption = consistencyOption;
            this.eventsContainer = eventsContainer;
            this.snapshotsContainer = snapshotsContainer;
            this.commandsContainer = commandsContainer;
        }

        public async Task<CommandResult<TError>> ExecuteAsync<TCommand>(TCommand command) where TCommand : Command
        {
            if (this is IAsyncCommandExecutor<TCommand, TError> commandExecutor)
            {
                await RehydrateAsync().ConfigureAwait(false);

                // Idempotency check
                if (command.Id != default && commandsContainer != null)
                {
                    var recievedCommand = await commandsContainer.GetItemViaStreamAsync<TCommand>(command.Id.ToString(), commandsPartitionKey).ConfigureAwait(false);
                    if (recievedCommand != null)
                    {
                        return new CommandResult<TError>(default(TError), null) { EventSourcingError = Error.IdempotencyFailure };
                    }
                }

                // Execute Command
                if (command.Time == default)
                {
                    command.Time = DateTime.UtcNow;
                    command.AggregateVersionWhenExecuted = LastAppliedEventNumber;
                }

                var result = await commandExecutor.ExecuteCommandAsync(command).ConfigureAwait(false);

                // Persist Events
                List<Event> appliedEvents = new List<Event>();
                foreach (var @event in result.Events)
                {
                    @event.CommandId = command.Id;
                    @event.Time = command.Time;
                    var res = await ((Task<Error>)this.GetType().GetMethod(nameof(ApplyAndPersistEventAsync), BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, new object[] { @event })).ConfigureAwait(false); //.MakeGenericMethod(@event.GetType())

                    if (res == Error.ConsistencyConflict)
                    {
                        // Failed, we undo the persisted ones.
                        foreach (var appliedEvent in appliedEvents)
                        {
                            await ReverseEventAsync(appliedEvent).ConfigureAwait(false);
                        }
                        await LoadSnapshotAsync().ConfigureAwait(false);
                        await RehydrateAsync().ConfigureAwait(false);
                        await SnapshotAsync().ConfigureAwait(false);
                        return new CommandResult<TError>(result.CommandError, null) { EventSourcingError = res };
                    }
                    else if (res == Error.None)
                    {
                        // This one persisted
                        appliedEvents.Add(@event);
                    }
                }
                await SnapshotAsync().ConfigureAwait(false);

                // Persist Command
                if (command.Id != default && commandsContainer != null)
                {
                    command.PartitionKey = commandsPartitionKey;

                    var statusCode = await commandsContainer.CreateItemViaStreamAsync(command, true).ConfigureAwait(false);

                    if ((int)statusCode.StatusCode < 200 || (int)statusCode.StatusCode >= 300)
                    {
                        // Reverting Events
                        foreach (var @event in result.Events)
                        {
                            await ReverseEventAsync(@event).ConfigureAwait(false);
                        }

                        await LoadSnapshotAsync().ConfigureAwait(false);
                        await RehydrateAsync().ConfigureAwait(false);
                        await SnapshotAsync().ConfigureAwait(false);
                        return new CommandResult<TError>(result.CommandError, result.Events.ToArray()) { EventSourcingError = Error.IdempotencyFailure };
                    }
                }

                return result;
            }

            return new CommandResult<TError>(default(TError), null) { EventSourcingError = Error.ConsistencyConflict };
        }

        public async Task ReverseEventAsync(Event @event)
        {
            @event.IsReversed = true;
            var result = await eventsContainer.UpsertItemViaStreamAsync(@event, true).ConfigureAwait(false);
        }

        private async Task<Error> ApplyAndPersistEventAsync(Event @event)
        {
            @event.SequenceNumber = ++LastAppliedEventNumber;
            await ((Task)this.GetType().BaseType.GetMethod(nameof(ApplyEventAsync), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(@event.GetType()).Invoke(this, new object[] { @event })).ConfigureAwait(false);
            int millisecondsToWaitOnFailure = 200;
            bool eventStorageStatus;
            @event.Id = Guid.NewGuid().ToString();
            @event.PartitionKey = eventsPartitionKey;
            do
            {
                eventStorageStatus = await StoreEventAsync(@event).ConfigureAwait(false);

                if (!eventStorageStatus && consistencyOption == ConsistencyRestriction.Loose)
                {
                    // Failed, we have to retry;
                    await Task.Delay(millisecondsToWaitOnFailure).ConfigureAwait(false);

                    // Preparing for next failure maybe?
                    millisecondsToWaitOnFailure = millisecondsToWaitOnFailure >= 1000 ? 1000 : millisecondsToWaitOnFailure * 2;

                    // Revert the state from Database
                    await LoadSnapshotAsync().ConfigureAwait(false);
                    await RehydrateAsync().ConfigureAwait(false);

                    // Change Event Number for next attempt.
                    @event.SequenceNumber = ++LastAppliedEventNumber;

                }
                else if (!eventStorageStatus && consistencyOption == ConsistencyRestriction.Strict)
                {
                    // failed we don't retry, the whole thing is invalid.
                    return Error.ConsistencyConflict;
                }

            } while (!eventStorageStatus && consistencyOption == ConsistencyRestriction.Loose);
            return Error.None;

        }

        private async Task ApplyEventAsync<TEvent>(TEvent @event) where TEvent : Event
        {
            if (!@event.IsReversed)
            {
                if (this is IAsyncEventHandler<TEvent> eventHandler)
                {
                    await eventHandler.ApplyEventAsync(@event).ConfigureAwait(false);
                    LastAppliedEventNumber = @event.SequenceNumber;
                }
                else
                {
                    throw new InvalidOperationException($"Aggregate does not implment IAsyncEventHandler<{typeof(TEvent).Name}>");
                }
            }
            else
            {
                LastAppliedEventNumber = @event.SequenceNumber;
            }
        }

        public async Task RehydrateAsync()
        {
            var results = await eventsContainer.RunQueryDefinitionViaStreamAsync<JObject>(new QueryDefinition("SELECT * FROM e where e.partitionKey = @partitionKey AND e.sequenceNumber > @lastAppliedEventNumber ORDER BY e._ts ASC").WithParameter("@partitionKey", eventsPartitionKey).WithParameter("@lastAppliedEventNumber", LastAppliedEventNumber)).ConfigureAwait(false);

            foreach (var @event in results)
            {
                var method = this.GetType().BaseType.GetMethod(nameof(ApplyEventAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).MakeGenericMethod(Type.GetType(@event.GetValue("$type").ToString()));
                await ((Task)method.Invoke(this, new object[] { @event.ToObject(Type.GetType(@event.GetValue("$type").ToString())) })).ConfigureAwait(false);
            }

        }

        private async Task<bool> StoreEventAsync(Event @event)
        {
            var storeResult = await eventsContainer.CreateItemViaStreamAsync(@event, true).ConfigureAwait(false);
            if (storeResult.StatusCode != HttpStatusCode.Created)
            {
                return false;
            }
            return true;
        }

        internal async Task LoadSnapshotAsync()
        {
            JObject snapshotJobject = await snapshotsContainer.GetItemViaStreamAsync<JObject>(Id, Id).ConfigureAwait(false);

            if (snapshotJobject != null)
            {
                var properties = this.GetType().GetProperties().ToList();
                properties.AddRange(this.GetType().BaseType.GetProperties());
                foreach (var item in snapshotJobject)
                {
                    if (item.Key == "_etag")
                    {
                        this.lastSnapshotEtag = item.Value.ToObject<string>();
                    }
                    else
                    {
                        // find the matching property
                        PropertyInfo prop = properties.Where(p => String.Equals(p.Name, item.Key, StringComparison.OrdinalIgnoreCase) && p.CanWrite).SingleOrDefault();
                        // set the property
                        prop?.SetValue(this, item.Value.ToObject(prop.PropertyType), BindingFlags.Public | BindingFlags.NonPublic, null, null, null);
                    }
                }
            }
        }

        private async Task SnapshotAsync()
        {
            (HttpStatusCode StatusCode, string eTag) result;
            do
            {
                result = await snapshotsContainer.UpsertItemViaStreamAsync(this, true, new ItemRequestOptions() { IfMatchEtag = lastSnapshotEtag }).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    await LoadSnapshotAsync().ConfigureAwait(false);
                    await RehydrateAsync().ConfigureAwait(false);
                }
            } while (result.StatusCode != HttpStatusCode.OK && result.StatusCode != HttpStatusCode.Created);
        }
    }
}
