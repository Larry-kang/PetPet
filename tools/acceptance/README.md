# PetPet.Modern 驗收與性能評測工具 (Acceptance & Benchmark Runner)

本目錄包含 PetPet.Modern 候選版本的真實瀏覽器端到端驗收（End-to-End Acceptance）與性能評測腳本。

## 環境需求

1. **.NET 8 SDK** (8.0.x)
2. **Node.js** (v18+ 或 v20+)
3. **Playwright Chromium**

---

## 執行步驟說明

### 步驟 1：建置 Runner 所需之 Release 成品

Runner 啟動獨立展示站點時，直接載入 `.artifacts_build/bin/PetPet.Web/release/PetPet.Web.dll`。請先於專案根目錄執行：

```powershell
# 於 Repository 根目錄執行建置
dotnet build PetPet.Modern/PetPet.Modern.sln -c Release -p:ArtifactsPath=./.artifacts_build
```

### 步驟 2：使用 Lockfile 安裝鎖定依賴

切換至 `tools/acceptance` 目錄，使用 `package-lock.json` 安裝精確版本之依賴（避免依賴漂移或外部 fallback）：

```powershell
cd tools/acceptance
npm ci
```

### 步驟 3：安裝 Playwright Chromium 瀏覽器

確保本機已安裝 Playwright 所需之 Chromium 核心：

```powershell
npx playwright install chromium
```

### 步驟 4：執行真實瀏覽器驗收與性能評測

```powershell
# 使用預設參數執行 (Port 5214)
node run-acceptance.mjs

# 或明確指定自訂 Run ID 與 Port
node run-acceptance.mjs --run-id 20261017-delivery-02 --port 5214
```

---

## 命令列參數說明

| 參數 | 預設值 | 說明 |
| :--- | :--- | :--- |
| `--run-id <ID>` | `20261016-delivery-01` | 指定本次驗收運行識別碼；成果檔案將寫入 `docs/reviews/results/<ID>/`。 |
| `--port <PORT>` | `5214` | 指定獨立 Kestrel 程序監聽之本機 Loopback 連接埠（**請勿使用 5213** 以免與使用者進行中之展示伺服器衝突）。 |

*(註：驗收腳本執行完畢後會自動優雅終止所啟動之 Kestrel 程序，釋放連接埠。)*

---

## 驗收核心測試情境

1. **G1: 統一聊天限頻與 ACK 量測**
   - HTTP `/Chat/Send` 與 SignalR Hub 共用 60/min 配額池，超額回傳 429 Retry-After。
   - 瀏覽器 SignalR ACK 往返延遲量測（關聯 ClientMessageId，間隔 1,250ms 連續 100 筆樣本）。
2. **G2: 真實斷線重連與並發交錯**
   - 雙 Context 停留聊天室，透過 Chrome DevTools Protocol (CDP) 模擬斷線與網路恢復。
   - 監控真實 `onreconnecting` 與 `onreconnected` 事件，證明離線訊息於離線期間絕不出現在接收方 DOM，連線恢復後於同頁自動補齊且恰好出現一次，無任何網頁重載 (reload) 或跳轉 (goto)。
   - 舊連線安全撤銷：改密碼或停權後，原連線無法繼續傳訊或標讀。
   - 封鎖與傳訊競爭測試（兩獨立 DB 連線與 Barrier 同步點）。
3. **G3: 嚴格 Assert 與 Fail-Closed 機制**
   - 包含腳本自檢，遇元素缺失、圖片載入失敗 (`naturalWidth === 0`)、水平溢出 (`scrollWidth > clientWidth`) 或非預期錯誤立即非零退出。
   - 行動端 375px 冷載入動態牆 `/Post/Index`（記錄 Transfer Size 與 Decoded Body Size，驗證零溢出）。
4. **G4: 候選版本凍結**
   - 產出 SHA-256 Manifest、產物 Hash、Playwright Trace (`trace.zip`) 與高解析度截圖 (`screenshots/`)。
