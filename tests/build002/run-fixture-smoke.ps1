param(
  [string]$Kernel = "X:\CODEeye\Worktrees\eyebrowse-build002-skill-plane\src\AgentBrowser.Kernel\bin\Release\net10.0\AgentBrowser.Kernel.dll",
  [string]$ProfileName = "build002-cft",
  [string]$RuntimeDir = "C:\AgentBrowser\runtime\build002-cft",
  [string]$PipeName = "eyebrowse-build002-cft",
  [string]$ArtifactRoot = "X:\AgentBrowser\Artifacts\build002-cft",
  [string]$Output = "X:\AgentBrowser\Artifacts\build002-cft\measurements\fixture-smoke.json"
)

$ErrorActionPreference = 'Stop'
$env:EYEBROWSE_PROFILE_NAME = $ProfileName
$env:EYEBROWSE_RUNTIME_DIR = $RuntimeDir
$env:EYEBROWSE_PIPE_NAME = $PipeName
$checks = [System.Collections.Generic.List[object]]::new()
$details = [ordered]@{}

$script:rpcId = 0
function Call-Eye([string]$Method, [hashtable]$Params = @{}) {
  $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', $env:EYEBROWSE_PIPE_NAME, [System.IO.Pipes.PipeDirection]::InOut)
  try {
    $pipe.Connect(5000)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $reader = New-Object System.IO.StreamReader($pipe, $utf8, $false, 65536, $true)
    $writer = New-Object System.IO.StreamWriter($pipe, $utf8, 65536, $true)
    $writer.AutoFlush = $true
    $script:rpcId += 1
    $wire = @{ id=$script:rpcId; method=$Method; params=$Params } | ConvertTo-Json -Compress -Depth 50
    $writer.WriteLine($wire)
    $line = $reader.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) { throw "RPC $Method returned no response." }
    $response = $line | ConvertFrom-Json
    if (-not $response.ok) { throw "RPC $Method failed: $($response.error | ConvertTo-Json -Compress -Depth 20)" }
    return $response.result
  }
  finally {
    if ($reader) { try { $reader.Dispose() } catch {} }
    if ($writer) { try { $writer.Dispose() } catch {} }
    if ($pipe) { try { $pipe.Dispose() } catch {} }
  }
}
function Check([string]$Name, [bool]$Pass, $Observed, [string]$Expected) {
  $checks.Add([ordered]@{ name=$Name; pass=$Pass; expected=$Expected; observed=$Observed })
  if (-not $Pass) { Write-Warning "$Name FAILED: expected $Expected; observed $Observed" }
}

function Open-Activate([string]$Url) {
  $opened = Call-Eye 'target.open' @{ url=$Url }
  if (-not $opened.target.Id) { throw "target.open did not resolve a logical target for $Url" }
  [void](Call-Eye 'target.activate' @{ target=$opened.target.Id })
  [void](Call-Eye 'wait.until' @{ target=$opened.target.Id; expression='document.readyState === "complete"'; timeoutMs=10000; intervalMs=50 })
  return $opened.target.Id
}

$status = Call-Eye 'browser.status'
$details.browser = $status
Check 'isolated-profile' ($status.ProfileName -eq $ProfileName) $status.ProfileName $ProfileName
Check 'isolated-pipe' ($status.pipe -eq $PipeName) $status.pipe $PipeName

# Neutral current context and semantic rebound.
$identityTarget = Open-Activate 'http://127.0.0.1:18762/identity'
$current = Call-Eye 'context.current'
Check 'context-current-target' ($current.target -eq $identityTarget -and -not $current.ambiguous) $current 'exact active fixture target'
$surface = Call-Eye 'observe.surface' @{ target=$identityTarget }
$replaceOne = @(Call-Eye 'query.find' @{ target=$identityTarget; role='button'; name='Replace one'; limit=5 })
Check 'replace-one-resolved-once' ($replaceOne.Count -eq 1) $replaceOne.Count '1'
$oldOne = $replaceOne[0].Id
[void](Call-Eye 'action.click' @{ id=$oldOne })
$oneIdentity = Call-Eye 'identity.resolve' @{ id=$oldOne }
Check 'unique-semantic-rebound' ($oneIdentity.Outcome -eq 'rebound' -and $oneIdentity.Incarnation -ge 2) $oneIdentity 'rebound with increased incarnation'

