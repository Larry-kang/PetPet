# PetPet.Modern 展示版與產品成熟化規格書

版本：0.1｜日期：2026-10-13（Asia/Taipei）｜狀態：待使用者檢視的施工規格，非已交付功能。

配套：[需求／驗收對照表](petpet-modern-demo-acceptance.md)。所有 `PP-` 編號為規範性需求；正文的預設、欄位、狀態與錯誤規則均屬該需求的驗收內容。驗收表不得取代或縮小正文。

## 1. 目的、權威與範圍

供飼主分享寵物生活、尋找玩伴、建立社交關係；展示者能在 3–5 分鐘說明會員驗證、業務流程、資料完整性與測試證據。clone 者只需 SDK 和首次套件還原網路即可本機操作。

本次任務只讀取 repository、撰寫這兩份規格文件；不修改產品、不安裝套件、不啟動服務、不刪除資料、不 commit／push／merge。文件不增加後續實作、資料處置、公開部署、寄信或 Git delivery 授權。

**PP-GOV-01（全階段）**：實作前重讀 applicable repository authority，保存無關變更及現有會員驗證成果。Modern 之外只允許必要的根目錄啟動指令、README、ignore、Modern CI 與規格／驗收紀錄；原始 `PetPet/` Framework 作品不改。不得把 AI 協作寫成全部本人手寫或正式營運成果。

2026-10-13 本輪查核：工作分支 `codex/member-authentication`、HEAD `1967a266ed71d069035cf928cced6dc6b212c8e0`，會員修改及既有 docs 尚未提交。檢查 D:\、D:\Dev、D:\Dev\GitHub、repo 根與 repo 內未找到適用 AGENTS／governance 文件；本聊天使用者規則仍適用。沒有 `.codegraph/`，因此使用 scoped source reads。上述是本輪快照，施工時重查。

舊草案 `docs/superpowers/specs/2026-10-13-demo-design.md` 與 `docs/superpowers/plans/2026-10-13-demo.md` 保留為歷史規劃；本完整規格獲使用者確認後作為此次功能／驗收基準，不自行改寫 repository authority。相較草案，將完整聊天與正式上線拆開，增加公開 MemberId、跨 provider uniqueness、軟刪除、操作收據、通知及封鎖規則。

非目標：微服務、Kubernetes、付費、廣告、真實 AI 推薦、繁殖／醫療判斷、商業營運規模、原始 Framework 移植、既有 SQL Server 自動搬移、SQLite 與 SQL Server 效能等價。主展示不要求 Docker、Email 或第三方 API。

## 2. 現況盤點與證據

路徑均相對 repo 根。狀態「有證據」在本輪只表示讀到原始碼／測試定義，**不表示本輪執行 PASS**。前一輪測試結果不沿用為當前 runtime 證明。

| 項目 | 本輪狀態 | 來源與具體缺口 |
|---|---|---|
| 四層 .NET 8／EF 8 MVC | 已實作且有原始碼證據，runtime 未驗證 | `PetPet.Modern/PetPet.Modern.sln`、各 csproj；Application 已有會員流程，其餘 Controller 仍直接使用 DbContext |
| 註冊／登入與密碼雜湊 | 部分實作，有測試定義，未重跑 | `src/PetPet.Application/Members/MemberAccountService.cs`、Infrastructure `MemberPasswordHasher.cs`／`MemberRepository.cs`、Web `MemberController.cs`；Cookie 使用 Email 識別，登出 GET，無修改密碼、公開 profile 或 session 撤銷 |
| 防偽與資源權限 | 部分實作 | Member、Pet、Post 有若干防偽／作者檢查；`AdminController.ToggleUser` 沒有明示防偽；上傳亦未檢查防偽，無停權 Cookie 重驗 |
| 資料庫與啟動 | SQL Server 已設定，runtime 未驗證 | Web `Program.cs` 使用 UseSqlServer／EnsureCreated；appsettings 指 localhost:1433，`run_local.ps1` 改 LocalDB，compose 使用 SQL Server 2022；初始化 catch 後繼續啟動 |
| SQLite／migration／一鍵 Demo | 尚未實作 | 未找到 SQLite provider、Demo launch profile、migration 或 start-demo.ps1 |
| seed | 部分實作 | Program 內 13 位會員、12 隻寵物、10 篇貼文的少量示範；在 News 不存在條件內，沒有交易或 seed 完成標記；新會員保存前有雜湊 |
| 寵物／社群 | 部分實作 | `PetController`／`PostController` 與相應 Views；新增直接綁 Entity，owner／author 與導覽欄位可能造成 validation 問題，需 HTTP 重現；圖片表單值未完整持久化 |
| 社群查詢 | 已實作但不符合大量資料目標 | Post Index 一次 Include 作者／Likes／全部 Comments，無分頁、搜尋、詳情、內容啟用 filter；留言字數只在 Entity 定義，Action 未充分驗證 |
| 好友 | 僅資料模型 | Domain `Friend.cs`、DbContext；未找到 Friend Controller／Views，無 unique pair、操作流程 |
| 推薦 | 部分實作 | Infrastructure `MatchService.cs` 隨機加分、忽略 IsMatchEnabled、所有候選載入；View 顯示「% 速配」、城市只有臺北／其他、品種以 ID 呈現 |
| 聊天 | 部分實作，未驗證連線 | `ChatController.cs`、`Hubs/ChatHub.cs`、`Views/Chat/Index.cshtml`；歷史無分頁，無好友／封鎖／長度／去重策略、未讀未閉環，SignalR client 用 CDN |
| 檢舉 | 部分實作 | `ReportController.cs`、Domain Report；狀態為任意字串，無 Reporter FK 明確設定、處理 Action／稽核；Admin ManageReports 回 View，但 Views/Admin 缺 ManageReports |
| 圖片／隱私 | 部分實作 | ImageController 只檢查 5MB 與副檔名，Shared image picker；多處引用不存在的 default 圖；Member Index 直接顯示 Email／電話，Privacy 仍模板 |
| 搜尋／通知／封鎖 | 尚未實作 | 未找到對應 application、entity、controller、view |
| CI | 已有檔案，但觸發／執行未驗證 | `.github/workflows/ci.yml` 對 main；目前基準為 master；只有 restore／build／既有 UnitTests，無 SQLite／browser gate |
| 真實資料庫／機器環境 | 無法確認 | 本輪未連 SQL Server，未查其服務／容器、未開網站或執行測試 |

表內 `src/...`、`tests/...` 均在 `PetPet.Modern/`。既有 `MemberAuthenticationTests.cs`、`MemberHttpTests.cs` 使用 EF InMemory，能描述登入／MVC 意圖，不能證明 SQL FK、unique、transaction 或多請求競爭。

