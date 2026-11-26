<p align="center"><img src="assets/app_256.png" width="128" alt="IME 繁簡切換器圖示"></p>

# IME 繁簡切換器（ImeSwitcher）

Windows 小工具，常駐在系統匣，主要做兩件事：

1. **一鍵切換「微軟注音」輸出繁體或簡體**：不用再打開輸入法設定頁。
2. **選取文字直接轉繁體或簡體**：在任何程式裡選取文字後按快速鍵，原地替換。轉換使用 OpenCC 詞庫，以詞為單位轉換，可選擇是否一併改用台灣用語。

單一執行檔、免安裝、完全離線，不用連網也不用付費 API。

📖 **完整操作步驟見 [使用教學](docs/USAGE.md)**

- 開發者：Enos Tseng
- 聯絡信箱：enostseng@gmail.com

## 下載

到 [Releases](https://github.com/Enostseng/ImeSwitcher/releases) 下載最新版的 `ImeSwitcher.exe`。

- **系統需求：** Windows 10／11（內建 .NET Framework 4.x）；切換輸出功能需要「微軟注音」輸入法。
- **第一次執行：** 程式沒有數位簽章，Windows 可能會跳出 SmartScreen 警告，按「其他資訊」→「仍要執行」。
- **確認檔案沒被竄改：** 對照 Release 頁面公布的 SHA-256。在 PowerShell 執行：
  ```powershell
  Get-FileHash .\ImeSwitcher.exe -Algorithm SHA256
  ```

## 操作

| 操作 | 功能 |
|---|---|
| `Alt+Shift+F` | 切換微軟注音輸出繁體／簡體 |
| `Alt+Shift+T` | 把選取的文字轉成繁體 |
| `Alt+Shift+S` | 把選取的文字轉成簡體 |
| `Ctrl+Shift+Alt+X` | 開關畫面提示 |
| 單擊系統匣圖示 | 切換繁體／簡體輸出 |
| 右鍵系統匣圖示 | 選單：狀態、切換、通知、開機自動啟動、台灣用語、設定檔、關於、結束 |

系統匣圖示直接顯示目前狀態：藍色「繁」、紅色「簡」。所有快速鍵都可以在設定檔修改。

## 選取文字轉換

| 模式 | 簡轉繁 | 繁轉簡 | 範例 |
|---|---|---|---|
| 預設（只轉字形） | `s2tw` | `tw2s` | 软件开发→軟件開發、头发→頭髮、一只鸟→一隻鳥 |
| 台灣用語（選用） | `s2twp` | `tw2sp` | 软件开发→軟體開發、信息→資訊、网络→網路 |

要改用台灣用語：右鍵系統匣圖示，勾選「選取轉換時改用台灣用語」。

運作方式：自動複製選取的文字 → 轉換 → 貼回 → 還原原本的剪貼簿內容。

**限制**
- 貼回的是純文字，原本的格式（粗體、顏色等）會遺失。
- 剪貼簿只會還原文字、圖片、檔案清單。
- 對以系統管理員權限執行的程式無效。
- 極罕用字（例如 樠、蹻、殢）在繁轉簡時會保持原字，見 `dict/README-dict.md`。

## 設定檔

位置：`%APPDATA%\ImeSwitcher\settings.ini`（右鍵選單 →「開啟設定檔」）

```ini
[General]
Notify=1
FirstRun=0
[Hotkeys]
ToggleHotkey=Alt+Shift+F
NotifyHotkey=Ctrl+Shift+Alt+X
ToTraditionalHotkey=Alt+Shift+T
ToSimplifiedHotkey=Alt+Shift+S
[Convert]
UseTaiwanPhrases=0
```

改完後，在右鍵選單按「重新載入設定」。不要在數值後面加註解。

紀錄檔在 `%APPDATA%\ImeSwitcher\log.txt`，出問題時可以先看這個檔案。

## 程式會修改的東西

為了公開透明，列出程式寫入的所有位置：

| 位置 | 用途 |
|---|---|
| `HKCU\SOFTWARE\Microsoft\IME\15.0\IMETC` 的 `Enable Simplified Chinese Output` | 微軟注音的「輸出簡體中文」設定；值不存在時以繁體建立 |
| `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` 的 `ImeSwitcher` | 只在勾選「開機自動啟動」時寫入，取消勾選就刪除 |
| `%APPDATA%\ImeSwitcher\` | 設定檔與紀錄檔 |

> ⚠ 微軟沒有為 `Enable Simplified Chinese Output` 發布官方文件，Windows 更新後這個切換功能可能失效。選取文字轉換不受影響。

**還原：** 在微軟注音設定頁關閉「輸出簡體中文」，並刪除 `%APPDATA%\ImeSwitcher` 資料夾。

## 從原始碼編譯

不需要 Visual Studio，使用 Windows 內建的 .NET Framework 編譯器：

```bat
build.bat              :: 產生 ImeSwitcher.exe（會把 dict\*.txt 內嵌進去）
tests\run_tests.bat    :: 轉換回歸測試
```

推送 `v*` 標籤時，GitHub Actions 會自動編譯、測試，並發布 Release（`.github/workflows/release.yml`）。

命令列參數：
- `ImeSwitcher.exe --status [輸出檔]`：只讀取，把登錄狀態寫成文字檔。
- `ImeSwitcher.exe --convert 模式 輸入檔 輸出檔 [統計檔]`：轉換整個 UTF-8 檔案。模式為 `s2tw`／`s2twp`／`tw2s`／`tw2sp`，或 `t`／`s`（依設定檔決定）。

## 專案結構

| 路徑 | 說明 |
|---|---|
| `ImeSwitcher.cs` | 全部原始碼（C# 5，單一檔案） |
| `build.bat` | 編譯腳本 |
| `dict/` | OpenCC 詞庫、授權條文、詞庫說明 |
| `docs/USAGE.md` | 使用教學 |
| `assets/app.ico` | 程式圖示 |
| `tools/gen_dict.py` | 產生衍生詞庫的腳本 |
| `tools/gen_icon.py` | 產生程式圖示的腳本 |
| `tests/` | 回歸測試與預期結果 |
| `CHANGELOG.md`、`TEST_REPORT.md` | 版本紀錄、實機測試紀錄 |

## 授權

- 本專案程式碼：MIT License（見 `LICENSE`）
- 內嵌的 OpenCC 詞庫：Apache License 2.0（見 `NOTICE.md`、`dict/OpenCC-LICENSE.txt`）
