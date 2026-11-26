# LogistiCore safe local update helper. Run from PowerShell outside Unity.
# Does NOT stash, reset, clean, commit, push, merge, or force-checkout.
[CmdletBinding()]
param(
    [string]$RepositoryPath = 'D:\Projects\unitylogisticore',
    [string]$TargetBranch = 'feature/depot-visual-06-yard-grounds-and-inspection',
    [switch]$CheckOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & git -C $RepositoryPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('Git command failed: git ' + ($Arguments -join ' '))
    }
}

try {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'Git could not be found in PATH. Install Git for Windows first.'
    }

    if (-not (Test-Path -LiteralPath $RepositoryPath -PathType Container)) {
        throw ('Repository folder not found: ' + $RepositoryPath)
    }

    # Only a branch name, never a refspec / option or shell command.
    if ([string]::IsNullOrWhiteSpace($TargetBranch) -or
        $TargetBranch.StartsWith('-') -or
        $TargetBranch.Contains('..') -or
        $TargetBranch.Contains(' ') -or
        $TargetBranch.Contains('~') -or
        $TargetBranch.Contains('^') -or
        $TargetBranch.Contains(':') -or
        $TargetBranch.Contains('\\') -or
        $TargetBranch.EndsWith('/') -or
        $TargetBranch.Contains('//')) {
        throw 'Invalid target branch name.'
    }

    $root = (& git -C $RepositoryPath rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw 'The selected folder is not a Git worktree.'
    }
    $expected = (Resolve-Path -LiteralPath $RepositoryPath).Path.TrimEnd('\', '/')
    $actual = ([string]$root).TrimEnd('\', '/').Replace('/', '\')
    if (-not [string]::Equals($expected, $actual,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ('For safety, use the repository root, not a subdirectory: ' + $root)
    }

    $origin = (& git -C $RepositoryPath remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0 -or
        ([string]$origin -notmatch '(?i)(github\.com[:/])hansaglam/unitylogisticore(\.git)?/?$')) {
        throw ('Unexpected origin remote. Stopped: ' + $origin)
    }

    $status = @(& git -C $RepositoryPath status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect local working tree.' }
    $changes = @($status | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($changes.Count -gt 0) {
        Write-Host ''
        Write-Warning ('There are ' + $changes.Count + ' local changed/untracked files.')
        $changes | ForEach-Object { Write-Host $_ }
        Write-Host ''
        Write-Host 'Stopped without changing any files or branches.' -ForegroundColor Yellow
        Write-Host 'Keep DepotPrototype.unity, generated art and ProjectSettings safe.'
        Write-Host 'Review the files and commit/backup/stash them manually before updating.'
        exit 2
    }

    $unity = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($unity.Count -gt 0 -and -not $CheckOnly) {
        Write-Warning 'Unity Editor appears to be running. Save and close it first.'
        exit 3
    }

    $current = (& git -C $RepositoryPath branch --show-current)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read current branch.' }
    Write-Host ('Current branch: ' + $current)
    Write-Host ('Target branch:  ' + $TargetBranch)
    Write-Host ('Origin:         ' + $origin)

    if ($CheckOnly) {
        Write-Host 'Read-only check passed. No fetch, switch or pull was performed.' -ForegroundColor Green
        exit 0
    }

    Write-Host 'Fetching origin ...'
    Invoke-Git -Arguments @('fetch', '--prune', 'origin')

    & git -C $RepositoryPath show-ref --verify --quiet ('refs/remotes/origin/' + $TargetBranch)
    if ($LASTEXITCODE -ne 0) {
        throw ('Target branch not found on origin: ' + $TargetBranch)
    }

    if ($current -ne $TargetBranch) {
        & git -C $RepositoryPath show-ref --verify --quiet ('refs/heads/' + $TargetBranch)
        $existsLocally = ($LASTEXITCODE -eq 0)
        if ($existsLocally) {
            Invoke-Git -Arguments @('switch', $TargetBranch)
        }
        else {
            Invoke-Git -Arguments @('switch', '--track', ('origin/' + $TargetBranch))
        }
    }

    # Only fast-forward; never create an implicit merge commit.
    Write-Host 'Updating with fast-forward only ...'
    Invoke-Git -Arguments @('pull', '--ff-only', 'origin', $TargetBranch)

    Write-Host ''
    Invoke-Git -Arguments @('status', '--short', '--branch')
    Write-Host 'LogistiCore updated safely. Open Unity Hub to review the scene.' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Error ('Update stopped: ' + $_.Exception.Message)
    exit 1
}
