# PetPet.Modern 產品現代化展示版

PetPet.Modern 為以 .NET 8 與 Clean Architecture 重新建構之毛孩社群平台展示版本，提供無須依賴外在資料庫伺服器的本機即時操作體驗。

---

## 🚀 快速啟動 (Quickstart)

```powershell
# 由 repository 根目錄執行（Windows / PowerShell 7 / .NET 8 SDK）
./start-demo.ps1

# 執行單元、SQLite 與 HTTP 整合測試（實際數量以輸出為準）
dotnet test PetPet.Modern/PetPet.Modern.sln

# 受控重設展示資料庫
./reset-demo.ps1 -RESET
```

- **執行環境**：.NET 8 SDK
- **展示連接埠**：預設 `http://127.0.0.1:5213`（限制本機 Loopback 請求，保護安全）
- **儲存提供者**：SQLite（預設）/ SQL Server（支援多 Provider Migrations，需明確指定連線）

---

## 📊 確定性展示種子資料統計 (Deterministic Seed Data)

執行 `./start-demo.ps1` 首次啟動時，系統將自動套用 SQLite Migrations 並生成確定性擬真資料（Seed=20261013）：

| 項目 | 數量 / 分布規則 | 說明 |
|---|---|---|
| **合成會員** | **120 位** | 含 5 組固定展示帳號，全數密碼雜湊為 `DemoPass!2026` |
| **寵物總數** | **240 隻** | 每位會員 0–4 隻寵物均勻分布（各 24 人），支援公開/隱私設定與月齡推估 |
| **社群貼文** | **1,200 篇** | 1,180 篇正常公開，20 篇預設遭檢舉隱藏（供後台審核展示） |
| **留言互動** | **2,400 則** | 分頁讀取 (20/頁)，嚴格保持關聯 integrity |
| **貼文按讚** | **8,399 筆** | 每篇貼文 0–20 位不同會員獨立點讚 |
| **好友關係** | **300 組** | 唯一無向 Pair；180 組已接受、120 組待確認 |

---

## 🛠️ 階段功能成熟度矩陣

### ✅ S0：一指令啟動與資料治理
- **Zero-Friction 啟動**：`start-demo.ps1` 具備程序生命週期鎖定（`.run.lock`）與初始化互斥鎖（`.init.lock`）。
- **多 Provider 架構**：分流 `PetPet.Migrations.Sqlite` 與 `PetPet.Migrations.SqlServer`。
- **資料保護與受控重設**：`reset-demo.ps1` 僅允許顯式 `-RESET` 旗標清理受控路徑，重啟不覆蓋使用者操作資料。

### ✅ S1：主展示路線與核心社交
- **身分驗證與即時撤銷**：Cookie Claims 包含 `PublicId` 與 `SecurityStamp`；`OnValidatePrincipal` 於停權或密碼重設時立即阻斷 Session。
- **寵物 CRUD**：樂觀併發版本控制 (`Version` GUID)，非本人操作阻絕。
- **社群牆與點讚**：20 篇/頁游標投影，留言分頁，冪等 `SetLike` 點讚切換。
- **好友狀態機**：`None -> Pending -> Accepted`，反向併發 409 衝突處理。
- **可解釋玩伴推薦**：同城市 +30、同品種 +30、確定性探索加分 0–19（總分 0–79），無「速配 %」或虛假 AI 宣稱。
- **管理後台基礎**：會員啟用/停權，防止停權自己與系統最後一位活躍管理員。

### ✅ S2：產品完整性與內容治理
- **會員公開/私密檔案**：對外 PublicId 白名單投影，嚴格屏蔽電話、私有 PK 與 Email。
- **搜尋引擎**：純文字 Literal Contains 檢索，支援城市篩選與排序。
- **站內通知**：好友邀請、好友接受、新留言自動產生通知；唯一事件鍵防重發，標記單筆/全部已讀。
- **雙向封鎖 (Block)**：同交易內撤銷既有好友關係，阻斷推薦、邀請與新訊息發起。
- **檢舉與後台審核**：會員檢舉分類（垃圾訊息、騷擾、不當內容、其他），管理後台提供案件解決（隱藏貼文）、駁回（附理由）與貼文還原。