## 3. 階段與完成定義

| 階段 | 可獨立交付成果 | 相依／不包含 |
|---|---|---|
| S0 展示基礎 | SDK 一指令啟動、SQLite migrations、seed、統計、重設與明確錯誤 | 先保留會員登入；其餘未驗收功能不得以可用主入口宣傳 |
| S1 核心展示 | 註冊／登入／登出、session 權限、寵物 CRUD、社群分頁／詳情／留言／讚、好友、基本推薦、最小管理停權 | 相依 S0；全部使用本機 preset 圖；主展示路線可走完 |
| S2 產品完整性 | 公開／私人 profile、修改密碼、寵物豐富檔案、搜尋、通知、封鎖、檢舉處理／稽核、介面一致性 | 相依 S1；通知開始後提供 backfill  fixture，不重建舊 seed 或通知歷史事件 |
| S3 進階本機能力 | 完整聊天、訊息狀態／重試、本機 SignalR、安全圖片上傳 | 相依 S2；不要求對外網路或寄信 |
| S4 正式上線準備 | Email 驗證／重設、刪除帳號、部署／secrets／備份還原／健康與負載 gate | 相依 S3；單獨 scope approval，非本機展示必要條件，未通過不可稱正式可上線 |

先 S0 解決啟動門檻，再 S1 驗證互動；S2 形成產品完整流程；S3 的傳送／檔案風險獨立驗收；S4 涉外部服務和資料治理單列。這是完整目標的階段切分，後續項目仍保留規格，不能因目前測試容易而永久縮減。

每階段完成＝相關 PP requirements 全部有可檢視證據，DTO／schema／views／tests／文件同步，無 placeholder 按鈕，fresh build／適用測試通過，上一階段無退化，scope diff 經檢查；實作完成不自動授權 delivery。

## 4. 角色、可見性與權限

| 動作 | 訪客 | 啟用會員 | 擁有者／對應關係一方 | 管理員 | 停權會員 |
|---|---|---|---|---|---|
| 公開內容／profile／寵物 | 可，只見公開欄位 | 可，套 block filter | 同左 | 同左，後臺另外看隱藏 | 登出後只享訪客公開內容 |
| 我的資料／寵物／私訊／通知 | 不可 | 只看自己／自己對話 | 同左 | 不可任意讀他人私訊 | 不可 |
| 建立貼文／留言／讚／邀請 | 登入導向 | 可，套狀態與限制 | 同左 | 一般操作同會員 | 不可 |
| 編輯／刪自己寵物、貼文、留言 | 不可 | 不能改別人 | 僅本人 | 透過另設 moderation 隱藏，不冒充作者編輯 | 不可 |
| 接受／拒絕邀請 | 不可 | 無關者不可 | 僅邀請收件者 | 不可代接受 | 不可 |
| 取消邀請／移除好友 | 不可 | 無關者不可 | 取消僅 requester；移除 accepted 任一方 | 不可代操作 | 不可 |
| 檢舉 | 不可 | 對可見非自己內容 | 可；不能看別人檢舉原文 | 後臺處理 | 不可 |
| 管理停權、內容恢復、檢舉原文 | 不可 | 不可 | 不可 | 可、有 reason／audit | 不可 |
| Demo 固定帳號說明 | 只在 Demo | 同左 | 同左 | 同左 | 同左 |

**PP-SEC-01（S1）**：每個受保護讀寫以資料庫最新 Member.IsEnabled／SecurityStamp 重驗，不只 claims；管理員 role 也重驗。失效 Cookie 登出；HTML GET 導登入，JSON／Hub 回未授權。既有 Hub session 每次 send 重驗。管理員不具有「所有個人資源的 owner」權限。

**PP-SEC-02（S1）**：所有變更用 POST／PUT／DELETE；MVC／fetch Cookie 操作必須 antiforgery，包含 logout、管理、檢舉、圖片；Hub 用同站 Origin 檢查與 cookie 身分，不接受跨站連線。訪客 view 不呈現寫入表單，server 同時執行權限。管理自己的停權及最後一名啟用 admin 停權拒絕 409。

**PP-SEC-03（S1→S3）**：服務端驗證、Razor 編碼與 JS textContent，內容純文字、不接受 HTML／Markdown 執行；導向僅 local returnUrl。UI 使用 Member.PublicId，不把 Email 當公開 route／DOM／Hub user identifier；EF 關聯暫保留 Email 私有鍵，不對外傳完整 Member、Password 或私有欄位。

封鎖不等於把公開網站變私密：匿名者仍可讀公開內容；對登入雙方的個人化查詢套用排除，私人聯絡永遠不公開。這個限制須在 block 說明及 Privacy 頁清楚呈現。

## 5. 執行模式、啟動與初始化

**PP-RUN-01（S0）**：repo 根 Windows PowerShell 5.1／7 指令 `./start-demo.ps1`；macOS／Linux 或已知環境可用 `dotnet run --project PetPet.Modern/src/PetPet.Web --launch-profile Demo`。前提 .NET 8 SDK 可選用（global.json pin 8.0.400、rollForward latestFeature，並先檢查安裝的 8.x）；不需 dotnet-ef 全域工具、SQL Server 或 Docker。首次 restore 網路、失敗原因與下一步須清楚。script 以 PSScriptRoot 定位、process-scoped env，離開時還原，不永久改執行政策／系統設定。缺 SDK 回非零並附安裝說明，不自動安裝。

**PP-RUN-02（S0）**：新增 `Demo` ASPNETCORE_ENVIRONMENT profile；預設 `http://127.0.0.1:5213`（沿用現有 port）。Demo.Enabled、environment Demo、provider Sqlite 三者須一致，任一矛盾 fail closed；禁止 Demo bind 非 loopback。script `-Port` 可指定 1024–65535，不偷偷改其他服務；端口占用明確退出。啟動成功必須 seed 已完成且 GET `/health/ready` 回 200，不能靠 process 啟動或固定 sleep。script 還原建置後，子進程以前景可管理方式運作，readiness 最長 60 秒，僅成功後開預設瀏覽器；中止清理自己建立的 handle，不終止其他 dotnet。

| 模式 | provider／自動資料操作 | 帳密／網路 |
|---|---|---|
| Demo | 專用 SQLite，auto migration + synthetic seed | loopback HTTP，固定帳密提示與 Demo banner |
| Development | SQL Server 顯式設定，或獨立 SQLite development path；缺 provider／connection fail | 不建立虛擬會員，不顯示 Demo 帳密 |
| Production | 明確 provider／外部 connection／schema 版本檢查；不自動 migration、seed 或 EnsureCreated | HTTPS、外部 secrets；S4 release gate，非展示宣稱 |

