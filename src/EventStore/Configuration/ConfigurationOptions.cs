namespace Leira.EventSourcing.Configuration
{
    public class ConfigurationOptions
    {
        public readonly string cosmosDbUrl;
        public readonly string cosmosDbKey;
        public readonly string cosmosDbDatabaseName;
        public readonly string cosmosDbSnapshotsContainerName;
        public readonly string cosmosDbCommandsContainerName;
        public readonly string cosmosDbEventsContainerName;
        public readonly bool autoSnapshot;
        public readonly double aggregatesSlidingWindowCacheSeconds;
        public readonly int? databaseSharedTrhoughputForNewDatabase;
        public readonly int? containerDedicatedThroughputForNewContainers;

        public ConfigurationOptions(string cosmosDbUrl, 
                                    string cosmosDbKey, 
                                    string cosmosDbDatabaseName, 
                                    string cosmosDbSnapshotsContainerName = "snapshots", 
                                    string cosmosDbCommandsContainerName = "commands",
                                    string cosmosDbEventsContainerName = "events",
                                    bool autoSnapshot = true,
                                    double aggregatesSlidingWindowCacheSeconds = 10,
                                    int? databaseSharedTrhoughputForNewDatabase = 400,
                                    int? containerDedicatedThroughputForNewContainers = null)
        {
            this.cosmosDbUrl = cosmosDbUrl;
            this.cosmosDbKey = cosmosDbKey;
            this.cosmosDbDatabaseName = cosmosDbDatabaseName;
            this.cosmosDbSnapshotsContainerName = cosmosDbSnapshotsContainerName;
            this.cosmosDbCommandsContainerName = cosmosDbCommandsContainerName;
            this.cosmosDbEventsContainerName = cosmosDbEventsContainerName;
            this.autoSnapshot = autoSnapshot;
            this.aggregatesSlidingWindowCacheSeconds = aggregatesSlidingWindowCacheSeconds;
            this.databaseSharedTrhoughputForNewDatabase = databaseSharedTrhoughputForNewDatabase;
            this.containerDedicatedThroughputForNewContainers = containerDedicatedThroughputForNewContainers;
        }


       
    }
}
