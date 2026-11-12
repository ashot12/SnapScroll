# SnapScroll

**A tiny screenshot tool for Windows 10/11, with region capture, scrolling capture and simple markup.**

One ~80 KB exe. No installer, no dependencies (it uses the .NET Framework that's already built into Windows), no internet access, no telemetry.

> **Beta (v0.1.x).** This is an early test release. Things may break; please [report anything odd](../../issues).

<!-- Add a screenshot or GIF here once you have one, e.g.:
![SnapScroll in action](docs/demo.gif)
-->

---

## Download

Grab the latest `SnapScroll-vX.Y.Z.zip` from the **[Releases page](../../releases)**, unzip it anywhere, and run `SnapScroll.exe`. It sits in the system tray (blue icon).

### Is the download safe?

- **Built in public:** every release is compiled by [GitHub Actions](../../actions) on a clean Windows machine, straight from the source in this repo.
- **Checkable:** the release notes list the SHA-256 hash of the exe and zip. You can confirm a download really came from that build with `gh attestation verify SnapScroll-vX.Y.Z.zip --repo ashot12/SnapScroll`.
- **Small footprint:** no network access, no telemetry, never asks for admin. See [SECURITY.md](SECURITY.md) for exactly what it touches.
- **Unsigned for now:** the exe isn't code-signed yet, so Windows SmartScreen will say it doesn't recognize the app. Once you're comfortable, choose **More info → Run anyway**. Or skip the download and [build it yourself](#build-from-source); it takes about five seconds.

---

## How to use it

| Action | How |
|---|---|
| Capture an area | **PrtSc**, drag a box, then pick from the menu (Enter = Copy + Save) |
| Capture a whole monitor | **PrtSc**, then single-click |
| Scrolling capture | **Shift+PrtSc** (or press **S** on the capture screen) |
| Cancel anything | **Esc** or right-click |
| Options | Right-click the tray icon |

After you select an area, the menu offers **Copy + Save**, **Copy only**, **Save as…**, **Edit / markup…** and **Scrolling capture…**

Screenshots are saved as PNG to `Pictures\SnapScroll`. You can change the folder from the tray menu. Alt+PrtSc and Win+PrtSc keep their normal Windows behavior.

### Scrolling capture

For pages longer than your screen:

1. Press **Shift+PrtSc** and **click the top-left corner** of what you want.
2. **Scroll down** with the mouse wheel at a normal pace. A label next to the mouse shows how much you've captured.
3. **Click the bottom-right corner.** SnapScroll stitches everything between your two clicks into one image and opens it in the editor.

Tips:
- Clicks during scrolling capture never reach the page, so you can't hit a link by accident.
- Scrolling back up is fine; nothing gets repeated.
- If the label turns orange ("lost track"), scroll back up a little and continue.
- Pinned headers, footers and chat bubbles aren't repeated in the result.
- Items that highlight on mouse-over (table rows, links) can appear highlighted, so keep the mouse over a plain area.
- The limit is 30,000 px, about 30 screens on a 1080p monitor.

### Markup editor

Everything you draw stays a separate object: select it, drag it somewhere else, recolor it or delete it. Only the saved or copied image is flattened.

| Tool | Key | |
|---|---|---|
| Select | **V** | Click an object to select it, drag to move, **Del** to delete, arrow keys to nudge (Shift = 10 px). Double-click text to edit it. |
| Box | **B** | Rectangle outline |
| Arrow | **A** | Drag from tail to tip |
| Pen | **P** | Freehand scribble |
| Highlight | **H** | Yellow see-through marker |
| Text | **T** | Drag an area and type; text wraps inside it. Enter = new line, click outside or Ctrl+Enter = done, Esc = cancel |
| ✔ Check | **C** | Click to stamp, or drag to size |
| ✖ X mark | **X** | Click to stamp, or drag to size |
| Redact | **R** | Solid black box to hide passwords, emails, account numbers |

- Anything you just drew is selected, so you can drag it straight away.
- Picking a color, line width or text size changes the selected object.
- **Esc** deselects, or closes the editor when nothing is selected.

**Ctrl+Z** undo (moves and deletes too) · **Ctrl+Y** redo · **Ctrl+C** copy · **Ctrl+S** save · **Ctrl+Shift+S** save as · **Ctrl +/−/0** zoom

---

## Known limitations (v0.1)

- **SmartScreen warning** on first run (unsigned exe; see [Is the download safe?](#is-the-download-safe)).
- **PrtSc doesn't work while an app running as Administrator is focused.** Windows blocks this; run SnapScroll as admin if you need it.
- **Windows 11 Snipping Tool opens instead:** turn off *Settings → Accessibility → Keyboard → "Use the Print screen key to open screen capture"*.
- **Scrolling capture can lose track** on pages with video, animated ads or content that rearranges itself while scrolling.
- Objects can be moved but not resized yet.

## Feedback

Testers: please open an **[Issue](../../issues)** with what you did, what happened, your Windows version and display scaling (Settings → Display → Scale). A screenshot helps, and SnapScroll can take it.

---

## Build from source

Requires nothing but Windows. Run:

```
build.bat
```

It compiles `SnapScroll.cs` with the C# compiler that ships with Windows (`csc.exe` from .NET Framework 4.x) and produces `SnapScroll.exe` next to it.

### Project layout

| File | What it is |
|---|---|
| `SnapScroll.cs` | The whole app (tray, capture overlay, scrolling capture, editor) |
| `app.ico` | App icon |
| `app.manifest` | Windows settings: per-monitor DPI awareness, runs without admin |
| `build.bat` | One-step build |
| `README.txt` | Plain-text user guide (ships inside the release zip) |
| `CHANGELOG.md` | Version history |
| `SECURITY.md` | What the app touches, and how to report a problem |
| `.github/workflows/release.yml` | Builds and publishes a release when a `v*` tag is pushed |
