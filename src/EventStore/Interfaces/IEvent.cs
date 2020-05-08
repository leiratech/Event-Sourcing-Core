using Leira.CosmosDb.Abstracts;
using System;

namespace Leira.EventSourcing.Interfaces
{
    public interface IEvent : ICosmosDocument
    {
        public long SequenceNumber { get; set; }
        public string ClassType { get; }
    }
}
