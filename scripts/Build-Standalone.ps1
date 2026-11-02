param([string]$DotnetPath = $env:MARCH7TH_DOTNET)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

try {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $outputRoot = Join-Path $repoRoot 'artifacts\standalone\win-x64'
    $resourceRoot = Join-Path $repoRoot 'Resources'
    if (-not (Test-Path -LiteralPath (Join-Path $resourceRoot 'ExcelOutput') -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $resourceRoot 'Config') -PathType Container)) {
        throw 'Place the game resource pack in Resources (ExcelOutput and Config) before building.'
    }

    # Find an SDK, not just a machine-wide runtime. MARCH7TH_DOTNET can override discovery.
    $candidates = @()
    if ($DotnetPath) {
        $candidates += $DotnetPath
    } else {
        $installed = Get-Command dotnet.exe -ErrorAction SilentlyContinue
        if ($installed) { $candidates += $installed.Source }
        if ($env:LOCALAPPDATA) { $candidates += Join-Path $env:LOCALAPPDATA 'Codex\Tools\dotnet10\dotnet.exe' }
        if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
        if ($env:USERPROFILE) { $candidates += Join-Path $env:USERPROFILE '.dotnet\dotnet.exe' }
    }
    $dotnetExe = $null
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $sdks = @(& $candidate --list-sdks)
        if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^10\.' })) {
            $dotnetExe = $candidate
            break
        }
    }
    if (-not $dotnetExe) { throw 'Install the .NET 10 SDK, or set MARCH7TH_DOTNET to its dotnet.exe path.' }

    Write-Host "SDK: $dotnetExe"
    Write-Host "Output: $outputRoot"
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    Push-Location -LiteralPath $repoRoot
    try {
        & $dotnetExe publish 'Program\Program.csproj' --configuration Release --runtime win-x64 --self-contained true `
            --output $outputRoot -p:UseAppHost=true -p:PublishSingleFile=false -p:PublishTrimmed=false `
            -p:DebugType=None -p:DebugSymbols=false -nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    } finally { Pop-Location }

    # Seed only bundled runtime defaults. Never replace an existing server's configuration or database.
    $configRoot = Join-Path $repoRoot 'Config'
    foreach ($subdir in @('Custom', 'Data', 'Json', 'Security')) {
        $source = Join-Path $configRoot $subdir
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
            $relative = $file.FullName.Substring($configRoot.Length + 1)
            $target = Join-Path (Join-Path $outputRoot 'Config') $relative
            if (-not (Test-Path -LiteralPath $target)) {
                New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target
            }
        }
    }
    $clientKey = Join-Path $configRoot 'ClientSecretKey.ec2b'
    $targetKey = Join-Path $outputRoot 'Config\ClientSecretKey.ec2b'
    if ((Test-Path -LiteralPath $clientKey) -and -not (Test-Path -LiteralPath $targetKey)) {
        Copy-Item -LiteralPath $clientKey -Destination $targetKey
    }

    Write-Host 'Copying Resources. The first build may take several minutes.'
    & robocopy.exe $resourceRoot (Join-Path $outputRoot 'Resources') /E /R:2 /W:1 /COPY:DAT /DCOPY:DAT /XJ /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Resource copy failed with robocopy exit code $LASTEXITCODE." }

    $launcher = @'
@echo off
setlocal
pushd "%~dp0"
"%~dp0March7thHoney.exe" %*
set "SERVER_EXIT=%ERRORLEVEL%"
popd
if not "%SERVER_EXIT%"=="0" pause
exit /b %SERVER_EXIT%
'@
    [IO.File]::WriteAllText((Join-Path $outputRoot 'Start-Server.bat'), ($launcher -replace '\r?\n', "`r`n") + "`r`n", [Text.Encoding]::ASCII)
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $outputRoot 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\StandaloneBuild_vi.md') -Destination (Join-Path $outputRoot 'README-BUILD.md')

    foreach ($required in @('March7thHoney.exe', 'March7thHoney.dll', 'coreclr.dll', 'hostfxr.dll', 'e_sqlite3.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputRoot $required) -PathType Leaf)) {
            throw "Incomplete publish: missing $required."
        }
    }
    & (Join-Path $outputRoot 'March7thHoney.exe') --version
    if ($LASTEXITCODE -ne 0) { throw 'The published executable failed its startup check.' }
    Write-Host "Build complete. Run: $outputRoot\Start-Server.bat"
    Write-Host 'Copy the entire win-x64 directory when moving the server to another machine.'
    exit 0
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
