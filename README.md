# NovaNet

Internet optimizer for Windows 10/11: live latency monitoring, one-click tweaks with full undo,
DNS benchmark, MTU finder, route doctor, bufferbloat test, and speed test. Built with C# WPF on .NET 8.

## Features

### 📊 Live Monitoring
- Real-time ping to your router, Cloudflare (1.1.1.1), Google (8.8.8.8), and custom hosts
- Sparkline graphs showing last 60 samples with packet loss indication
- Connection quality verdict (Excellent/Good/Average/High)
- Network adapter info with link speed, IP, gateway, and DNS
- **New:** Historical latency tracking with configurable retention
- **New:** Export latency history to CSV for analysis
- **New:** Add/remove custom monitoring targets

### ⚙️ Optimize (System Tweaks)
- **TCP Stack:** Low-latency tuning (auto-tuning, RSC, ECN, timestamps, heuristics)
- **Nagle/ACK:** Disable Nagle algorithm and delayed ACK per interface
- **Multimedia Throttling:** Remove Windows network throttling for media/games
- **QoS Reservations:** Release reserved bandwidth (set to 0%)
- **Delivery Optimization:** Disable Windows Update peer-to-peer
- **Power Plan:** Switch to Ultimate Performance plan
- **NIC Properties:** Interrupt Moderation, RSS, Jumbo Frames, Flow Control
- **IPv6 Preference:** Prefer IPv4 over IPv6
- **DNS Client:** Negative caching, NetBIOS over TCP/IP
- **Risk Levels:** Each tweak tagged as "Safe" or "Advanced"
- **Full Undo:** Every change backed up to `%AppData%\NovaNet\backup.json` before applying
- **Apply/Revert All:** One-click batch operations

### 🌐 DNS Benchmark
- Tests 6 major providers: Cloudflare, Google, Quad9, OpenDNS, AdGuard, plus current
- Measures both cached (popular domains) and cold (unique domains) lookups
- Median-based scoring with visual bar comparison
- One-click apply to network adapter
- Custom DNS server support
- Reset to automatic (router-provided)

### 🔧 Network Tools (Route Doctor)
- **Route Analysis:** Default routes with metrics, detects double NAT, VPN conflicts
- **Path Traceroute:** First 8 hops to 1.1.1.1 with private/CGNAT detection
- **MTU Finder:** Binary search for optimal MTU (PPPoE typically 1492)
- **Quick Fixes:** Flush DNS, Flush ARP/Routes, Renew IP, Reset Network Stack
- Copy full diagnostic report to clipboard

### ⚡ Speed & Bufferbloat Test
- Saturates line for 10 seconds (download + upload via Cloudflare)
- Measures latency under load vs idle
- Bufferbloat grade: A+ (<5ms), A (<30ms), B (<60ms), C (<200ms), D (<400ms), F
- Real-time progress with live Mbps and ping
- QoS recommendation for B grade or worse

### 📖 Router Guide
- Bridge mode ONT configuration
- PPPoE MTU setup
- Router-level DNS configuration
- Wired vs Wi-Fi guidance
- QoS setup for bufferbloat
- IPv6 handling
- Continuous monitoring tips

### 🎨 UI/UX Enhancements
- **Theme Support:** Light / Dark / System (auto) with live switching
- **System Tray:** Minimize to tray, background monitoring, notifications
- **Settings Window:** Comprehensive configuration (theme, behavior, monitoring, updates)
- **Responsive Layout:** Adapts to window size
- **Accessibility:** Keyboard navigation, focus indicators, tooltips
- **Modern Controls:** Styled buttons, switches, inputs, progress bars

### 🔒 Safety & Reliability
- All changes backed up before apply (registry + NIC properties)
- "Undo All" restores exact original values
- Runs elevated only when needed (UAC prompt)
- Crash logging to `%AppData%\NovaNet\crash.log`
- Automatic update checks from GitHub Releases

## Build on GitHub
1. Create a repo and push this folder.
2. Open the **Actions** tab. The `Build NovaNet` workflow runs on every push.
3. Download the `NovaNet-win-x64` artifact, unzip it anywhere, and run `NovaNet.exe` from inside that folder (keep the other files next to it).
4. For a release, push a tag like `v1.1.0`; a zip is attached to the release.

## Run
Right-click `NovaNet.exe`, run as administrator (it asks automatically).
Windows SmartScreen may warn because the exe is unsigned: More info → Run anyway.

## Requirements
- Windows 10/11 (x64)
- .NET 8 Desktop Runtime (included in self-contained build)
- Administrator privileges for network tweaks

## Safety
Every registry value and network-card option is saved to `%AppData%\NovaNet\backup.json`
before it changes. "Undo all" in the Optimize tab restores your exact original values.

## Architecture
- **MVVM** with CommunityToolkit.Mvvm
- **Dependency Injection** with Microsoft.Extensions.Hosting
- **Services:** PingMonitor, DnsBench, LoadTest, MtuFinder, Fix, Store, Settings, Theme, Tray, Update
- **ViewModels:** Live, Optimize, Dns, Net, Speed, Guide, Settings, Main
- **Unit Tests:** xUnit + Moq for core services

## Data Locations
- Settings: `%AppData%\NovaNet\settings.json`
- Backup: `%AppData%\NovaNet\backup.json`
- Crash Logs: `%AppData%\NovaNet\crash.log`
- History Exports: `%AppData%\NovaNet\latency_history_*.csv`

## License
MIT
