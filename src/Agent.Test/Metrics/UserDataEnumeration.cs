namespace Loupe.Agent.Test.Metrics
{
    /// <summary>
    /// This is a standin for any user defined data enumeration (not in our normal libraries)
    /// </summary>
    public enum UserDataEnumeration
    {
        /// <summary>
        /// The experiment completed successfully
        /// </summary>
        Success,

        /// <summary>
        /// The experiment was not completed because the user canceled it
        /// </summary>
        Cancel,

        /// <summary>
        /// The experiment was terminated early because of a communication failure
        /// </summary>
        Quit
    }
}
