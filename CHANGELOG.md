# Changelog

## 0.2.0 (beta)

Editor upgrade.

- **Everything you draw is now an object** you can select, move, recolor or delete until you save. New Select tool (V); freshly drawn objects are selected so you can drag them right away; Del deletes; arrow keys nudge; double-click text to edit it.
- **Pen (P):** freehand scribbles.
- **Check (C) and X (X) stamps:** click to place, or drag to size.
- **Text areas:** drag out an area and type; text wraps inside it, Enter starts a new paragraph.
- Color, line width and text size apply to the selected object.
- Undo/redo now covers moves, deletes, edits and recolors.
- Fixed the Save button icon.

## 0.1.1 (beta)

Fixes from the first code review.

- **PrtSc can no longer silently stop working.** The keyboard and mouse hooks now run on their own thread, so a slow save or a busy clipboard can't make Windows drop them.
- **A failed capture can't leave the app stuck.** The capture screen always closes and the app always resets, even if cropping fails.
- **Mixed-DPI monitors:** added an app manifest declaring per-monitor DPI awareness (e.g. a laptop at 150% with a monitor at 100%).
- **Scrolling capture is more forgiving:** an animated area (ad, video, fading image) no longer makes it lose track, and after "lost track" it reliably picks up again when you scroll back up.
- Multi-line text (Shift+Enter) is drawn line by line; falls back to a default font if Segoe UI is missing.
- Fit-width zoom leaves room for the scrollbar only when one is needed.
- Ctrl+Shift+= also zooms in.
- Save As no longer creates the save folder when you cancel.
- Clean error message if startup fails; small resource leaks fixed.
- Removed unused Windows API declarations left over from an earlier auto-scroll design.
- Releases are now built by GitHub Actions on Windows, with SHA-256 hashes and build-provenance attestation.

## 0.1.0 (beta)

First test release.

- Region capture on PrtSc with frozen-screen overlay; click for whole monitor
- Copy + Save, Copy, Save as, Edit actions
- Scrolling capture (Shift+PrtSc): click top-left, scroll, click bottom-right; ignores pinned headers/footers
- Markup editor: box, arrow, highlight, text, redact; color/width/size; undo/redo; zoom
- Tray menu: open folder, change save folder, start with Windows