$replaceTwo = @(Call-Eye 'query.find' @{ target=$identityTarget; role='button'; name='Replace two'; limit=5 })
Check 'replace-two-resolved-once-before-split' ($replaceTwo.Count -eq 1) $replaceTwo.Count '1'
$oldTwo = $replaceTwo[0].Id
[void](Call-Eye 'action.click' @{ id=$oldTwo })
$twoIdentity = Call-Eye 'identity.resolve' @{ id=$oldTwo }
Check 'ambiguous-split-abstention' ($twoIdentity.Outcome -eq 'ambiguous' -and @($twoIdentity.Candidates).Count -ge 2) $twoIdentity 'ambiguous with >=2 candidates'

# Same-document SPA navigation preserves d_*.
$spaTarget = Open-Activate 'http://127.0.0.1:18762/spa'
$spaBefore = Call-Eye 'observe.surface' @{ target=$spaTarget }
$routeA = @(Call-Eye 'query.find' @{ target=$spaTarget; role='button'; name='Route A'; limit=5 })
[void](Call-Eye 'action.click' @{ id=$routeA[0].Id })
$spaAfter = Call-Eye 'observe.surface' @{ target=$spaTarget }
Check 'spa-document-continuity' ($spaBefore.Document -eq $spaAfter.Document -and $spaAfter.Url -match '#a$') ([ordered]@{before=$spaBefore.Document;after=$spaAfter.Document;url=$spaAfter.Url}) 'same d_* with #a route'

# BFCache should restore the original document/heap when Chrome keeps it.
$bfcTarget = Open-Activate 'http://127.0.0.1:18762/bfcache/a'
$bfcA = Call-Eye 'observe.surface' @{ target=$bfcTarget }
$nonceA = (Call-Eye 'js.evaluate' @{ target=$bfcTarget; expression='globalThis.heapNonce' }).result.value
$toB = @(Call-Eye 'query.find' @{ target=$bfcTarget; role='link'; name='Go B'; limit=5 })
[void](Call-Eye 'action.click' @{ id=$toB[0].Id })
[void](Call-Eye 'wait.until' @{ target=$bfcTarget; expression='location.pathname === "/bfcache/b"'; timeoutMs=10000; intervalMs=50 })
$bfcB = Call-Eye 'observe.surface' @{ target=$bfcTarget }
$back = @(Call-Eye 'query.find' @{ target=$bfcTarget; role='link'; name='Back A'; limit=5 })
[void](Call-Eye 'action.click' @{ id=$back[0].Id })
[void](Call-Eye 'wait.until' @{ target=$bfcTarget; expression='location.pathname === "/bfcache/a"'; timeoutMs=10000; intervalMs=50 })
$bfcRestored = Call-Eye 'observe.surface' @{ target=$bfcTarget }
$nonceRestored = (Call-Eye 'js.evaluate' @{ target=$bfcTarget; expression='globalThis.heapNonce' }).result.value
Check 'bfcache-document-restored' ($bfcA.Document -eq $bfcRestored.Document -and $bfcA.Document -ne $bfcB.Document) ([ordered]@{a=$bfcA.Document;b=$bfcB.Document;restored=$bfcRestored.Document}) 'A d_* restored; B has different d_*'
Check 'bfcache-heap-restored' ($nonceA -eq $nonceRestored) ([ordered]@{before=$nonceA;after=$nonceRestored}) 'same heap nonce'

# Runtime developer tools: discovery, execution, DOM correlation, disappearance.
$runtimeTarget = Open-Activate 'http://127.0.0.1:18762/runtime-tools'
$runtimeTools = @(Call-Eye 'runtime_tools.list' @{ target=$runtimeTarget })
Check 'runtime-tools-discovered' ((@($runtimeTools.Name) -contains 'increment') -and (@($runtimeTools.Name) -contains 'get-node')) @($runtimeTools.Name) 'increment and get-node'
$increment = Call-Eye 'runtime_tools.execute' @{ target=$runtimeTarget; name='increment'; input=@{by=3} }
Check 'runtime-tool-executes' ($increment.Value.value -eq 3) $increment 'value=3'
$getNode = Call-Eye 'runtime_tools.execute' @{ target=$runtimeTarget; name='get-node'; input=@{} }
Check 'runtime-tool-dom-correlates' ($getNode.Element -match '^e_\d+$' -and $getNode.BackendNodeId) $getNode 'existing e_* + backend node'
[void](Call-Eye 'cdp.send' @{ target=$runtimeTarget; method='Page.navigate'; params=@{url='http://127.0.0.1:18762/runtime-tools-empty'} })
[void](Call-Eye 'wait.until' @{ target=$runtimeTarget; expression='location.pathname === "/runtime-tools-empty"'; timeoutMs=10000; intervalMs=50 })
$runtimeGone = @(Call-Eye 'runtime_tools.list' @{ target=$runtimeTarget })
Check 'runtime-tools-document-scoped' ($runtimeGone.Count -eq 0) $runtimeGone.Count '0 after navigation'

