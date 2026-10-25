param(
    [string]$DalamudDir = "$env:APPDATA\XIVLauncherCN\addon\Hooks\26-10-21-01",
    [string]$PromeDir = "$env:APPDATA\XIVLauncherCN\installedPlugins\PromeRotation\1.5.10.4",
    [string]$RepositoryUrl = 'https://github.com/Displaced5/Displaced',
    [switch]$UseSdk,
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = Join-Path (Split-Path $PSScriptRoot -Parent) '.tools\dotnet\dotnet.exe'
if ($dotnetCommand) {
    $installedSdks = & $dotnetCommand.Source --list-sdks
    if ($installedSdks -match '^10\.') { $dotnetExe = $dotnetCommand.Source }
}
if (-not (Test-Path -LiteralPath $dotnetExe)) { throw '.NET 10 SDK 未找到，请安装并加入 PATH。' }
$hasHost = (Test-Path -LiteralPath (Join-Path $DalamudDir 'Dalamud.dll')) -and
    (Test-Path -LiteralPath (Join-Path $PromeDir 'PromeRotation.dll'))
if (-not $UseSdk -and -not $hasHost) { throw '宿主依赖不存在。请指定 DalamudDir/PromeDir，或使用 -UseSdk。' }
if ($Install -and -not $hasHost) { throw '安装前必须提供实际宿主以校验发布包。' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$useInstalledHost = if ($UseSdk) { 'false' } else { 'true' }
$buildProperties = @("-p:UseInstalledHost=$useInstalledHost", "-p:DalamudDir=$DalamudDir", "-p:PromeDir=$PromeDir")
& $dotnetExe build (Join-Path $PSScriptRoot 'ViperACR.csproj') -c Release --nologo @buildProperties
if ($LASTEXITCODE -ne 0) { throw 'ACR 编译失败' }
& $dotnetExe run --project (Join-Path $PSScriptRoot 'tests\ViperACR.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw '离线循环检查失败' }
& $dotnetExe build (Join-Path $PSScriptRoot 'tools\ScriptCheck\ScriptCheck.csproj') -c Release --nologo @buildProperties
if ($LASTEXITCODE -ne 0) { throw '独立起手脚本编译失败' }
$output = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows'
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $output 'ViperACR.dll')).ProductVersion.Split('+')[0]
[xml]$props = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw
$constant = [regex]::Match((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Core\ReleaseInfo.cs') -Raw), 'Version\s*=\s*"([^"]+)"').Groups[1].Value
if ($version -ne [string]$props.Project.PropertyGroup.Version -or $version -ne $constant) { throw '程序集、元数据与构建版本不一致。' }
$referenceDll = Join-Path $PromeDir 'PromeRotation.dll'
if ($UseSdk) {
    $assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
    $sdk = $assets.libraries.PSObject.Properties | Where-Object Name -Like 'PromeRotation.SDK.CNGL/*' | Select-Object -First 1
    if (-not $sdk) { throw '无法解析公开 SDK。' }
    $referenceDll = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $sdkFolder = Join-Path $folder $sdk.Value.path
        if (Test-Path -LiteralPath $sdkFolder) {
            $referenceDll = Get-ChildItem -LiteralPath (Join-Path $sdkFolder 'ref') -Filter PromeRotation.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
            if ($referenceDll) { break }
        }
    }
    if (-not $referenceDll) { throw '找不到 SDK 的 PromeRotation 引用程序集。' }
}
$referenceVersion = [System.Reflection.AssemblyName]::GetAssemblyName($referenceDll).Version.ToString()
$package = Join-Path $PSScriptRoot 'dist\ViperACR'
New-Item -ItemType Directory -Path $package -Force | Out-Null
foreach ($name in @('ViperACR.dll', 'ViperACR.deps.json')) {
    Copy-Item -LiteralPath (Join-Path $output $name) -Destination $package -Force
}
foreach ($name in @('README.md', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'scripts\蝰蛇标准起手.txt') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor\ErosUI\LICENSE') -Destination (Join-Path $package 'ErosUI-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor\ErosUI\NOTICE.md') -Destination (Join-Path $package 'ErosUI-NOTICE.md') -Force
if ($hasHost) {
    $env:VIPER_DALAMUD_DIR = $DalamudDir
    $env:VIPER_PROME_DIR = $PromeDir
    & $dotnetExe run --project (Join-Path $PSScriptRoot 'tools\HostTools\HostTools.csproj') -c Release '-p:UseInstalledHost=true' "-p:DalamudDir=$DalamudDir" -- --validate-package $package
    if ($LASTEXITCODE -ne 0) { throw '实际 PR 加载器／包元数据校验失败' }
    & $dotnetExe run --no-build --project (Join-Path $PSScriptRoot 'tools\HostTools\HostTools.csproj') -c Release -- --validate-opener-script (Join-Path $PSScriptRoot 'tools\ScriptCheck\bin\Release\net10.0-windows\ViperOpener.ScriptCheck.dll')
    if ($LASTEXITCODE -ne 0) { throw '独立起手脚本契约校验失败' }
} else {
    Write-Host '未提供实际宿主；已完成 SDK 编译和纯逻辑检查，未运行宿主加载器验证。'
}
$dllHash = (Get-FileHash -LiteralPath (Join-Path $package 'ViperACR.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $package 'SHA256.txt') -Value "$dllHash  ViperACR.dll" -Encoding utf8
$releaseFiles = @('ViperACR.dll', 'ViperACR.deps.json', 'README.md', 'THIRD_PARTY_NOTICES.md', '蝰蛇标准起手.txt', 'ErosUI-LICENSE.txt', 'ErosUI-NOTICE.md', 'SHA256.txt')
$archive = Join-Path $PSScriptRoot 'dist\Displaced.zip'
Compress-Archive -LiteralPath ($releaseFiles | ForEach-Object { Join-Path $package $_ }) -DestinationPath $archive -Force
$repository = $RepositoryUrl.TrimEnd('/')
if ($repository -notmatch '^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'RepositoryUrl 必须是 GitHub HTTPS 仓库地址。' }
$manifest = [ordered]@{
    author = 'ViperACR'
    version = $version
    description = 'Displaced VPR - 蝰蛇剑士 PVE 自动循环，支持日随与高难模式。'
    supportedJobs = @([ordered]@{ job = 'VPR'; contentScope = 'All' })
    apiVersion = 15
    referencePromeVersion = $referenceVersion
    downloadUrl = "$repository/releases/latest/download/Displaced.zip"
    sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceRepositoryUrl = $repository
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'dist\repo.json') -Encoding utf8
Write-Host "发布包：$archive；版本 $version；PR 引用 $referenceVersion"
if ($Install) {
    $target = Join-Path $env:APPDATA 'XIVLauncherCN\pluginConfigs\PromeRotation\ACR\ViperACR'
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $existing = Join-Path $target 'ViperACR.dll'
    if (Test-Path -LiteralPath $existing) {
        Copy-Item -LiteralPath $existing -Destination ($existing + '.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    }
    foreach ($name in $releaseFiles) { Copy-Item -LiteralPath (Join-Path $package $name) -Destination $target -Force }
    & $dotnetExe run --no-build --project (Join-Path $PSScriptRoot 'tools\HostTools\HostTools.csproj') -c Release -- --validate-package $target
    if ($LASTEXITCODE -ne 0) { throw '已安装目录 PR 校验失败' }
    Write-Host '已更新本机 ACR。请在 PR 中刷新并选择 Displaced VPR。'
}
