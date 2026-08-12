param(
  [string]$PipeName = 'eyebrowse-build002-preflight',
  [string]$ArtifactRoot = 'X:\AgentBrowser\Artifacts\build002-preflight',
  [string]$ActiveSource = 'C:\AgentBrowser\runtime\build002-cross-eye\active.js',
  [string]$Node = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$MeasureDir = Join-Path $ArtifactRoot 'measurements'
$ArgDir = 'C:\AgentBrowser\runtime\build002-preflight\program-args'
$Probe = Join-Path $PSScriptRoot 'developer-cross-eye-browser-probe.mjs'
$ProgramRunner = Join-Path $RepoRoot 'program-host\src\run.mjs'
$Broken = Join-Path $RepoRoot 'tests\fixtures\cross-eye\broken.js'
$Fixed = Join-Path $RepoRoot 'tests\fixtures\cross-eye\fixed.js'
$Git = 'C:\Program Files\Git\cmd\git.exe'
New-Item -ItemType Directory -Force -Path $MeasureDir,(Split-Path $ActiveSource),$ArgDir | Out-Null
$env:EYEBROWSE_PIPE_NAME = $PipeName

function Run-NodeJson([string[]]$Arguments, [string]$Path) {
  $lines = & $Node @Arguments
  $exit = $LASTEXITCODE
  $text = $lines -join [Environment]::NewLine
  [IO.File]::WriteAllText($Path, $text + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
  if ($exit -ne 0) { throw "Node command failed ($exit): $($Arguments -join ' ')" }
  return $text | ConvertFrom-Json
}

function Sha256([string]$Path) { (Get-FileHash $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

Copy-Item $Broken $ActiveSource -Force
$brokenHash = Sha256 $Broken
$activeBeforeHash = Sha256 $ActiveSource
$baselinePath = Join-Path $MeasureDir 'cross-eye-baseline-probe.json'
$baseline = Run-NodeJson @($Probe,'baseline') $baselinePath

$baselineArgs = Join-Path $ArgDir 'cross-eye-baseline-investigate.json'
@{target=$baseline.target;contains='cross-eye.js';limit=100} | ConvertTo-Json -Compress | Set-Content $baselineArgs -Encoding ascii
$baselineInvestigationPath = Join-Path $MeasureDir 'cross-eye-baseline-investigation.json'
$baselineInvestigation = Run-NodeJson @($ProgramRunner,'developer.investigate-console-error',('@'+$baselineArgs)) $baselineInvestigationPath

& $Node --check $Broken | Out-Null
$brokenSyntaxExit = $LASTEXITCODE
& $Node --check $Fixed | Out-Null
$fixedSyntaxExit = $LASTEXITCODE
$diffPath = Join-Path $MeasureDir 'cross-eye-source-fix.diff'
$diffErr = Join-Path $MeasureDir 'cross-eye-source-fix.diff.stderr.txt'
$diffLines = & $Git -c core.autocrlf=false diff --no-index -- $Broken $Fixed 2>$diffErr
$diffExit = $LASTEXITCODE
[IO.File]::WriteAllText($diffPath, (($diffLines -join [Environment]::NewLine) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
if ($diffExit -notin @(0,1)) { throw "git diff --no-index failed with $diffExit" }

$fixStartedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
Copy-Item $Fixed $ActiveSource -Force
$fixedHash = Sha256 $Fixed
$activeAfterHash = Sha256 $ActiveSource
$engineering = [ordered]@{
  substrate = 'CODEeye/local source engineering'
  fixStartedAtUtc = $fixStartedAtUtc
  activeSource = $ActiveSource
  brokenTemplate = $Broken
  fixedTemplate = $Fixed
  brokenHash = $brokenHash
  activeBeforeHash = $activeBeforeHash
  fixedHash = $fixedHash
  activeAfterHash = $activeAfterHash
  brokenSyntaxCheckExit = $brokenSyntaxExit
  fixedSyntaxCheckExit = $fixedSyntaxExit
  diffExit = $diffExit
  diffArtifact = $diffPath
  sourceChanged = $activeBeforeHash -ne $activeAfterHash
  fixedMaterialized = $activeAfterHash -eq $fixedHash
}
$engineering | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $MeasureDir 'cross-eye-engineering.json') -Encoding utf8

$fixedProbePath = Join-Path $MeasureDir 'cross-eye-fixed-probe.json'
$fixedProbe = Run-NodeJson @($Probe,'fixed',$baseline.target,[string]$baseline.consoleMax,[string]$baseline.exceptionMax) $fixedProbePath
$freshFixedPath = Join-Path $MeasureDir 'cross-eye-fixed-fresh-probe.json'
$freshFixed = Run-NodeJson @($Probe,'fixed-fresh') $freshFixedPath
$fixedArgs = Join-Path $ArgDir 'cross-eye-fixed-investigate.json'
@{target=$freshFixed.target;contains='cross-eye.js';limit=100} | ConvertTo-Json -Compress | Set-Content $fixedArgs -Encoding ascii
$fixedInvestigationPath = Join-Path $MeasureDir 'cross-eye-fixed-investigation.json'
$fixedInvestigation = Run-NodeJson @($ProgramRunner,'developer.investigate-console-error',('@'+$fixedArgs)) $fixedInvestigationPath

$checks = @(
  [ordered]@{name='baseline-source-hash';pass=$baseline.sourceHash -eq $brokenHash;observed=$baseline.sourceHash;expected=$brokenHash},
  [ordered]@{name='baseline-runtime-diagnosis';pass=[bool]$baseline.diagnosedControlledFailure -and [bool]$baseline.sourceContainsControlledFailure;observed=$baseline.newErrorCount;expected='controlled failure in runtime events and script source'},
  [ordered]@{name='baseline-program-host-diagnosis';pass=$baselineInvestigation.result.errorCount -ge 2 -and @($baselineInvestigation.result.relatedScripts).Count -ge 1 -and @($baselineInvestigation.result.relatedNetwork).Count -ge 1;observed=[ordered]@{errors=$baselineInvestigation.result.errorCount;relatedScripts=@($baselineInvestigation.result.relatedScripts).Count;relatedNetwork=@($baselineInvestigation.result.relatedNetwork).Count};expected='>=2 errors, >=1 related script, >=1 related request'},
  [ordered]@{name='codeeye-source-change';pass=$engineering.sourceChanged -and $engineering.fixedMaterialized -and $fixedSyntaxExit -eq 0;observed=$engineering;expected='material source changed to syntax-valid fixed hash'},
  [ordered]@{name='same-target-reload';pass=$fixedProbe.target -eq $baseline.target -and $fixedProbe.document -ne $baseline.document;observed=[ordered]@{target=$fixedProbe.target;beforeDocument=$baseline.document;afterDocument=$fixedProbe.document};expected='same t_* with new d_* after reload'},
  [ordered]@{name='fixed-source-hash';pass=$fixedProbe.sourceHash -eq $fixedHash;observed=$fixedProbe.sourceHash;expected=$fixedHash},
  [ordered]@{name='fixed-runtime-outcome';pass=$fixedProbe.state.phase -eq 'fixed' -and [int]$fixedProbe.state.result -eq 42 -and $fixedProbe.statusText -eq 'fixed:42';observed=[ordered]@{phase=$fixedProbe.state.phase;result=$fixedProbe.state.result;status=$fixedProbe.statusText};expected='fixed / 42 / fixed:42'},
  [ordered]@{name='zero-new-errors-after-fix';pass=[int]$fixedProbe.newErrorCount -eq 0 -and -not [bool]$fixedProbe.sourceContainsControlledFailure -and [bool]$fixedProbe.sourceContainsFixedMarker;observed=$fixedProbe.newErrorCount;expected='0 new browser errors and fixed runtime source'},
  [ordered]@{name='fresh-fixed-program-host';pass=$fixedInvestigation.result.errorCount -eq 0 -and $freshFixed.sourceHash -eq $fixedHash;observed=[ordered]@{errors=$fixedInvestigation.result.errorCount;hash=$freshFixed.sourceHash;kernelOperations=$fixedInvestigation.kernelOperations};expected='0 errors on fresh fixed page'}
)
$failed = @($checks | Where-Object { -not $_.pass })
$result = [ordered]@{
  ok = $failed.Count -eq 0
  runAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
  pipe = $PipeName
  baseline = [ordered]@{target=$baseline.target;document=$baseline.document;sourceHash=$baseline.sourceHash;newErrorCount=$baseline.newErrorCount;programHostKernelOperations=$baselineInvestigation.kernelOperations;programHostErrorCount=$baselineInvestigation.result.errorCount;relatedScripts=@($baselineInvestigation.result.relatedScripts).Count;relatedNetwork=@($baselineInvestigation.result.relatedNetwork).Count}
  engineering = $engineering
  fixed = [ordered]@{target=$fixedProbe.target;document=$fixedProbe.document;sourceHash=$fixedProbe.sourceHash;newErrorCount=$fixedProbe.newErrorCount;phase=$fixedProbe.state.phase;result=$fixedProbe.state.result;status=$fixedProbe.statusText;freshProgramHostKernelOperations=$fixedInvestigation.kernelOperations;freshProgramHostErrorCount=$fixedInvestigation.result.errorCount}
  checks = $checks
  artifacts = [ordered]@{baselineProbe=$baselinePath;baselineInvestigation=$baselineInvestigationPath;engineering=(Join-Path $MeasureDir 'cross-eye-engineering.json');sourceDiff=$diffPath;fixedProbe=$fixedProbePath;freshFixedProbe=$freshFixedPath;fixedInvestigation=$fixedInvestigationPath}
}
$output = Join-Path $MeasureDir 'developer-cross-eye-flagship.json'
$result | ConvertTo-Json -Depth 20 | Set-Content $output -Encoding utf8
$result | ConvertTo-Json -Depth 12
if ($failed.Count -gt 0) { exit 1 }
