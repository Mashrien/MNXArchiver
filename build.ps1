param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$Clean,
    [switch]$Test,
    [switch]$PackagesOnly
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Build Windows Native AOT on Windows with the .NET 10 SDK and Visual Studio C++ build tools.' }
$mnxRoot = $PSScriptRoot
$mnxParent = Split-Path -Parent $mnxRoot
$mnxComponent = 'mnx-gui'
$mnxPrimary = 'windows/MNXFile.WinForms.csproj'
if ($PackagesOnly -and $mnxComponent -ne 'mnxfile') { throw '-PackagesOnly applies to MNXFile only.' }
$mnxSdk = & dotnet --version
if ($LASTEXITCODE -ne 0 -or [int]($mnxSdk.Split('.')[0]) -lt 10) { throw '.NET SDK 10 or newer is required.' }
if (-not $env:MNXBuildRoot) { $env:MNXBuildRoot = Join-Path $mnxParent "mnx_clutter/build/$mnxComponent" }
if (-not $env:MNXReleaseRoot) { $env:MNXReleaseRoot = Join-Path $mnxParent "MNX_Builds/$mnxComponent" }
New-Item -ItemType Directory -Force -Path $env:MNXBuildRoot, $env:MNXReleaseRoot | Out-Null
$mnxLock = [IO.File]::Open((Join-Path $env:MNXBuildRoot 'release.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$mnxOriginalBuildNumber = $env:MNXBuildNumber
try {
    $env:MSBUILDDISABLENODEREUSE = '1'
    if (-not $env:MNXBuildNumber) {
        $mnxReserved = & dotnet msbuild (Join-Path $mnxRoot $mnxPrimary) -nologo -verbosity:quiet -target:ReserveMNXBuildNumber -getProperty:MNXBuildNumber
        if ($LASTEXITCODE -ne 0) { throw 'Build-number reservation failed.' }
        $env:MNXBuildNumber = ($mnxReserved -join "`n").Trim()
    }
    if ($env:MNXBuildNumber -notmatch '^\d+$' -or [int]$env:MNXBuildNumber -lt 1 -or [int]$env:MNXBuildNumber -gt 65534) { throw 'MNXBuildNumber must be 1..65534.' }
    $mnxOutput = Join-Path $env:MNXReleaseRoot $Runtime
    New-Item -ItemType Directory -Force -Path $mnxOutput | Out-Null
    $mnxFeed = $env:MNXPackageFeed
    if (-not $mnxFeed) { $mnxFeed = Join-Path $mnxParent 'MNX_Builds/mnxfile/packages' }
    $mnxUseLocalFeed = $env:MNXPackageFeed -or (Test-Path -LiteralPath $mnxFeed -PathType Container)
    $mnxFeed = [Security.SecurityElement]::Escape($mnxFeed)
    $mnxConfig = Join-Path $env:MNXBuildRoot 'nuget.config'
    $mnxSources = '<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
    if ($mnxUseLocalFeed) { $mnxSources = '<add key="MNX client feed" value="' + $mnxFeed + '" />' + $mnxSources }
    [IO.File]::WriteAllText($mnxConfig, '<configuration><packageSources><clear />' + $mnxSources + '</packageSources></configuration>')
    $mnxOptions = @('-c', 'Release', '-p:UseSharedCompilation=false', "-p:RestoreConfigFile=$mnxConfig")
    function Invoke-MNXDotnet {
        & dotnet @args
        if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
    }
    Write-Host "Building $mnxComponent release $env:MNXBuildNumber for $Runtime"
    if ($Clean) {
        # Remove both RID-specific publish intermediates and RID-independent package builds.
        foreach ($mnxGeneratedFolder in @('bin', 'obj')) {
            $mnxGeneratedPath = Join-Path $env:MNXBuildRoot $mnxGeneratedFolder
            if (Test-Path -LiteralPath $mnxGeneratedPath) {
                Remove-Item -LiteralPath $mnxGeneratedPath -Recurse -Force
                }
            }
        foreach ($mnxCleanProject in @('windows/MNXFile.WinForms.csproj')) {
            Invoke-MNXDotnet restore (Join-Path $mnxRoot $mnxCleanProject) -r $Runtime "-p:RestoreConfigFile=$mnxConfig"
            Invoke-MNXDotnet clean (Join-Path $mnxRoot $mnxCleanProject) -c Release -r $Runtime -nologo
        }
    }

    if ($PackagesOnly) { throw '-PackagesOnly applies to MNXFile only.' }
    Invoke-MNXDotnet publish (Join-Path $mnxRoot 'windows/MNXFile.WinForms.csproj') @mnxOptions -r $Runtime --self-contained true -p:PublishAot=false -p:PublishTrimmed=false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:CopyOutputSymbolsToPublishDirectory=false -o $mnxOutput
    Copy-Item (Join-Path $mnxRoot 'src/Icons/LICENSE.txt') (Join-Path $mnxOutput 'MNXFile.Icons.LICENSE.txt')
    [IO.File]::WriteAllText((Join-Path $mnxOutput 'DEPENDENCIES.txt'), "Windows 10 x64 or later. Install MNXFile.Native.dll using MNXFile/scripts/install-runtime.ps1. The .NET runtime is bundled; GTK is not required. This WinForms GUI is self-contained, not NativeAOT.")
    if ($Test) {
        $mnxSmokeDirectory = Join-Path $env:MNXBuildRoot ('tests/' + $Runtime + '/' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $mnxSmokeDirectory -Force | Out-Null
        $mnxGuiProcess = Start-Process -FilePath (Join-Path $mnxOutput 'mnx-gui.exe') -ArgumentList ('--smoke-test "' + $mnxSmokeDirectory + '"') -PassThru
        if (-not $mnxGuiProcess.WaitForExit(300000)) {
            $mnxGuiProcess.Kill()
            throw ('WinForms smoke test timed out. Inspect ' + $mnxSmokeDirectory)
            }
        if ($mnxGuiProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $mnxSmokeDirectory 'PASS.txt'))) {
            if (Test-Path -LiteralPath (Join-Path $mnxSmokeDirectory 'FAIL.txt')) {
                Get-Content -LiteralPath (Join-Path $mnxSmokeDirectory 'FAIL.txt') | Write-Host
                }
            throw ('WinForms runtime smoke failed. Inspect ' + $mnxSmokeDirectory)
            }
        Get-Content -LiteralPath (Join-Path $mnxSmokeDirectory 'PASS.txt') | Write-Host
        Write-Host ('WinForms smoke evidence: ' + $mnxSmokeDirectory)
        }

    $mnxSymbols = Join-Path $env:MNXBuildRoot "symbols/$Runtime"
    New-Item -ItemType Directory -Force -Path $mnxSymbols | Out-Null
    Get-ChildItem -LiteralPath $mnxOutput -File | Where-Object { $_.Extension -in '.pdb', '.dbg' } | Move-Item -Destination $mnxSymbols -Force
    Write-Host "Release output: $env:MNXReleaseRoot"
} finally {
    $env:MNXBuildNumber = $mnxOriginalBuildNumber
    $mnxLock.Dispose()
}
