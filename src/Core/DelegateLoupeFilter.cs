using System;
using Loupe.Core.IO.Serialization;
using Loupe.Core.Messaging;

namespace Loupe.Core
{
    /// <summary>
    /// Creates a Loupe Filter out of the provided delegate function
    /// </summary>
    /// <remarks>Adapts simple lambda functions and other delegates to be used as a Loupe Filter.
    /// </remarks>
    public class DelegateLoupeFilter : ILoupeFilter
    {
        private readonly Func<IMessengerPacket, bool> _func;
        private readonly Action<IMessengerPacket> _action;

        /// <summary>
        /// Create a new loupe filter for the specified action
        /// </summary>
        /// <param name="action">The action to apply to each packet</param>
        /// <remarks>The provided action can modify the packet but can't prevent the packet from being written;
        /// to have the option to cancel the packet specify a Func&lt;IMessengerPacket, bool&gt; instead.</remarks>
        public DelegateLoupeFilter(Action<IMessengerPacket> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            _action = action;
        }

        /// <summary>
        /// Create a new Loupe filter for the specified function
        /// </summary>
        /// <param name="func">The function to apply to each packet</param>
        /// <remarks>The function can cancel the packet from being written by returning false.  Return true to allow
        /// the (possibly modified) packet to be written.</remarks>
        public DelegateLoupeFilter(Func<IMessengerPacket, bool> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            _func = func;
        }

        /// <inheritdoc />
        public void Process(IMessengerPacket packet, ref bool cancel)
        {
            if (_func != null)
            {
                try
                {
                    cancel = !_func(packet); //note we are inverting the boolean - most people think of functions returning true for success.
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                }
            }
            else
            {
                try
                {
                    _action(packet);
                }
                catch (Exception ex)
                {
                    GC.KeepAlive(ex);
                }
            }
        }
    }
}
