param(
    [Parameter(Mandatory = $false)]
    [string]$RemoteUrl = "https://github.com/rammseth-spec/CasaMigaPOS.git",

    [Parameter(Mandatory = $false)]
    [string]$RemoteName = "origin"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$Message) {
    Write-Host ""
    Write-Host "BLOCKED: $Message" -ForegroundColor Red
    exit 1
}

function Info([string]$Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

if (-not (Test-Path ".git")) {
    Fail "Run this script from the canonical Casa Miga POS repository root."
}

Info "Repository root: $(Get-Location)"

$status = git status --porcelain
if ($LASTEXITCODE -ne 0) { Fail "git status failed." }
if ($status) {
    Write-Host $status
    Fail "Working tree is not clean. Commit/stash/review changes before GitHub import."
}

$head = (git rev-parse HEAD).Trim()
$branch = (git branch --show-current).Trim()
Info "Current branch: $branch"
Info "Current HEAD: $head"

Write-Host ""
Write-Host "=== REMOTES ==="
git remote -v

Write-Host ""
Write-Host "=== RECENT HISTORY ==="
git log --oneline --decorate --graph -30

Write-Host ""
Write-Host "=== DIFF CHECK ==="
git diff --check
if ($LASTEXITCODE -ne 0) { Fail "git diff --check failed." }

$forbiddenPatterns = @(
    '\.db$',
    '\.db-',
    '\.sqlite',
    '\.cmbackup',
    '\.cmbackup\.partial$',
    '(^|/)terminal\.json$',
    '(^|/)development\.settings\.json$',
    '(^|/)\.env$',
    '\.log$',
    '(^|/)events-.*\.jsonl$',
    '(^|/)checkout\.pending',
    '(^|/)casamiga\.lock$'
)

$tracked = git ls-files
$blocked = @()
foreach ($file in $tracked) {
    foreach ($pattern in $forbiddenPatterns) {
        if ($file -match $pattern) {
            $blocked += $file
            break
        }
    }
}

if ($blocked.Count -gt 0) {
    Write-Host ""
    Write-Host "Sensitive/runtime files are tracked:" -ForegroundColor Red
    $blocked | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" }
    Fail "Remove sensitive/runtime files from Git tracking before import. Do not delete production data from disk."
}

$required = @(
    "src",
    "tests",
    "migrations",
    "scripts/verify.ps1",
    "CasaMigaPOS.slnx",
    "global.json",
    ".gitignore"
)

$missing = $required | Where-Object { -not (Test-Path $_) }
if ($missing.Count -gt 0) {
    Write-Host ""
    $missing | ForEach-Object { Write-Host "Missing: $_" -ForegroundColor Yellow }
    Fail "This does not look like the complete canonical project."
}

Info "Running canonical verification..."
& ./scripts/verify.ps1
if ($LASTEXITCODE -ne 0) {
    Fail "scripts/verify.ps1 failed. Nothing should be pushed."
}

Info "Canonical verification passed."

$existingRemoteUrl = $null
try {
    $existingRemoteUrl = (git remote get-url $RemoteName 2>$null).Trim()
} catch {}

if ([string]::IsNullOrWhiteSpace($existingRemoteUrl)) {
    Info "Adding remote '$RemoteName' => $RemoteUrl"
    git remote add $RemoteName $RemoteUrl
} elseif ($existingRemoteUrl -ne $RemoteUrl) {
    Write-Host "Existing '$RemoteName': $existingRemoteUrl" -ForegroundColor Yellow
    Write-Host "Expected URL: $RemoteUrl" -ForegroundColor Yellow
    Fail "Remote name already points elsewhere. Review manually; the script will not overwrite it."
}

Info "Fetching GitHub remote..."
git fetch $RemoteName --prune
if ($LASTEXITCODE -ne 0) { Fail "git fetch failed." }

$importBranch = "import/canonical-$($head.Substring(0,8))"

$localBranches = git branch --format="%(refname:short)"
if ($localBranches -contains $importBranch) {
    Fail "Import branch '$importBranch' already exists. Review it manually."
}

Info "Creating import branch '$importBranch' from canonical HEAD."
git switch -c $importBranch
if ($LASTEXITCODE -ne 0) { Fail "Could not create import branch." }

Write-Host ""
Write-Host "AUDIT PASSED." -ForegroundColor Green
Write-Host "Canonical HEAD: $head"
Write-Host "Import branch: $importBranch"
Write-Host ""
Write-Host "Next safe step requires history review before any merge or push."
Write-Host "Run and share these outputs:"
Write-Host "  git merge-base HEAD $RemoteName/main"
Write-Host "  git log --oneline --decorate --graph --all -50"
Write-Host "  git diff --stat $RemoteName/main...HEAD"
Write-Host ""
Write-Host "The script intentionally does NOT force-push, merge unrelated histories, or rewrite history."
