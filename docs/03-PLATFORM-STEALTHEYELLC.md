# 03 — Target Platform: STEALTHEYELLC

Status: **Canonical machine inventory**  
Target machine: **STEALTHEYELLC**  
Primary interactive user: **STEALTHEYELLC\StealthEye**  
Latest refresh: **2026-08-08**

## 1. Measurement notes

This document separates:

- **refreshed facts** measured through the Eye connector during repository initialization on 2026-08-08;
- **same-session prior live probes** already measured earlier on 2026-08-08, especially the direct Chrome/CDP/GPU feasibility probe.

The Eye control process currently reports:

```text
product: StealthEye
executable: eye
version: 0.1.0.0
machine: STEALTHEYELLC
identity: NT AUTHORITY\SYSTEM
```

Because Eye executes in SYSTEM context, command lookup/path observations are explicitly described as SYSTEM-context observations where relevant. GUI work for eyebrowse should ultimately use an interactive user-session SessionHost rather than relying on session-0 SYSTEM execution.

## 2. Hardware and operating system

### Machine

```text
Manufacturer: HP
Model: OMEN Gaming Laptop 16-ap0xxx
System type: x64-based PC
```

### Operating system

Latest direct `cmd /c ver` refresh:

```text
Microsoft Windows [Version 10.0.26100.8973]
```

Canonical platform classification:

```text
Windows 11 Home
64-bit / x64
build family 26100
current measured revision 8973
```

### CPU

Measured:

```text
AMD Ryzen 9 8940HX with Radeon Graphics
16 physical cores
32 logical processors
```

This is substantially more than required for a persistent Chrome process, event-driven semantic kernel, multiple browser targets, local TypeScript Program Host, and parallel state processing.

### RAM

Measured physical memory:

```text
33,342,455,808 bytes
≈ 33.34 GB decimal
≈ 31.05 GiB
```

Design implication: keep hot semantic state for active workflows in memory while using hot/warm/cold target tiers to avoid needless duplication across many tabs.

## 3. GPU and display

### Discrete GPU

Measured through `nvidia-smi`:

```text
NVIDIA GeForce RTX 5060 Laptop GPU
8,151 MiB VRAM
NVIDIA driver 592.19
```

Windows WMI reports the device as present; its `AdapterRAM` field truncates/under-reports modern VRAM and must not be used as the authoritative VRAM measurement. `nvidia-smi` is authoritative for the NVIDIA memory figure in this document.

### Integrated GPU

Measured:

```text
AMD Radeon(TM) 610M
```

### Display

Measured/observed:

```text
1920 × 1200
144 Hz (same-session live inventory)
```

### Same-session Chrome rendering probe

A prior live Chrome 151 CDP/SystemInfo probe on this machine showed Chrome rendering through:

```text
ANGLE → D3D11 → NVIDIA RTX 5060 Laptop GPU
```

with Chrome GPU features including:

- GPU compositing;
- rasterization;
- multiple raster threads;
- 2D Canvas;
- OpenGL;
- WebGL;
- WebGPU;
- hardware video encode/decode.

This makes the machine suitable for headful GPU Chrome, high-frequency screenshot capture, local image/frame preprocessing, and later GPU-assisted vision/temporal analysis.

## 4. Storage

Latest measured volumes:

### C:

```text
Filesystem: NTFS
Total: 700,747,608,064 bytes
      ≈ 700.75 GB decimal
      ≈ 652.62 GiB
Free:  616,882,794,496 bytes
      ≈ 616.88 GB decimal
      ≈ 574.52 GiB
Health: Healthy
```

### X:

```text
Filesystem: ReFS
Total: 322,122,547,200 bytes
      ≈ 322.12 GB decimal
      = 300.00 GiB
Free:  317,897,478,144 bytes
      ≈ 317.90 GB decimal
      ≈ 296.07 GiB
Health: Healthy
```

Same-session disk inventory identified the physical device as a Samsung NVMe-class ~1 TB device.

### Canonical filesystem placement

Use:

```text
C:\AgentBrowser\Profiles\...
```

for Chrome persistent user-data directories and other state that benefits from ordinary NTFS compatibility.

Use:

```text
X:\AgentBrowser\Artifacts\...
X:\AgentBrowser\Temp\...
```

for downloads, screenshots, recordings, PDFs, generated files, large response bodies, benchmark output, and disposable bulk data.

