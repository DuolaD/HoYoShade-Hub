English | [简体中文](./Localization.zh-CN.md) | [繁體中文](./Localization.zh-TW.md)

# Localization

First and foremost, we would like to express our sincerest thanks to all contributors and translators of this project. Thanks to your selfless contributions, HoYoShade Hub can be accessible to users in different languages around the world. Whether your contribution is a new language translation, a wording fix, or a suggestion, your work adds immense value to the project.

## Translation Guidance

If you wish to contribute to the localization of HoYoShade Hub, please read the following guidelines. There are two parts of the project that require translation:

1. **In-app Text** (UI strings, dialogs, settings, error messages)
2. **Documentation** (Markdown documents in the `docs/` folder)

---

## 1. In-app Text Translation

The in-app text translation for HoYoShade Hub is hosted on the **[Crowdin](https://crowdin.com/project/hoyoshade-hub)** platform, where you can translate and improve text content directly in your web browser.

👉 **Crowdin Project**: [https://crowdin.com/project/hoyoshade-hub](https://crowdin.com/project/hoyoshade-hub)

### Translation Workflow
1. **Join the Translation**: Visit the Crowdin project page and choose the language you want to contribute to.
2. **Automated Synchronization**: New translations submitted on Crowdin will be automatically synchronized to the repository via automated Pull Requests.
3. **Automated Build & Preview**: Whenever Crowdin synchronization opens or updates a Pull Request, GitHub Actions will automatically compile a preview build. You can find the corresponding workflow run in [GitHub Actions](https://github.com/DuolaD/HoYoShade-Hub/actions/workflows/build.yml) and download the compiled Artifacts to test the translated text in the real application.
4. **Source Strings Modification**: The source language of the application is English (in `Lang.resx`). If you find any typos or inaccuracies in the source English text, please submit a Pull Request directly on GitHub to edit `src/HoYoShadeHub.Language/Lang.resx`.

---

## 2. Documentation Translation

Document translations refer to the Markdown files in the `docs/` directory of this repository.

### Rules for Document Translation
- All translated Markdown files should be placed inside the `docs/` folder.
- Filenames must include the corresponding language-region tag. For example:
  - Original: `README.md` (English)
  - Simplified Chinese: `docs/README.zh-CN.md`
  - Traditional Chinese: `docs/README.zh-TW.md`
- Add language switch links at the very top of each document so readers can navigate between language versions easily.
- Submit your translated Markdown files to the repository by creating a [Pull Request](https://github.com/DuolaD/HoYoShade-Hub/pulls).
