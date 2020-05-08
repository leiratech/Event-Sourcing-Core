using Leira.EventSourcing.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Leira.EventSourcing
{
    public class CommandResult<TError>
    {
        public TError Error { get; set; }
        public IEnumerable<IEvent> Events { get; set; }
    }
}
