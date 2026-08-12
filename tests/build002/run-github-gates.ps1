param(
  [string]$EvidenceRoot = 'X:\AgentBrowser\Artifacts\build002-release-preflight\measurements',
  [string]$G1Clone = 'X:\SkillPlaneTest\g1-eyebrowse',
  [string]$G3File = 'X:\SkillPlaneTest\current.md',
  [string]$G7Repo = 'X:\SkillPlaneTest\g7-cli',
  [string]$GitExe = 'C:\Program Files\Git\cmd\git.exe'
)

$ErrorActionPreference = 'Stop'

function Read-Json([string]$Name) {
  $path = Join-Path $EvidenceRoot $Name
  if (-not (Test-Path $path)) { throw "Missing evidence artifact: $path" }
  return Get-Content $path -Raw | ConvertFrom-Json
}

function File-Hash([string]$Path) {
  if (-not (Test-Path $Path)) { return $null }
  return (Get-FileHash $Path -Algorithm SHA256).Hash
}

function Gate([string]$Name, [bool]$Pass, $Observed, $Expected = $null) {
  [ordered]@{ name=$Name; pass=$Pass; observed=$Observed; expected=$Expected }
}

$gates = [ordered]@{}

# G1 - repository acquisition: browser resolution + material Git clone.
$g1 = Read-Json 'github-g1-evidence.json'
$g1Remote = if (Test-Path (Join-Path $G1Clone '.git')) { (& $GitExe -C $G1Clone remote get-url origin).Trim() } else { $null }
$g1Branch = if (Test-Path (Join-Path $G1Clone '.git')) { (& $GitExe -C $G1Clone branch --show-current).Trim() } else { $null }
$g1Head = if (Test-Path (Join-Path $G1Clone '.git')) { (& $GitExe -C $G1Clone rev-parse HEAD).Trim() } else { $null }
$g1Status = if (Test-Path (Join-Path $G1Clone '.git')) { ((& $GitExe -C $G1Clone status --porcelain) -join "`n") } else { 'missing-clone' }
$g1Checks = @(
  (Gate 'evidence-ok' ([bool]$g1.ok) $g1.ok $true),
  (Gate 'repository' ($g1.repository -eq 'StealthEyeLLC/eyebrowse') $g1.repository 'StealthEyeLLC/eyebrowse'),
  (Gate 'default-ref' ($g1.defaultBranch -eq 'main' -and $g1.ref -eq 'main') ([ordered]@{defaultBranch=$g1.defaultBranch;ref=$g1.ref}) 'main'),
  (Gate 'git-materialized' (Test-Path (Join-Path $G1Clone '.git')) $G1Clone '.git exists'),
  (Gate 'remote-exact' ($g1Remote -eq $g1.cloneUrl) $g1Remote $g1.cloneUrl),
  (Gate 'branch-exact' ($g1Branch -eq 'main') $g1Branch 'main'),
  (Gate 'head-stable' ($g1Head -eq $g1.head) $g1Head $g1.head),
  (Gate 'worktree-clean' ([string]::IsNullOrWhiteSpace($g1Status)) $g1Status 'clean')
)
$gates.G1 = [ordered]@{ pass=(-not ($g1Checks | Where-Object { -not $_.pass })); checks=$g1Checks; artifact=(Join-Path $EvidenceRoot 'github-g1-evidence.json') }

