using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("MapDiffBot GitHub Webhook")]
[assembly: AssemblyDescription("Listens for pull request events and comments before and after photos of changed .dmm map files")]

[assembly: ComVisible(false)]

[assembly: SuppressMessage("Microsoft.Design", "CA1020:AvoidNamespacesWithFewTypes", Scope = "namespace", Target = "MapDiffBot.WebHook")]

[assembly: Guid("9fa20795-ebeb-411f-bee2-df16a1ca4b84")]

[assembly: CLSCompliant(true)]
