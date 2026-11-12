# Security

SnapScroll is a small screenshot tool. This page lists everything it touches, so you can judge it yourself.

## What it does

| Area | Details |
|---|---|
| **Keyboard** | A system-wide low-level keyboard hook, so PrtSc works from any app. It acts only on PrtSc (plus Esc, Enter and S while a capture is in progress). Keystrokes are never logged, stored or sent anywhere. Alt+PrtSc and Win+PrtSc are passed through to Windows. |
| **Mouse** | A low-level mouse hook, installed **only during scrolling capture**, so your corner clicks don't reach the page. It is removed as soon as the capture ends. |
| **Screen** | Reads the screen only when you press PrtSc or start a capture. |
| **Files** | Writes PNG/JPG/BMP files only to the folder you save to (default `Pictures\SnapScroll`). These are ordinary image files: anything that can read your Pictures folder (OneDrive sync, backups, other apps) can read them. |
| **Clipboard** | Copy puts the image on the clipboard. If Windows clipboard history or cross-device sync is on, Windows may keep or sync it. |
| **Registry** | `HKCU\Software\SnapScroll` (save folder) and, only if you turn on "Start with Windows", a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Nothing machine-wide. |
| **Network** | None. No update checks, no telemetry, no outbound connections. |
| **Privileges** | Runs as the normal user (`asInvoker`); never asks for admin. |

**Redact** paints solid black into the saved pixels. It is not a removable layer, so the hidden content is gone from the saved file.

## Verifying a download

Release builds are produced by GitHub Actions from the tagged source (see `.github/workflows/release.yml`). Each release lists SHA-256 hashes, and you can verify the provenance with:

```
gh attestation verify SnapScroll-vX.Y.Z.zip --repo ashot12/SnapScroll
```

Or build it yourself with `build.bat`.

## Reporting a problem

Please report security issues privately via **Security → Report a vulnerability** on this repository, rather than in a public issue.
