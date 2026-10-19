# PetPet.Modern 需求／驗收對照表

版本：0.1｜日期：2026-10-13（Asia/Taipei）｜狀態：規格草案驗收設計，非產品 PASS。

基準：[完整產品規格](petpet-modern-demo-product-spec.md)。每列覆蓋該 requirement 正文全部規則；表中案例是最小必測組合，不授權刪減規格中的其他限制。

## 1. 證據與判定規則

- NOT_RUN：尚未執行；PASS：在對應版本、環境實際驗證全部條件；FAIL：有不符；BLOCKED：缺必要環境／授權，不能當PASS；PARTIAL：部分證明，不算階段完成。
- 本輪所有產品需求為 NOT_RUN（全新spec驗收尚未執行），原始碼／測試定義只能證明現況，不是runtime驗收。
- 每次驗收保存 commit HEAD + working diff／untracked source fingerprint、UTC及臺灣時間、OS／CPU／RAM／SDK、provider／DB schema／seed version／counts、測試命令與exit、案例結果、log／screenshot路徑。
- 測試名為未來實作建議，不表示已有這些測試；不得僅建同名空測試就宣稱覆蓋。
- unit／HTTP／SQLite／SQL Server／browser的證明範圍分開；SQLite PASS不能推論SQL Server PASS；InMemory不證明FK／unique／rollback／併發。
- scope檢查應比對施工前後檔案，確保原始Framework、使用者變更與既有會員成果未遺失。規格本身不授權跑真實使用者資料庫或外部服務。

## 2. 需求驗收矩陣

### 2.1 權限、執行與資料

