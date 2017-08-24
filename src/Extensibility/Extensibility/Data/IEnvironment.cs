namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A set of related computers operating together to provide a set of applications
    /// </summary>
    /// <remarks>An environment typically denotes a hosting environment - like the set of 
    /// computers located in a particular data center or region of a data center.</remarks>
    public interface IEnvironment
    {
        /// <summary>
        /// The unique name of this environment, used to refer to it in a session.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The order of display for this environment in the set of environments
        /// </summary>
        int Sequence { get; }

        /// <summary>
        /// The display caption for this environment.
        /// </summary>
        /// <remarks>This value can be edited to change how the environment displays.  It defaults
        /// to the name.</remarks>
        string Caption { get; }

        /// <summary>
        /// Optional. A description of this environment.
        /// </summary>
        string Description { get; }
    }
}
