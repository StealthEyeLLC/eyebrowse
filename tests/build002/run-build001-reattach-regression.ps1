param(
  [string]$KernelDll = 'X:\CODEeye\Worktrees\eyebrowse-build002-skill-plane\src\AgentBrowser.Kernel\bin\Release\net10.0\AgentBrowser.Kernel.dll',
  [string]$NodeExe = 'C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe',
  [string]$FixtureBase = 'http://127.0.0.1:18762',
  [string]$Profile = ('build002-reattach-' + [Guid]::NewGuid().ToString('N').Substring(0,12)),
  [switch]$KeepSubject
)

$ErrorActionPreference = 'Stop'
$worktree = 'X:\CODEeye\Worktrees\eyebrowse-build002-skill-plane'
$proofScript = Join-Path $worktree 'tests\build002\build001-reattach-proof.mjs'
$extensionPath = Join-Path $worktree 'extension\agent-bridge'
$runtime = "C:\AgentBrowser\runtime\$Profile"
$userData = "C:\AgentBrowser\Profiles\$Profile"
$artifactRoot = "X:\AgentBrowser\Artifacts\$Profile"
$measurements = Join-Path $artifactRoot 'measurements'
$pipe = "eyebrowse-$Profile"
$descriptorPath = Join-Path $runtime "$Profile.json"
$kernelRuntimePath = Join-Path $runtime "kernel-$Profile.json"
$beforePath = Join-Path $measurements 'reattach-before.json'
$afterPath = Join-Path $measurements 'reattach-after.json'
$resultPath = Join-Path $measurements 'reattach-regression.json'
$browserPid = $null
$currentKernelPid = $null