| Requirement | 階段 | 建議測試／證據 | 輸入／觸發 | 必須觀察到的結果 |
|---|---|---|---|---|
| PP-GOV-01 | 全部 | ScopePreservation + git diff／檔案hash | 實作前後比較，原始PetPet/與既有會員修改 | 只涵蓋授權scope；舊未提交內容保留；README作者貢獻誠實；無未授權delivery |
| PP-SEC-01 | S1 | SessionRevocationHttp／HubAuthorization | member停權、改stamp/role、舊cookie／舊Hub再request | 每個受保護操作拒絕；HTML導登入、API401；admin不可讀他人私人資源 |
| PP-SEC-02 | S1 | AntiforgeryAndRoleMatrix | 每個mutation缺／錯token、GET logout、跨Origin Hub、最後admin停權 | 400或method拒絕、DB不變；跨站Hub拒絕；本人／最後admin保護409；一般會員無admin權限 |
| PP-SEC-03 | S1–S3 | PublicProjectionAndEncoding | DOM／JSON／routes檢查、script內容、外部returnUrl | 只用PublicId；無Email／Phone／hash洩露；文字編碼不執行；拒外部導向 |
| PP-RUN-01 | S0 | CleanCheckoutSmoke／MissingSdk | 全新含當前修改的checkout、SDK有／無、restore網路失敗、不同cwd | 一指令成功或明確非零原因；不需SQL/Docker/ef全域工具；不永久改env／policy；替代跨平臺command可用 |
| PP-RUN-02 | S0 | ModeAndReadiness／PortConflict | Demo設定矛盾、非loopback、port占用、ready延遲、Ctrl+C | fail closed；只ready200後開browser；只清自己的process；60秒逾時有原因，不假成功 |
| PP-RUN-03 | S0 | DemoPathAndIgnore | 從不同cwd啟動，SQL connection env同時存在 | 只Web App_Data/demo固定DB；不fallback SQL；DB/WAL/SHM/uploads ignored，migration/assets retained；一般secrets不log |
| PP-DB-01 | S0 | ProviderMigrationIsolation | SQLite新庫/upgrade，SQL Server獨立新驗證庫、舊EnsureCreated庫 | 各自assembly/snapshot；SQLite可Migrate；一般舊SQL不自動baseline/seed；不混provider migration |
| PP-DB-02 | S0 | SeedFaultInjection／ConcurrentStartup | 第N筆或最後SeedRecord保存前注入失敗、兩啟動 | seed全rollback無Completed；schema可保留；第二排他10秒退出或接手；非空lock檔但無handle不誤判永久鎖 |
| PP-DB-03 | S0 | RestartAndUnmarkedDatabase | restart、使用者增刪改、非空無標記、版本不相容 | counts不倍增、不補回、不覆寫；非空無標記停止；版本有明確升級／reset說明，無混資料 |
| PP-DB-04 | S0 | ResetContainmentNegative | 未輸入RESET、網站live、檔鎖、symlink／reparse、外部path | 不刪任何檔；有效停止Demo確認後只固定allowlist被刪，SQL／一般uploads不受影響；重新start恢復fixture |
| PP-RUN-04 | S0 | StartupFailureCategories | DB readonly、diskfull模擬、SQLite busy、schema mismatch、port occupied | 非零及適當繁中原因／trace；ready不200，無stack/secrets給user；不假網站可用 |
| PP-SEED-01 | S0 | DeterministicGenerator | seed20261013/base2026-10-01、兩次generate、未來base | 相同排序業務內容／fingerprint，hash salt不必同；未來base拒絕；版本保存，日常新增用現在UTC |
| PP-SEED-02 | S0 | SeedCountsAndRoles | 查全實體／各owner數量及固定角色 | Member120、Pet240、每組24、Post1200(1180visible)、Comment2400、Friend300(180/120)、Like每篇0–20；固定角色與所有邀請情境可操作 |
| PP-SEED-03 | S0 | SeedRealismAndBoundaryFixtures | 查template／assets/time／public posts | 繁中模板下限、非真人、無外網；零/20讚、無/30+留言必存在；每eligible作者有文；有效FK、comment≥post且≤base、圖片200 |
| PP-SEED-04 | S0 | SeedDatabaseAudit | commit後counts/anti-join/duplicate checks/hashverify | FK無孤兒、pair/like無重複／自加、hash可驗；啟動實際統計正確；initial與即時counts分開；S2/S3fixtures只加入一次 |
| PP-DATA-01 | S0–S4 | NormalizationAndLegacyMigration | case/space Email、新PublicId、collision舊SQL、無PublicId cookie | normalized uniqueness且密碼不trim；舊碰撞不自動合併；公開IDunique；FK完整；舊session重登入 |
| PP-DATA-02 | S1–S3 | SchemaIndexFkInspectionBothProviders | migration inspect、違法FK插入、雙Member關聯與query plan | 規格列索引/unique存在，真FK拒孤兒；無SQL cascade path問題；provider未跑明標NOT_RUN |
| PP-DATA-03 | S1 | DeletionWithRelations | 作者刪post有comment/like/report；admin恢復；刪pet | soft post/comment不公開；report/audit仍可處理；restore不解除作者deleted；pet僅owner刪、FK無錯；通知不可用不洩漏 |
| PP-DATA-04 | S1 | TwoContextConcurrencyAndBusy | stale Version、兩Ctx edit、SQLite busy、commit-response失聯 | 一更新成功另一409+保留draft；freshVersion；receipt/domain/audit/notification原子；busy重試上限後503、不雙寫 |

### 2.2 介面與會員、寵物、社群

