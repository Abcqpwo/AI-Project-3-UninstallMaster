@echo off
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"卸载大师.exe" /win32manifest:"src\app.manifest" /win32icon:"src\app.ico" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll src\Program.cs src\Theme.cs src\Models.cs src\InstalledApps.cs src\Cleaner.cs src\ResidueScanner.cs src\TargetResolver.cs src\ShellMenu.cs src\Forms.cs src\MainForm.cs src\ResidueForm.cs
if errorlevel 1 (
  echo.
  echo BUILD FAILED - see messages above
) else (
  echo.
  echo BUILD OK: 卸载大师.exe
)
echo.
pause
