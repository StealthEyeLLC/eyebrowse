param(
  [string]$ArtifactRoot = 'X:\AgentBrowser\Artifacts\build002-release-second-site-v3',
  [string]$ExpectedOrigin = 'http://127.0.0.2:18762'
)

$ErrorActionPreference = 'Stop'
$prePath = Join-Path $ArtifactRoot 'measurements\precondition.json'
$evidenceRoot = Join-Path $ArtifactRoot 'measurements\second-site'
$workflowPath = Join-Path $evidenceRoot 'browser-workflow.json'
$downloadPath = Join-Path $evidenceRoot 'attachment-download.json'
$materialPath = Join-Path $evidenceRoot 'message-37.txt'
$outPath = Join-Path $evidenceRoot 'second-site-controlled.json'

function Read-Json([string]$Path) {
  if (-not (Test-Path $Path)) { throw "Missing evidence artifact: $Path" }
  return Get-Content $Path -Raw | ConvertFrom-Json
}
function Check([string]$Name,[bool]$Pass,$Observed,$Expected) {
  [ordered]@{name=$Name;pass=$Pass;observed=$Observed;expected=$Expected}
}

$pre = Read-Json $prePath
$workflow = Read-Json $workflowPath
$downloadHost = Read-Json $downloadPath
$download = $downloadHost.result.download
$artifact = $downloadHost.result.artifact
$expectedMaterial = "Second Mail attachment 37`ninvoice-evidence-37`n"
$material = if(Test-Path $materialPath){[IO.File]::ReadAllText($materialPath)}else{$null}
$composeIds = @($workflow.compose.composeId,$workflow.compose.toId,$workflow.compose.subjectId,$workflow.compose.bodyId,$workflow.compose.sendId)
$authCookies = @($workflow.persistence.authCookies)
$checks = @(
  (Check 'fresh-precondition' ([bool]$pre.ok -and [int]$pre.cookieCount -eq 0 -and @($pre.pages).Count -eq 1 -and $pre.pages[0].url -eq 'about:blank') ([ordered]@{ok=$pre.ok;cookies=$pre.cookieCount;pages=$pre.pages}) 'bridge-ready, zero cookies, only about:blank'),
  (Check 'unauthenticated-redirect' ([string]$workflow.unauthenticated.url -eq "$ExpectedOrigin/second-mail/sign-in") $workflow.unauthenticated "$ExpectedOrigin/second-mail/sign-in"),
  (Check 'authenticated-primary' ($workflow.authenticated.origin -eq $ExpectedOrigin -and $workflow.authenticated.authState -eq 'authenticated' -and [string]$workflow.authenticated.target -like 't_*' -and [string]$workflow.authenticated.document -like 'd_*') $workflow.authenticated 'authenticated t_*/d_* on second-site origin'),
  (Check 'virtualized-inbox' ($workflow.authenticated.initialCount -eq 'showing 25 of 240') $workflow.authenticated.initialCount 'showing 25 of 240'),
  (Check 'persistent-auth-second-tab' ($workflow.persistence.origin -eq $ExpectedOrigin -and $workflow.persistence.authState -eq 'authenticated' -and [string]$workflow.persistence.secondTarget -like 't_*' -and $workflow.persistence.secondTarget -ne $workflow.authenticated.target) $workflow.persistence 'authenticated second t_* on same second-site origin'),
  (Check 'http-only-cookie-authority' ($authCookies.Count -eq 1 -and $authCookies[0].name -eq 'second_mail_auth' -and $authCookies[0].value -eq 'yes' -and $authCookies[0].domain -eq '127.0.0.2' -and [bool]$authCookies[0].httpOnly) $authCookies 'one HttpOnly second_mail_auth cookie for 127.0.0.2'),
  (Check 'bounded-search' ([string]$workflow.search.control -like 'e_*' -and $workflow.search.count -eq 'showing 6 of 6' -and [int]$workflow.search.messageButtons -ge 6) $workflow.search 'semantic search e_*, six invoice results'),
  (Check 'semantic-message-open' ([string]$workflow.search.selected -like 'e_*' -and [string]$workflow.search.selectedName -match '^Open Invoice 037 ' -and $workflow.message.status -eq 'opened:37') ([ordered]@{selected=$workflow.search.selected;name=$workflow.search.selectedName;status=$workflow.message.status}) 'semantic Invoice 037 button opened'),
  (Check 'semantic-attachment-link' ([string]$workflow.message.attachmentId -like 'e_*' -and $workflow.message.attachmentUrl -eq "$ExpectedOrigin/second-mail/attachment/37.txt") ([ordered]@{id=$workflow.message.attachmentId;url=$workflow.message.attachmentUrl}) "$ExpectedOrigin/second-mail/attachment/37.txt"),
  (Check 'native-authenticated-download' ([bool]$downloadHost.ok -and [bool]$downloadHost.result.ok -and [string]$download.id -like 'dl_*' -and $download.state -eq 'completed' -and [int64]$download.receivedBytes -eq 46 -and [int64]$download.totalBytes -eq 46 -and $download.url -eq $workflow.message.attachmentUrl) $download 'completed dl_* with 46/46 bytes'),
  (Check 'download-artifact-materialized' ([string]$artifact.id -like 'a_*' -and $artifact.path -eq $materialPath -and [int64]$artifact.size -eq 46 -and (Test-Path $materialPath)) $artifact 'registered 46-byte artifact at requested path'),
  (Check 'attachment-bytes-exact' ($material -eq $expectedMaterial) ([ordered]@{sha256=if(Test-Path $materialPath){(Get-FileHash $materialPath -Algorithm SHA256).Hash}else{$null};text=$material}) $expectedMaterial),
  (Check 'semantic-compose-controls' ($composeIds.Count -eq 5 -and @($composeIds|Where-Object{[string]$_ -notlike 'e_*'}).Count -eq 0) $composeIds 'five semantic e_* compose controls'),
  (Check 'rich-text-send' ($workflow.compose.sent.to -eq 'recipient@example.test' -and $workflow.compose.sent.subject -eq 'Second-site deterministic message' -and $workflow.compose.sent.body -eq 'Rich text body from semantic contenteditable control.' -and [bool]$workflow.compose.composerHidden) $workflow.compose 'exact To/Subject/Body and composer hidden after Send'),
  (Check 'primary-target-preserved' ($workflow.currentTarget -eq $workflow.authenticated.target) ([ordered]@{primary=$workflow.authenticated.target;current=$workflow.currentTarget}) 'same primary t_* after second-tab and compose workflow')
)
$failed=@($checks|Where-Object{-not $_.pass})
$result=[ordered]@{
  ok=($failed.Count -eq 0)
  runAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
  classification='controlled pre-subject second-site generalization; NOT final live Gmail gate'
  origin=$ExpectedOrigin
  checkCount=$checks.Count
  passed=$checks.Count-$failed.Count
  failed=$failed.Count
  checks=$checks
  artifacts=[ordered]@{precondition=$prePath;browserWorkflow=$workflowPath;download=$downloadPath;material=$materialPath;adjudication=$outPath}
}
[IO.File]::WriteAllText($outPath,($result|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))
$result|ConvertTo-Json -Depth 30
if(-not $result.ok){exit 1}