**PP-RUN-03（S0）**：Demo DB 唯一路徑為 Web content root 下 `App_Data/demo/petpet-demo.db`；以絕對路徑解析、非 current directory。忽略該目錄內 DB／WAL／SHM、demo lock，以及 `App_Data/uploads/`、私有本機設定；migration／模板／preset assets 必須提交。不得沿用或 fallback 到 SQL Server DefaultConnection。輸出模式、網址、DB 路徑與 seed 實際統計，不輸出一般環境 secrets。

網站本身（含直接 dotnet run）在整段生命週期持有同目錄 `.run.lock` 的排他 handle，與短期 `.init.lock` 分開。reset 必須先能取得 `.run.lock` 且 DB 檔案未被使用；不能以 lock 檔案存在或不存在判斷程序是否運作。正常退出或程序終止釋放 handle，保留的空檔案不阻擋下次啟動。

**PP-DB-01（S0）**：一個 shared DbContext model，新增 SQLite／SQL Server 兩個 migrations assembly 與 design-time factory，依 provider 明確選取；每個 schema 變動兩邊都有 migration／snapshot，不能用單純不同 folder 冒充 provider 隔離。包版本維持相容 EF 8，實作時核對並記錄確切版本。Demo 使用 Migrate，移除啟動 EnsureCreated；SQL Server migrations 由部署明確執行，不對既有 DB 自動 baseline。舊 EnsureCreated DB 的採納需 schema diff、備份與獨立授權，不由 Demo 指令處理。

**PP-DB-02（S0）**：初始化 acquire 專屬 demo `.init.lock` 排他 OS handle，第二啟動最多等 10 秒後退出；檔案存在本身不表示鎖定，handle 釋放後可接手。migration 完成後，在同一 DB transaction 建全部 seed 及 SeedRecord；任何失敗 rollback seed，記錄不含敏感值的錯誤，process exit 非零。migration 可能已完成不要求同時 rollback schema；重試從有效空 schema 繼續。

**PP-DB-03（S0）**：業務表空且無 seed record 才 seed；完成紀錄存在就保留所有使用者增刪改，不補回 fixture、不重洗讚／朋友。非空無紀錄停止並提示「非展示初始資料，請選擇獨立展示資料庫」，不擦除。版本不相容停止並說明 schema migration 或明確重設；支援的 schema upgrade 不重播 business seed，新增階段 fixture 用獨立一次性版本標記。

**PP-DB-04（S0）**：`reset-demo.ps1` 只刪固定 demo DB、WAL／SHM、Demo 專屬 uploads（S3 才有），不刪資料夾外檔案或一般 uploads。顯示解析後絕對路徑、要求輸入 RESET，網站 running handle 或檔案鎖存在則拒絕；沒有 headless 強制 flag。驗證每個 target containment、reject reparse point／symlink，native PowerShell LiteralPath 操作，不跨 shell 拼刪除指令。完成後提示重新 start，未確認不變動。

**PP-RUN-04（S0）**：SDK／restore、init timeout、SQLite lock／read-only／disk-full、schema version、port conflict 分類訊息；不出現「啟動成功但資料庫不可用」。ready endpoint 只給 ready／not-ready，不暴露資料或連線。完整診斷以 traceId 關聯，stack 不向一般使用者顯示。

## 6. Seed 契約

**PP-SEED-01（S0）**：預設 seed=20261013、GeneratorVersion=1、BaseTimeUtc=2026-10-01T00:00:00Z；seed 設定只供首次 seed／明確重設。注入 clock，base 晚於現在拒絕；POST／聊天使用現在 UTC，不使用 seed 時鐘。以明確固定 PRNG 演算法及固定排序重現；內容／關聯 deterministic，Identity 密碼 salt 不必相同。

| 實體 | 精確 seed 數量與分布 |
|---|---|
| Member | 120（含 1 admin、1 disabled、118 enabled 非 admin）；不需提供真實電話，空字串為預設 |
| Pet | 240；0–4 每組 24 人，順序 seed 打散但保留展示角色指定分組 |
| Post | 1,200；其中 20 隱藏、1,180 啟用；作者選 enabled 非 admin |
| Comment | 2,400；作者有效，PostId 有 FK；隱藏貼文留言可存在但不公開 |
| Like | 每篇 0–20 人；不固定總數，由 generator 驗證及 seed record 記實際總數 |
| Friend | 300 唯一 pair，180 accepted、120 pending；雙方 enabled 非 admin，sender／receiver 不同 |

**PP-SEED-02（S0）**：包含 `demo-owner@example.test`（4 寵物）、`demo-nopet@example.test`（0 寵物）、`demo-friend@example.test`（可接受 owner 的邀請）、`admin@example.test`、`disabled@example.test`；固定密碼 `DemoPass!2026` 僅合成 Demo、保存 hash。保證 owner 有 incoming／outgoing pending、accepted 及可移除關係，owner 啟用配對。其餘使用 user001 等匿名代稱與合成繁中姓名；管理員不進推薦／好友 seed。

**PP-SEED-03（S0）**：模板本機、繁中、純文字，主題散步／餵食／玩具／領養／訓練／日常；至少 30 title、30 body、20 comment 模板與不同寵物／城市／時間組合，不把編號當唯一內容。Member 公開圖與 Pet／Post 圖從提交的本機 assets 取，無第三方 URL、真人姓名／照片、抓文或 AI API。每個有效非 admin 作者至少 1 篇；選定可見貼文保證零讚、20 讚、無留言、≥30 留言各存在；時間在 base 前 90 天，comment≥post 且≤base，DOB 不在未來。

**PP-SEED-04（S0）**：生成後 DB 查詢驗證精確數量、分組、無自加／反向 pair／重複 like、FK、時間、Hash 可驗證、公開資源可取得；交易提交後輸出實際 counts、seed、version、baseTime。SeedRecord 保存初始 counts 與業務內容摘要 fingerprint（排除 hash salt、DB auto IDs、實際完成時間）；首頁另外查即時 counts，兩者不得混淆。4 隻只限制 fixture，不限制產品新增。S2／S3 的 10 檢舉、20 通知、30 對話訊息 fixture 用各自標記一次建立，既有資料不重洗。

## 7. 資料設計與相容性

**PP-DATA-01（S0→S4）**：保留 Email 當既有 private PK／FK，登入 Email 暫不允許自行修改；新增 PublicId GUID unique 作公開識別及 SignalR user identifier。新增 NormalizedEmail=trim + invariant uppercase、unique；登入／註冊一致正規化，密碼不 trim。SQL Server 舊資料大小寫碰撞先輸出衝突清單阻止 migration，不合併帳號。一般新會員使用隨機 GUID 保存；fixture PublicId 由 seed＋固定會員序號以 SHA256 前 16 bytes 固定產生 GUID，保證重設可重現；既有會員 migration 新增 GUID 一次保存。既有 cookies 無 PublicId 一律要求重登入；後續不從 url/email 推導身分。

