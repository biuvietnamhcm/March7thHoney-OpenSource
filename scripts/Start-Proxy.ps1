param(
    [ValidateSet('local', 'regular')][string]$Mode = 'local',
    [string]$ServerUrl = 'http://127.0.0.1:21000',
    [ValidateRange(1024, 65535)][int]$Port = 18080,
    [string]$GameProcess = 'StarRail.exe',
    [string]$MitmdumpPath = $env:MARCH7TH_MITMDUMP
)
$ErrorActionPreference = 'Stop'
try {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if (-not $MitmdumpPath) {
        $command = Get-Command mitmdump.exe -ErrorAction SilentlyContinue
        if ($command) { $MitmdumpPath = $command.Source }
        else { $MitmdumpPath = Join-Path $env:ProgramFiles 'mitmproxy\bin\mitmdump.exe' }
    }
    if (-not (Test-Path -LiteralPath $MitmdumpPath -PathType Leaf)) {
        throw 'mitmdump.exe not found. Install mitmproxy or set MARCH7TH_MITMDUMP.'
    }
    $target = [uri]$ServerUrl
    if ($target.Scheme -notin @('http','https') -or $target.UserInfo -or $target.AbsolutePath -ne '/' -or $target.Query -or $target.Fragment) {
        throw 'ServerUrl must be an HTTP(S) origin, for example http://127.0.0.1:21000.'
    }
    if ($GameProcess -notmatch '^[a-zA-Z0-9_.-]+$') { throw 'GameProcess must be one process name or PID.' }
    $proxyArguments = @('--mode', "regular@$Port", '--listen-host', '127.0.0.1',
        '--set', 'connection_strategy=lazy', '--set', 'upstream_cert=false',
        '--set', 'flow_detail=0', '--set', 'termlog_verbosity=info',
        '--set', "march7th_server=$ServerUrl",
        '--allow-hosts', '(^|\.)(hoyoverse\.com|mihoyo\.com|starrails\.com|bhsr\.com)(:[0-9]+)?$',
        '-s', (Join-Path $repoRoot 'scripts\proxy\starrail_redirect.py'))
    if ($Mode -eq 'local') { $proxyArguments += @('--mode', "local:$GameProcess") }
    Write-Host "March7th proxy -> $ServerUrl; test proxy: 127.0.0.1:$Port; mode: $Mode"
    Write-Host 'Local mode captures only the selected game process. Stop with Ctrl+C.'
    Write-Host 'HTTPS requires the mitmproxy CA to be trusted by the client. See docs/Proxy_vi.md.'
    & $MitmdumpPath @proxyArguments
    exit $LASTEXITCODE
} catch {
    Write-Error $_
    exit 1
}
