[CmdletBinding()]
param(
    [ValidateSet('merge', 'rebase')]
    [string]$Mode = 'rebase',

    [string]$BaseBranch,

    [string]$RemoteName = 'origin',

    [switch]$NoPush,

    [switch]$KeepTempFile
)

$ErrorActionPreference = 'Stop'

if ($IsWindows -eq $false) {
    throw "This script requires a Windows environment. The temporary batch runner (.bat) is not supported on non-Windows platforms."
}

function Assert-GitSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if ($LASTEXITCODE -ne 0) {
        throw $Message
    }
}

function Test-GitStateFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return Test-Path (Join-Path $gitDir $Name)
}

function Get-StatusPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusLine
    )

    if ($StatusLine.Length -lt 4) {
        return $null
    }

    $pathPortion = $StatusLine.Substring(3)
    $renameParts = $pathPortion -split ' -> ', 2
    return $renameParts[-1].Trim()
}

function Test-IgnoredDotFolderStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusLine
    )

    if (-not $StatusLine.StartsWith('!! ')) {
        return $false
    }

    $path = Get-StatusPath $StatusLine
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $false
    }

    $normalizedPath = $path.Replace('\', '/')
    return $normalizedPath -match '^\.[^/]+/'
}

function Get-IgnoredDotFolder {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusLine
    )

    if (-not (Test-IgnoredDotFolderStatus $StatusLine)) {
        return $null
    }

    $path = Get-StatusPath $StatusLine
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $null
    }

    $normalizedPath = $path.Replace('\', '/')
    $pathParts = $normalizedPath -split '/', 2

    if ($pathParts.Count -eq 0 -or [string]::IsNullOrWhiteSpace($pathParts[0])) {
        return $null
    }

    return $pathParts[0]
}