Do not casually move the actual Chrome user-data directory onto ReFS without a specific compatibility/performance reason and explicit testing.

## 5. Installed browsers

Latest refreshed executable/version measurements:

### Google Chrome

```text
Path: C:\Program Files\Google\Chrome\Application\chrome.exe
Version: 151.0.7922.109
```

This is the canonical durable eyebrowse browser.

### Microsoft Edge

```text
Path: C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
Version: 151.0.4129.72
```

Edge is a future secondary Chromium/BiDi target, not the canonical Build 001 browser.

### WebView2

Same-session inventory measured WebView2 at the Edge 151 generation. WebView2 is not the primary browser architecture but remains a useful possible embedded/native experiment later.

### Other browsers

Same-session inventory did not find Firefox, Brave, Vivaldi, or Opera in the audited standard installation locations.

## 6. Existing browser profiles

### Chrome

Latest refresh:

```text
C:\Users\StealthEye\AppData\Local\Google\Chrome\User Data
→ NOT_FOUND
```

There is no normal existing Chrome profile in the standard StealthEye user location.

This is favorable for eyebrowse because `C:\AgentBrowser\Profiles\...` can become the explicit Chrome identity root without needing to reinterpret an existing normal Chrome profile.

### Edge

Latest refresh found:

```text
C:\Users\StealthEye\AppData\Local\Microsoft\Edge\User Data\Default
```

An Edge Default profile exists.

## 7. Chrome/CDP feasibility already demonstrated

Earlier on the same date, the installed Chrome 151 was launched against a disposable non-default profile and successfully connected through remote debugging.

The live browser exposed:

- browser-level CDP WebSocket;
- ordinary page target;
- extension background-page/service-worker targets;
- Chrome/browser-UI targets.

The endpoint identified the installed browser as:

```text
Chrome 151.0.7922.109
```

The runtime protocol exposed major domains including:

```text
Accessibility
Browser
CacheStorage
DOM
DOMSnapshot
DOMStorage
Extensions
Fetch
IndexedDB
Input
Media
Network
Page
Runtime
ServiceWorker
Storage
Target
WebAudio
WebAuthn
WebMCP
```

Same-session protocol inspection also found notable current commands/events including:

```text
Page.getAnnotatedPageContent
Extensions.loadUnpacked
Target.setAutoAttach
Accessibility.nodesUpdated
Browser.downloadWillBegin
Browser.downloadProgress
Network.webSocketFrameReceived
Network.webSocketFrameSent
Network.eventSourceMessageReceived
Fetch.continueResponse
Storage tracking operations
ServiceWorker lifecycle events
WebMCP.invokeTool
```

These prior live observations materially justify the direct-dynamic-CDP architecture.

## 8. Browser debugging/listeners and policies

Same-session inventory found no pre-existing listeners on common automation/debug ports such as 9222/9223/9515/4444 at inventory time.

Latest machine-level registry refresh:

```text
HKLM\SOFTWARE\Policies\Google\Chrome → absent
HKLM\SOFTWARE\Policies\Microsoft\Edge → absent
```

Earlier same-session inventory also found no relevant Chrome/Edge policy keys in the audited standard machine/user policy locations.

Design implication: eyebrowse can select dynamic nonzero loopback debugging ports and should not assume 9222.

## 9. Installed developer runtimes/tools

Latest SYSTEM-context command lookup:

### Present

```text
.NET SDK     10.0.302
Git          2.55.0.windows.3
WSL          2.7.11.0
PowerShell   Windows PowerShell 5.1 family (same-session inventory)
```

WSL version output also reports:

```text
WSL kernel: 6.18.33.2-2
WSLg: 1.0.73.2
Windows version reported by WSL: 10.0.26100.8973
```

An attempt to enumerate WSL distributions from the Eye SYSTEM context returns `WSL_E_LOCAL_SYSTEM_NOT_SUPPORTED`; this is a limitation of executing WSL as LocalSystem, not evidence that WSL is unavailable to the interactive StealthEye user. Earlier same-session inventory found Ubuntu 24.04 WSL2 registered.

### Not found in current Eye/SYSTEM PATH

```text
node
npm
python
py
rustc
cargo
java
javac
cmake
ninja
docker
podman
```

### Package managers not found in current Eye/SYSTEM PATH

