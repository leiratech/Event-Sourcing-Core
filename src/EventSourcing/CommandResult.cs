using Leira.EventSourcing.Abstracts;
using Leira.EventSourcing.Enums;
using System.Collections.Generic;

namespace Leira.EventSourcing
{
    public class CommandResult<TError>
    {
        public TError CommandError { get; set; }
        public IEnumerable<Event> Events { get; set; }
        public Error EventSourcingError { get; internal set; }
        public CommandResult(TError error, params Event[] events )
        {
            CommandError = error;
            Events = events;
        }


    }
}