# Forms and browser-native download/artifact.
$formsTarget = Open-Activate 'http://127.0.0.1:18762/forms'
[void](Call-Eye 'observe.surface' @{ target=$formsTarget })
$name = @(Call-Eye 'query.find' @{ target=$formsTarget; role='textbox'; name='Name'; limit=5 })
$role = @(Call-Eye 'query.find' @{ target=$formsTarget; role='combobox'; name='Role'; limit=5 })
$enabled = @(Call-Eye 'query.find' @{ target=$formsTarget; role='checkbox'; name='Enabled'; limit=5 })
$submit = @(Call-Eye 'query.find' @{ target=$formsTarget; role='button'; name='Submit'; limit=5 })
Check 'form-semantic-resolution' ($name.Count -eq 1 -and $role.Count -eq 1 -and $enabled.Count -eq 1 -and $submit.Count -eq 1) ([ordered]@{name=$name.Count;role=$role.Count;enabled=$enabled.Count;submit=$submit.Count}) 'one semantic object for each field'
[void](Call-Eye 'action.fill' @{ id=$name[0].Id; text='Build 002' })
[void](Call-Eye 'action.select' @{ id=$role[0].Id; values=@('Operator') })
[void](Call-Eye 'action.check' @{ id=$enabled[0].Id })
[void](Call-Eye 'action.click' @{ id=$submit[0].Id })
$formResult = (Call-Eye 'js.evaluate' @{ target=$formsTarget; expression='document.querySelector("#result").textContent' }).result.value | ConvertFrom-Json
Check 'form-state-correct' ($formResult.name -eq 'Build 002' -and $formResult.role -eq 'Operator' -and $formResult.enabled) $formResult 'submitted semantic values'

$downloadBefore = @(Call-Eye 'download.list')
$downloadLink = @(Call-Eye 'query.find' @{ target=$formsTarget; role='link'; name='Download fixture'; limit=5 })
[void](Call-Eye 'action.click' @{ id=$downloadLink[0].Id })
$deadline = (Get-Date).AddSeconds(10); $newDownload = $null
while((Get-Date) -lt $deadline -and -not $newDownload) {
  Start-Sleep -Milliseconds 100
  $downloads = @(Call-Eye 'download.list')
  $newDownload = $downloads | Where-Object { $downloadBefore.Id -notcontains $_.Id } | Select-Object -First 1
}
Check 'download-begin-observed' ($null -ne $newDownload) $newDownload 'new browser download'
if ($newDownload) {
  $completed = Call-Eye 'download.wait' @{ id=$newDownload.Id; timeoutMs=10000 }
  Check 'download-completes' ($completed.State -eq 'completed') $completed.State 'completed'
  $saved = Call-Eye 'download.save' @{ id=$newDownload.Id; destination=(Join-Path $ArtifactRoot 'saved-fixture.txt') }
  Check 'download-artifact-materialized' (Test-Path $saved.Path) $saved 'material file exists'
}

$screenshot = Call-Eye 'screenshot.full_page' @{ target=$formsTarget; destination=(Join-Path $ArtifactRoot 'forms.png') }
Check 'browser-screenshot-artifact' (Test-Path $screenshot.Path) $screenshot.Path 'PNG artifact exists'
$metrics = Call-Eye 'performance.metrics' @{ target=$formsTarget }
Check 'performance-metrics-on-demand' (@($metrics.metrics).Count -gt 0) @($metrics.metrics).Count '>0 metrics'

# Offscreen/application data can answer without scrolling rendered rows.
$virtualTarget = Open-Activate 'http://127.0.0.1:18762/virtual'
$virtual = (Call-Eye 'js.evaluate' @{ target=$virtualTarget; expression='({total:__records.length, group3:__records.filter(r=>r.group===3).length, rendered:document.querySelectorAll("[role=row]").length, scrollY})' }).result.value
Check 'offscreen-data-lens' ($virtual.total -eq 5000 -and $virtual.rendered -eq 40 -and $virtual.group3 -gt 40 -and $virtual.scrollY -eq 0) $virtual '5000 application records, only 40 rendered, no scroll'

