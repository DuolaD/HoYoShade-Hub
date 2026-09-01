[English](./Localization.md) | [简体中文](./Localization.zh-CN.md) | **繁體中文**

# 本地化

首先，我們真誠地向所有為本項目做出貢獻的貢獻者和翻譯者致以最由衷的感謝！感謝大家的無私奉獻，讓世界各地不同語言的用戶都能夠順暢使用 HoYoShade Hub。無論是翻譯一種新語言、修正一處文案措辭，還是提出一條寶貴的本地化建議，您的付出都為這個項目增添了巨大的價值。

## 翻譯指南

如果您希望為 HoYoShade Hub 的本地化工作做出貢獻，請閱讀以下資訊。本項目有兩個部分需要翻譯：

1. **應用內文案**（介面按鈕、選單、設定項、提示訊息等）
2. **倉庫文檔**（`docs/` 目錄下的 Markdown 文檔）

---

## 1. 應用內文字翻譯

HoYoShade Hub 的應用內文字翻譯託管在 **[Crowdin](https://crowdin.com/project/hoyoshade-hub)** 平台上，您可以在網頁上直接參與翻譯和校對。

👉 **Crowdin 項目主頁**：[https://crowdin.com/project/hoyoshade-hub](https://crowdin.com/project/hoyoshade-hub)

### 翻譯工作流說明
1. **參與翻譯**：進入 Crowdin 項目主頁，選擇您想要貢獻的語言即可開始翻譯。
2. **自動化同步**：在 Crowdin 上提交的翻譯內容會自動定期同步到 GitHub 倉庫並創建 Pull Request。
3. **自動化構建與實時預覽**：每次 Crowdin 提交 PR 時，GitHub Actions 會自動觸發構建。您可以前往 [GitHub Actions](https://github.com/DuolaD/HoYoShade-Hub/actions/workflows/build.yml) 找到最新的構建記錄並下載打包好的 Artifacts 壓縮包，解壓後即可直接在真實應用中實時預覽翻譯效果。
4. **源語言修改**：本項目的源語言為英文（定義於 `Lang.resx` 中）。如果您發現預設的英文原文存在筆誤或不當之處，請直接在 GitHub 上提交 Pull Request 修改 `src/HoYoShadeHub.Language/Lang.resx`。

---

## 2. 文檔翻譯

文檔翻譯是指本倉庫 `docs/` 目錄下的 Markdown 文件。

### 文檔翻譯規則
- 所有翻譯後的 Markdown 文件都應存放在 `docs/` 目錄下。
- 文件命名需包含對應的語言-區域代碼。例如：
  - 原版英文：`README.md`
  - 簡體中文：`docs/README.zh-CN.md`
  - 繁體中文：`docs/README.zh-TW.md`
  - 日語：`docs/README.ja-JP.md`
- 翻譯完成後，請在文檔最上方添加語言切換鏈接，便於讀者在各語言版本之間導航。
- 翻譯完成後，請通過創建 [Pull Request](https://github.com/DuolaD/HoYoShade-Hub/pulls) 提交到本倉庫。
