SnapScroll - lightweight screenshot tool for Windows 10/11
==========================================================

RUN
  Double-click SnapScroll.exe. It sits in the system tray (blue icon).
  Right-click the tray icon to: open the screenshots folder, change the save folder,
  turn on "Start with Windows", or exit.
  Screenshots save to:  Pictures\SnapScroll  (changeable)

CAPTURE
  PrtSc ............ freezes the screen, then drag a box (click once = whole monitor)
  After you release, a menu pops up:
     Copy + Save (Enter)      copy to clipboard AND save a PNG
     Copy only
     Save as...
     Edit / markup...         opens the editor
     Scrolling capture...     (see below)
  Esc or right-click ...... cancel
  Alt+PrtSc and Win+PrtSc still do the normal Windows thing.

SCROLLING CAPTURE  (Shift+PrtSc, or press S on the capture screen, or tray menu)
  1. Click the TOP-LEFT corner of what you want.
  2. Scroll down with the mouse wheel at a normal pace. A label next to the
     mouse shows how much you've captured (in screens and pixels).
  3. Click the BOTTOM-RIGHT corner. The image is cut exactly between your two clicks
     and opens in the editor.
  - Clicks during scrolling capture never reach the page (no accidental links).
  - Esc or right-click cancels. Enter finishes at the bottom of the current view.
  - Scrolling back up is fine; nothing gets repeated.
  - If the label turns orange ("lost track"), scroll back up a little and continue.
  - Pinned headers, footers and chat bubbles are not repeated.
  - Things that highlight when the mouse is over them (table rows, links) may show
    highlighted in the capture. Keep the mouse over a plain area while scrolling.
  - Size limit: 30,000 px, about 30 screens on a 1080p monitor.

EDITOR (markup)
  B  Box         A  Arrow       H  Highlight (yellow)
  T  Text (click, type, Enter; Shift+Enter = new line)
  R  Redact (black box to hide passwords, emails, etc.)
  Color / line width / text size are on the toolbar.
  Ctrl+Z undo   Ctrl+Y redo   Ctrl+C copy   Ctrl+S save   Ctrl+Shift+S save as
  Ctrl +/-/0 zoom          Esc close

TROUBLESHOOTING
  - Windows 11 Snipping Tool opens instead: Settings > Accessibility > Keyboard >
    turn off "Use the Print screen key to open screen capture".
  - SmartScreen "Windows protected your PC": click More info > Run anyway
    (the exe isn't code-signed). Or build it yourself: run build.bat, which uses
    the C# compiler already built into Windows.
  - PrtSc does nothing while an app running as Administrator is focused
    (Windows blocks that). Run SnapScroll as admin too if you need that.
