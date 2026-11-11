@echo off
rem Builds SnapScroll.exe using the C# compiler built into Windows (.NET Framework 4.x). No installs needed.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /out:SnapScroll.exe /win32icon:app.ico /resource:app.ico,SnapScroll.app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll SnapScroll.cs
if errorlevel 1 (echo Build failed & pause & exit /b 1)
echo Built SnapScroll.exe
pause