# G2 - route normalization across eight route families.
$g2 = Read-Json 'github-g2-evidence.json'
$expectedCases = [ordered]@{
  repository=[ordered]@{routeFamily='repository';defaultBranch='trunk'}
  'readme-blob'=[ordered]@{routeFamily='blob';ref='trunk';path='README.md'}
  'architecture-blob'=[ordered]@{routeFamily='blob';ref='trunk';path='docs/project-layout.md'}
  'docs-tree'=[ordered]@{routeFamily='tree';ref='trunk';path='docs'}
  issue=[ordered]@{routeFamily='issues';issue='14134'}
  pull=[ordered]@{routeFamily='pull';pullRequest='14006'}
  commit=[ordered]@{routeFamily='commit';commit='c7456733f5ecfee07250b282e14d12da246d95fd'}
  actions=[ordered]@{routeFamily='actions';workflowRun='31587681547'}
}
$g2CaseChecks = @()
foreach ($name in $expectedCases.Keys) {
  $row = @($g2.cases | Where-Object { $_.name -eq $name })[0]
  $expected = $expectedCases[$name]
  $pass = $null -ne $row -and [bool]$row.pass -and $row.repository -eq 'cli/cli' -and [string]$row.repositoryId -eq '212613049'
  foreach ($key in $expected.Keys) { $pass = $pass -and ([string]$row.$key -eq [string]$expected[$key]) }
  $g2CaseChecks += Gate $name $pass $row $expected
}
$g2Checks = @(
  (Gate 'evidence-ok' ([bool]$g2.ok) $g2.ok $true),
  (Gate 'eight-cases' ([int]$g2.caseCount -eq 8 -and [int]$g2.passed -eq 8) ([ordered]@{cases=$g2.caseCount;passed=$g2.passed}) '8/8'),
  (Gate 'one-repository' (@($g2.repositoryIdentities).Count -eq 1 -and $g2.repositoryIdentities[0] -eq 'cli/cli') $g2.repositoryIdentities @('cli/cli')),
  (Gate 'one-repository-id' (@($g2.repositoryIds).Count -eq 1 -and [string]$g2.repositoryIds[0] -eq '212613049') $g2.repositoryIds @('212613049'))
) + $g2CaseChecks
$gates.G2 = [ordered]@{ pass=(-not ($g2Checks | Where-Object { -not $_.pass })); checks=$g2Checks; artifact=(Join-Path $EvidenceRoot 'github-g2-evidence.json') }

# G3 - exact file acquisition and authoritative bytes.
$g3 = Read-Json 'github-g3-evidence.json'
$g3RawPath = [string]$g3.authoritativeArtifact
$g3LocalHash = File-Hash $G3File
$g3RawHash = File-Hash $g3RawPath
$g3Checks = @(
  (Gate 'evidence-ok' ([bool]$g3.ok -and [bool]$g3.identical) ([ordered]@{ok=$g3.ok;identical=$g3.identical}) $true),
  (Gate 'identity' ($g3.repository -eq 'cli/cli' -and $g3.ref -eq 'trunk' -and $g3.path -eq 'docs/project-layout.md') ([ordered]@{repository=$g3.repository;ref=$g3.ref;path=$g3.path}) 'cli/cli trunk docs/project-layout.md'),
  (Gate 'destination-exists' (Test-Path $G3File) $G3File 'exists'),
  (Gate 'authoritative-exists' (Test-Path $g3RawPath) $g3RawPath 'exists'),
  (Gate 'hash-exact' ($g3LocalHash -eq $g3RawHash -and $g3LocalHash -eq $g3.authoritativeSha256) ([ordered]@{local=$g3LocalHash;raw=$g3RawHash;evidence=$g3.authoritativeSha256}) $g3.authoritativeSha256),
  (Gate 'bytes-exact' ((Get-Item $G3File).Length -eq (Get-Item $g3RawPath).Length -and (Get-Item $G3File).Length -eq [int64]$g3.authoritativeBytes) ([ordered]@{local=(Get-Item $G3File).Length;raw=(Get-Item $g3RawPath).Length;evidence=$g3.authoritativeBytes}) $g3.authoritativeBytes)
)
$gates.G3 = [ordered]@{ pass=(-not ($g3Checks | Where-Object { -not $_.pass })); checks=$g3Checks; artifact=(Join-Path $EvidenceRoot 'github-g3-evidence.json') }