function Assert-True([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

function Write-Launcher([string]$Tag) {
  $launcher = Join-Path $runtime "start-$Tag.cmd"
  $stdout = Join-Path $runtime "kernel-$Tag.stdout.log"
  $stderr = Join-Path $runtime "kernel-$Tag.stderr.log"
  $run = '"C:\Program Files\dotnet\dotnet.exe" "' + $KernelDll + '" serve 1>"' + $stdout + '" 2>"' + $stderr + '"'
  $lines = @(
    '@echo off',
    "set `"EYEBROWSE_PROFILE_NAME=$Profile`"",
    "set `"EYEBROWSE_USER_DATA_DIR=$userData`"",
    "set `"EYEBROWSE_RUNTIME_DIR=$runtime`"",
    "set `"EYEBROWSE_PIPE_NAME=$pipe`"",
    "set `"EYEBROWSE_ARTIFACT_ROOT=$artifactRoot`"",
    "set `"EYEBROWSE_DOWNLOAD_ROOT=$artifactRoot\downloads`"",
    "set `"EYEBROWSE_EXTENSION_PATH=$extensionPath`"",
    'set "EYEBROWSE_LIMIT_EXTENSIONS=0"',
    'set "EYEBROWSE_HEADLESS=1"',
    'set "EYEBROWSE_CHROME_ARGS_JSON=["--enable-experimental-web-platform-features"]"',
    $run
  )
  [IO.File]::WriteAllLines($launcher, $lines, [Text.ASCIIEncoding]::new())
  return [pscustomobject]@{ Launcher=$launcher; Stdout=$stdout; Stderr=$stderr }
}

function Start-Kernel([string]$Tag) {
  Remove-Item $kernelRuntimePath -Force -ErrorAction SilentlyContinue
  $files = Write-Launcher $Tag
  Remove-Item $files.Stdout,$files.Stderr -Force -ErrorAction SilentlyContinue
  $command = 'cmd.exe /d /c ""' + $files.Launcher + '""'
  $created = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $command }
  Assert-True ($created.ReturnValue -eq 0) "Win32_Process.Create failed with $($created.ReturnValue)."
  $deadline = (Get-Date).AddSeconds(20)
  $readyRecord = $null
  do {
    Start-Sleep -Milliseconds 200
    $line = Get-Content $files.Stdout -ErrorAction SilentlyContinue | Where-Object { $_ -like '*"ready":true*' } | Select-Object -Last 1
    if ($line) {
      try { $readyRecord = $line | ConvertFrom-Json } catch { $readyRecord = $null }
    }
  } while (-not $readyRecord -and (Get-Date) -lt $deadline)
  if (-not $readyRecord) {
    $stdoutText = Get-Content $files.Stdout -Raw -ErrorAction SilentlyContinue
    $stderrText = Get-Content $files.Stderr -Raw -ErrorAction SilentlyContinue
    throw "Kernel readiness timeout for $Tag.`nSTDOUT:`n$stdoutText`nSTDERR:`n$stderrText"
  }
  $kernelPidValue = [int]$readyRecord.pid
  Assert-True ([bool](Get-Process -Id $kernelPidValue -ErrorAction SilentlyContinue)) "Kernel ready line reported dead PID $kernelPidValue."
  return [pscustomobject]@{ Pid=$kernelPidValue; Ready=$readyRecord; Files=$files }
}
function Invoke-Proof([string]$Mode, [string]$ExpectedPath = '') {
  $previous = $env:EYEBROWSE_PIPE_NAME
  try {
    $env:EYEBROWSE_PIPE_NAME = $pipe
    Push-Location $worktree
    try {
      if ($Mode -eq 'prepare') {
        $output = & $NodeExe $proofScript prepare 2>&1
      } else {
        $output = & $NodeExe $proofScript verify ('@' + $ExpectedPath) 2>&1
      }
      $exit = $LASTEXITCODE
    } finally {
      Pop-Location
    }
    $text = $output -join "`n"
    if ($exit -ne 0) { throw "Proof $Mode failed with exit $exit.`n$text" }
    return $text | ConvertFrom-Json
  } finally {
    $env:EYEBROWSE_PIPE_NAME = $previous
  }
}

try {
  Assert-True (Test-Path $KernelDll) "Kernel DLL does not exist: $KernelDll"
  Assert-True (Test-Path $NodeExe) "Node executable does not exist: $NodeExe"
  Assert-True (Test-Path $proofScript) "Proof script does not exist: $proofScript"
  Assert-True (Test-Path $extensionPath) "Bridge extension does not exist: $extensionPath"
  $health = Invoke-RestMethod "$FixtureBase/health"
  Assert-True ([bool]$health.ok) "Fixture host is not healthy at $FixtureBase."

  Remove-Item $runtime,$userData,$artifactRoot -Recurse -Force -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Force -Path $runtime,$measurements | Out-Null

  $first = Start-Kernel 'before'
  $currentKernelPid = $first.Pid
  $before = Invoke-Proof 'prepare'
  [IO.File]::WriteAllText($beforePath, ($before | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))

  Assert-True ([bool]$before.bridge.enabled) 'Bridge was not enabled before fixture navigation.'
  Assert-True ($before.document -like 'd_*') 'Prepare did not establish a logical document identity.'
  Assert-True ($before.name.id -like 'e_*') 'Prepare did not establish the Name logical element identity.'
  Assert-True ($before.submit.id -like 'e_*') 'Prepare did not establish the Submit logical element identity.'

  $browser = Get-CimInstance Win32_Process -Filter "Name='chrome.exe'" | Where-Object {
    $_.CommandLine -like "*$userData*" -and $_.CommandLine -notlike '*--type=*'
  } | Select-Object -First 1
  Assert-True ([bool]$browser) 'Root Chrome process for proof profile was not found.'
  $browserPid = [int]$browser.ProcessId

  Stop-Process -Id $currentKernelPid -Force
  $currentKernelPid = $null
  Start-Sleep -Milliseconds 500
  Assert-True (-not [bool](Get-Process -Id $first.Pid -ErrorAction SilentlyContinue)) 'Kernel did not die.'
  Assert-True ([bool](Get-Process -Id $browserPid -ErrorAction SilentlyContinue)) 'Chrome died with the controller.'

  $version = Invoke-RestMethod ("http://127.0.0.1:{0}/json/version" -f $before.status.port)
  $survivingBrowserId = ([uri]$version.webSocketDebuggerUrl).Segments[-1].TrimEnd('/')
  Assert-True ($survivingBrowserId -eq $before.status.browserId) 'Browser identity changed after controller death.'

  $second = Start-Kernel 'after'
  $currentKernelPid = $second.Pid
  Assert-True ($second.Pid -ne $first.Pid) 'Replacement kernel PID did not change.'

  $after = Invoke-Proof 'verify' $beforePath
  [IO.File]::WriteAllText($afterPath, ($after | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))

  $checks = @(
    [ordered]@{ name='bridge-before-navigation'; pass=[bool]$before.bridge.enabled; observed=$before.bridge.id },
    [ordered]@{ name='controller-died-browser-survived'; pass=$true; observed=[ordered]@{ oldKernel=$first.Pid; chrome=$browserPid; browserId=$survivingBrowserId } },
    [ordered]@{ name='replacement-controller'; pass=($second.Pid -ne $first.Pid); observed=[ordered]@{ old=$first.Pid; new=$second.Pid } },
    [ordered]@{ name='exact-target'; pass=[bool]$after.exactTarget; observed=$after.target; expected=$before.target },
    [ordered]@{ name='exact-document'; pass=[bool]$after.exactDocument; observed=$after.document; expected=$before.document },
    [ordered]@{ name='exact-name-element'; pass=[bool]$after.exactName; observed=$after.name.id; expected=$before.name.id },
    [ordered]@{ name='exact-submit-element'; pass=[bool]$after.exactSubmit; observed=$after.submit.id; expected=$before.submit.id },
    [ordered]@{ name='surviving-value'; pass=($after.name.beforeValue -eq $before.sentinel); observed=$after.name.beforeValue; expected=$before.sentinel },
    [ordered]@{ name='old-id-action-after-restart'; pass=[bool]$after.oldIdActionSucceeded; observed=$after.name.afterValue },
    [ordered]@{ name='post-restart-delta-same-id'; pass=[bool]$after.postRestartDelta; observed=@($after.delta.changed | ForEach-Object { $_.id }) }
  )
  $ok = -not ($checks | Where-Object { -not $_.pass })
  $result = [ordered]@{
    ok=$ok
    runAtUtc=[DateTimeOffset]::UtcNow.ToString('o')
    profile=$Profile
    kernelDll=$KernelDll
    chrome=[ordered]@{ pid=$browserPid; browserId=$before.status.browserId; port=$before.status.port; version=$before.status.browserVersion }
    before=[ordered]@{ kernelPid=$first.Pid; target=$before.target; rawTargetId=$before.rawTargetId; document=$before.document; name=$before.name; submit=$before.submit; sentinel=$before.sentinel; cursor=$before.cursor; bridge=$before.bridge }
    after=[ordered]@{ kernelPid=$second.Pid; target=$after.target; rawTargetId=$after.rawTargetId; document=$after.document; name=$after.name; submit=$after.submit; exactTarget=$after.exactTarget; exactDocument=$after.exactDocument; exactName=$after.exactName; exactSubmit=$after.exactSubmit; oldIdActionSucceeded=$after.oldIdActionSucceeded; postRestartDelta=$after.postRestartDelta }
    checks=$checks
    artifacts=[ordered]@{ before=$beforePath; after=$afterPath; result=$resultPath }
  }
  [IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
  $result | ConvertTo-Json -Depth 30
  if (-not $ok) { exit 1 }
}
finally {
  if (-not $KeepSubject) {
    if ($currentKernelPid) { Stop-Process -Id $currentKernelPid -Force -ErrorAction SilentlyContinue }
    if ($browserPid -and (Get-Process -Id $browserPid -ErrorAction SilentlyContinue)) {
      & taskkill.exe /PID $browserPid /T /F *> $null
    }
  }
}