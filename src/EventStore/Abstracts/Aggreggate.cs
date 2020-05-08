using Leira.CosmosDb.Abstracts;
using Leira.EventSourcing.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Leira.EventSourcing.Abstracts
{
    public abstract class Aggreggate<TError> : ICosmosDocument where TError : Enum
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }

        public async Task<IEnumerable<IEvent>> ExecuteAsync<TCommand>(TCommand command, bool autoPersist = true) where TCommand : ICommand
        {
            if (this is IAsyncCommandExecutor<TCommand, TError> commandExecutor)
            {
                var result = await commandExecutor.ExecuteCommandAsync(command).ConfigureAwait(false);

                foreach (var @event in result.Events)
                {
                    await PersistAsync(@event).ConfigureAwait(false);
                }

                return events;
            }

            throw new InvalidOperationException($"Aggregate does not implment IAsyncCommandHandler<{typeof(TCommand).Name}>");
        }

        public async Task<bool> PersistAsync<TEvent>(TEvent @event) where TEvent : IEvent
        {
            if (this is IAsyncEventHandler<TEvent> eventHandler)
            {
                var result = await eventHandler.ApplyEvent(@event).ConfigureAwait(false);
                StoreEvent();

                return result;
            }
            return false;
        }
    }

    public abstract class Event : IEvent
    {
        public long SequenceNumber { get; set; }
        public string ClassType => this.GetType().FullName;
        public string Id { get; set; }
        public string PartitionKey { get; set; }
    }

    public abstract class Command : ICommand
    {
        public DateTime Time { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Id { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string PartitionKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }

}