| Requirement | 階段 | 建議測試／證據 | 輸入／觸發 | 必須觀察到的結果 |
|---|---|---|---|---|
| PP-UX-01 | S1 | HttpErrorContract | HTML invalid／JSON invalid／unknown／forbidden／busy | HTML保留欄位無DB變化；JSON各code/status/trace符合；成功PRG303；無private/stack且有返回入口 |
| PP-UX-02 | S1 | DuplicateSubmissionAndFeedback | 連點、失敗重試同CommandId、同key異payload、刪除取消 | 單筆/原結果；異payload409；按讚desired state不反轉；UI loading/empty/success/error可辨識；密碼不回填；未驗入口明示或隱藏 |
| PP-UX-03 | S1–S2 | PaginationAndReturnContext | page0/-1/non-number/過大、unknown sort、查詢%/_、刪尾頁 | 預設20、穩定Id排序、0/-1校正、invalid400/尾頁校正；literal查詢；query≤100；操作保留合法page/filter、無open redirect |
| PP-UX-04 | S1–S2 | ResponsiveKeyboardTime | 320/375/768/1440px、keyboard、missing image、跨UTC日 | 主頁無溢出、所有操作可keyboard/label/error焦點、aria狀態與modal返回；UTC→臺灣一致、生日不偏日、fallback有效 |
| PP-SEC-04 | S1–S3 | RateLimitBoundary | 指定window內上限與超1請求，member/IP分開 | 上限內正常、超額429/Retry-After；DB無副作用；next window恢復；不log原始Email/秘密，Hub/Upload各自限頻 |
| PP-MEM-01 | S1 | RegistrationLoginHttpSQLite | valid／invalid／duplicate／case Email、8/128邊界、unknown/wrong/disabled/legacy | hash only、拒role overpost，valid可登入、失敗同訊息無cookie；正規化相同；rehash持久化且非明文 |
| PP-MEM-02 | S1 | CookieLifecycle | GET/POST logout、idle30/absolute8h注入clock、stamp改、Prod HTTP | GET不登出、POST有效；逾時/revoke全舊session失效；HttpOnly/Lax、Prod Secure；Demo不能偽裝Prod放寬 |
| PP-MEM-03 | S2 | ProfilePrivacyEdit | self vs outsider/admin、unknown city、false opt-in、disabled | 正確公開欄位，無phone/email/DOB/gender/role；owner編輯/Version，非法city/長度拒絕；unknown不造生日；停權profile404 |
| PP-MEM-04 | S2 | PasswordChangeRevokesSessions | wrong current、mismatch、good new、兩browser舊cookie | 失敗不變hash；成功新hash/stamp且兩舊session失效；新密碼可登入；不回填secret/receipt存secret；legacy不自動轉換 |
| PP-PET-01 | S1 | PetCrudHttp | 新owner claim、overposted OwnerEmail、5th pet、invalid catalog、other delete | DTO有效新增；claim owner；第5隻允許；非法名稱/catalog/preset拒絕；編改刪僅owner+Version；空白CTA |
| PP-PET-02 | S2 | PublicPetFields | private/unknown gender/兩種age同給/future DOB/age600/601 | 僅public可見、owner可看private；未知明示；互斥/未來/上限拒；不洩owner contact；fallback有效 |
| PP-SOC-01 | S1 | FeedSqlProjectionAndDetails | 1200文/2400留言、hidden/deleted、相同CreatedAt | 每頁20正確順序、僅visible；SQL/JSON不含全likes/comments；詳情有分頁；一般404、admin後臺另外讀 |
| PP-SOC-02 | S1 | PostCrudAndOwnership | validTitle/body/preset、空白/100/101/4000/4001、連點、他人Version | DTO可發/改、author不可overpost、界線驗證；soft刪不刪report；sameCommand只有1、UpdatedAt、無role bypass |
| PP-SOC-03 | S1 | CommentCrudAndLimits | 長度0/500/501、hidden post、作者與post作者改留言 | 有效comment、author session、同時排序分頁；他人/post作者403，刪後不列；新留言通知非自我、編輯不重發（S2） |
| PP-SOC-04 | S1 | DesiredLikeTwoRequests | 兩獨立Ctx liked=true、重送false、隱藏目標 | unique至多1、same state不toggle；不存在/hidden/block拒絕；counts正確且保留位置；self like無自通知 |
| PP-SOC-05 | S2 | SearchVisibilityAndLiteral | 繁中keyword/%/_、city/author filters、mostliked tie、hidden | literal title/body查詢、無private search；stable tie、paging；hidden/block不穿透、空白可清filter |

### 2.3 好友、推薦、通知與封鎖

