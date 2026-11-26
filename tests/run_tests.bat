@echo off
rem Conversion regression tests: output must equal expected_*.txt (verified against OpenCC 1.4.2)
setlocal
cd /d "%~dp0"
set FAIL=0
for %%m in (s2tw s2twp) do (
  "..\ImeSwitcher.exe" --convert %%m cases_s.txt out_%%m.txt
  fc /b out_%%m.txt expected_%%m.txt >nul && (echo [PASS] %%m) || (echo [FAIL] %%m & set FAIL=1)
)
for %%m in (tw2s tw2sp) do (
  "..\ImeSwitcher.exe" --convert %%m cases_t.txt out_%%m.txt
  fc /b out_%%m.txt expected_%%m.txt >nul && (echo [PASS] %%m) || (echo [FAIL] %%m & set FAIL=1)
)
if %FAIL% neq 0 (
  echo Some tests failed.
  exit /b 1
)
echo All tests passed.
exit /b 0
