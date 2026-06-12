# Windows Setup

This file is the future Windows-specific getting-started guide.

The current system has been developed primarily on macOS with Docker Desktop and is being prepared for Ubuntu testing. Windows support should be treated as a portability case study until validated.

## Expected Windows Paths To Validate

Potential options:

1. Windows + Docker Desktop + WSL2 Ubuntu backend.
2. Native Windows Unity Editor for Quest APK build/deploy.
3. ADB installed on Windows for Quest USB wired mode.

## TODO For Future Agent

Fill this guide after testing on Windows:

- Windows version and hardware.
- Whether backend runs inside WSL2 or Docker Desktop directly.
- Docker/WSL2 networking behavior for `127.0.0.1:5026` and `127.0.0.1:10001`.
- Whether `adb reverse` works from Windows host or must run inside WSL2.
- Unity Editor version and Android build support setup.
- APK install command that worked.
- Known Windows-specific limitations.

## Placeholder Commands To Validate

```powershell
adb devices
adb reverse tcp:5026 tcp:5026
adb reverse tcp:10001 tcp:10001
adb install -r -d "UnityApp\App_Build\R&U_7.0.1.apk"
```

If using WSL2, test whether the Quest is visible inside WSL2 or only on the Windows host. This is likely the main Windows-specific networking issue.
