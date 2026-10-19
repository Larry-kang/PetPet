# PetPet.Modern 公開展示版交付

本次交付包含本機 SQLite Demo 程式、雙 provider migrations、啟動/重設工具、單元與整合測試、規格與可重現驗收工具。原始 MVC 版本保留。

## 交付前核對

- 2026-10-19 於 Windows 執行 Release 測試，72 passed、0 failed、0 skipped；使用獨立 artifacts 目錄，未操作既有 5213 展示程序。
- 檢查會員密碼雜湊/停權驗證、HTTP 與 Hub 權限、封鎖及訊息寫入、媒體解碼/重新編碼、啟動 loopback 限制與資料庫初始化流程，並核對相鄰測試。
- 提交範圍排除本機資料庫、上傳檔、bin/obj、`.artifacts*`、node_modules、TRX、瀏覽器 trace 與原始測試日誌。
- README 區分合成資料、本機 Demo 與正式營運需求；不引用舊版本效能數字作本輪新量測。
- CI 使用 Windows runner，對齊本輪實測與 README 支援範圍；其他作業系統未在本輪驗證。

## 重現

依根 README clone 後，由 repository 根目錄執行：

```powershell
pwsh -File ./start-demo.ps1 -Port 5215 -NoBrowser
```

首次啟動需自動建立 SQLite schema 與合成資料；首頁、`/health/live`、`/health/ready` 應回應 200。停止程序後連接埠應釋放。

獨立測試：`dotnet test PetPet.Modern/PetPet.Modern.sln -c Release`。

`tools/verify-clean-checkout.ps1` 驗證的是 working-tree 匯出，腳本本身不宣稱是遠端 clone；遠端 clone 的執行結果另於交付核對中記錄。

## 遠端 clone 實測結果

2026-10-19 從 GitHub `master` 全新 clone 提交 `18bd09c5e72298fa665828737a6577b47461529b`，確認沒有既有資料庫，第一次建置/啟動操作即執行上述 `start-demo.ps1`：

- 首次 ready：18,600 ms，包含建置、migrations 與 seeding；不是只有 Kestrel 程序啟動時間。
- 首頁、動態牆、`/health/live`、`/health/ready`、`PetPet.Web.styles.css` 皆為 HTTP 200。
- 唯讀查核生成 SQLite：120 會員、240 寵物，每會員 0–4 隻且每組各 24 人、1,200 貼文、2,400 留言、8,399 按讚、300 好友關係（180 已接受、120 待確認）。
- 測試結束後僅停止本輪程序樹，5215 已釋放；既有 5213 展示程序保留。

此結果證明遠端來源的首次下載啟動與合成資料生成。本輪沒有重新執行整套瀏覽器互動/效能評測，不把舊結果當作本輪新結果。

## 保留的限制

這是本機展示交付，沒有新增外部郵件、帳號刪除匿名化、正式金鑰/多執行個體設定或 SQL Server 實體遷移與備援驗證。聊天聯絡人列表仍有每聯絡人查詢的效能改善空間，記憶體限頻也不適用多執行個體；不阻擋固定規模本機展示，不代表正式營運準備完成。

### 相依套件更新優先度：高

本輪 `dotnet list <solution absolute path> package --vulnerable --include-transitive` 查得既有套件的已知公告，包含 Microsoft.Data.SqlClient 5.1.1、Microsoft.Extensions.Caching.Memory 8.0.0、SQLitePCLRaw.lib.e_sqlite3 2.1.6、System.Text.Json 8.0.0、Azure.Identity 1.7.0 等。測試通過不能視為安全掃描通過。

本次先交付僅 loopback、合成資料的展示版本，保留既有相依以避免未經驗證的套件升級混入交付。套件修補與對應回歸列為下一步優先工作；不核准公開營運或真實資料使用。可由上述命令重新取得最新公告；本輪未宣稱完成所有漏洞可利用性分析。
