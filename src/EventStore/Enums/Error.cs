namespace Leira.EventSourcing.Enums
{
    public enum Error
    {
        /// <summary>
        /// Operation Successful.
        /// </summary>
        None = 0,
        /// <summary>
        /// A Command with the same Id was previously executed.
        /// </summary>
        IdempotencyFailure = 1,
        /// <summary>
        /// On strict Consistency, The Aggregate got new event(s) before the current event can be applied.
        /// </summary>
        ConsistencyConflict = 2,
       
    }
}
