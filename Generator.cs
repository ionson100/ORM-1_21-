namespace ORM_1_21_
{
    /// <summary>
    /// Table primary key generator type
    /// </summary>
    public enum Generator
    {
        /// <summary>
        /// Database assignable as autoincrement field
        /// </summary>
        Native = 0,
        /// <summary>
        /// User assignable
        /// </summary>
        Assigned
    }
}