| 實體／階段 | 必要變更、欄位規則 |
|---|---|
| Member S0/S1/S2 | S0 增 PublicId／NormalizedEmail 支援 seed，S1 加 SecurityStamp／Version，S2 豐富 profile；Email≤254、Password hash≤4000、Name 1–40；Bio nullable≤500、Phone nullable≤20 私人、CityId nullable catalog；Birthday nullable 私人、Gender nullable 私人、Photo≤1000 asset path；IsEnabled／IsAdmin／IsMatchEnabled。未知值 null，不把註冊日當生日或硬填城市／性別；舊資料不猜真實值 |
| Pet S1/S2 | Name 1–40、OwnerEmail FK、公開 Pet.Id；KindCode（Dog/Cat/Other）、VarietyId catalog FK 可未知、Gender nullable、BirthDate nullable 或 EstimatedAgeMonths nullable 0–600（二擇一），Bio≤500、IsPublic=true、Photo nullable 本機 asset、Version GUID |
| City／Variety S1/S2 | 提交本機唯讀 catalog，繁中名稱及穩定 code；city Taiwan 縣市、不收精確住址；Variety 1／2 保留與現有 dog／cat 示例相容，明示「狗／貓／其他／未知」，不把現有 ID 當精確犬貓品種；S2 才擴充品種與 Kind 相依 |
| Post S1 | Title 1–100、Content 1–4000、ImageUrl nullable asset path、Author FK、CreatedAtUtc、UpdatedAtUtc、IsEnabled、IsDeleted=false、Version GUID；pure text |
| Comment S1 | Post FK、Author FK、Content 1–500、CreatedAtUtc、UpdatedAtUtc、IsDeleted、Version GUID |
| Like S1 | unique(PostId,UserEmail)、兩邊 FK；新增只在可見有效貼文；cancel 重複無副作用 |
| Friend S1 | 保留 Requester／Addressee、IsAccepted；新增 MemberA／MemberB 私有標準化 pair、unique(A,B)、AcceptedAtUtc nullable、Version；A≠B、pair 按 NormalizedEmail ordinal 排序，只由 server 設定 |
| Block S2 | Blocker／Blocked Member FK、unique 有向 pair、CreatedAtUtc；拒絕自己 |
| Notification S2 | Recipient FK、Actor FK nullable、Type、EventKey≤200、TargetType／TargetPublicId、CreatedAtUtc、ReadAtUtc nullable、純文字安全摘要；unique(Recipient,EventKey) |
| Message S3 | 既有兩邊 FK、Content 1–2000、SentAtUtc、ReadAtUtc nullable（取代僅 bool），ClientMessageId GUID；unique(Sender,ClientMessageId)，Conversation pair index |
| Report S2 | PostId nullable FK／作者軟刪仍有 row、Reporter FK、Category enum、Reason≤500、Status enum Pending/Resolved/Dismissed、CreatedAtUtc、HandledAtUtc／HandledBy nullable、HandlingNote≤1000、Version |
| AdminAudit S1/S2 | 操作者 FK、公用 TargetId、Action、Reason 1–1000、Before／After 最少必要 JSON、UTC、TraceId；immutable、禁止把 Password／完整 private payload 寫入 |
| SeedRecord S0 | 唯一 GeneratorVersion／seed package key、Seed、BaseTimeUtc、CompletedAtUtc、Counts JSON、BusinessFingerprint；完成 transaction 中寫入 |
| OperationReceipt S1 | Actor FK、Operation、CommandId GUID、PayloadDigest、ResultCode／TargetId、CompletedAtUtc；unique(Actor,Operation,CommandId)，與 domain write 同交易，不存密碼操作 payload |
| Media S3 | Id GUID、Owner FK、StorageKey（伺服器檔名）、ContentType、ByteLength、Width／Height、CreatedAtUtc、OrphanedAtUtc nullable；引用由 Member／Pet／Post 的 MediaId nullable FK 管理，preset 路徑與 upload MediaId 二擇一 |
| Member S4 增量 | EmailVerifiedAtUtc nullable；Demo fixture 有驗證時間，正式註冊無；非已驗證帳號不能進行受限制操作 |
| AccountToken S4 | Id GUID、Member FK、Purpose VerifyEmail/ResetPassword、TokenHash unique、ExpiresAtUtc、UsedAtUtc／RevokedAtUtc nullable、CreatedAtUtc；只有 hash，不保留原始 token |
| MailJob S4 | Id GUID、Member FK、Template／Purpose、必要收件資訊、狀態 Pending/Processing/Sent/Failed/Cancelled、AttemptCount、NextAttemptAtUtc、LeaseUntilUtc、ProviderMessageId nullable、CreatedAtUtc；機密 token 只在加密寄件 payload 中、完成／失效即清除，不記 log |
| AccountDeletion S4 | Id GUID、Member FK、狀態 Requested/Anonymized/Completed/Failed、CreatedAtUtc／CompletedAtUtc、媒體清理任務與不含秘密的 FailureCode；unique(Member,未完成請求)以服務交易及 provider 對應限制維護，不在完成紀錄保留原始 Email |

**PP-DATA-02（S1→S3）**：索引 Post(IsEnabled,IsDeleted,CreatedAt,Id)、Comment(PostId,IsDeleted,CreatedAt,Id)、Pet(OwnerEmail,Id)、Friend(Requester,IsAccepted)／(Addressee,IsAccepted)、Notification(Recipient,ReadAt,CreatedAt,Id)、Message(conversationPair,SentAt,Id)、Report(Status,CreatedAt,Id)。FK default Restrict，刪除流程顯式依序處理；雙 Member FK 不 cascade，避免 SQL Server multiple cascade path。每個 provider 真實 DB 驗證，未驗證不得稱相容。

**PP-DATA-03（S1）**：post／comment 作者「刪除」採 soft-delete，對讀者隱藏，保留已存在檢舉／audit；管理隱藏用 IsEnabled，不等於作者刪除，恢復不解除 IsDeleted。Pet 可 hard-delete（目前無其他業務依附，刪前檢查）；Member 不在 S1 hard-delete。Friend／Like 移除 physical delete。Notification 指向刪除／隱藏內容時顯示不可用，不洩露原文；不使用 generic FK 指向已刪資料造成錯誤。帳號刪除依 S4 的匿名化流程。

