namespace Loupe.Extensibility.Data
{
    /// <summary>
    /// A software promotion level used to distinguish different levels of maturity of application versions
    /// </summary>
    /// <remarks>Promotion levels typically indicate the role of a computer or application - such as development, certification, or production.</remarks>
    public interface IPromotionLevel
    {
        /// <summary>
        /// The unique name of this promotion level, used to refer to it in a session.
        /// </summary>
        string Name { get;}

        /// <summary>
        /// The order of display for this promotion level in the set of levels.
        /// </summary>
        int Sequence { get; }

        /// <summary>
        /// The display caption for this promotion level.
        /// </summary>
        /// <remarks>This value can be edited to change how the promotion level displays.  It defaults
        /// to the name.</remarks>
        string Caption { get; }

        /// <summary>
        /// Optional. A description of this promotion level.
        /// </summary>
        string Description { get; }
    }
}