| Requirement | 階段 | 建議測試／證據 | 輸入／觸發 | 必須觀察到的結果 |
|---|---|---|---|---|
| PP-FRD-01 | S1 | FriendStateMatrix | None/Pending/Accepted，requester/receiver/stranger/self/disabled | 所有正文轉換，取消只requester/接受拒絕只receiver/移除任方；反向409不自動接受；列表三分頁正確 |
| PP-FRD-02 | S1 | ConcurrentFriendCommands | 反向同時request、accept vs reject/cancel、receipt replay | 唯一pair、不自加；一個版本成功其他409；同ID原結果；friend/notification atomic（S2） |
| PP-REC-01 | S1/S2 | RecommendationEligibility | 自己optout/0pet、candidate disabled/admin/optout/privatepet/block | 設定/新增CTA；沒有不合eligible候選；filters/20頁/空結果；公開DTO不漏private |
| PP-REC-02 | S1 | DeterministicScoring | 同城known/unknown、同品種/unknown、多寵、重跑/反pair | +30項各一次、0–19穩定加分、總0–79、ties一致；理由符合；不顯百分比/AI、selectedpet規則可重現 |
| PP-NOT-01 | S2 | RecipientNotificationViews | invitation/accepted/comment、自行留言、other recipient mark、target delete | 正確recipient/nonself、20頁/unread，onlyrecipient；單/allread冪等；target不可用有安全提示、不能洩漏 |
| PP-NOT-02 | S2 | NotificationAtomicDedup | 事件重試/transaction注入failure、pending取消、block | unique event每recipient1；失敗全部rollback；獨立comment新event；撤銷target、禁block摘要；不把commit當Email已寄 |
| PP-BLK-01 | S2 | BlockEffectsMatrix | A block B，pending/accepted/history/feed/search/profile，anonymous，unblock | 好友/邀請去除，登入雙方互動/推薦/內容排除，新send拒；匿名公開仍可讀且說明；unblock不恢復關系/通知、onlyblocker |

### 2.4 聊天、管理、媒體與正式準備

| Requirement | 階段 | 建議測試／證據 | 輸入／觸發 | 必須觀察到的結果 |
|---|---|---|---|---|
| PP-MSG-01 | S3 | ConversationAuthorization | friend/nonfriend/blocked/disabled/self、第三人/admin取history、50+訊息 | 僅有效accepted可send；歷史onlyparties/50cursor穩定；removed/block雙方可read-only歷史、inactive不可，admin無role越權 |
| PP-MSG-02 | S3 | MessageAckDedup | timeout重送sameClientId、異content、多裝置delivery、XSS text | DB1列/同ack/異content409；Sending→Sent/Failed，ack後才確定；messageId去重，無innerHTML執行、不冒充已讀 |
| PP-MSG-03 | S3 | ReconnectReadAndRevocation | 初連失敗/斷線漏事件/reconnect、sender markread、舊Hub後block | 本機JS無CDN、重連補缺不重複、read onlyreceiver且時間合理、舊hub拒新send、UI明確斷線 |
| PP-MOD-01 | S2 | ReportSubmissionAndVisibility | 自己/不存在/hiddenpost、Other空、Reason501、重複pending | 拒無效、有效分類/author/session；每reporter/post最多1pending；本人可看case狀態、不見他人原文 |
| PP-MOD-02 | S2 | ModerationStateAudit | Pending resolve/dismiss、no note、Version競爭、restore authordeleted | 狀態合法/HandledByAt、note/audit原子；終態409、receipt重放；hide/restore不解除authordelete；真實View存在可用 |
| PP-MOD-03 | S1/S2 | SetEnabledAndAudit | 同desiredstate重送、自己/最後admin停權、正常member停權 | 不toggle；reason要求、409保護、stamp撤銷；audit不可改且不含hash/privatepayload；不開放任意私訊 |
| PP-MEDIA-01 | S1 | PresetRoundtripAndNoUpload | guest presets、register/pet/post保存、../外部url/不存在圖 | 本機allowlist/有效保存/fallback；未驗收upload endpoint404與tab禁用；guest不用先登入選preset |
| PP-MEDIA-02 | S3 | ImageDecodeAndOwnership | 偽.jpg/SVG/polyglot/超5MiB/巨像素、不同owner reuse、fault save | MIME/decoder/reencode全驗，拒400/413；metadata剝除/randompath/非wwwroot；media讀寫權限/nosniff，無孤兒；維護只allowlist |
| PP-REL-01 | S4 | EmailTokenFlowAndDeliveryEvidence | verify24h/reset30min過期/重放/新token、帳號不存在、未verified寫入 | purpose/hash/單次正確；回應不枚舉；未verified限制、reset stamp撤銷；外部寄件須provider實際結果非job完成；Demo不寄 |
| PP-REL-02 | S4 | AccountDeletionAndReferences | recent password/明示確認、故障後重入、private欄位與retained history | private清除/Email key tombstone/FK完整、public內容刪除；其他party匿名歷史說明；media cleanup、audit匿名最少；不把disable當delete |
| PP-REL-03 | S4 | ProductionReleaseGate | Demo flag/publicbind、HTTP、丟secrets、SQL schema不符、multiinstance | 拒危險設定；HTTPS/key/共享撤銷與limits/leastprivilege；真實SQL full suite；ready/liveness安全；外部部署仍需授權 |
| PP-REL-04 | S4 | BackupRestoreDrill | SQLite含WAL／uploads、SQL full backup、獨立restore與失敗 | 新target可用與counts/業務hash匹配，失敗不覆蓋原庫；量測RPO24h/RTO60min；保留政策確認、無未授權自動清理 |
| PP-ARCH-01 | S1–S3 | LayerContractAndTransactionReview | code dependency／View DTO／DB transaction tracing | Web不背業務/DbContext不外漏、sharedvisibility一致、真實差異adapter；receipt/domain/notification/audit原子，不靠mock證明並發 |
| PP-QA-01 | 全部 | StageCiAndRegression | 實際default/工作PR觸發、unit+HTTP+SQLite+browser/SQL獨立 | CI正確觸發、meaningful tests及build；SQL缺環境標NOT_RUN；原始Framework與已有流程不退化 |
| PP-QA-02 | S0/S1/S3 | RecordedBenchmark | 同機Release、精確seed、20warmup+100requests、100chat | 記環境與raw data，startup<=60s(另記restore)/restart<=10s/list p95<=500ms、375px牆<=1MiB、chat ack p95<=1s；未達不假PASS/不減seed |
| PP-QA-03 | 全部 | ReadmeEvidenceAudit | 新人只按README、screenshot版本與實際功能比對 | SDK/首次網路/帳號/reset/SQL差異可理解；source fingerprint/結果證據完整，作者貢獻不虛構、未驗明確 |
| PP-DEMO-01 | S1–S3 | TimedBrowserWalkthrough | 固定owner/nopet/friend/admin/disabled，S1→S3逐階段 | 主展示3–5min可講完；真實counts，0寵CTA/停權失敗，所有關鍵入口實際可操作；未階段不宣傳已完 |

