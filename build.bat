@echo off
rem ImeSwitcher build script - Enos Tseng
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found. .NET Framework 4.x is required.
  echo [ERROR] csc.exe not found > build.log
  if not defined CI pause
  exit /b 1
)
if not exist "dict\STPhrases.txt" (
  echo [ERROR] dict folder is missing.
  echo [ERROR] dict folder is missing > build.log
  if not defined CI pause
  exit /b 1
)
set "RES="
for %%f in (dict\*.txt) do set "RES=!RES! /resource:dict\%%~nxf,%%~nxf"
set "ICON="
if exist "assets\app.ico" set "ICON=/win32icon:assets\app.ico"
echo Compiler: %CSC%
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:ImeSwitcher.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll %ICON% !RES! ImeSwitcher.cs > build.log 2>&1
set ERR=%ERRORLEVEL%
type build.log
if %ERR% neq 0 (
  echo.
  echo [FAILED] exit code %ERR%  ^(see build.log^)
  >>build.log echo exit=%ERR%
  if not defined CI pause
  exit /b %ERR%
)
>>build.log echo exit=0
echo.
echo [OK] ImeSwitcher.exe built.
if not defined CI timeout /t 3 >nul
exit /b 0
