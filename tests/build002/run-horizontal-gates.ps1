param(
  [string]$PipeName = 'eyebrowse-build002-release-preflight',
  [string]$EvidenceRoot = 'X:\AgentBrowser\Artifacts\build002-release-preflight\measurements\horizontal'
)

$ErrorActionPreference = 'Stop'
$root = 'X:\CODEeye\Worktrees\eyebrowse-build002-skill-plane'
$node = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe'
$helper = Join-Path $root 'tests\build002\horizontal-browser-helper.mjs'
$runner = Join-Path $root 'program-host\src\run.mjs'
$base = 'http://127.0.0.1:18762'
New-Item -ItemType Directory -Force -Path $EvidenceRoot,(Join-Path $EvidenceRoot 'downloads'),(Join-Path $EvidenceRoot 'authoritative') | Out-Null
$env:EYEBROWSE_PIPE_NAME = $PipeName

function Open-Page([string]$Url) {
  Push-Location $root
  $oldEap = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $raw = & $node $helper open $Url 2>&1
    $exit = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $oldEap
    Pop-Location
  }
  $text = ($raw | ForEach-Object { $_.ToString() }) -join "`n"
  if ($exit -ne 0) { throw "open failed: $Url`n$text" }
  return $text | ConvertFrom-Json
}

function Helper-Json([string[]]$HelperArgs) {
  Push-Location $root
  $oldEap = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $raw = & $node $helper @HelperArgs 2>&1
    $exit = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $oldEap
    Pop-Location
  }
  $text = ($raw | ForEach-Object { $_.ToString() }) -join "`n"
  if ($exit -ne 0) { throw "helper failed: $($HelperArgs -join ' ')`n$text" }
  return $text | ConvertFrom-Json
}

function Run-Program([string]$Program, $Arguments, [string]$Stem) {
  $argPath = Join-Path $EvidenceRoot "$Stem-args.json"
  $resultPath = Join-Path $EvidenceRoot "$Stem.json"
  [IO.File]::WriteAllText($argPath, ($Arguments | ConvertTo-Json -Depth 20 -Compress), [Text.UTF8Encoding]::new($false))
  Push-Location $root
  $oldEap = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    $raw = & $node $runner $Program ('@'+$argPath) 2>&1
    $exit = $LASTEXITCODE
  } finally {
    $ErrorActionPreference = $oldEap
    Pop-Location
  }
  $text = ($raw | ForEach-Object { $_.ToString() }) -join "`n"
  [IO.File]::WriteAllText($resultPath, $text, [Text.UTF8Encoding]::new($false))
  if ($exit -ne 0) { throw "program $Program failed exit $exit`n$text" }
  $programHost = $text | ConvertFrom-Json
  if (-not [bool]$programHost.ok) { throw "program host reported failure for $Program`n$text" }
  return [pscustomobject]@{ Host=$programHost; Path=$resultPath; Args=$argPath }
}

function Check([string]$Name, [bool]$Pass, $Observed, $Expected) {
  [ordered]@{ name=$Name; pass=$Pass; observed=$Observed; expected=$Expected }
}
$health = Invoke-RestMethod "$base/health"
if (-not $health.ok) { throw 'fixture host is not healthy' }
$checks = @()
$programs = [ordered]@{}

