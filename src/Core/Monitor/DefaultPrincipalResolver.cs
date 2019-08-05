using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Principal;

namespace Gibraltar.Monitor
{
    /// <summary>
    /// A simple default principal resolver
    /// </summary>
    public class DefaultPrincipalResolver : IPrincipalResolver
    {
        private readonly bool IsWindows;

        public DefaultPrincipalResolver()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        /// <inheritdoc />
        public IPrincipal ResolveCurrentPrincipal()
        {
            var userPrincipal = ClaimsPrincipal.Current;

            if (userPrincipal == null && IsWindows)
            {
                //fall back to the windows identity..
                WindowsIdentity windowsIdentity;
                try
                {
                    windowsIdentity = WindowsIdentity.GetCurrent(); 
                }
                catch
                {
                    windowsIdentity = null;
                }

                if (windowsIdentity != null)
                {
                    userPrincipal = new WindowsPrincipal(windowsIdentity);
                }
            }


            return userPrincipal;
        }
    }
}
