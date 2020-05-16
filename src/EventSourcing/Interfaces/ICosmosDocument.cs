namespace Leira.EventSourcing.Interfaces
{
    internal interface ICosmosDocument
    {
        public string Id { get; }
        public string PartitionKey { get; }
    }
}
