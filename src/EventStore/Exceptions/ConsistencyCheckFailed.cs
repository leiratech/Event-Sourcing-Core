using System;

namespace Leira.EventSourcing.Exceptions
{
    [Obsolete]
    public class ConsistencyCheckFailed : Exception
    {
        public ConsistencyCheckFailed() : base("Aggregate changed by another instance")
        { }
    }
}
