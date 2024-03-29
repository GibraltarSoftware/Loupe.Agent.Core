﻿using System;
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
        private readonly bool _IsWindows;

        /// <summary>
        /// Create a new default principal resolver
        /// </summary>
        public DefaultPrincipalResolver()
        {
            _IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        /// <inheritdoc />
        public bool TryResolveCurrentPrincipal(out IPrincipal principal)
        {
            principal = ClaimsPrincipal.Current; //returns Thread.CurrentPrincipal by default.

            if (principal == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                    principal = new WindowsPrincipal(windowsIdentity);
                }
            }

            return (principal != null);
        }
    }
}
