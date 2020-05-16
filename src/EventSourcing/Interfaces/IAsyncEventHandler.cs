using Leira.EventSourcing.Abstracts;
using System.Threading.Tasks;

namespace Leira.EventSourcing.Interfaces
{
    public interface IAsyncEventHandler<TEvent> where TEvent : Event
    {
        public Task ApplyEventAsync(TEvent @event);
    }
}