# G4 - structured PR provider truth + independent browser visual truth.
$g4Host = Read-Json 'github-g4-pr.json'
$g4 = $g4Host.result
$g4Visual = Read-Json 'github-g4-visual-evidence.json'
$providerFields = @('pullRequestStatus','filesStatus','issueCommentsStatus','reviewCommentsStatus','reviewsStatus','checksStatus','statusStatus')
$provider200 = $true
foreach ($field in $providerFields) { $provider200 = $provider200 -and ([int]$g4.provider.$field -eq 200) }
$g4Checks = @(
  (Gate 'program-ok' ([bool]$g4Host.ok -and [bool]$g4.ok) ([ordered]@{program=$g4Host.ok;result=$g4.ok}) $true),
  (Gate 'pr-identity' ($g4.repository -eq 'cli/cli' -and [int]$g4.pullRequest -eq 14006) ([ordered]@{repository=$g4.repository;pullRequest=$g4.pullRequest}) 'cli/cli#14006'),
  (Gate 'structured-core' (-not [string]::IsNullOrWhiteSpace($g4.title) -and -not [string]::IsNullOrWhiteSpace($g4.author) -and -not [string]::IsNullOrWhiteSpace($g4.base.ref) -and -not [string]::IsNullOrWhiteSpace($g4.head.ref)) ([ordered]@{title=$g4.title;author=$g4.author;base=$g4.base;head=$g4.head}) 'nonempty'),
  (Gate 'changed-files-exact' ([int]$g4.fileCount -eq [int]$g4.changedFilesReported -and [int]$g4.fileCount -gt 0) ([ordered]@{provider=$g4.fileCount;reported=$g4.changedFilesReported}) 'equal > 0'),
  (Gate 'structured-review-checks' (@($g4.reviewComments).Count -gt 0 -and @($g4.reviews).Count -gt 0 -and @($g4.checks).Count -gt 0) ([ordered]@{reviewComments=@($g4.reviewComments).Count;reviews=@($g4.reviews).Count;checks=@($g4.checks).Count;annotations=@($g4.annotations).Count}) 'reviews/checks present; annotations may be empty'),
  (Gate 'provider-statuses' $provider200 $g4.provider 'all core provider resources HTTP 200'),
  (Gate 'visual-observation' ([bool]$g4Visual.ok -and [int]$g4Visual.semanticObjects -gt 0) ([ordered]@{target=$g4Visual.target;document=$g4Visual.document;semanticObjects=$g4Visual.semanticObjects}) 'browser-rendered PR observed'),
  (Gate 'visual-artifact' ((Test-Path $g4Visual.screenshot.path) -and (Get-Item $g4Visual.screenshot.path).Length -gt 0) ([ordered]@{path=$g4Visual.screenshot.path;size=if(Test-Path $g4Visual.screenshot.path){(Get-Item $g4Visual.screenshot.path).Length}else{0}}) 'nonempty screenshot')
)
$gates.G4 = [ordered]@{ pass=(-not ($g4Checks | Where-Object { -not $_.pass })); checks=$g4Checks; artifacts=@((Join-Path $EvidenceRoot 'github-g4-pr.json'),(Join-Path $EvidenceRoot 'github-g4-visual-evidence.json')) }

