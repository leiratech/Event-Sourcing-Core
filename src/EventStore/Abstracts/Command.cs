using Leira.EventSourcing.Interfaces;
using System;

namespace Leira.EventSourcing.Abstracts
{
    public class Command : ICosmosDocument
    {
        public string  Id { get; set; }
        public string PartitionKey { get; set; }
        public DateTime Time { get; set; }
        public long AggregateVersionWhenExecuted { get; set; }
    }
}
