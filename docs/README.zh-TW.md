[English](.Readme.md) | [简体中文](./docs/README.zh-CN.md) | **繁體中文**

# HoYoShade Hub

> **HoYoShade Hub** 是一個爲 HoYoShade 開發的全新 GUI 啓動器，基於 Starward 二次開發。

> **HoYoShade Hub** 的 Logo 設計參考了知名抗審查代理內核 [Sing-Box](https://github.com/SagerNet/sing-box) 的 Logo 設計，除了因爲其 Logo 設計符合 Hub 本身的定義，並且這也符合並代表着我們對 HoYoShade 自身的一種新的願景——
> 
> 即使我們永遠無法獲得我們應有的自由，但也不要忘記去爲此爭鬥。
> 
> 去享受我們爭取的那片刻又短暫的自由，那本該是你的。

HoYoShade Hub 是一個爲了解決 HoYoShade 當前 Bat 啓動器的缺點而開發的開源 GUI 啓動器，支持miHoYo（米哈遊）/ HoYoverse 面向 Windows PC 端的所有遊戲客戶端。包括中國大陸/BiliBili/全球/特殊發行渠道下的 公開/Beta/Devkit/SDK/創作者體驗服等客戶端。目標是完全替代 HoYoShade 現有 Bat 啓動器。

> [!NOTE]
> 需要注意的是，此項目並未完全開發完畢，部分文本可能與實際情況有所出入/未做更改；部分功能可能未能按照預期正常工作，有待進一步收集日誌/issues後並修復，或者暫未在 HoYoShade Hub 中實現。詳情請查看 [GitHub Release](https://github.com/DuolaD/HoYoShade-Hub/releases) 中所展示的更新日誌。

> [!NOTE]
> 如果你對功能性/穩定性有一定要求，我們建議你前往 [HoYoShade倉庫](https://github.com/DuolaD/HoYoShade/) 中使用舊版Bat啓動器。

> [!NOTE]
> 該 GUI 啓動器將會在全部功能開發完畢後替代 HoYoShade 現有 Bat 啓動器。屆時，HoYoShade 將只能使用 HoYoShade Hub 作爲啓動器進行使用。

## 安裝

首先，您的設備需要滿足以下要求：

- Windows 10 1809 (17763) 及以上的版本
- 已安裝 [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2)
- 已安裝 [WebP 映像擴展](https://apps.microsoft.com/detail/9pg2dk419drg)
- 爲了更好的使用體驗，請在系統設置中開啓**透明效果**和**動畫效果**
>[WebP 映像擴展](https://apps.microsoft.com/detail/9pg2dk419drg) 一般情況下系統自帶，如果程序無法正常顯示背景圖片請自行檢查是否安裝。

然後在 [GitHub Release](https://github.com/DuolaD/HoYoShade-Hub/releases) 下載對應 CPU 架構的壓縮包，解壓後運行 `HoYoShadeHub.exe` 並按提示操作。

> [!NOTE]
> 繼續使用即代表你同意 [我們的用戶協議及隱私策略](https://hoyosha.de/zh_hk/user-agreement.html) 。

## 開發

在本地編譯應用，你需要安裝 Visual Studio 2022 （或更高版本）並選擇以下負載：

-  .NET 桌面開發
-  使用 C++ 的桌面開發
-  通用 Windows 平臺開發


## 贊助

開發不易，如果你覺得 HoYoShade Hub 好用，可以前往 [我的GitHub主頁](https://github.com/DuolaD) 贊助我。

## 來源

我們真誠的在此向本項目引用的所有來源的貢獻者致以最誠摯的感謝。有了你們，HoYoShade Hub 才能變得更好。

| 名字 | 介紹 | 網址 |
| --- | --- | --- |
| **Starward** | HoYoShade Hub基於此進行二次開發。前人栽樹，後人乘涼。 | [官方倉庫](https://github.com/Scighost/Starward/) \ [官方網站](https://starward.scighost.com/) |
| **HoYoShade** | HoYoShade 框架 | [官方倉庫](https://github.com/DuolaD/HoYoShade/) \ [官方網站](https://hoyosha.de) |
| **MiSans系列字體** | HoYoShade Hub默認字體，版權歸小米集團所有。  | [官方網站](https://www.mi.com) \ [MiSans系列字體官方網站](https://hyperos.mi.com/font/) |
| **時間同步工具** | 在此基礎上進行開發，並將其集成到 Blender/留影機插件 修復工具中。 | [官方倉庫](https://gitee.com/haitangyunchi/TimeSyncTool) |

以及本項目中使用的[第三方庫](./docs/ThirdParty.md)。

## 貢獻者

我們真誠的在此向本項目引用的所有來源的貢獻者致以最誠摯的感謝。有了你們，HoYoShade Hub 才能變得更好。

感謝所有貢獻者對本項目的無私貢獻！

<div align="center">
    <table>
        <tr>
            <td>
                <h3>DuolaDStudio Hong Kong Ltd.</h3>
                <a href="https://github.com/DuolaDStudio">
                    <img src="https://avatars.githubusercontent.com/u/152937804?s=200&v=4" width="70" style="border-radius: 50%" alt="DuolaDStudio Hong Kong Ltd.">
                </a>
		<h3>也就是以下成員：</h3>
		<h5>哆啦D夢|DuolaD & 琳尼特|LynetteNotFound</h5>
		<a href="https://github.com/DuolaD"><img src="https://avatars.githubusercontent.com/u/110040721?v=4" width="70" style="border-radius: 50%" alt="DuolaD"></img></a>
		<a href="https://github.com/LynetteNotFound">
                    <img src="https://avatars.githubusercontent.com/u/159673876?v=4" width="70" style="border-radius: 50%" alt="LynetteNotFound">
                </a>
            </td>
	    <td>
                <a href="https://github.com/DuolaDStudio">組織的GitHub主頁</a><br>
		<a href="https://github.com/DuolaD">哆啦D夢|DuolaD的GitHub個人主頁</a><br>
		<a href="https://github.com/LynetteNotFound">琳尼特|LynetteNotFound的GitHub個人主頁</a><br>
		<br>
		<a>注意:哆啦D夢|DuolaD其它個人主頁鏈接見上;</a><br>
		<a>琳尼特|LynetteNotFound沒有公開聯繫方式</a>
            </td>
	</tr>
        <tr>
            <td>
                <h3>淵麒|ZelbertYQ</h3>
                <a href="https://space.bilibili.com/435289515">
                    <img src="https://avatars.githubusercontent.com/u/116244982?v=4" width="70" style="border-radius: 50%" alt="ZelbertYQ">
                </a>
            </td>
            <td>
    <a href="https://github.com/ZelbertYQ">GitHub個人主頁</a><br>
		<a href="https://space.bilibili.com/435289515">嗶哩嗶哩頻道</a>
    <a href="https://v.douyin.com/gah7b4ZwQqo">抖音頻道</a>
    <a href="https://www.xiaohongshu.com/user/profile/660ad54b000000001701a61c">小紅書頻道</a>
            </td>
        </tr>
    </table>
</div>

感謝 CloudFlare 提供的免費 CDN，它帶給了所有人良好的更新體驗。

<img alt="cloudflare" width="300px" src="https://user-images.githubusercontent.com/61003590/246605903-f19b5ae7-33f8-41ac-8130-6d0069fde27a.png" />

# 開源協議

HoYoShade Hub 將會繼續沿用 Starward 所使用的 MIT License 進行開源發佈，這也與我們對於 HoYoShade 自身的新願景所吻合。

但在實際開發過程中，請確保你會遵守 MIT License 開源許可證 和 [我們的用戶協議](https://hoyosha.de/zh_cn/user-agreement.html) ，否則你有可能會因此受到來自我們的起訴。