# current-page-export: Markdown + table CSV.
$exportPage = Open-Page "$base/horizontal/export"
$mdPath = Join-Path $EvidenceRoot 'horizontal-page.md'
$csvPath = Join-Path $EvidenceRoot 'horizontal-table.csv'
$md = Run-Program 'common.export-page' ([ordered]@{target=$exportPage.target;destination=$mdPath;format='markdown';maxChars=200000}) 'export-markdown'
$csv = Run-Program 'common.export-page' ([ordered]@{target=$exportPage.target;destination=$csvPath;format='csv';selector='#inventory';limit=100}) 'export-csv'
$programs.exportMarkdown = $md.Path
$programs.exportCsv = $csv.Path
$mdText = Get-Content $mdPath -Raw
$csvText = Get-Content $csvPath -Raw
$checks += Check 'export-markdown-program' ([bool]$md.Host.result.ok -and $md.Host.result.format -eq 'markdown') $md.Host.result 'markdown ok'
$checks += Check 'export-markdown-material' ($mdText.Contains('Horizontal export fixture') -and $mdText.Contains('generic page export') -and -not $mdText.Contains('<html')) ([ordered]@{chars=$mdText.Length;sha256=(Get-FileHash $mdPath -Algorithm SHA256).Hash}) 'useful semantic Markdown, not HTML dump'
$checks += Check 'export-csv-program' ([bool]$csv.Host.result.ok -and [int]$csv.Host.result.rows -eq 4) $csv.Host.result '4 table rows including header'
$checks += Check 'export-csv-material' ($csvText.Contains('Name,Count,Status') -and $csvText.Contains('gamma,8,ready')) ([ordered]@{bytes=(Get-Item $csvPath).Length;sha256=(Get-FileHash $csvPath -Algorithm SHA256).Hash}) 'exact fixture table CSV'

# artifact-download: bounded text + CSV + PDF resources with native download association and byte verification.
$downloadPage = Open-Page "$base/horizontal/downloads"
$resources = @(
  [ordered]@{name='text';url="$base/horizontal/download/text.txt";file='fixture-note.txt';magic='horizontal fixture text resource'},
  [ordered]@{name='csv';url="$base/horizontal/download/data.csv";file='fixture-data.csv';magic='name,count'},
  [ordered]@{name='pdf';url="$base/horizontal/download/report.pdf";file='fixture-report.pdf';magic='%PDF-1.4'}
)
$downloadEvidence = @()
foreach ($resource in $resources) {
  $dest = Join-Path (Join-Path $EvidenceRoot 'downloads') $resource.file
  $ref = Join-Path (Join-Path $EvidenceRoot 'authoritative') $resource.file
  Remove-Item $dest,$ref -Force -ErrorAction SilentlyContinue
  $run = Run-Program 'common.download-resource' ([ordered]@{target=$downloadPage.target;url=$resource.url;filename=$resource.file;destination=$dest;discoverTimeoutMs=10000;timeoutMs=30000}) ("download-"+$resource.name)
  & curl.exe --fail --location --silent --show-error --output $ref $resource.url
  if ($LASTEXITCODE -ne 0) { throw "authoritative resource fetch failed: $($resource.url)" }
  $destHash = (Get-FileHash $dest -Algorithm SHA256).Hash
  $refHash = (Get-FileHash $ref -Algorithm SHA256).Hash
  $head = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dest)[0..([Math]::Min(80,(Get-Item $dest).Length-1))])
  $native = $run.Host.result.download
  $artifact = $run.Host.result.artifact
  $pass = [bool]$run.Host.result.ok -and $native.state -eq 'completed' -and [string]$native.id -like 'dl_*' -and (Test-Path $dest) -and $destHash -eq $refHash -and $head.Contains($resource.magic)
  $checks += Check ("download-"+$resource.name) $pass ([ordered]@{downloadId=$native.id;state=$native.state;browserPath=$native.path;destination=$dest;bytes=(Get-Item $dest).Length;sha256=$destHash;authoritativeSha256=$refHash;artifact=$artifact}) 'completed native download, material bytes exact'
  $downloadEvidence += [ordered]@{name=$resource.name;program=$run.Path;downloadId=$native.id;browserPath=$native.path;destination=$dest;authoritative=$ref;sha256=$destHash;bytes=(Get-Item $dest).Length}
}