**PP-DATA-04（S1）**：跨 provider 使用 application-managed Version GUID 作 optimistic concurrency，更新 WHERE Version=提交版本，每次更新生成新值；不依賴 SQLite 不支援的 SQL Server rowversion。stale edit 回 409 並保留草稿、提示重新載入。mutation transaction 同時處理 receipt、domain、通知／audit；constraint violation 只轉換已知衝突，不吞其他 DB errors。SQLite SQLITE_BUSY 最多兩次整個未成功交易重試（100/200ms）；仍失敗 503，UI 保留 CommandId，不重播已完成 receipt。

好友、封鎖及私訊的「檢查後寫入」也在同一短交易內：SQL Server 用 Serializable／固定 pair 鎖定順序，SQLite 先取得寫入交易後重新查依賴狀態，不在交易外讀 friend/block 再保存。遇 deadlock 只重試整個未提交命令一次，之後 503。驗收安排 block 與 accept／send 同時執行：若 send 先提交可保留該歷史訊息；block 已提交後不得再接受邀請或保存新訊息。

## 8. 通用輸入、HTTP 與操作契約

**PP-UX-01（S1）**：HTML 表單錯誤 200 view + field／summary、不變動 DB；成功 PRG 303、TempData 清楚提示；fetch JSON 400 VALIDATION、401 UNAUTHENTICATED、403 FORBIDDEN、404 NOT_FOUND、409 CONFLICT、429 RATE_LIMITED（Retry-After）、503 TEMPORARY_UNAVAILABLE、500 UNEXPECTED，含 code/message/traceId、可選 fieldErrors。不存在或對 blocked／非可見 target 的讀取 404；已確認可見但非 owner 的寫入 403；不得回 stack／私密值。自助恢復有返回列表／重試入口。

**PP-UX-02（S1）**：送出中 disabled、成功／失敗／empty／loading 各有繁中提示；驗證失敗保留除密碼以外欄位，never 回填密碼／hash。刪除確認列出資源名稱；重複提交帶 CommandId 的 create／friend／moderation 依 receipt 回原結果，同 key 不同 digest 409。Like 改 desired state liked=true/false，不以「toggle」使重試反轉；Notification mark-read／unblock 同樣 idempotent。未完成階段入口顯示「尚未納入驗收」或不顯示，不留可點卻失敗的功能。

**PP-UX-03（S1→S2）**：列表預設 page=1、pageSize=20 fixed，page<1 校正 1、非數字／未知 filter 400；超出尾頁導末頁，empty 用 page1；sort whitelist、stable Id tie-breaker。query≤100 trim，關鍵字 literal contains，不當 SQL pattern；列表主體維持投影不載全 entity。成功導回帶 page/filter 的 local URL，目標刪除後校正尾頁。S1 Post/Comment 詳情、好友與管理分頁；S2 profile／notification/search 一致。

**PP-UX-04（S1→S2）**：UTC DateTime 保存事件，畫面以 Asia/Taipei 顯示「YYYY/MM/DD HH:mm」及 timezone，生日用 DateOnly 不換時區；seed 時間不得當 last login。320／375／768／1440px 不水平溢出主頁，表格可明示 scroll；keyboard 全程操作、label 對應 input、error focus summary、loading aria-live、modal 焦點可返回。圖片 fallback 本機、alt 具意義；色彩不作唯一狀態提示。S1 主展示做到，S2 全功能做到。

**PP-SEC-04（S1→S3）**：單 instance local rate limit 預設 login IP 10/min + normalized identity 5/min（不在 log 記原始 Email）、註冊 IP 5/min、內容寫入 member 30/min、邀請／檢舉各 10/min、上傳 5/min、Hub send 60/min；fixed windows，429/Retry-After，不排隊。server + DB limits 必須一致；Demo 瀏覽器不得繞過。S4 多 instance 改共享 limits；本規格不宣稱現有單 instance 防護可營運規模化。

## 9. 功能規格

### 9.1 會員與 profile

**PP-MEM-01（S1）**：註冊 Email／Name／Password 必填，Phone 選填；Email 格式≤254、Normalized unique；密碼 8–128、不任意組合要求、hash保存、不比對明文。重複註冊顯示帳號已存在（本機展示接受枚舉風險，正式 stage需再評估）；成功導登入。登入 unknown／wrong／disabled／legacy malformed hash 相同訊息，不設 Cookie；有效登入沿用 Identity 驗證與需要時 rehash。不允許 form 設 IsAdmin／IsEnabled／PublicId。S4 未驗證 Email 的限制再加。

**PP-MEM-02（S1）**：logout 僅 POST／anti，登出目前 browser session 回首頁；Cookie HttpOnly/SameSite=Lax，idle 30min、absolute 8h，Demo loopback HTTP 允許非 Secure，Production必須 Secure+HTTPS。session includes PublicId、SecurityStamp、IssuedUtc；每次受保護請求重驗啟用、stamp及role，不查不過就不能寫。角色變更、停權、修改密碼會改 stamp使所有舊 session 失效。

**PP-MEM-03（S2）**：`/Members/{PublicId}` 公開只顯 Name／Bio／City 名稱／Photo／公開寵物／可見貼文；不顯 Email／Phone／Birthday／Gender／精確位置／LastLogin／role。self `/Account/Profile` 編輯 Name 1–40、Bio≤500、City catalog或unknown、preset image；Phone 私有。Edit Version conflicts 409；IsMatchEnabled opt-in預設 false、成功提示，沒有寵物仍可關閉／設定。停權 profile 不提供公開推薦與連結，讀取 404；既有公開貼文按獨立可見規則不自動刪除。

**PP-MEM-04（S2）**：修改密碼需 current password 正確、新密碼8–128及confirm一致；失敗不變 hash，成功新 hash、stamp換新、撤銷全部 sessions並導登入，不回填密碼。舊明文帳號必須使用受控管理重設／S4忘記密碼，不在啟動自動轉換。此流程不接受通用 OperationReceipt 保存密碼 payload。

### 9.2 寵物

**PP-PET-01（S1）**：我的寵物 only self，新增 DTO Name／Kind／Variety／Gender／Photo；owner從最新 session決定。name trim 1–40、catalog Kind／Variety 相容、未知有明確選項，不手填ID；preset選取必須在 allowlist。新增成功導我的列表、可建立第5隻（fixture不是product quota）。編輯／刪除僅owner+Version，admin不代改；無寵物顯示新增入口，不假資料。

**PP-PET-02（S2）**：公開詳情只顯 IsPublic 寵物；生日或估算月齡二選一／可皆未知，生日不得未來，月齡0–600；自介≤500、性別未知／男／女、城市從owner公開profile取。公開設定false後訪客與其他會員404、owner可看。修改檔案保留公開設定，不曝光電話與OwnerEmail；fallback如動物unknown使用通用圖。

### 9.3 社群與探索