# G5 - failed Actions diagnosis with explicit browser auth boundary and credential-safe provider log handoff.
$g5Host = Read-Json 'github-g5-local-run.json'
$g5 = $g5Host.result
$g5Log = Read-Json 'github-g5-authenticated-log-summary.json'
$g5BaselinePath = Join-Path $EvidenceRoot 'g7-baseline-go-test.log'
$g5Baseline = Get-Content $g5BaselinePath -Raw
$logAccessRows = if ($null -ne $g5.logsAccess.jobs) { @($g5.logsAccess.jobs) } else { @($g5.logsAccess) }
$logStatuses = @($logAccessRows | ForEach-Object { [int]$_.status })
$g5Checks = @(
  (Gate 'program-ok' ([bool]$g5Host.ok -and [bool]$g5.ok) ([ordered]@{program=$g5Host.ok;result=$g5.ok}) $true),
  (Gate 'run-identity' ($g5.repository -eq 'cli/cli' -and [int64]$g5.workflowRun -eq 30928568021) ([ordered]@{repository=$g5.repository;run=$g5.workflowRun}) 'cli/cli run 30928568021'),
  (Gate 'failed-jobs-steps' ([int]$g5.failedJobCount -eq 3 -and [int]$g5.failedStepCount -ge 3) ([ordered]@{jobs=$g5.failedJobCount;steps=$g5.failedStepCount}) '3 failed jobs, >=3 failed steps'),
  (Gate 'workflow-source' ([bool]$g5.workflowSource.ok -and $g5.workflowSource.path -eq '.github/workflows/go.yml' -and @($g5.failedStepSource | Where-Object { $_.snippet -like '*go test -race -tags=integration ./...*' }).Count -ge 1) ([ordered]@{workflowPath=$g5.workflowSource.path;workflowRef=$g5.workflowSource.ref;failedStepSource=$g5.failedStepSource}) 'exact failing test command in workflow/step source'),
  (Gate 'browser-log-boundary' ($logStatuses.Count -ge 1 -and @($logStatuses | Where-Object { $_ -ne 403 }).Count -eq 0) $logStatuses 'one or more explicit 403 browser-log boundaries'),
  (Gate 'provider-handoff-safe' (-not [bool]$g5Log.authority.credentialsCopiedIntoEyeBrowse -and -not [bool]$g5Log.authority.secretsStored -and -not [bool]$g5Log.authority.rawLogStored) $g5Log.authority 'no credentials/secrets/raw logs stored'),
  (Gate 'provider-log-pins-ci' ($g5Log.relevantLog.checkoutRef -eq 'c7456733f5ecfee07250b282e14d12da246d95fd' -and $g5Log.relevantLog.goVersion -eq '1.26.5' -and $g5Log.relevantLog.failingRequest -match 'heads%2Ffeature' -and $g5Log.relevantLog.signature -match 'no registered HTTP stubs matched') $g5Log.relevantLog 'historical SHA + Go 1.26.5 + encoded-ref mismatch'),
  (Gate 'local-reproduction-same-signature' ($g5Baseline -match 'heads%2Ffeature' -and $g5Baseline -match 'no registered HTTP stubs matched' -and $g5Baseline -match 'FAIL') $g5BaselinePath 'same failure signature reproduced locally')
)
$gates.G5 = [ordered]@{ pass=(-not ($g5Checks | Where-Object { -not $_.pass })); checks=$g5Checks; artifacts=@((Join-Path $EvidenceRoot 'github-g5-local-run.json'),(Join-Path $EvidenceRoot 'github-g5-authenticated-log-summary.json'),$g5BaselinePath) }

# G6 - pressure: supported variants normalize, unsupported routes explicitly refuse.
$g6Raw = Read-Json 'github-g6-pressure.json'
$g6 = @($g6Raw)
$supportedNames = @('repo-query','blob-query','issue-query','pr-subroute')
$unsupportedNames = @('notifications','settings-profile')
$g6Checks = @()
foreach ($name in $supportedNames) {
  $row = @($g6 | Where-Object { $_.name -eq $name })[0]
  $g6Checks += Gate $name ($null -ne $row -and [bool]$row.resultOk -and $row.repository -eq 'cli/cli') $row 'supported cli/cli route'
}
foreach ($name in $unsupportedNames) {
  $row = @($g6 | Where-Object { $_.name -eq $name })[0]
  $pass = $null -ne $row -and -not [bool]$row.resultOk -and [string]::IsNullOrWhiteSpace([string]$row.repository) -and [string]$row.reason -match 'repository identity is not supported'
  $g6Checks += Gate $name $pass $row 'explicit refusal, no repository synthesis'
}
$gates.G6 = [ordered]@{ pass=(-not ($g6Checks | Where-Object { -not $_.pass })); checks=$g6Checks; artifact=(Join-Path $EvidenceRoot 'github-g6-pressure.json') }

