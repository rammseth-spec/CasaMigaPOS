param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$CheckpointName,

    [Parameter(Mandatory = $false)]
    [string]$RemoteName = "origin",

    [Parameter(Mandatory = $false)]
    [string]$ExpectedRepository = "rammseth-spec/CasaMigaPOS"
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

function SafeSlug([string]$Value) {
    $slug = $Value.ToLowerInvariant()
    $slug = [regex]::Replace($slug, "[^a-z0-9]+", "-")
    $slug = $slug.Trim("-")
    if ([string]::IsNullOrWhiteSpace($slug)) { return "checkpoint" }
    if ($slug.Length -gt 40) { $slug = $slug.Substring(0, 40).Trim("-") }
    return $slug
}

$repoRoot = git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repoRoot)) {
    Fail "Run this from inside the canonical Casa Miga POS Git repository."
}
Set-Location $repoRoot.Trim()
Info "Repository: $(Get-Location)"

$required = @("src","tests","migrations","scripts/verify.ps1","CasaMigaPOS.slnx","global.json",".gitignore")
$missing = $required | Where-Object { -not (Test-Path $_) }
if ($missing.Count -gt 0) {
    Fail ("Canonical local tree incomplete. Missing: " + ($missing -join ", "))
}

$status = git status --porcelain
if ($LASTEXITCODE -ne 0) { Fail "git status failed." }
if ($status) {
    Write-Host $status
    Fail "Working tree must be clean before checkpoint synchronization. Commit/review the authorized work first."
}

$remoteUrl = git remote get-url $RemoteName 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteUrl)) {
    Fail "Remote '$RemoteName' is not configured."
}
$remoteUrl = $remoteUrl.Trim()
if ($remoteUrl -notmatch [regex]::Escape($ExpectedRepository)) {
    Fail "Remote '$RemoteName' does not point to expected repository '$ExpectedRepository'. Found: $remoteUrl"
}

Info "Fetching $RemoteName..."
git fetch $RemoteName --prune
if ($LASTEXITCODE -ne 0) { Fail "git fetch failed." }

$remoteTree = git ls-tree -d --name-only "$RemoteName/main"
foreach ($dir in @("src","tests","migrations","scripts")) {
    if ($remoteTree -notcontains $dir) {
        Fail "GitHub canonical import is not complete: '$RemoteName/main' lacks '$dir'. Resolve Issue #3/import workflow first."
    }
}

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

$blocked = @()
foreach ($file in (git ls-files)) {
    foreach ($pattern in $forbiddenPatterns) {
        if ($file -match $pattern) {
            $blocked += $file
            break
        }
    }
}
if ($blocked.Count -gt 0) {
    $blocked | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    Fail "Sensitive/runtime files are tracked. Remove them from Git tracking before synchronization."
}

Info "Running git diff --check..."
git diff --check
if ($LASTEXITCODE -ne 0) { Fail "git diff --check failed." }

Info "Running canonical verification..."
& ./scripts/verify.ps1
if ($LASTEXITCODE -ne 0) { Fail "scripts/verify.ps1 failed. Checkpoint will not be pushed." }

$head = (git rev-parse HEAD).Trim()
$shortHead = (git rev-parse --short=8 HEAD).Trim()

git merge-base --is-ancestor "$RemoteName/main" HEAD
if ($LASTEXITCODE -ne 0) {
    Fail "Current HEAD does not contain $RemoteName/main. Reconcile history safely before pushing; no automatic merge/rebase is performed."
}

$slug = SafeSlug $CheckpointName
$pushBranch = "codex/$slug-$shortHead"
$existingRemote = git ls-remote --heads $RemoteName "refs/heads/$pushBranch"
if ($existingRemote) { Fail "Remote checkpoint branch already exists: $pushBranch" }

Info "Creating checkpoint branch '$pushBranch' at $head..."
git branch $pushBranch HEAD
if ($LASTEXITCODE -ne 0) { Fail "Could not create local checkpoint branch." }

Info "Pushing checkpoint branch without force..."
git push -u $RemoteName "${pushBranch}:${pushBranch}"
if ($LASTEXITCODE -ne 0) {
    git branch -D $pushBranch 2>$null
    Fail "Push failed."
}

$prUrl = $null
$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($null -ne $gh) {
    & gh auth status *> $null
    if ($LASTEXITCODE -eq 0) {
        Info "GitHub CLI authenticated; creating pull request..."
        $title = "Codex checkpoint: $CheckpointName"
        $body = "Automated safe checkpoint after local verification.`n`nCheckpoint: $CheckpointName`nCommit: $head`nVerification: scripts/verify.ps1 passed locally`nSensitive tracked-file scan: passed`nForce push: not used`nManual/physical smoke: NOT implied by this PR.`n`nGitHub CI/CodeQL and required manual validation must be reviewed before merge."
        $prUrl = (& gh pr create --repo $ExpectedRepository --base main --head $pushBranch --title $title --body $body).Trim()
        if ($LASTEXITCODE -ne 0) {
            Write-Host "PR creation failed, but branch push succeeded." -ForegroundColor Yellow
            $prUrl = $null
        }
    } else {
        Write-Host "GitHub CLI is installed but not authenticated; branch push succeeded, PR not created." -ForegroundColor Yellow
    }
} else {
    Write-Host "GitHub CLI not installed; branch push succeeded, PR not created automatically." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "CHECKPOINT SYNC COMPLETE" -ForegroundColor Green
Write-Host "Checkpoint: $CheckpointName"
Write-Host "HEAD:       $head"
Write-Host "Branch:     $pushBranch"
if ($prUrl) { Write-Host "PR:         $prUrl" }
Write-Host ""
Write-Host "Do not merge automatically. Review GitHub CI/CodeQL and required manual/physical smoke first."
