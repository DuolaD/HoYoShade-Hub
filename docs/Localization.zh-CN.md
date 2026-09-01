[English](./Localization.md) | **简体中文** | [繁體中文](./Localization.zh-TW.md)

# 本地化

首先，我们真诚地向所有为本项目做出贡献的贡献者和翻译者致以最由衷的感谢！感谢大家的无私奉献，让世界各地不同语言的用户都能够顺畅使用 HoYoShade Hub。无论是翻译一种新语言、修正一处文案措辞，还是提出一条宝贵的本地化建议，您的付出都为这个项目增添了巨大的价值。

## 翻译指南

如果您希望为 HoYoShade Hub 的本地化工作做出贡献，请阅读以下信息。本项目有两个部分需要翻译：

1. **应用内文案**（界面按钮、菜单、设置项、提示信息等）
2. **仓库文档**（`docs/` 目录下的 Markdown 文档）

---

## 1. 应用内文本翻译

HoYoShade Hub 的应用内文本翻译托管在 **[Crowdin](https://crowdin.com/project/hoyoshade-hub)** 平台上，您可以在网页上直接参与翻译和校对。

👉 **Crowdin 项目主页**：[https://crowdin.com/project/hoyoshade-hub](https://crowdin.com/project/hoyoshade-hub)

### 翻译工作流说明
1. **参与翻译**：进入 Crowdin 项目主页，选择您想要贡献的语言即可开始翻译。
2. **自动化同步**：在 Crowdin 上提交的翻译内容会自动定期同步到 GitHub 仓库并创建 Pull Request。
3. **自动化构建与实时预览**：每次 Crowdin 提交 PR 时，GitHub Actions 会自动触发构建。您可以前往 [GitHub Actions](https://github.com/DuolaD/HoYoShade-Hub/actions/workflows/build.yml) 找到最新的构建记录并下载打包好的 Artifacts 压缩包，解压后即可直接在真实应用中实时预览翻译效果。
4. **源语言修改**：本项目的源语言为英文（定义于 `Lang.resx` 中）。如果您发现默认的英文原文存在笔误或不当之处，请直接在 GitHub 上提交 Pull Request 修改 `src/HoYoShadeHub.Language/Lang.resx`。

---

## 2. 文档翻译

文档翻译是指本仓库 `docs/` 目录下的 Markdown 文件。

### 文档翻译规则
- 所有翻译后的 Markdown 文件都应存放在 `docs/` 目录下。
- 文件命名需包含对应的语言-区域代码。例如：
  - 原版英文：`README.md`
  - 简体中文：`docs/README.zh-CN.md`
  - 繁体中文：`docs/README.zh-TW.md`
- 翻译完成后，请在文档最上方添加语言切换链接，便于读者在各语言版本之间导航。
- 翻译完成后，请通过创建 [Pull Request](https://github.com/DuolaD/HoYoShade-Hub/pulls) 提交到本仓库。
