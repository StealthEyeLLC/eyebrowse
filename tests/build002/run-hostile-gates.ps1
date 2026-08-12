param(
  [string]$CoreEvidence = 'X:\AgentBrowser\Artifacts\build002-release-preflight\measurements\fixture-smoke.json',
  [string]$HostileEvidence = 'X:\AgentBrowser\Artifacts\build002-release-hostile-v3\measurements\hostile-missing.json',
  [string]$ReattachEvidence = 'X:\AgentBrowser\Artifacts\build002-release-reattach\measurements\reattach-regression.json',
  [string]$Output = 'X:\AgentBrowser\Artifacts\build002-release-hostile-v3\measurements\hostile-gates.json'
)
$ErrorActionPreference='Stop'
function ReadJ([string]$p){if(-not(Test-Path $p)){throw "Missing evidence: $p"};Get-Content $p -Raw|ConvertFrom-Json}
function C([string]$name,[bool]$pass,$observed,$expected){[ordered]@{name=$name;pass=$pass;observed=$observed;expected=$expected}}
function CoreCategory([string]$name,[string]$pattern,$checks){
  $matches=@($checks|Where-Object{[string]$_.name -match $pattern})
  $pass=$matches.Count -gt 0 -and @($matches|Where-Object{-not [bool]$_.pass}).Count -eq 0
  C $name $pass (@($matches|ForEach-Object{[ordered]@{name=$_.name;pass=$_.pass;observed=$_.observed}})) "one or more matching core checks, all pass"
}
$core=ReadJ $CoreEvidence
$hostile=ReadJ $HostileEvidence
$reattach=ReadJ $ReattachEvidence
$coreChecks=@($core.checks)
$checks=@()
$coreGreen=(($null -ne $core.ok -and [bool]$core.ok) -or ($null -eq $core.ok -and [int]$core.failed -eq 0 -and [int]$core.passed -eq $coreChecks.Count)) -and $coreChecks.Count -ge 20 -and @($coreChecks|Where-Object{-not [bool]$_.pass}).Count -eq 0
$checks+=C 'core-suite-green' $coreGreen ([ordered]@{ok=$coreGreen;count=$coreChecks.Count;failed=@($coreChecks|Where-Object{-not [bool]$_.pass}|ForEach-Object{$_.name})}) 'full deterministic core green'
$checks+=CoreCategory 'identity-rebinding' '(?i)replace-one|replace-two|semantic-rebound|rebind' $coreChecks
$checks+=CoreCategory 'ambiguity-refusal' '(?i)ambig' $coreChecks
$checks+=CoreCategory 'same-target-spa' '(?i)spa|same.document|same-target' $coreChecks
$checks+=CoreCategory 'bfcache' '(?i)bfcache' $coreChecks
$checks+=CoreCategory 'browser-native-download' '(?i)download' $coreChecks
$checks+=CoreCategory 'oopif' '(?i)oopif|cross.origin.*frame|frame.*cross' $coreChecks
$checks+=CoreCategory 'hundred-target-lazy-cognition' '(?i)100|lazy|cognition|scale' $coreChecks
$rendererPass=[bool]$hostile.ok -and [bool]$hostile.renderer.pass -and $hostile.renderer.target -like 't_*' -and $hostile.renderer.beforeDocument -like 'd_*' -and $hostile.renderer.afterDocument -like 'd_*' -and $hostile.renderer.beforeDocument -ne $hostile.renderer.afterDocument -and [string]$hostile.renderer.beforeRenderer.id -ne [string]$hostile.renderer.afterRenderer.id
$checks+=C 'renderer-process-change' $rendererPass ([ordered]@{target=$hostile.renderer.target;beforeDocument=$hostile.renderer.beforeDocument;afterDocument=$hostile.renderer.afterDocument;beforeRenderer=$hostile.renderer.beforeRenderer;afterRenderer=$hostile.renderer.afterRenderer;beforeOrigin=$hostile.renderer.beforeOrigin;afterOrigin=$hostile.renderer.afterOrigin}) 'same t_*, new d_*, different measured renderer PID'
$popupPass=[bool]$hostile.popup.pass -and $hostile.popup.primaryTarget -like 't_*' -and $hostile.popup.popupTarget -like 't_*' -and $hostile.popup.primaryTarget -ne $hostile.popup.popupTarget -and $hostile.popup.button -like 'e_*' -and $hostile.popup.popupDocument -like 'd_*' -and $hostile.popup.currentAfterReactivate -eq $hostile.popup.primaryTarget
$checks+=C 'popup-new-tab' $popupPass $hostile.popup 'semantic button creates new t_*, primary can be restored'
$replacementPass=[bool]$hostile.replacement.pass -and -not [bool]$hostile.replacement.oldStillPresent -and [bool]$hostile.replacement.staleInspectPass -and [bool]$hostile.replacement.staleIdentityPass -and [bool]$hostile.replacement.staleActuationPass -and $hostile.replacement.oldInspect.identity -eq 'stale' -and @($hostile.replacement.oldInspect.actions).Count -eq 0 -and $hostile.replacement.oldIdentity.outcome -eq 'stale' -and $null -eq $hostile.replacement.oldIdentity.backendNodeId -and [string]$hostile.replacement.oldClickError -match 'stale' -and $hostile.replacement.oldTarget -ne $hostile.replacement.newTarget -and $hostile.replacement.oldDocument -ne $hostile.replacement.newDocument -and $hostile.replacement.oldElement -ne $hostile.replacement.newElement
$checks+=C 'target-replacement-wrong-object-refusal' $replacementPass ([ordered]@{oldTarget=$hostile.replacement.oldTarget;oldDocument=$hostile.replacement.oldDocument;oldElement=$hostile.replacement.oldElement;oldStillPresent=$hostile.replacement.oldStillPresent;oldInspect=$hostile.replacement.oldInspect;oldIdentity=$hostile.replacement.oldIdentity;oldClickError=$hostile.replacement.oldClickError;staleInspectPass=$hostile.replacement.staleInspectPass;staleIdentityPass=$hostile.replacement.staleIdentityPass;staleActuationPass=$hostile.replacement.staleActuationPass;newTarget=$hostile.replacement.newTarget;newDocument=$hostile.replacement.newDocument;newElement=$hostile.replacement.newElement}) 'dead e_* is explicitly stale with zero actions/no backend node; actuation is rejected before CDP; replacement has new t_*/d_*/e_*'
$reattachChecks=@($reattach.checks)
$reattachPass=[bool]$reattach.ok -and $reattachChecks.Count -eq 10 -and @($reattachChecks|Where-Object{-not [bool]$_.pass}).Count -eq 0
$checks+=C 'controller-death-recovery-extra' $reattachPass ([ordered]@{ok=$reattach.ok;count=$reattachChecks.Count;names=@($reattachChecks|ForEach-Object{$_.name})}) '10/10 exact controller-death reattachment regression'
$failed=@($checks|Where-Object{-not $_.pass})
$result=[ordered]@{ok=($failed.Count-eq 0);runAtUtc=[DateTimeOffset]::UtcNow.ToString('o');classification='Build 002 mandatory hostile/recovery pre-freeze adjudication';checkCount=$checks.Count;passed=$checks.Count-$failed.Count;failed=$failed.Count;checks=$checks;artifacts=[ordered]@{core=$CoreEvidence;missingHostile=$HostileEvidence;reattach=$ReattachEvidence;adjudication=$Output}}
New-Item -ItemType Directory -Force -Path (Split-Path $Output)|Out-Null
[IO.File]::WriteAllText($Output,($result|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Depth 30
if(-not $result.ok){exit 1}