## 3. 階段 Gate 與案例執行順序

### S0：一指令啟動與豐富資料

1. 建立包含實際候選修改的全新隔離checkout與空Demo目錄，記source fingerprint。
2. 只安裝SDK、不提供SQL/Docker，從repo根與不同cwd各跑啟動入口，保存stdout/exit/ready結果。
3. 查SQLite counts、0–4分組、anti-join、duplicate pair/like、時間、hash與asset request；比對seed record。
4. 正常停止／重啟，確認DB業務fingerprint不改；使用者新增或刪除後再重啟，不恢復fixture。
5. 使用隔離temp DB做fault injection／concurrent init／nonempty-unmarked／read-only／busy，不用真使用者DB。
6. reset negative cases先驗證無改動，再stop+RESET，確認只allowlist，重新start驗seed。

通過條件：S0編號及PP-GOV-01、對應PP-QA完整。SQL Server尚未可驗時，示範SQLite階段可驗收，但SQL相容性保留NOT_RUN，不能在README稱兩者都通過。

### S1：主展示路線

1. HTTP cookie/anti/角色/session復驗與registration SQLite tests。
2. 寵物新增（含第5隻）、編輯／刪除、有效DTO與非法owner；社群create/edit/delete/comments/desiredlike。
3. 兩獨立context驗unique／Version／receipts；所有與hidden/deleted post的互動拒絕。
4. 好友完整state矩陣及反向併發，推薦eligibility與可重現分數。
5. 管理SetEnabled與stamp撤銷、audit、lastadmin protection。
6. 真瀏覽器guest→owner→friend→admin→disabled/nopet，mobile/keyboard/fallback/console；量測performance。

通過條件：所有S1規範，沒有採用未驗Upload/Chat作主功能入口；profile尚未完整時也不能洩漏私有欄位。

### S2：產品完整性