### ✅ S3：即時聊天與安全媒體
- **即時私訊 (PP-MSG-01 ~ 03)**：
  - 僅允許已接受之好友傳送訊息；任一方封鎖或停權即刻拒絕新訊息。
  - 既有對話歷史支援唯讀檢視（好友解除或封鎖後保留純文字歷史）。
  - 50 筆游標分頁（`(SentAt, Id)` 穩定排序）。
  - 送出端具備 `ClientMessageId` (GUID) 去重，DB Commit 後回傳 ACK。
  - 本機 SignalR 客戶端 (`signalr.min.js`) 具備斷線重連與重新拉取未讀訊息機制。
  - 收件端批次標記已讀 (`ReadAtUtc`)。
  - 管理員不得因角色特權窺探任何私人對話。
- **安全媒體儲存 (PP-MEDIA-02)**：
  - 上傳嚴格限制已驗證會員、Anti-Forgery Token、最大 5 MiB。
  - 僅接受 JPEG、PNG、WebP（嚴格拒絕 SVG、GIF 動畫及惡意腳本）。
  - 檢驗 Magic Bytes 與可靠解碼器，維度上限 4096×4096 且總像素 ≤ 1,600 萬。
  - 利用 SkiaSharp 重新編碼（Decode + Re-encode）剝除中繼資料 (EXIF) 與潛在惡意負載。
  - 實體儲存於 `App_Data/uploads`（非公開 wwwroot），透過 `/Media/{id}` 依擁有者或公開實體引用權限檢查後提供。
  - 回應標頭強制附加 `X-Content-Type-Options: nosniff`。
  - 具備 24 小時未引用孤兒檔案安全清理常式 (`CleanupOrphansAsync`)。

---

## 📌 S4 正式上線準備缺口與待辦事項 (Production Readiness Gaps)

展示版已完備本機操作與展示流程，以下項目為投入外部正式營運前必須之相依與前置準備：

1. **外部郵件發送服務 (PP-REL-01)**：
   - 需完成整合企業級外部 SMTP / 郵件 API（如 SendGrid、AWS SES 或公司 Mail Relay）。
   - 需實作非同步持久化寄件 Job（Worker Lease、重試最多 3 次、過期 Token 自動取消）。
2. **不可逆帳號刪除與匿名化交易 (PP-REL-02)**：
   - 需實作「5 分鐘內重新輸入密碼」之明示確認流程。
   - 需實作交易式匿名 Tombstone 會員替換，解開 Post/Comment/Message/Report 之 FK 關聯並抹除個人資料。
3. **正式金鑰與多執行個體設定 (PP-REL-03)**：
   - ASP.NET Core Data Protection Key 需持久化至 Redis / Azure Key Vault / AWS KMS。
   - 正式部署需強制 HTTPS、HSTS、CORS 來源白名單與 Secure Cookie 旗標。
4. **SQL Server 正式環境驗證 (PP-REL-04)**：
   - 本機測試套件使用隔離 SQLite 檔案資料庫；執行結果見 repository 根目錄交付檢查連結。
   - SQL Server 遷移與執行環境需於獨立授權之測試伺服器完成端對端備份與還原實測（目前標記為 `NOT_RUN`）。

---

## 🔍 3–5 分鐘快速展示路線

1. **啟動展示**：執行 `./start-demo.ps1`，等待瀏覽器自動開啟首頁 `http://127.0.0.1:5213`。
2. **訪客瀏覽**：直接瀏覽首頁社群牆，觀察 20 篇分頁、貼文內容與即時點讚統計。
3. **飼主登入**：點選登入，使用 `demo-owner@example.test`（密碼：`DemoPass!2026`）。
4. **寵物與設定**：進入「我的萌寵」，查看 4 隻毛孩檔案；進入「個人設定」查看去識別化白名單公開頁。
5. **社交與配對**：
   - 點選「玩伴推薦」，查看同城/同品種透明加分理由。
   - 點選「即時私訊」，向好友 `demo-friend@example.test` 發送即時訊息並查看已讀狀態。
6. **管理員稽核**：
   - 登出後使用 `admin@example.test` 登入。
   - 進入「檢舉處理」查看已通報案件，執行「隱藏貼文」或「駁回檢舉」。
   - 進入「會員管理」查看停權防護機制（無法停權唯一管理員）。
