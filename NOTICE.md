# NOTICE

IME 繁簡切換器（ImeSwitcher）
Copyright (c) 2026 Enos Tseng — MIT License

## 第三方元件

### OpenCC（Open Chinese Convert）詞庫

- 專案：https://github.com/BYVoid/OpenCC
- 版本：1.4.2（commit `025f371dc76b598d77384fbdab90c937471844d8`）
- 授權：Apache License 2.0，全文見 `dict/OpenCC-LICENSE.txt`
- 使用方式：`dict/` 資料夾的詞庫檔在編譯時內嵌進 `ImeSwitcher.exe`。
  - `TWVariantsRev.txt`、`STPhrases_GeneratedFromRegionalPhrases.txt` 兩個檔案，是依 OpenCC 的產生規則從原始詞庫衍生而來（產生腳本：`tools/gen_dict.py`）。
  - 其他詞庫檔都沒有修改。
- 轉換演算法是本專案以 C# 重新實作，沒有使用 OpenCC 的程式碼。

### Noto Sans CJK 字型（只用於產生圖示）

- 程式圖示（`assets/app.ico`）上的「繁」「簡」兩字，是用 Noto Sans CJK TC Bold 繪製的圖像。
- 授權：SIL Open Font License 1.1。程式本身沒有內含或散布這個字型檔。
