using Leira.EventSourcing.Interfaces;
using System;

namespace Leira.EventSourcing.Abstracts
{
    public class Event : ICosmosDocument
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }
        public long SequenceNumber { get; set; }
        public string CommandId { get; set; }
        public bool IsReversed { get; set; }
        public DateTime Time { get; set; }
    }
}
