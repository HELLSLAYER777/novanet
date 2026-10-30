# NovaNet

Internet optimizer for Windows 10/11: live latency, one-click tweaks with full undo,
DNS race, MTU finder, route doctor and a bufferbloat test. Built with C# WPF on .NET 8.

## Build on GitHub
1. Create a repo and push this folder.
2. Open the **Actions** tab. The `Build NovaNet` workflow runs on every push.
3. Download the `NovaNet-win-x64` artifact. It is one self-contained `NovaNet.exe`.
4. For a release, push a tag like `v1.0.0`; the exe is attached to the release.

## Run
Right-click `NovaNet.exe`, run as administrator (it asks automatically).
Windows SmartScreen may warn because the exe is unsigned: More info, Run anyway.

## Safety
Every registry value and network-card option is saved to `%AppData%\NovaNet\backup.json`
before it changes. "Undo all" restores your exact original values.
