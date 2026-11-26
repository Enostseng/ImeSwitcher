# CLAUDE.md：IME 繁簡切換器（ImeSwitcher）專案說明

給 Claude 和協作者看的專案總覽。之後要修改、除錯或發新版前，先讀這份。

## 專案基本資料

| 項目 | 內容 |
|---|---|
| 名稱 | IME 繁簡切換器（ImeSwitcher） |
| 開發者 | Enos Tseng（enostseng@gmail.com） |
| Repo | https://github.com/Enostseng/ImeSwitcher（Public，`main` 分支） |
| 授權 | 程式碼 MIT；內嵌的 OpenCC 1.4.2 詞庫採 Apache 2.0（見 NOTICE.md） |
| 目前版本 | v1.0.0（2026-11-26 發布） |
| 正式版本來源 | **以 GitHub 為準**。電腦裡 Downloads 下的舊資料夾只是開發過程的工作副本 |

## 功能

1. **切換微軟注音輸出繁體／簡體**（`Alt+Shift+F`）：改寫 `HKCU\SOFTWARE\Microsoft\IME\15.0\IMETC` 的 `Enable Simplified Chinese Output`（REG_SZ `"0x00000000"`／`"0x00000001"`）。
2. **選取文字轉繁體／簡體**（`Alt+Shift+T`／`Alt+Shift+S`）：Ctrl+C → OpenCC 轉換 → Ctrl+V → 還原剪貼簿。
   - 預設 `s2tw`／`tw2s`，只轉字形。
   - 開啟「台灣用語」後改用 `s2twp`／`tw2sp`。
3. 系統匣：藍色「繁」、紅色「簡」；設定檔在 `%APPDATA%\ImeSwitcher\settings.ini`，紀錄檔在同資料夾的 `log.txt`。

## 檔案結構

| 路徑 | 說明 |
|---|---|
| `ImeSwitcher.cs` | 全部原始碼（單檔，**必須維持 C# 5 語法**：不用 `$""`、`?.`、`=>` 成員、`nameof`） |
| `build.bat` | 用 Windows 內建 `csc.exe`（.NET Framework 4.x）編譯，內嵌 `dict\*.txt` 和 `assets\app.ico`。**必須維持全 ASCII 和 CRLF** |
| `dict/` | OpenCC 1.4.2 詞庫；說明見 `dict/README-dict.md` |
| `tools/gen_dict.py` | 產生衍生詞庫（TWVariantsRev、STPhrases_GeneratedFromRegionalPhrases） |
| `tools/gen_icon.py` | 產生 `assets/app.ico`（Noto Sans CJK TC Bold） |
| `tests/run_tests.bat` | 回歸測試：4 種模式的輸出必須和 `expected_*.txt` 位元組完全相同 |
| `.github/workflows/release.yml` | 推送 `v*` 標籤時自動編譯 → 測試 → 打包 → 發布 Release |
| `docs/USAGE.md` | 使用者教學 |
| `CHANGELOG.md`、`TEST_REPORT.md` | 版本紀錄、實機測試紀錄 |

## 開發規則

- **改版前先備份：** 只做小步驟、低風險的修改；重大修改先在 CHANGELOG 記錄變更範圍、依據、結果、測試、未完成項。
- **不能把推測當成已驗證：** 沒在 Windows 實機或 CI 跑過的，要標「未驗證」。
- **詞庫檔不要手動修改：** 更新 OpenCC 版本時用 `tools/gen_dict.py` 重新產生，並重新產生 `tests/expected_*.txt`，再和官方 OpenCC Python 套件比對。
- **刻意不含 `TSCharactersExt`：** 避免繁轉簡時出現顯示成方框的罕用字。這是已知且刻意的差異。
- **`.gitattributes`：** `dict/*.txt` 和 `tests/*.txt` 設為 `-text`，避免換行轉換導致測試比對失敗。
- **快速鍵：** 用 `RegisterHotKey`；選取轉換在獨立的 STA 執行緒執行，加上 5 秒看門狗，避免剪貼簿卡住讓 UI 當掉（v0.3 曾發生過）。

## 發新版流程（SOP）

1. 修改 `ImeSwitcher.cs`，同步更新版號：檔頭註解、`AssemblyVersion`、`AssemblyFileVersion`、`Program.AppVersion`。
2. 更新 `CHANGELOG.md`，必要時一起更新 `README.md` 和 `docs/USAGE.md`。
3. 本機驗證：`build.bat` → `tests\run_tests.bat` 全部 PASS。
4. 推送到 `main`。
5. **到 GitHub 網頁建立 Release**（例如 `v1.0.1`，Target 選 `main`）。發布時會自動建立標籤，並觸發 Actions：編譯、回歸測試，然後把 `ImeSwitcher.exe`、`ImeSwitcher_vX.Y.Z.zip`、`SHA256SUMS.txt` 和說明附到 Release 上。
   - 注意：Claude 的雲端工作環境不能直接 `git push` 標籤（會 HTTP 403），所以改從網頁建立 Release。
6. 等 Actions 全部成功後，下載 Release 的 exe 驗證：SHA-256、版號、`--convert` 測試。

## 已知限制

- 系統匣右鍵選單「選取轉換時改用台灣用語」的勾選操作，**還沒在實機點擊測試**（設定檔方式已驗證）。
- 選取轉換貼回的是純文字；剪貼簿只會還原文字、圖片、檔案清單；對系統管理員權限的程式無效。
- `Enable Simplified Chinese Output` 這個登錄值沒有微軟官方文件，Windows 更新後切換功能可能失效。
- exe 沒有數位簽章，第一次執行可能會跳出 SmartScreen 警告。

## 開發歷程摘要

| 版本 | 內容 |
|---|---|
| v0.1 | AutoHotkey 版（沒有實測就放棄，改用 C#） |
| v0.2 | C# 單一 exe，12 項實機測試 |
| v0.3 | 新增選取轉換：先用 LCMapStringEx（逐字轉換，錯誤多）；修正 UI 執行緒卡死 |
| v0.3.1 | 加上開發者資訊 |
| v0.4.0 | 改用 OpenCC 詞庫，新增「台灣用語」開關 |
| v1.0.0 | 程式圖示、使用教學、MIT 授權、GitHub Actions 自動發布 |