**PP-SOC-01（S1）**：GET動態牆只visible IsEnabled&&!IsDeleted，desc(CreatedAt,Id)，每頁20；列表取得 Title、純文字摘要前160字、safe author、image、LikeCount／CommentCount（未刪留言）與viewerHasLiked，不 Include 全部likes／comments。詳情才取全文及comment page；隱藏／刪除向一般404，admin透過專用後臺看，不繞普通路由。

**PP-SOC-02（S1）**：發文DTO Title1–100／Content1–4000、preset Image選填；claims author不可覆寫，Server UTC；命令receipt防連點、成功回新詳情。作者編輯+Version、顯示更新時間；不能由發文表單更改IsEnabled。刪除確認soft-delete，同交易失效target notifications（S2），不cascade掉檢舉。server拒絕過長／空白，即使client驗證被略過也不寫入。

**PP-SOC-03（S1）**：comment view chronologically ascending(CreatedAt,Id)、20頁；trim1–500，post必須可見、有權互動、最新啟用member；author來session。編輯／刪除僅comment作者+Version（post作者不得冒充留言作者）；mod可隱藏違規post，不改他人comment文字。通知新增僅post作者且非本人，comment修改不重發。

**PP-SOC-04（S1）**：liked desired state由server upsert／delete unique；相同狀態重試不反轉、不重複；兩獨立 concurrent liked=true最多1列，最後狀態衝突回可重試。post不存在／hidden／deleted／block導404。維持頁面位置與新數量；自讚允許但不產生自通知。

**PP-SOC-05（S2）**：keyword包含title/body，filters作者PublicId、作者city、最新／讚最多；stable order，query輸入為literal、不把%/_當wildcard，不搜尋Email／電話。空結果顯示清除filter入口。所有search與profile內容套相同visible/block規則，不用另一條查詢洩露hidden。SQLite中文檢索先用literal contains、小型作品不引入外部search engine。

### 9.4 好友狀態

**PP-FRD-01（S1）**：好友／incoming／outgoing 各20頁，public profile或recommend action可發邀請。狀態 `None → Pending → Accepted`；拒絕／取消／移除回None，無隱含Rejected永久狀態。UI顯示正確動作與結果，不提供admin代接受。

| 現況 | 命令／actor | 結果 |
|---|---|---|
| None | requester邀請enabled非自己且未block | Pending，記錄方向 |
| Pending | 相同requester重送 | 回既有Pending，不建新列 |
| Pending | 收件者發反向邀請 | 409提示已有收到邀請，需明確接受，不能自動接受 |
| Pending | 收件者接受／拒絕 | Accepted／刪除回None |
| Pending | requester取消 | 刪除回None |
| Accepted | 任一方移除 | 刪除回None |
| 任意 | 無關actor、自加、停權、blocked | 403／400／404依通用可見規則，無變動 |

**PP-FRD-02（S1）**：pair unique及Version enforcement，accept／reject／cancel同時只有一個當前版本可成功，其他409+refetch；跨向建立只保留1列。CommandId replay回原結果，已移除後不同命令才可再次邀請；接受notification與friend同交易（S2），最後session state反覆檢查。

### 9.5 玩伴推薦

**PP-REC-01（S1；block S2）**：自己IsMatchEnabled=false顯示設定入口、不推薦；無寵物顯示新增寵物，不假想同品種。候選 enabled、opt-in、非admin、非自己、至少1隻public pet；S2排除任一方向block。city／Kind filters使用catalog。每頁20，與固定快照排序一致；空結果提示放寬條件。

**PP-REC-02（S1）**：同城市+30（雙方城市已知）、任一對相同非unknown品種+30；各項只計一次。額外0–19分由穩定SHA256(seed+pairPublicIds)取值，版本記錄，稱「探索加分」；總分0–79，只顯「推薦分數」與各理由，不標百分比、成功率或稱AI。ties PublicId ordinal；primary pet選排序最小Id的符合篩選public pet，規則可測。改profile／pet後query重新計算，不持久化虛假命中率。

### 9.6 站內通知

**PP-NOT-01（S2）**：events friend-requested、friend-accepted、comment-created；actor=recipient不送。20頁desc時間/Id、unread badge、single/all read；讀取與標讀onlyrecipient、重復mark-read不變更首次ReadAt。click先驗證target可見，不可用顯示「內容已刪除或不可查看」且可標讀，不洩露摘要。

**PP-NOT-02（S2）**：通知與domain write同transaction，EventKey用該mutation事件ID，unique(Recipient,EventKey)；receipt重試不重發。同actor再次獨立留言會有新事件；same CommandId不會。pending取消／reject、block／hidden／delete後相關通知不再有可點有效target；既有安全通用摘要可保留但不顯示被封鎖者內容。不引入broker/outbox；S4外部Email另設持久化job，不能把local notification commit當Email sent。

### 9.7 封鎖與聊天

**PP-BLK-01（S2）**：有向Block建立需enabled actor、非自己、有目標；任一方向block即雙向停止邀請／推薦／新私訊，建立block同交易移除friend/pending、撤銷相關通知target。登入個人化牆/profile隱藏雙方內容，server write強制；匿名公開內容仍可見，UI事先說明。unblock僅blocker，冪等、不恢復舊好友、不重送舊通知。管理員不代解block，moderation可看公開檢舉證據。

**PP-MSG-01（S3）**：只能accepted friends且雙方enabled、未block傳訊；receiverPublicId非自己且有效，trim1–2000。private history only conversation parties，50則cursor pagination、(SentAt,Id)穩定，初次最新50再載更早；admin不能因role讀私訊。移除好友/block後可讀自己既有對話的純文字歷史，但readonly、不可再send，與profile/推薦排除分開；停權者不能讀受保護歷史。

**PP-MSG-02（S3）**：送出ClientMessageId GUID，DB commit後回server MessageId／UTC與ack；Client狀態Sending→Sent或Failed，逾時可用同ID重試；同sender/id/content回同結果，不同content409，不重複插入。SignalR只最佳努力通知，不能把連線delivery當已讀；傳送端從ack渲染、不先以未確認文字假裝已送。以messageId去重多裝置推播；接收／sender更新使用純文字，不innerHTML。

**PP-MSG-03（S3）**：client library本機、withAutomaticReconnect，加初次連線重試；重連用last serverId補歷史，斷線明示／禁止新送或顯示Failed，不靜默丟訊息。ReadAt由收件者在可見對話讀到指定message後server批次設定，只改收到訊息，不由sender聲稱；read≤現在、≥sent。每次send驗證stamp/role/block/friend，blocked後即使舊hub仍連線也拒絕。

### 9.8 檢舉與管理

