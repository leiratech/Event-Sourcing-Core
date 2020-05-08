using Leira.EventSourcing.Abstracts;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Leira.EventSourcing.Interfaces
{
    public interface IAsyncEventHandler<TEvent> where TEvent : IEvent
    {
        public Task<bool> ApplyEvent(TEvent @event);
    }
}
