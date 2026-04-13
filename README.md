# ADB over WireGuard

Windows GUI and backend scripts for exposing a local Android `adb` server through WireGuard and optionally automating MikroTik port forwarding.

## Public package

The public release is built from this project without:

- private SSH keys
- local state and logs
- machine-specific cache
- hardcoded router IPs or user-specific WireGuard settings

Users must import their own MikroTik key and configure their own router settings in the app if they want router automation.

## Project layout

- `AdbWireGuardGui` - WinForms frontend
- `BackendSource/ADB-WireGuard` - backend scripts and `platform-tools`
- `Build-AdbWireGuardGuiVariants.ps1` - builds the GUI package
- `Build-AdbWireGuardComponentsPackage.ps1` - builds the backend components ZIP

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-AdbWireGuardComponentsPackage.ps1
powershell -ExecutionPolicy Bypass -File .\Build-AdbWireGuardGuiVariants.ps1
```

Artifacts are written to `release/`.

## Security note

Do not commit or publish:

- `mikrotik/`
- any imported private keys
- local `state/`
- any machine-specific `package-root.txt`
