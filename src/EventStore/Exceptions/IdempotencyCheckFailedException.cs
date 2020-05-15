using System;

namespace Leira.EventSourcing.Exceptions
{
    [Obsolete]
    public class IdempotencyCheckFailedException : Exception
    {
        public IdempotencyCheckFailedException() : base("Command Executed Already")
        { }
    }
}
