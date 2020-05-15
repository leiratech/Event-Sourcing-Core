using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Leira.EventSourcing.CosmosDb
{
    public class ExtendedCosmosClient
    {
        public readonly CosmosClient client;
        private const string partitionKeyFieldName = "partitionKey";
        private ConcurrentDictionary<string, Container> containersCache = new ConcurrentDictionary<string, Container>();

        public int? NewDatabaseSharedTrhouput { get; }
        public int? NewContainerDedicatedThroughput { get; }

        /// <summary>
        /// Creates a new instance of the CosmosDbExtendabilityClient using default CosmosClientOptions. \n
        /// MaxRetriesOnRateLimit = 10,
        /// CamelCaseProperties for document storage.
        /// Stores Null Values.
        /// Stores Data Indented (Great for debugging, might use extra space).
        /// </summary>
        /// <param name="cosmosAccountUrl"></param>
        /// <param name="authKey"></param>
        /// <param name="databaseSharedTrhouput">Shared Database Throughput when the requested database doesn't exist</param>
        /// <param name="containerDedicatedThroughput">Dedicated Container Throughput when the container doesn't exist</param>
        public ExtendedCosmosClient(string cosmosAccountUrl, string authKey, int? databaseSharedTrhouput = 400, int? containerDedicatedThroughput = null) : this(cosmosAccountUrl, authKey,
            new CosmosClientOptions()
            {

                MaxRetryAttemptsOnRateLimitedRequests = 10,
                ConnectionMode = ConnectionMode.Direct,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
                OpenTcpConnectionTimeout = TimeSpan.FromSeconds(30),
                SerializerOptions = new CosmosSerializationOptions()
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    IgnoreNullValues = false,
                    Indented = false,
                }
            }, databaseSharedTrhouput, containerDedicatedThroughput)
        {
            // Do nothing
        }

        /// <summary>
        /// Creates a new instance of the CosmosDbExtendabilityClient using supplied CosmosClientOptions
        /// </summary>
        /// <param name="cosmosAccountUrl">Url of CosmosDb Account</param>
        /// <param name="authKey">Auth Key of CosmosDb Account</param>
        /// <param name="clientOptions">Client Options</param>
        public ExtendedCosmosClient(string cosmosAccountUrl, string authKey, CosmosClientOptions clientOptions, int? databaseSharedTrhouput = 400, int? containerDedicatedThroughput = null)
        {
            client = new CosmosClient(cosmosAccountUrl, authKey, clientOptions);
            NewDatabaseSharedTrhouput = databaseSharedTrhouput;
            NewContainerDedicatedThroughput = containerDedicatedThroughput;
        }

        /// <summary>
        /// Gets the container of specific database, if exists in cache, it will be retrieved from there, if not, it 
        /// </summary>
        /// <param name="databaseName">The name of the Cosmos Database</param>
        /// <param name="containerName">The name of the Container within the database</param>
        /// <returns></returns>
        public async Task<Container> GetContainerAsync(string databaseName, string containerName, string uniqueKey = null)
        {
            return containersCache.GetOrAdd($"{databaseName}:{containerName}",
                await GetCosmosContainer(databaseName, containerName, partitionKeyFieldName, uniqueKey).ConfigureAwait(false)
            );
        }


        private async Task<Container> GetCosmosContainer(string databaseName, string containerName, string partitionKeyFieldName, string uniqueKey = null)
        {
            if (!string.IsNullOrEmpty(partitionKeyFieldName))
            {
                await client.CreateDatabaseIfNotExistsAsync(databaseName, NewDatabaseSharedTrhouput).ConfigureAwait(false);
                var containerDefinition = client.GetDatabase(databaseName).DefineContainer(containerName, $"/{partitionKeyFieldName}");
                if (!string.IsNullOrWhiteSpace(uniqueKey))
                {
                    containerDefinition.WithUniqueKey().Path($"/{uniqueKey}").Attach();
                }

                await containerDefinition.CreateIfNotExistsAsync(NewContainerDedicatedThroughput).ConfigureAwait(false);
            }
            return client.GetContainer(databaseName, containerName);
        }

    }
}