# forms: semantic ID discovery + local batch fill/select/check/fill + submit.
$formPage = Open-Page "$base/forms"
$formIds = Helper-Json @('form-ids',$formPage.target)
$required = @($formIds.name,$formIds.role,$formIds.enabled,$formIds.notes,$formIds.submit)
$checks += Check 'form-semantic-identities' (@($required | Where-Object { $null -eq $_ -or [string]$_.id -notlike 'e_*' }).Count -eq 0) ($required | Select-Object id,role,name) 'all fields mapped to e_*'
$formRun = Run-Program 'common.batch-form-fill' ([ordered]@{
  fields=@(
    [ordered]@{id=$formIds.name.id;kind='fill';value='Ada Horizontal'},
    [ordered]@{id=$formIds.role.id;kind='select';value='Researcher'},
    [ordered]@{id=$formIds.enabled.id;kind='check';value=$true},
    [ordered]@{id=$formIds.notes.id;kind='fill';value='Batch note'}
  );submit=$formIds.submit.id
}) 'forms-batch'
$programs.forms = $formRun.Path
Start-Sleep -Milliseconds 150
$formResultText = Helper-Json @('value',$formPage.target,"document.querySelector('#result').textContent")
$formResult = [string]$formResultText | ConvertFrom-Json
$formPass = [bool]$formRun.Host.result.ok -and [int]$formRun.Host.result.fields -eq 4 -and [bool]$formRun.Host.result.submitted -and $formResult.name -eq 'Ada Horizontal' -and $formResult.role -eq 'Researcher' -and [bool]$formResult.enabled -and $formResult.notes -eq 'Batch note'
$checks += Check 'forms-batch-submit' $formPass ([ordered]@{program=$formRun.Host.result;submitted=$formResult}) '4 semantic fields + submit exact'

# multi-tab: inspect three persistent targets without stealing primary tab.
$primary = Open-Page "$base/horizontal/export?primary=1"
$secondary = Open-Page "$base/forms?secondary=1"
$third = Open-Page "$base/virtual?third=1"
$beforeCurrent = Helper-Json @('activate',$primary.target)
$multi = Run-Program 'common.multi-tab-compare' ([ordered]@{targets=@($primary.target,$secondary.target,$third.target);maxTargets=3;textChars=5000}) 'multi-tab'
$programs.multiTab = $multi.Path
$afterCurrent = Helper-Json @('current')
$urls = @($multi.Host.result.tabs | ForEach-Object { $_.url })
$multiPass = [bool]$multi.Host.result.ok -and [int]$multi.Host.result.count -eq 3 -and $urls.Count -eq 3 -and $afterCurrent.target -eq $primary.target
$checks += Check 'multi-tab-preserve-primary' $multiPass ([ordered]@{primary=$primary.target;before=$beforeCurrent.target;after=$afterCurrent.target;tabs=$multi.Host.result.tabs}) '3 targets compared, primary remains current'

# bounded collection traversal: three pages, unique Next, explicit end.
$page1 = Open-Page "$base/horizontal/page/1"
$pagination = Run-Program 'common.search-pagination' ([ordered]@{target=$page1.target;collectExpression="Array.from(document.querySelectorAll('[data-item]')).map(x=>x.textContent)";maxPages=5;nextRole='button';nextName='Next';quietMs=75;timeoutMs=10000}) 'pagination'
$programs.pagination = $pagination.Path
$collected = @($pagination.Host.result.collected)
$flat = @($collected | ForEach-Object { $_.value })
$finalUrl = Helper-Json @('value',$page1.target,'location.href')
$paginationPass = [bool]$pagination.Host.result.ok -and [int]$pagination.Host.result.pages -eq 3 -and $pagination.Host.result.stopped -eq 'end' -and ($flat -join ',') -eq 'p1-a,p1-b,p2-a,p2-b,p3-a,p3-b' -and [string]$finalUrl -match '/horizontal/page/3$'
$checks += Check 'bounded-pagination' $paginationPass ([ordered]@{pages=$pagination.Host.result.pages;stopped=$pagination.Host.result.stopped;values=$flat;finalUrl=$finalUrl}) '3 pages, 6 values, explicit end'

$failed = @($checks | Where-Object { -not $_.pass })
$result = [ordered]@{
  ok=($failed.Count -eq 0)
  runAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
  pipe=$PipeName
  fixturePid=$health.pid
  checkCount=$checks.Count
  passed=$checks.Count-$failed.Count
  failed=$failed.Count
  checks=$checks
  programs=$programs
  downloads=$downloadEvidence
  artifacts=[ordered]@{markdown=$mdPath;csv=$csvPath;root=$EvidenceRoot}
}
$outPath = Join-Path $EvidenceRoot 'horizontal-gates.json'
[IO.File]::WriteAllText($outPath, ($result | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
$result | ConvertTo-Json -Depth 30
if (-not $result.ok) { exit 1 }
