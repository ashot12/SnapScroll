@echo off
rem Builds SnapScroll.exe with the C# compiler built into Windows (.NET Framework 4.x). No installs needed.
rem Also used by the GitHub Actions release build (CI=true skips the pauses).
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo Could not find csc.exe from .NET Framework 4.x & if not defined CI pause & exit /b 1)
"%CSC%" /nologo /target:winexe /optimize+ /out:SnapScroll.exe /win32icon:app.ico /win32manifest:app.manifest /resource:app.ico,SnapScroll.app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll SnapScroll.cs
if errorlevel 1 (echo Build failed & if not defined CI pause & exit /b 1)
echo Built SnapScroll.exe
if not defined CI pause
exit /b 0