profile/privacy/password/pet age/search/notification/block/report處理；執行跨功能矩陣：blocked雙方登入內容查詢與私訊發起入口不可用、匿名公開限制有說明，hidden/deleted/report/notification相互一致。S2一次性fixture不覆寫S0資料。所有S1 regression保持。

### S3：聊天與媒體

兩個獨立browser session及第三人/admin，測50+history、斷線/reconnect、ack遺失重試、readack、friend移除/block之後舊Hub拒send；停網後本機SignalR仍可載入。再測圖片內容／像素／metadata／ownership及fault cleanup。禁止只看連線成功就稱聊天完成。

### S4：正式上線準備

先取得外部服務／部署授權及政策確認，使用獨立環境跑Email tokens與provider delivery、帳戶刪除可重入／retained匿名化、SQL Server完整 suite、HTTPS／key／多instance、backup restore實作drill。任何gate NOT_RUN則正式上線未完成；不得阻止已可用的local Demo被標示展示用途。

## 4. 測試組織與命令規劃

保留現有UnitTests與會員HTTP成果。新增SQLite integration使用temp file（每case獨立）、provider-specific SQL Server suite用明確隔離connection，browser suite按階段標記。實際項目名稱可由施工決定，但需求ID mapping必須留下，不以測試名稱當證據。

建議將需求ID加到xUnit Trait，例如 `Requirement=PP-FRD-02`，拆 case 可多Trait；各case應檢查DB前後與HTTP response，不只expected view。HTTP測試使用真實SQLite，mock僅用于clock／故障inject／非持久化純算法。SQL Server suite不能因connection缺失silent pass。

未來執行命令（本輪未跑）：

```powershell
dotnet restore PetPet.Modern/PetPet.Modern.sln
dotnet build PetPet.Modern/PetPet.Modern.sln --no-restore
dotnet test PetPet.Modern/PetPet.Modern.sln --no-restore
./start-demo.ps1
```

執行者需保存provider-specific命令及browser tool實際操作/截圖/console，不臨時啟動或清空未經授權的真SQL資料庫。benchmark用本規格固定資料量，raw samples不只平均值。

## 5. 證據記錄模板

```text
Requirement / Case:
Source HEAD + working-source fingerprint:
Execution time (UTC / Asia-Taipei):
Environment (OS/CPU/RAM/SDK/build/provider):
DB schema / seed / actual counts:
Preconditions and inputs:
Actual command/tool action:
Expected / actual HTTP + DB + UI:
Exit code / tests passed-failed-skipped:
Evidence file/log/screenshot:
Result: NOT_RUN | PASS | FAIL | BLOCKED | PARTIAL
Limitations / next action:
```

## 6. 本輪規格完成審查

本節只記錄**文件驗證**，不作產品驗收。

- 已讀取附件完整請求、現行分支/HEAD/status、tracked diff與untracked會員程式碼、舊草案、模型/DbContext/Controller/View/設定/CI及測試定義。
- 沒找到repository-local AGENTS/governance或CodeGraph；沒有用歷史草案覆蓋當前source。
- 已執行需求 ID 檢查：60 個唯一規範性需求、60 列矩陣；沒有重複定義、缺少或未知需求 ID。階段、seed 數、權限、刪除、block、通知與 chat 已人工交叉檢查；數值目標均聲明未量測。
- 已執行相對文件連結與 UTF-8 完整性檢查，兩文件互連可解析、無 replacement character；`git diff --check` 通過。提到的 placeholder 是禁止未完成按鈕的規則，沒有未填欄位或 TBD 決策空洞。
- 已比對本輪前後 137 個 PetPet.Modern 非 bin／obj 檔案的 SHA256，數量與內容全部一致；現有會員成果、設定與測試未修改。根目錄／原始 Framework／舊草案亦未編輯；本輪僅新增兩份指定規格文件。
- 本輪沒執行restore/build/test/瀏覽器/SQL、沒安裝依賴/開啟外部服務/修改產品/commit；任何產品測試狀態仍NOT_RUN。
- 上述 PASS 僅代表文件結構、覆蓋與保存現有檔案，不能當作 runtime 產品驗證。產品施工與實測留待規格檢視及相應授權後執行。
