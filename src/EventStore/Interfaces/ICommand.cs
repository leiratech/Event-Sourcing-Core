using Leira.CosmosDb.Abstracts;
using System;

namespace Leira.EventSourcing.Interfaces
{
    public interface ICommand : ICosmosDocument
    {
        public DateTime Time { get; set; }
    }
}
