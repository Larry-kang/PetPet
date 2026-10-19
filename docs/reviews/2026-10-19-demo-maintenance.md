# 本機 Demo 維護紀錄

## 變更

- .NET 8 的 EF Core、ASP.NET Core 測試套件及 dotnet-ef 更新至 8.0.32。
- 明確引用 SQLitePCLRaw.bundle_e_sqlite3 2.1.14，涵蓋應用程式與 seed-verifier，避免保留舊原生 SQLite 相依。
- CI 更新為 actions/checkout v7.0.1、actions/setup-dotnet v6.0.0，權限限制為 contents: read，保留 Windows 建置與測試。
- README 增加四張本機實際操作截圖與展示圖集。

## 實際驗證

- 隔離產物目錄還原、Release 建置與測試：73 通過、0 失敗、0 略過。
- NuGet 漏洞查詢涵蓋 solution 七個專案及間接相依：當次查詢未列出已知漏洞套件；這不是完整安全稽核。
- seed-verifier Release 建置通過，對全新展示資料庫驗證成功：120 位會員、240 隻寵物、1,200 篇貼文、2,400 則留言、8,399 筆按讚與 300 組好友關係。
- 更新後 Release 於獨立 loopback 連接埠啟動，實際瀏覽首頁、寵物與動態牆；聊天室顯示已連線，測試訊息顯示已送出。
- 原有 5213 展示程序未停止。截圖使用獨立測試資料庫。

本機 Demo 的範圍與正式營運前待辦維持 README 所述。

## 版本來源

- [checkout 官方版本](https://github.com/actions/checkout/releases/tag/v7.0.1)
- [setup-dotnet 官方版本](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0)
- [SQLite 原生相依公告](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
