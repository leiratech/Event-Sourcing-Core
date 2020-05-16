namespace Leira.EventSourcing.Enums
{
    public enum ConsistencyRestriction
    {
        /// <summary>
        /// Events Sequence doesn't matter, applicable to most applications.
        /// </summary>
        Loose = 0,

        /// <summary>
        /// Events Sequence Matters, if another event is written to the database before the current commit, the operation will fail.
        /// </summary>
        Strict = 1
    }
}
