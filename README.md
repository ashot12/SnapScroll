# SnapScroll

**A tiny screenshot tool for Windows 10/11, with region capture, scrolling capture and simple markup.**

One ~80 KB exe. No installer, no dependencies (it uses the .NET Framework that's already built into Windows), no internet access, no telemetry.

> **Beta (v0.1).** This is an early test release. Things may break; please [report anything odd](../../issues).

<!-- Add a screenshot or GIF here once you have one, e.g.:
![SnapScroll in action](docs/demo.gif)
-->

---

## Download

Grab the latest `SnapScroll-vX.Y.Z.zip` from the **[Releases page](../../releases)**, unzip it anywhere, and run `SnapScroll.exe`. It sits in the system tray (blue icon).

### "Windows protected your PC"?

SnapScroll isn't code-signed yet, so Windows SmartScreen doesn't recognize it. Click **More info → Run anyway**. If you'd rather not trust a downloaded exe, [build it yourself](#build-from-source) in about five seconds; the full source is in this repo.

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

| Tool | Key | |
|---|---|---|
| Box | **B** | Rectangle outline |
| Arrow | **A** | Drag from tail to tip |
| Highlight | **H** | Yellow see-through marker |
| Text | **T** | Click, type, Enter (Shift+Enter for a new line) |
| Redact | **R** | Solid black box to hide passwords, emails, account numbers |

You can also pick the color, line width and text size from the toolbar.

**Ctrl+Z** undo · **Ctrl+Y** redo · **Ctrl+C** copy · **Ctrl+S** save · **Ctrl+Shift+S** save as · **Ctrl +/−/0** zoom · **Esc** close

---

## Known limitations (v0.1)

- **SmartScreen warning** on first run (unsigned exe, see above).
- **PrtSc doesn't work while an app running as Administrator is focused.** Windows blocks this; run SnapScroll as admin if you need it.
- **Windows 11 Snipping Tool opens instead:** turn off *Settings → Accessibility → Keyboard → "Use the Print screen key to open screen capture"*.
- **Scrolling capture can lose track** on pages with video, animated ads or content that rearranges itself while scrolling.
- Shapes can't be moved after drawing yet. Use undo.

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
| `build.bat` | One-step build |
| `README.txt` | Plain-text user guide (ships inside the release zip) |
| `CHANGELOG.md` | Version history |
