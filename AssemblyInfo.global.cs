using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

// Fuck strong name keys
[assembly: SuppressMessage("Microsoft.Design", "CA2210:AssembliesShouldHaveValidStrongNames")]

// Allow tests to anally probe
[assembly: InternalsVisibleTo("MapDiffBot.Tests")]

//You cannot one definition the version number
//Believe me, I've tried, the compiler hates it so much
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0.0")]
