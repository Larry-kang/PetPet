# PetPet｜毛孩社群本機展示

PetPet.Modern 是可下載執行的 .NET 8 社群 Demo，使用 SQLite 自動建立展示資料庫，無須安裝 SQL Server 或 Docker。僅於本機展示，未對外營運。

## 下載與啟動

需求：Windows、PowerShell 7（`pwsh`）、.NET 8 SDK。首次還原 NuGet 套件需要網路。

```powershell
git clone https://github.com/Larry-kang/PetPet.git
cd PetPet
pwsh -File ./start-demo.ps1
```

開啟 `http://127.0.0.1:5213`。啟動腳本自動建置、套用 SQLite migrations 並產生合成資料；後續啟動保留操作資料。連接埠被使用時可加 `-Port 5214`；不自動開啟瀏覽器可加 `-NoBrowser`。按 Ctrl+C 停止展示程序。

```powershell
# 在 repository 根目錄執行；只重設受控的展示資料庫
pwsh -File ./reset-demo.ps1 -RESET

# 自動化測試
dotnet test PetPet.Modern/PetPet.Modern.sln
```

## 展示帳號與資料

下列均為合成帳號，密碼統一為 `DemoPass!2026`，只能用於本機 Demo。

| 帳號 | 情境 |
| --- | --- |
| `demo-owner@example.test` | 飼主、4 隻寵物、貼文與好友互動 |
| `demo-nopet@example.test` | 0 隻寵物的新會員引導 |
| `demo-friend@example.test` | 好友邀請與即時聊天 |
| `admin@example.test` | 檢舉審核及停權操作 |
| `disabled@example.test` | 停權登入拒絕 |

初始資料包含 120 位會員、240 隻寵物（每會員 0–4 隻）、1,200 篇貼文、2,400 則留言、8,399 筆按讚及 300 組好友關係。固定 seed 讓每次受控重設後可重現相同情境；這些數字不代表真實使用者或營運規模。

建議展示路線：訪客動態牆 → 飼主登入 → 寵物管理 → 好友邀請 → 兩個瀏覽器視窗互傳訊息 → 管理員檢舉審核。

## 展示畫面

以下為本機 Demo 實際操作畫面，帳號與內容均為合成資料。[查看完整展示圖集](docs/demo-gallery.md)。

![本機展示首頁](docs/images/demo-overview.png)

## 版本與開發分工

- [PetPet.Modern](PetPet.Modern/README.md)：ASP.NET Core MVC、EF Core、SQLite、SignalR，提供會員驗證、寵物、社群、好友、封鎖、通知、搜尋與聊天。
- [原始 PetPet](PetPet/)：轉職職訓期間自行撰寫的 .NET Framework／ASP.NET MVC 作品。

Modern 改造中，定義功能規格、展示情境與驗收條件，透過 Agent 協作建立程式並核對操作結果。

本機 Demo 限制 loopback 存取。外部郵件、帳號刪除匿名化、正式金鑰與多執行個體設定、SQL Server 實體遷移與備援驗證仍屬正式營運前的待辦；不能直接把展示模式當作公開服務部署。

## 規格與驗證

- [功能規格](docs/specs/petpet-modern-demo-product-spec.md)
- [驗收條件](docs/specs/petpet-modern-demo-acceptance.md)
- [交付檢查](docs/reviews/2026-10-19-public-demo-delivery.md)
- [瀏覽器驗收工具](tools/acceptance/README.md)

Larry Kang｜C#／.NET 後端工程師 · [GitHub](https://github.com/Larry-kang) · [LinkedIn](https://www.linkedin.com/in/larry-kang/)
