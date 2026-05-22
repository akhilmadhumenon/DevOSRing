# Builds DevOSCore, all 4 plugins, runs xunit tests, packs each plugin
# into a .lplug4, and packs the companion extension into a .vsix.
# Output goes to dist/.

param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

function Log { param($msg) Write-Host "[devos] $msg" -ForegroundColor Cyan }
function Fail { param($msg) Write-Host "[devos] $msg" -ForegroundColor Red; exit 1 }

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail 'dotnet CLI not found' }
if (-not (Get-Command npm    -ErrorAction SilentlyContinue)) { Fail 'npm not found' }

Log "Building DevOS.sln ($Configuration)..."
$env:DEVOS_SKIP_LINK = 'true'
dotnet build -c $Configuration --nologo -v minimal (Join-Path $repoRoot 'DevOS.sln')

Log "Running unit tests..."
dotnet test -c $Configuration --nologo --no-build --verbosity quiet (Join-Path $repoRoot 'DevOS.sln')

$plugins = @(
    @{ Short = 'AIRefactor';    Proj = 'AIRefactorPlugin' },
    @{ Short = 'TestAction';    Proj = 'TestActionPlugin' },
    @{ Short = 'ReviewAction';  Proj = 'ReviewActionPlugin' },
    @{ Short = 'GitCommitPush'; Proj = 'GitCommitPushPlugin' }
)

foreach ($p in $plugins) {
    $bin = Join-Path $repoRoot "$($p.Proj)\bin\$Configuration"
    if (-not (Test-Path $bin)) { Fail "missing $bin" }

    if (Get-Command logiplugintool -ErrorAction SilentlyContinue) {
        Log "Packing $($p.Short) via logiplugintool..."
        logiplugintool pack $bin (Join-Path $dist "$($p.Short).lplug4")
    } else {
        Log "logiplugintool not on PATH; zipping plugin layout for $($p.Short)..."
        $parent = Split-Path $bin -Parent
        $zipPath = Join-Path $dist "$($p.Short).lplug4"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $parent '*') -DestinationPath $zipPath
    }
}

Log "Building devos-companion VSIX..."
Push-Location (Join-Path $repoRoot 'devos-companion')
try {
    if (-not (Test-Path 'node_modules')) { npm install --silent | Out-Null }
    npm run build --silent
    npx --yes '@vscode/vsce' package --out (Join-Path $dist 'devos-companion.vsix') | Out-Null
} finally {
    Pop-Location
}

Log 'Artefacts:'
Get-ChildItem $dist | Format-Table Name, Length, LastWriteTime
