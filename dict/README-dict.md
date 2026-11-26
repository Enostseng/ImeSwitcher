# 繁簡轉換詞庫

- **來源：** OpenCC（https://github.com/BYVoid/OpenCC）`ver.1.4.2` 標籤（commit `025f371dc76b598d77384fbdab90c937471844d8`）的 `data/dictionary`
- **授權：** Apache License 2.0（全文見 `OpenCC-LICENSE.txt`）
- **編譯：** `build.bat` 會把這個資料夾的 `*.txt` 內嵌進 `ImeSwitcher.exe`

## 檔案

| 檔案 | 來源 |
|---|---|
| STPhrases、STCharacters、TSPhrases、TSCharacters、TWPhrases、TWPhrasesRev、TWVariants、TWVariantsPhrases、TWVariantsRevPhrases | OpenCC 原檔，未修改 |
| TWVariantsRev.txt | 依 OpenCC `reverse.py` 規則由 TWVariants.txt 反轉產生（含 `@reverse-prefer`） |
| STPhrases_GeneratedFromRegionalPhrases.txt | 依 OpenCC `generate_st_phrases_from_regional_phrases.py` 規則：HKPhrases、TWPhrases 的詞經 t2s 轉換，長度 3 以上的反向對應 |

## 轉換模式

| 模式 | 分詞 | 第 1 步 | 第 2 步 |
|---|---|---|---|
| s2tw（預設，簡→繁，只轉字形） | STPhrases＋Generated | STPhrases＋Generated＋STCharacters | TWVariantsPhrases＋TWVariants |
| s2twp（簡→繁＋台灣用語） | 同上 | 同上 | TWPhrases＋TWVariantsPhrases＋TWVariants |
| tw2s（預設，繁→簡，只轉字形） | TSPhrases | TWVariantsRevPhrases＋TWVariantsRev | TSPhrases＋TSCharacters |
| tw2sp（繁→簡＋大陸用語） | TSPhrases | TWPhrasesRev＋TWVariantsRevPhrases＋TWVariantsRev | TSPhrases＋TSCharacters |

## 和官方的差異

官方的 tw2s／tw2sp 轉換鏈還包含 `TSCharactersExt`。它會把少數極罕用字（例如 樠、蹻、殢）轉成 CJK 擴充 B 區的字，很多字型顯示不出來，會變成方框。本程式**刻意不含**這個詞庫，所以這幾個字會保持原樣。除此之外，輸出和官方 OpenCC 1.4.2 一致，驗證方式見 TEST_REPORT.md。
