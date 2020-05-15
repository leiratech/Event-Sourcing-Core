using Leira.EventSourcing.Abstracts;
using Leira.EventSourcing.Configuration;
using Leira.EventSourcing.CosmosDb;
using Leira.EventSourcing.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Leira.EventSourcing
{
    public class EventStoreClient<TError> where TError : struct
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ConfigurationOptions configurationOptions;
        MemoryCache aggregatesCache;
        private readonly bool cacheEnabled;
        private readonly ExtendedCosmosClient extendedCosmosClient;

        public Guid Id { get; set; }
        /// <summary>
        /// Creates new instance of EventStoreClient, from here you can access all your aggregates. 
        /// You can use dependency injection in all child classes, only singletons, scoped and transient are not supported.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public EventStoreClient(IServiceProvider serviceProvider, ConfigurationOptions configurationOptions)
        {
            this.Id = Guid.NewGuid();
            this.serviceProvider = serviceProvider;
            this.configurationOptions = configurationOptions;
            if (configurationOptions.aggregatesSlidingWindowCacheSeconds == 0)
            {
                cacheEnabled = false;
            }
            else if (configurationOptions.aggregatesSlidingWindowCacheSeconds < 0)
            {
                throw new InvalidOperationException("aggregatesObjectsCacheDurationSeconds must be a non-negative number");
            }
            else
            {
                aggregatesCache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(10) });
                cacheEnabled = true;
            }
            this.extendedCosmosClient = new ExtendedCosmosClient(configurationOptions.cosmosDbUrl, configurationOptions.cosmosDbKey, configurationOptions.databaseSharedTrhoughputForNewDatabase, configurationOptions.containerDedicatedThroughputForNewContainers);
        }

        /// <summary>
        /// Gets or creates an Aggregate of type TAggregate.
        /// New aggregates are created in memory until the first event is persisted. Once persisted, the Id is used in the database and cannot be used again.
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <param name="aggregateId">The Id of the aggregate, if the aggregate belongs to a different type, it may throw an exception</param>
        /// <param name="consistencyRestriction">Desired Consistency Restrictions, if caching is used and the aggregate is cached, this will be ignored.</param>
        /// <param name="customParameters">custom constructor parameters of the TAggregate supplied, if the supplied are Injected via depency injection, they will be picked up automatically.</param>
        /// <returns>Aggregate requested, fully hydrated to the latest event persisted.</returns>
        public async Task<TAggregate> GetOrCreateAggregateAsync<TAggregate>(string aggregateId, ConsistencyRestriction consistencyRestriction = ConsistencyRestriction.Loose, params object[] customParameters) where TAggregate : Aggregate<TError>
        {
            Array.Resize<object>(ref customParameters, (customParameters?.Length ?? 0) + 5);
            TAggregate instance;
            customParameters[customParameters.Length - 5] = aggregateId;
            customParameters[customParameters.Length - 4] = consistencyRestriction;
            customParameters[customParameters.Length - 3] = await extendedCosmosClient.GetContainerAsync(configurationOptions.cosmosDbDatabaseName, configurationOptions.cosmosDbEventsContainerName, "sequenceNumber").ConfigureAwait(false);
            customParameters[customParameters.Length - 2] = await extendedCosmosClient.GetContainerAsync(configurationOptions.cosmosDbDatabaseName, configurationOptions.cosmosDbSnapshotsContainerName).ConfigureAwait(false);
            customParameters[customParameters.Length - 1] = await extendedCosmosClient.GetContainerAsync(configurationOptions.cosmosDbDatabaseName, configurationOptions.cosmosDbCommandsContainerName).ConfigureAwait(false);
            if (cacheEnabled)
            {
                instance = aggregatesCache.GetOrCreate<TAggregate>(aggregateId, cacheEntry =>
                {
                    cacheEntry.SlidingExpiration = TimeSpan.FromSeconds(configurationOptions.aggregatesSlidingWindowCacheSeconds);
                    return ActivatorUtilities.CreateInstance<TAggregate>(serviceProvider, customParameters);
                });
            }
            else
            {
                instance = ActivatorUtilities.CreateInstance<TAggregate>(serviceProvider, customParameters);
            }

            await instance.LoadSnapshotAsync().ConfigureAwait(false);
            await instance.RehydrateAsync().ConfigureAwait(false);

            return instance;
        }


    }
}