**PP-MOD-01（S2）**：enabled會員檢舉可見非自己post；category Spam/Harassment/Inappropriate/Other，Other補充必填，其餘可選≤500；server target存在/visible，同Reporter/Post最多1個Pending，重複回已有case。本人可看自己case狀態，不見其他reporter資料；filter/status不任意字串。S2先只post，其他target不冒充已支援。

**PP-MOD-02（S2）**：admin後臺Pending／Resolved／Dismissed各20頁、查看有限證據、reason/notes；Pending→Resolved或Dismissed，終態不重開（新證據建新case）。Resolved可伴隨HidePost，Dismissed必附理由；HandledBy／At／note／audit同transaction。隱藏可被admin restore，restore不解除author deletion；終態case不自動改。重複sameCommand回原結果，不同命令終態409。缺View要真正補齊，不留返回不存在模板。

Report 舊值 `Ignored` migration 明確轉為 Dismissed；Pending／Resolved 保留，其他未定義字串列出並停止 migration，不預設當作已處理。新增 Category 缺值的舊 case 設 Other 並顯示「舊版未分類」，不捏造補充理由；若新規則要求新增 FK 而現存 reporter 不存在，先提出修復清單，不自動刪案或指向任意會員。

**PP-MOD-03（S1最小，S2完整）**：停權命令是SetEnabled(bool,Version,Reason)，不toggle；reason1–1000，保護本人及最後active admin；state改變更新SecurityStamp並audit，啟用不恢復舊session。AdminAudit不能從UI刪改；管理原文、聯絡資料僅適當admin頁可見；不讀私訊作常態管理，不在公開stats暴露案件詳情。

### 9.9 本機媒體

**PP-MEDIA-01（S1）**：preset all local、allowlist server驗證路徑，禁止任意http/file/../；註冊匿名可選preset，不必先登入才能取固定圖庫。avatar/pet/post選取結果確實保存，缺圖fallback，讀取不得產生路徑穿越。S0–S2禁用現有Upload endpoint及upload tab（404／功能未開放），不是留未驗收上傳給人操作。

**PP-MEDIA-02（S3）**：authenticated+anti、max5MiB、只JPEG/PNG/WebP（不接SVG/GIF動畫）；檢查magic及可靠decoder、最大4096×4096且≤16M pixels，decode+reencode移除metadata與主動內容，隨機server檔名；image type不信任client mime／extension。files在App_Data/uploads非wwwroot，由Media/{id}讀取owner/public scope，DB media record檢查reference及ownership，Content-Type正確+nosniff；錯誤400/413，不留孤兒檔。刪除引用只remove reference，24h orphan cleanup由明確維護命令操作受控路徑；不擅自背景刪其他檔。維護用鎖，備份包括媒體與DB。

### 9.10 正式上線準備

**PP-REL-01（S4）**：Email verification／reset需已授權外部寄信服務及持久化寄信job，不在demo寄信。隨機opaque token≥256bits、DB存hash、purpose/account/expiry、驗證24h、重設30min、單次使用、發新token廢前token；回應不透露帳號存在，限頻且不log token。Production signup emailVerified=false、只能看帳戶/verification，不可發文/好友/聊天；reset成功hash+stamp更新；verify不自動授admin。Demo fixed accounts marked verified不呼叫外部服務。

Token 與 MailJob 同交易建立；寄件 worker 使用 60 秒 lease，重試最多 3 次（1／5／15 分鐘）、失效 token 或停權／刪帳則 Cancelled。只有 provider 接受結果可標 Sent，未保證使用者實際收到；provider 不支援 idempotency 時可能重寄同一封，不能宣稱 exactly-once。寄件 payload 以外部持久化 key 加密，成功／失敗終態清除 token 明文與收件 payload；不將 Email、token 放進 audit 或錯誤訊息。

**PP-REL-02（S4）**：刪帳需 5 分鐘內再次驗證目前密碼與明示確認；建立 DeletionRequested 並停權／撤銷 sessions 後，匿名化交易移除 Phone／Birthday／Bio／Photo、寵物、讚、好友、封鎖、通知、tokens、未送 MailJob 及操作收據。保留同一 PublicId 的匿名 tombstone，公開名稱「已刪除會員」、不保留原 Email／Password hash／NormalizedEmail；私有 PK 改成唯一 `deleted-{PublicId}@example.invalid`。

不能直接更改 EF tracked PK：專用流程在交易內將原列 PublicId 暫移為新 GUID，插入匿名 Member 使用原 PublicId 與 tombstone 私有鍵，將保留的 Post／Comment／Message／Report／Audit／Deletion FK 顯式改指匿名列，再刪除原 Member。所有這些 FK 使用 Restrict、不靠 cascade。自身貼文／留言 soft-delete 並清空文字／圖片；既有檢舉僅保留必要違規摘要，若涉及保留內容須依上線前確認政策。訊息給另一方保留文字歷史但作者匿名，事先告知無法撤回對方副本；Audit 僅保留匿名 actor 與必要事件，移除可能包含聯絡資訊的 Before／After。

媒體清理與 DB 匿名化分階段：Anonymized 狀態下持久化受控檔案清理任務，重試完成才標 Completed；失敗不重新插入或刪除其他會員。整個流程可重入，所有 provider 驗證 FK／tombstone uniqueness／部分失敗；不將軟停權當作完成刪除。Demo reset 不是個人刪帳替代，上線前使用者確認 retention／他人保留歷史政策。

**PP-REL-03（S4）**：無repo secrets，外部設定／least privilege DB；HTTPS、restricted CORS/hosts、secure cookie、DataProtection key持久化、多instance共用撤銷/限頻、依賴與runtime support檢查、結構化不含秘密紀錄。只部署到使用者明確授權目標，release阻止Demo=true、公網loopback範圍不符或固定合成admin。readiness DB連通與schema match；liveness不洩漏details。計畫S4驗證SQL Server restore/build/migrations/full functional suite，不只可連線。

**PP-REL-04（S4）**：部署前提供備份／恢復runbook及實作drill：SQLite使用正確online backup或停止網站後DB+媒體一致備份（不只複製正在寫入的主db而漏WAL），SQL Server full backup+驗證restore；target獨立、失敗不覆蓋原資料。單機目標RPO24h、RTO60min作為待量測驗收目標；資料保留預設audit90天、私訊/通知90天，可設定清理前說明，正式環境政策需確認。必須量測並保留證據，未驗證不宣稱正式可營運。

## 10. 模組與交易施工邊界

**PP-ARCH-01（S1→S3）**：Web負責binding/anti/session/HTTP，Application負責業務流程、可見性、validation與結果，Infrastructure EF adapter與transaction，Domain保留狀態不變量。按功能Members/Pets/Social/Friends/Recommendations/Notifications/Moderation/Messaging組織，不為每條CRUD增加無行為pass-through。DbContext、完整Member不能進入View contracts；public DTO只有可見欄位，private內部identity只在服務之間傳。