# G7 - developer handoff: historical CI reproduction -> local source/test fix -> broader passing validation.
$g7Tests = Read-Json 'g7-local-fix-tests.json'
$g7BaselinePath = Join-Path $EvidenceRoot 'g7-baseline-go-test.log'
$g7DiffPath = Join-Path $EvidenceRoot 'g7-source-fix.diff'
$g7Baseline = Get-Content $g7BaselinePath -Raw
$g7Diff = Get-Content $g7DiffPath -Raw
$g7Head = (& $GitExe -C $G7Repo rev-parse HEAD).Trim()
$g7Branch = (& $GitExe -C $G7Repo branch --show-current).Trim()
$g7Remote = (& $GitExe -C $G7Repo remote get-url origin).Trim()
$g7StatusLines = @(& $GitExe -C $G7Repo status --porcelain)
$upstreamRemote = ((& $GitExe -C $G7Repo config --get ("branch.$g7Branch.remote") 2>$null) -join "").Trim()
$upstreamMerge = ((& $GitExe -C $G7Repo config --get ("branch.$g7Branch.merge") 2>$null) -join "").Trim()
$hasUpstream = -not [string]::IsNullOrWhiteSpace($upstreamRemote) -or -not [string]::IsNullOrWhiteSpace($upstreamMerge)
$allTestsPass = [bool]$g7Tests.focused.pass -and [bool]$g7Tests.'full-package'.pass -and [bool]$g7Tests.'worktree-parser'.pass
$g7Checks = @(
  (Gate 'historical-head' ($g7Head -eq 'c7456733f5ecfee07250b282e14d12da246d95fd') $g7Head 'c7456733f5ecfee07250b282e14d12da246d95fd'),
  (Gate 'local-fix-branch' ($g7Branch -eq 'eyebrowse-g7-local-fix') $g7Branch 'eyebrowse-g7-local-fix'),
  (Gate 'remote-source' ($g7Remote -eq 'https://github.com/cli/cli.git') $g7Remote 'https://github.com/cli/cli.git'),
  (Gate 'no-upstream-push-target' (-not $hasUpstream) ([ordered]@{remote=$upstreamRemote;merge=$upstreamMerge}) 'no configured upstream'),
  (Gate 'only-expected-modification' ($g7StatusLines.Count -eq 1 -and $g7StatusLines[0] -match '^ M pkg/cmd/pr/merge/merge_test\.go$') $g7StatusLines @(' M pkg/cmd/pr/merge/merge_test.go')),
  (Gate 'baseline-fails-exactly' ($g7Baseline -match 'heads%2Ffeature' -and $g7Baseline -match 'no registered HTTP stubs matched' -and $g7Baseline -match 'FAIL') $g7BaselinePath 'encoded-ref unmatched-stub failure'),
  (Gate 'minimal-diff' ($g7Diff -match 'refs/heads/feature' -and $g7Diff -match 'refs/heads%2Ffeature') $g7DiffPath 'two historical stub strings encoded'),
  (Gate 'post-fix-tests' $allTestsPass $g7Tests 'focused + full package + parser pass')
)
$gates.G7 = [ordered]@{ pass=(-not ($g7Checks | Where-Object { -not $_.pass })); checks=$g7Checks; artifacts=@($g7BaselinePath,$g7DiffPath,(Join-Path $EvidenceRoot 'g7-local-fix-tests.json')) }

$gateList = @($gates.GetEnumerator() | ForEach-Object { [ordered]@{ gate=$_.Key; pass=$_.Value.pass; details=$_.Value } })
$passed = @($gateList | Where-Object { $_.pass }).Count
$result = [ordered]@{
  ok=($passed -eq 7)
  runAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
  evidenceRoot=$EvidenceRoot
  gateCount=7
  passed=$passed
  failed=7-$passed
  gates=$gateList
}
$outPath = Join-Path $EvidenceRoot 'github-gates-adjudication.json'
[IO.File]::WriteAllText($outPath, ($result | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
$result | ConvertTo-Json -Depth 30
if (-not $result.ok) { exit 1 }
