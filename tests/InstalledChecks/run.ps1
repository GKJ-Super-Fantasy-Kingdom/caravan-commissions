param(
    [Parameter(Mandatory)] [string] $GameDir,
    [Parameter(Mandatory)] [string] $CoreDir,
    [string] $CheatDll,
    [string] $DailyDll
)
# Use a fresh Windows PowerShell 5.1 process per mod combination.
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'Super Fantasy Kingdom_Data\Managed'
$plugin = (Resolve-Path "$PSScriptRoot\..\..\bin\Release\netstandard2.1\CaravanCommissions.dll").Path
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
public static class ProbeResolver {
    public static void Use(params string[] directories) {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            foreach (var d in directories) {
                var file = Path.Combine(d, new AssemblyName(e.Name).Name + ".dll");
                if (File.Exists(file)) return Assembly.LoadFrom(file);
            }
            return null;
        };
    }
}
'@
[ProbeResolver]::Use($managed,$CoreDir)
$refs = @("$managed\Assembly-CSharp.dll","$managed\UnityEngine.CoreModule.dll","$CoreDir\BepInEx.dll","$CoreDir\0Harmony.dll",
    "$managed\UnityEngine.UI.dll","$managed\netstandard.dll",'System.Core.dll','System.Web.Extensions.dll')
Add-Type -Path "$PSScriptRoot\Probe.cs" -ReferencedAssemblies $refs
if ($CheatDll) { $CheatDll = (Resolve-Path -LiteralPath $CheatDll).Path }
if ($DailyDll) { $DailyDll = (Resolve-Path -LiteralPath $DailyDll).Path }
[CommissionProbe]::Run($plugin,$CheatDll,$DailyDll)