function Get-TrimmedGitOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = git -C $repoRoot @Arguments
    Assert-GitSuccess "Git command failed: git -C `"$repoRoot`" $($Arguments -join ' ')"

    if ($null -eq $output) {
        return ''
    }

    return ([string]$output).Trim()
}

function Test-GitRefExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RefName
    )

    git -C $repoRoot show-ref --verify --quiet $RefName
    return $LASTEXITCODE -eq 0
}

function Test-BuildArtifactStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusLine
    )

    if (-not ($StatusLine.StartsWith('?? ') -or $StatusLine.StartsWith('!! '))) {
        return $false
    }

    $path = Get-StatusPath $StatusLine
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $false
    }

    $normalizedPath = $path.Replace('\', '/')
    return $normalizedPath -match '(^|/)(bin|obj)(/|$)'
}

function Get-BuildArtifactDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$StatusLine
    )

    if (-not (Test-BuildArtifactStatus $StatusLine)) {
        return $null
    }

    $path = Get-StatusPath $StatusLine
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $null
    }

    $normalizedPath = $path.Replace('\', '/')
    $match = [regex]::Match($normalizedPath, '^(.*?)(?:^|/)(bin|obj)(?:/.*)?$')
    if (-not $match.Success) {
        return $null
    }

    $prefix = $match.Groups[1].Value.TrimEnd('/')
    $folderName = $match.Groups[2].Value
    if ([string]::IsNullOrWhiteSpace($prefix)) {
        return $folderName
    }

    return "$prefix/$folderName"
}

function Get-BaseBranchNameFromRef {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RefName
    )

    $pathParts = $RefName -split '/'
    if ($pathParts.Count -eq 0) {
        return $null
    }

    return $pathParts[-1]
}

function Resolve-BaseBranch {
    if (-not [string]::IsNullOrWhiteSpace($BaseBranch)) {
        return $BaseBranch.Trim()
    }

    $remoteHeadRef = git -C $repoRoot symbolic-ref --quiet "refs/remotes/$RemoteName/HEAD"
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($remoteHeadRef)) {
        return Get-BaseBranchNameFromRef ([string]$remoteHeadRef).Trim()
    }

    $candidateRefs = @(
        "refs/heads/master",
        "refs/heads/main",
        "refs/remotes/$RemoteName/master",
        "refs/remotes/$RemoteName/main"
    )

    foreach ($candidateRef in $candidateRefs) {
        if (Test-GitRefExists $candidateRef) {
            return Get-BaseBranchNameFromRef $candidateRef
        }
    }

    throw "Unable to determine the base branch automatically. Specify -BaseBranch explicitly."
}

function Test-AnyGitState {
    foreach ($stateFile in $stateFiles) {
        if (Test-GitStateFile $stateFile) {
            return $true
        }
    }

    return $false
}

$repoRoot = (Resolve-Path $PSScriptRoot).Path
$tempBat = $null
$stashCreated = $false
$stashRef = $null
$stashName = $null
$exitCode = 0

git -C $repoRoot rev-parse --show-toplevel | Out-Null
Assert-GitSuccess "This script must be run from inside a git repository."

git -C $repoRoot remote get-url $RemoteName | Out-Null
Assert-GitSuccess "Remote '$RemoteName' was not found."

$currentBranch = Get-TrimmedGitOutput @('branch', '--show-current')

if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    throw "Detached HEAD is not supported. Check out a branch first."
}

$resolvedBaseBranch = Resolve-BaseBranch
$baseBranchRef = "refs/heads/$resolvedBaseBranch"
$remoteBaseBranchRef = "refs/remotes/$RemoteName/$resolvedBaseBranch"

if (-not (Test-GitRefExists $baseBranchRef) -and -not (Test-GitRefExists $remoteBaseBranchRef)) {
    throw "Base branch '$resolvedBaseBranch' was not found locally. Fetch it or specify a different -BaseBranch."
}

if ($currentBranch.Equals($resolvedBaseBranch, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "You are already on '$resolvedBaseBranch'. Switch to a feature branch before running this script."
}

$gitDir = (git -C $repoRoot rev-parse --git-dir).Trim()
Assert-GitSuccess "Unable to locate the .git directory."

if (-not [System.IO.Path]::IsPathRooted($gitDir)) {
    $gitDir = Join-Path $repoRoot $gitDir
}

$stateFiles = @(
    'MERGE_HEAD',
    'CHERRY_PICK_HEAD',
    'REVERT_HEAD',
    'BISECT_LOG',
    'rebase-apply',
    'rebase-merge'
)

foreach ($stateFile in $stateFiles) {
    if (Test-GitStateFile $stateFile) {
        throw "Repository has an in-progress git operation ('$stateFile'). Finish or abort it before running this script."
    }
}

$statusOutput = @(git -C $repoRoot status --porcelain=v1 --untracked-files=all --ignored=matching)
Assert-GitSuccess "Unable to inspect the worktree state."

$buildArtifactDirectories = @(
    $statusOutput |
        ForEach-Object { Get-BuildArtifactDirectory $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
)

foreach ($buildArtifactDirectory in $buildArtifactDirectories) {
    $candidateBuildArtifactPath = Join-Path $repoRoot ($buildArtifactDirectory.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    $fullBuildArtifactPath = [System.IO.Path]::GetFullPath($candidateBuildArtifactPath)
    $repoRootWithSeparator = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullBuildArtifactPath.StartsWith($repoRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove build artifacts outside the repository root: $buildArtifactDirectory"
    }

    if (Test-Path -LiteralPath $fullBuildArtifactPath -PathType Container) {
        Remove-Item -LiteralPath $fullBuildArtifactPath -Recurse -Force
        Write-Host "Removed generated build artifacts: $buildArtifactDirectory"
    }
}

if ($buildArtifactDirectories.Count -gt 0) {
    $statusOutput = @(git -C $repoRoot status --porcelain=v1 --untracked-files=all --ignored=matching)
    Assert-GitSuccess "Unable to inspect the worktree state after cleaning build artifacts."
}

$stashRelevantStatus = @($statusOutput | Where-Object { -not (Test-IgnoredDotFolderStatus $_) })

if ($stashRelevantStatus.Count -gt 0) {
    $stashName = "{0} {1}" -f $Mode, (Get-Date -Format 'yyyy-MM-dd HH:mm')
    $ignoredDotFolders = @(
        $statusOutput |
            ForEach-Object { Get-IgnoredDotFolder $_ } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Sort-Object -Unique
    )

    $stashArguments = @('stash', 'push', '--all', '--message', $stashName, '--', '.')
    foreach ($ignoredDotFolder in $ignoredDotFolders) {
        $stashArguments += ":(top,glob,exclude)$ignoredDotFolder/**"
    }

    git -C $repoRoot @stashArguments | Out-Null
    Assert-GitSuccess "Unable to stash local changes before switching branches."

    $stashRef = (git -C $repoRoot stash list -1 --format="%gd").Trim()
    Assert-GitSuccess "Unable to inspect the created stash."

    if ([string]::IsNullOrWhiteSpace($stashRef)) {
        throw "A stash was created, but the script could not identify it for restoration."
    }

    $stashCreated = $true
}

$tempBat = Join-Path ([System.IO.Path]::GetTempPath()) ("update-from-base-{0}.bat" -f ([guid]::NewGuid().ToString('N')))
$repoRootEscaped = $repoRoot.Replace('%', '%%').Replace('"', '""')
$currentBranchEscaped = $currentBranch.Replace('%', '%%').Replace('"', '""')
$baseBranchEscaped = $resolvedBaseBranch.Replace('%', '%%').Replace('"', '""')
$remoteNameEscaped = $RemoteName.Replace('%', '%%').Replace('"', '""')
$pushChanges = if ($NoPush) { 'false' } else { 'true' }

$batchContent = @"
@echo off
setlocal

set "REPO_ROOT=$repoRootEscaped"
set "ORIGINAL_BRANCH=$currentBranchEscaped"
set "BASE_BRANCH=$baseBranchEscaped"
set "REMOTE_NAME=$remoteNameEscaped"
set "MODE=$Mode"
set "PUSH_CHANGES=$pushChanges"

cd /d "%REPO_ROOT%"
if errorlevel 1 exit /b 1

echo.
echo ^> git switch "%BASE_BRANCH%"
git switch -- "%BASE_BRANCH%"
if errorlevel 1 exit /b 1

echo.
echo ^> git pull --ff-only "%REMOTE_NAME%" "%BASE_BRANCH%"
git pull --ff-only "%REMOTE_NAME%" "%BASE_BRANCH%"
if errorlevel 1 exit /b 1

echo.
echo ^> git switch "%ORIGINAL_BRANCH%"
git switch -- "%ORIGINAL_BRANCH%"
if errorlevel 1 exit /b 1

echo.
if /i "%MODE%"=="rebase" (
    echo ^> git rebase "%BASE_BRANCH%"
    git rebase "%BASE_BRANCH%"
) else (
    echo ^> git merge "%BASE_BRANCH%"
    git merge "%BASE_BRANCH%"
)
if errorlevel 1 exit /b 1

if /i "%PUSH_CHANGES%"=="true" (
    echo.
    if /i "%MODE%"=="rebase" (
        echo ^> git push --force-with-lease "%REMOTE_NAME%" "%ORIGINAL_BRANCH%"
        git push --force-with-lease "%REMOTE_NAME%" "%ORIGINAL_BRANCH%"
    ) else (
        echo ^> git push "%REMOTE_NAME%" "%ORIGINAL_BRANCH%"
        git push "%REMOTE_NAME%" "%ORIGINAL_BRANCH%"
    )
    if errorlevel 1 exit /b 1
) else (
    echo.
    echo ^> Push skipped because -NoPush was specified.
)

echo.
echo Done.
exit /b 0
"@

Set-Content -LiteralPath $tempBat -Value $batchContent -Encoding Default

Write-Host "Original branch: $currentBranch"
Write-Host "Base branch: $resolvedBaseBranch"
Write-Host "Integration mode: $Mode"
Write-Host "Remote: $RemoteName"
Write-Host "Push changes: $(-not $NoPush)"
Write-Host "Temporary runner: $tempBat"

if ($stashCreated) {
    Write-Host "Stashed current worktree as: $stashName ($stashRef)"
} else {
    Write-Host "Worktree had no stash-worthy changes outside ignored dot folders. No stash created."
}

try {
    & $tempBat
    $exitCode = $LASTEXITCODE
}
finally {
    $branchAfterRun = ''
    try {
        $branchAfterRun = Get-TrimmedGitOutput @('branch', '--show-current')
    }
    catch {
        Write-Warning "Unable to determine the current branch during cleanup. Continuing with stash restoration and temp-file cleanup."
    }

    $hasGitState = $false
    if (-not [string]::IsNullOrWhiteSpace($branchAfterRun) -and $branchAfterRun -ne $currentBranch) {
        $hasGitState = Test-AnyGitState

        if (-not $hasGitState) {
            git -C $repoRoot switch -- $currentBranch | Out-Null
            $switchBackExitCode = $LASTEXITCODE

            if ($switchBackExitCode -ne 0) {
                Write-Warning "Failed to switch back to the original branch '$currentBranch' during cleanup. You may still be on '$branchAfterRun'."
            } else {
                $branchAfterRun = $currentBranch
            }
        }
    }

    $hasGitState = Test-AnyGitState

    if ($stashCreated -and -not [string]::IsNullOrWhiteSpace($stashRef)) {
        if ($hasGitState) {
            Write-Warning "Skipped stash restoration because the repository is in the middle of a git operation. The stash '$stashRef' was kept."
        } elseif ($branchAfterRun -ne $currentBranch) {
            Write-Warning "Skipped stash restoration because the repository is not back on '$currentBranch'. The stash '$stashRef' was kept."
        } else {
            git -C $repoRoot stash apply --index $stashRef | Out-Null

            if ($LASTEXITCODE -eq 0) {
                git -C $repoRoot stash drop $stashRef | Out-Null

                if ($LASTEXITCODE -eq 0) {
                    Write-Host "Restored stashed worktree from: $stashName"
                } else {
                    Write-Warning "The stashed worktree was applied, but dropping '$stashRef' failed."
                }
            } else {
                Write-Warning "Failed to restore stashed worktree from '$stashRef'. The stash was kept."
            }
        }
    }

    if ($KeepTempFile) {
        Write-Host "Temporary batch file kept at: $tempBat"
    } else {
        Remove-Item -LiteralPath $tempBat -ErrorAction SilentlyContinue
        Write-Host "Temporary batch file removed."
    }
}

$pushSummary = if ($NoPush) { 'push skipped' } else { 'push attempted' }

if ($stashCreated) {
    Write-Host "Summary: stashed dirty worktree as '$stashName', updated from '$resolvedBaseBranch' using '$Mode', and $pushSummary."
} else {
    Write-Host "Summary: no stash-worthy changes outside ignored dot folders were found, updated from '$resolvedBaseBranch' using '$Mode', and $pushSummary."
}

exit $exitCode
