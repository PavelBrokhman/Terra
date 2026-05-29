<#
.SYNOPSIS
    Sync DEV-* feature branches with DEV.

.DESCRIPTION
    Merges the latest origin/DEV into every DEV-* feature branch (or the ones
    you name) and pushes them. Run this after committing directly on DEV so the
    feature branches don't drift.

    For each feature branch the script:
      1. fast-forwards the local branch to its remote (safe; skips if diverged),
      2. merges origin/DEV into it,
      3. pushes.
    On a merge conflict it aborts that branch and continues with the rest,
    reporting failures at the end.

.PARAMETER Branches
    Optional explicit branch names. If omitted, all remote DEV-* branches are used.

.EXAMPLE
    pwsh scripts/sync-feature-branches.ps1
    pwsh scripts/sync-feature-branches.ps1 -Branches DEV-Actions
#>
param(
    [string[]] $Branches
)

$startBranch = (git rev-parse --abbrev-ref HEAD).Trim()

Write-Host "Fetching origin..." -ForegroundColor Cyan
git fetch origin --prune
if ($LASTEXITCODE -ne 0) { Write-Host "fetch failed" -ForegroundColor Red; exit 1 }

if (-not $Branches) {
    $Branches = git for-each-ref --format='%(refname:short)' 'refs/remotes/origin/DEV-*' |
        ForEach-Object { $_ -replace '^origin/', '' }
}

if (-not $Branches) {
    Write-Host "No DEV-* feature branches found."
    exit 0
}

$failed = @()

foreach ($b in $Branches) {
    Write-Host "`n=== Syncing $b with DEV ===" -ForegroundColor Cyan

    # Ensure a local branch exists, tracking origin.
    git show-ref --verify --quiet "refs/heads/$b"
    if ($LASTEXITCODE -ne 0) {
        git branch --track $b "origin/$b"
        if ($LASTEXITCODE -ne 0) { Write-Host "cannot create local $b" -ForegroundColor Red; $failed += $b; continue }
    }

    git checkout $b
    if ($LASTEXITCODE -ne 0) { $failed += $b; continue }

    # Bring local up to its remote without clobbering local-only commits.
    git merge --ff-only "origin/$b"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "$b has local commits not on origin/$b (diverged) — skipping." -ForegroundColor Yellow
        $failed += $b; continue
    }

    git merge "origin/DEV" --no-edit
    if ($LASTEXITCODE -ne 0) {
        Write-Host "MERGE CONFLICT in $b — aborting this branch." -ForegroundColor Red
        git merge --abort
        $failed += $b; continue
    }

    git push origin $b
    if ($LASTEXITCODE -ne 0) { Write-Host "push failed for $b" -ForegroundColor Red; $failed += $b; continue }

    Write-Host "$b synced." -ForegroundColor Green
}

git checkout $startBranch | Out-Null

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Host "Done with issues. Needs attention: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "All feature branches synced with DEV." -ForegroundColor Green
