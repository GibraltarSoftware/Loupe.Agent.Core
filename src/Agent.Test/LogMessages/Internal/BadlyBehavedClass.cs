using System;
using System.Runtime.CompilerServices;

namespace Loupe.Agent.Test.LogMessages.Internal
{
    internal class BadlyBehavedClass
    {

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void MethodThatThrowsException()
        {
            throw new InvalidOperationException("This is just so we can check the call stack");
        }
    }
}
