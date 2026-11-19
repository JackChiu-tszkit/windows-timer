$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$dotnet = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

& $dotnet test "WindowsTimer.sln" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force }
& $dotnet publish "src\WindowsTimer\WindowsTimer.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o (Join-Path $root "publish")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "Inno Setup compiler (ISCC.exe) was not found." }
& $iscc "installer\Timer.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Output "Built publish\Timer.exe and dist\Timer-Setup.exe"