```text
winget
choco
scoop
```

This does not prove that user-scoped App Installer/winget is unavailable to the interactive session; it means these commands were not available in the current SYSTEM command-resolution context.

### Same-session broader developer-tool inventory

Earlier inventory found no Visual Studio/C++ Build Tools, CMake/Ninja, Docker, Python, Rust, or Java installations in the audited standard locations.

## 10. Build 001 dependency consequence

The machine already has the principal kernel runtime:

```text
.NET 10 SDK 10.0.302
```

Build 001 should install/add **Node 24 LTS** for:

- TypeScript extension build;
- Agent Program Host;
- optional JS build tooling.

Do not add Python/Rust/C++/Java simply because browser-agent projects commonly use them.

## 11. Windows UI/accessibility/capture capability

Latest type/assembly refresh confirmed:

```text
UIAutomationClient assembly: available
Windows.Media.Ocr.OcrEngine WinRT type: available
Windows.Graphics.Capture.GraphicsCaptureItem WinRT type: available
```

Same-session inventory also established D3D/DXGI/Windows graphics APIs.

Architectural consequence:

A later interactive .NET SessionHost can reasonably target:

- Microsoft UI Automation;
- Windows Graphics Capture;
- Win32 window management;
- clipboard;
- native mouse/keyboard input;
- OCR/capture workflows subject to normal Windows packaging/session requirements.

Build 001 intentionally does not depend on the SessionHost.

## 12. Networking

Latest refreshed adapter inventory:

### Ethernet

```text
Realtek Gaming GbE Family Controller
Status: Up
Link speed: 1 Gbps
```

### Wi-Fi

```text
MediaTek Wi-Fi 6E MT7922 (RZ616) 160MHz PCIe Adapter
Status at refresh: Disconnected
```

### WSL virtual network

```text
vEthernet (WSL (Hyper-V firewall))
Hyper-V Virtual Ethernet Adapter
Status: Up
Reported link speed: 10 Gbps
```

Same-session inventory found no configured WinHTTP/per-user web proxy of architectural significance.

Windows includes useful built-in networking utilities including `pktmon`, `netsh`, `curl`, and OpenSSH. A packet/MITM layer is not part of the default architecture because CDP exposes richer decrypted browser/application traffic for the primary use case.

## 13. Execution topology implication

The current Eye connector runs as:

```text
NT AUTHORITY\SYSTEM
```

The logged-on interactive Windows user measured through `Win32_ComputerSystem.UserName` is:

```text
STEALTHEYELLC\StealthEye
```

Therefore:

- process supervision/infrastructure may be performed from a SYSTEM-level supervisor when useful;
- browser GUI/native interaction must live in the interactive StealthEye desktop session;
- the end-state architecture keeps `AgentBrowser.SessionHost` as a separate interactive process for this reason.

## 14. Machine-specific architectural conclusions

STEALTHEYELLC is unusually well suited to the full eyebrowse architecture:

1. **CPU:** enough cores for concurrent browser, semantic, network, Program Host, and native work.
2. **RAM:** enough for multiple Chrome renderers and resident semantic state.
3. **RTX 5060:** enough GPU capability for headful Chrome plus local image/frame preprocessing and selected local multimodal experiments.
4. **Storage:** enough fast local storage for multiple profiles, Chromium experiments if ever required, and large artifact collections.
5. **.NET 10 already installed:** strong fit for the Windows-centric kernel/native architecture.
6. **Chrome 151 already proven over direct CDP:** no hypothetical browser-control assumption is required for Build 001.
7. **No existing normal Chrome profile:** clean dedicated eyebrowse profile layout.
8. **UIA/Graphics Capture/OCR APIs present:** later native/visual boundary is feasible without changing target machines.
9. **WSL available to the user environment:** useful auxiliary development capability if later needed, but not part of the browser kernel.

No cloud/browser-hosting dependency is required to begin or to reach a highly capable local version.

## 15. Facts that should be re-measured automatically later

Once Build 001 exists, add a non-invasive `eyebrowse doctor`/inventory command that reports current operational facts such as:

```text
Windows build
Chrome executable/version
CDP protocol/version/capabilities
profile roots
GPU/display summary
.NET/Node versions
artifact/profile free space
current eyebrowse runtime descriptors
```

This is environment discovery, not an audit/receipt system.
