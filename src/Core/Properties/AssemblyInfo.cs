using System.Runtime.CompilerServices;


//Here's the problem:  Since we get signed, we can only expose internals to other strong named assemblies.
[assembly: InternalsVisibleTo("Loupe.Core.Test")]