# Cross-origin frame pressure: record whether Chrome exposes an iframe target separately.
$oopifTarget = Open-Activate 'http://127.0.0.1:18762/oopif'
[void](Call-Eye 'observe.surface' @{ target=$oopifTarget })
$targetInventory = @(Call-Eye 'target.list')
$frameTargets = @($targetInventory | Where-Object { $_.Type -eq 'iframe' -or $_.Url -like '*oopif-child*' })
$details.oopifTargets = $frameTargets
Check 'cross-origin-frame-visible-to-target-census' ($frameTargets.Count -ge 1) $frameTargets.Count '>=1 iframe/cross-origin child target'

# WebMCP: on headless Chrome the test may be unavailable; record without manufacturing a pass.
$webmcpTarget = Open-Activate 'http://127.0.0.1:18762/webmcp'
$webmcpStatus = (Call-Eye 'js.evaluate' @{ target=$webmcpTarget; expression='document.querySelector("#webmcp-status").textContent' }).result.value
$webmcpTools = @(Call-Eye 'webmcp.list' @{ target=$webmcpTarget })
$details.webmcp = [ordered]@{pageStatus=$webmcpStatus;tools=$webmcpTools}
Check 'webmcp-headless-observation-recorded' ($webmcpStatus -in @('webmcp-unavailable','webmcp-registered')) $webmcpStatus 'explicit registered/unavailable state'

# Many-target lazy-cognition pressure.
$scaleCreated = [System.Collections.Generic.List[string]]::new()
$proc = Get-Process -Id $status.kernelPid -ErrorAction SilentlyContinue
$chromeRoot = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'chrome.exe' -and $_.CommandLine -like ('*'+(Join-Path 'C:\AgentBrowser\Profiles' $ProfileName)+'*') } | Sort-Object ProcessId | Select-Object -First 1
$chromeBefore = if($chromeRoot){Get-Process -Id $chromeRoot.ProcessId}else{$null}
$memoryBefore = if($chromeBefore){$chromeBefore.WorkingSet64}else{0}
$cpuBefore = if($chromeBefore){$chromeBefore.CPU}else{0}
$sw=[Diagnostics.Stopwatch]::StartNew()
for($i=1;$i -le 100;$i++) {
  $opened=Call-Eye 'target.open' @{url="http://127.0.0.1:18762/many?i=$i"}
  [void]$scaleCreated.Add($opened.target.Id)
}
$inventory = @(Call-Eye 'target.cognition')
$scaleRows = @($inventory | Where-Object { $scaleCreated -contains $_.Target })
$sw.Stop()
$chromeAfter = if($chromeRoot){Get-Process -Id $chromeRoot.ProcessId}else{$null}
$memoryAfter = if($chromeAfter){$chromeAfter.WorkingSet64}else{0}
$cpuAfter = if($chromeAfter){$chromeAfter.CPU}else{0}
$attachedScale = @($scaleRows | Where-Object { $_.Attached -or $_.State -ne 'cold' })
Check 'scale-100-targets-created' ($scaleRows.Count -eq 100) $scaleRows.Count '100'
Check 'scale-lazy-zero-deep-activation-before-observe' ($attachedScale.Count -eq 0) $attachedScale.Count '0 of the 100 created targets hot/attached'
$details.scale=[ordered]@{created=100;elapsedMs=$sw.ElapsedMilliseconds;attachedOrNonCold=$attachedScale.Count;chromeWorkingSetBefore=$memoryBefore;chromeWorkingSetAfter=$memoryAfter;chromeCpuSecondsBefore=$cpuBefore;chromeCpuSecondsAfter=$cpuAfter}

$passed = @($checks | Where-Object pass).Count
$failed = @($checks | Where-Object { -not $_.pass }).Count
$result = [ordered]@{
  runAtUtc = [DateTimeOffset]::UtcNow
  passed = $passed
  failed = $failed
  checks = $checks
  details = $details
}
$dir = Split-Path $Output
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$result | ConvertTo-Json -Depth 100 | Set-Content -Path $Output -Encoding utf8
Write-Output ($result | ConvertTo-Json -Depth 12)
if ($failed -gt 0) { exit 1 }
