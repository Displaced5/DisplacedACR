param([string]$Pattern = 'IRotation$|IRotationEventHandler$|IOpener$|PAction$|RotationMetadata|JobGaugeHelper|ActionHelper$|StatusHelper$|BattleCharaExtensions$|SkillExtensions$|PromeSettings$|SolverStatus$|TargetHelper$|GameData$', [string]$AssemblyName = 'PromeRotation')
$ErrorActionPreference = 'Stop'
$plugin = "$env:APPDATA\XIVLauncherCN\installedPlugins\PromeRotation\1.5.10.4"
$dalamud = "$env:APPDATA\XIVLauncherCN\addon\Hooks\26-10-21-01"
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
public static class HostAssemblyResolver {
    public static string[] Directories = Array.Empty<string>();
    public static Assembly Resolve(AssemblyLoadContext context, AssemblyName name) {
        if (name.Name.EndsWith(".resources", StringComparison.Ordinal)) return null;
        foreach (var dir in Directories) {
            var candidate = Path.Combine(dir, name.Name + ".dll");
            if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
        }
        return null;
    }
    public static void Attach() => AssemblyLoadContext.Default.Resolving += Resolve;
    public static void Detach() => AssemblyLoadContext.Default.Resolving -= Resolve;
}
'@
[HostAssemblyResolver]::Directories = @($plugin, $dalamud)
[HostAssemblyResolver]::Attach()
try {
    $assemblyPath = Join-Path $plugin ($AssemblyName + '.dll')
    if (-not (Test-Path -LiteralPath $assemblyPath)) { $assemblyPath = Join-Path $dalamud ($AssemblyName + '.dll') }
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)
    try { $types = $assembly.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $null -ne $_ }; $_.Exception.LoaderExceptions | Select-Object -First 4 | ForEach-Object { Write-Warning $_.Message } }
    foreach ($type in ($types | Where-Object { $_.FullName -match $Pattern } | Sort-Object FullName)) {
        "TYPE $($type.FullName) Public=$($type.IsPublic -or $type.IsNestedPublic)"
        foreach ($m in $type.GetMembers([System.Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly')) {
            $value = if ($m -is [System.Reflection.FieldInfo] -and $m.IsLiteral) { ' = ' + $m.GetRawConstantValue() } else { '' }
            "  $($m.MemberType): $m$value"
        }
    }
} finally { [HostAssemblyResolver]::Detach() }