推薦 seams：`DemoDataGenerator.Generate(seed,baseTime)`純資料；`DemoSeeder.SeedAsync`交易；`VisibilityPolicy`用于feed/detail/search/hub共享決策；好友／內容／管理command services用`CommandId+Version`；notifications是同transaction內部寫入，非Controller額外呼叫後才補。不強制通用repository掩蓋EF能力。migration assemblies是provider真實差異，資料庫異常由adapter分類。

並發驗收必須用兩個獨立DbContext/HTTP requests，不只single mock。sameCommandReceipt unique失敗後讀取已完成結果；incomplete未commit收據與寫入rollback，不能卡永久Processing。提交成功回應丟失仍可重試；已知衝突409，資料庫不可用503，不把例外當成功。好友pending變化／notification與稽核必須保持原子性。

## 11. 非功能目標與證據

**PP-QA-01（每階段）**：保持有行為意義的unit tests；真SQLite integration驗證migrations/FK/unique/事務/並發，WebApplicationFactory驗證HTTP/session，真瀏覽器驗證DOM/console/mobile流程；SQL Server suite獨立。CI在實際default及工作PR branch觸發，不只main；Modern restore/build/test與各階段gate缺一不得用「edit complete」代替。依賴版本固定／lock與README更新、保留原始版本區隔。

**PP-QA-02（S0/S1，S3擴展）**：記錄OS/CPU/RAM/SDK/provider/build設定及資料counts。統一Release、同機無debugger、localhost：首次啟動目標≤60秒（套件restore不計，但另記）、warm restart≤10秒；列表每route20次warmup後100次單使用者request，p95≤500ms；375px首次牆頁除cache靜態資源傳輸≤1MiB、請求取≤20post且沒有全部comments。目標未經實測，不當作成果；超標保留trace/SQL定位並修復，不壓低seed數量來過關。S3 chat ack單機p95≤1秒，以100條分散請求量測，不能聲稱公網延遲。

**PP-QA-03（每階段）**：文件根README先顯示Modern quickstart與原始作品入口，標明確切SDK/PowerShell、首次網路、Demo密碼/loopback、SQL選配未驗狀態、reset及已完成/後續功能；演示錄影/截圖僅來自實際驗收版本，提供source HEAD+working diff fingerprint。驗收證據標明測試名稱/命令/環境/時間/exit/結果，不能以測試定義或靜態檢查宣稱runtime PASS。

## 12. 展示路線

**PP-DEMO-01（S1→S3）**：首頁Demo banner、即時實際counts與角色入口。S1主路線：start→guest分頁牆→owner登入→自己的4寵物/新增→發文/留言/按讚→查看推薦理由→傳送好友邀請→friend登入接受→admin查看統計/停權測試帳號。3–5分鐘可講主流程，不必把1200篇翻完；admin不得停掉唯一admin。無寵物帳號展示empty CTA，disabled帳號示範登入失敗。

S2補profile編輯→search→收到通知→block後推薦/邀請關閉→report→admin resolve/hide；S3兩個browser sessions chat/斷線補歷史/圖片選取。Demo統計公開只給總量、不列個人電話、私訊或案件細節；未驗證的階段入口隱藏或明示未驗收。所有關鍵主流程本機可完成。

## 13. 階段交付與驗證命令

這些是**將來施工驗收命令**，本輪未執行；實際名稱需在實作時對齊並保持需求對應，不可把刪掉測試當作減少範圍。

```powershell
dotnet restore PetPet.Modern/PetPet.Modern.sln
dotnet build PetPet.Modern/PetPet.Modern.sln --no-restore
dotnet test PetPet.Modern/PetPet.Modern.sln --no-restore
./start-demo.ps1
# 真 SQLite 與 SQL Server suites 必須明確 provider，SQLite 可用隔離 temp 文件。
# SQL Server 只有在獨立驗證庫可用且已授權時執行，不讀寫使用者既有庫。
```

每階段均先寫失敗測試/重現當前缺口，再實現並執行focused驗證、相關regression及browser主流程；避免全部新功能最後一起驗收。全新checkout smoke必須包含當前實際待交付修改，不能只clone舊HEAD然後稱新版本可執行。正式delivery另依授權決定，不提交未確認scope。

## 14. 風險、建議預設與待確認

| 風險 | 規格處理／剩余限制 |
|---|---|
| 舊SQL/EnsureCreated無migration baseline | 不自動采用，單獨schema比對與backup/授權；本次不搬舊庫 |
| Email作現有FK與SQL大小寫差異 | 私有鍵暫留、PublicId對外、normalized unique；碰撞阻止migration而不合並 |
| 千筆牆頁與全Include | projection+pagination、真實SQL檢查，不能減資料量繞過 |
| fixture與生產混用 | Demo一致設定、loopback、release gate，Development不再seed |
| 現有上傳/聊天缺防護 | 在相應階段前禁用未驗入口及endpoint；S3做完整流程，不靠已有Hub宣稱可用 |
| 刪除與moderation衝突 | soft-delete、admin獨立狀態、audit與report保留，S4匿名化獨立設計 |
| .NET 8與EF包支援／升級 | 本規格保持net8目標；S4查最新support/修補，必要升級作為單獨變更，不捏造當前最新版 |

建議預設已明確：精確seed數、帖文與留言分別≥1000、0–4非產品quota、僅好友聊天、非AI玩伴推薦、block對匿名公開限制、管理員不讀私訊、S4非首版gate。

只有兩項影響產品/資料政策的確認項，未答覆不阻擋本輪規格完成：

1. **聊天開放對象**：建議僅已接受好友；若要陌生人聊天需額外請求匣、同意與防騷擾規則，不能施工時隨意放開。
2. **正式資料保留/刪帳**：建議90天私訊/通知/稽核與匿名tombstone；此為上線前需確認的產品政策，不是本輪法律合規結論。S0–S3不執行自動清理或不可逆個人資料刪除。

SQLite限制及provider隔離依官方文件：[SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)、[Migrations with multiple providers](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)。只引用多provider需獨立migration與SQLite型別/並發差異；不把EF9 locking誤套EF8。

## 15. 本輪交付狀態

產品目標尚未完成。本輪僅規格文件：重新讀取工作樹及適用文件、模型/Controller/View/設定/測試定義與diff，核對官方SQLite依據；沒執行build/test/網站/資料庫/瀏覽器、沒安裝依賴/傳送訊息/修改產品/commit。文件語義與ID覆蓋檢查結果見配套驗收表末節。產品PASS需未來實際證據。
