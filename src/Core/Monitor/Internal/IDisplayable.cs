namespace Gibraltar.Monitor.Internal
{
    /// <summary>
    /// A standard interface for ensuring an item can be displayed in user interfaces by providing an end user short caption and long description
    /// </summary>
    /// <remarks>Captions should be as short as feasible, typically less than 80 characters.  Descriptions can be considerably longer, but neither should
    /// have embedded formatting outside of normal carriage return and line feed.</remarks>
    internal interface IDisplayable
    {
        /// <summary>
        /// A short end-user display caption 
        /// </summary>
        string Caption { get; }

        /// <summary>
        /// An extended description without formatting.
        /// </summary>
        string Description { get; }
    }
}
