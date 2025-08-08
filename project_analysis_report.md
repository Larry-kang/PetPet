# 專案分析報告：PetPet

這份報告旨在分析 `PetPet` 專案的結構、技術和功能。

## 1. 總覽

`PetPet` 是一個使用 ASP.NET MVC 框架開發的 Web 應用程式。從其功能模組（如會員、寵物、貼文、配對、聊天）來看，它似乎是一個以寵物為主題的社群平台，旨在讓寵物主人可以為他們的寵物建立檔案、分享貼文並進行互動。專案還包含一個後台管理系統（代號似乎是 "QueenTyphoon"）。

## 2. 技術棧 (Technology Stack)

*   **後端框架**: ASP.NET MVC 5
*   **語言**: C#
*   **.NET 版本**: .NET Framework 4.6.1
*   **資料存取**: Entity Framework 6 (使用 EDMX，推斷為 Model-First 或 Database-First 的開發模式)
*   **資料庫**: SQL Server LocalDB (檔案型資料庫 `petpet.mdf`)
*   **前端**:
    *   **樣板引擎**: Razor (`.cshtml` 檔案)
    *   **CSS 框架**: Bootstrap
    *   **JavaScript**: jQuery
*   **相依性管理**: NuGet (`packages.config`)

## 3. 專案結構

專案遵循標準的 ASP.NET MVC 結構：

*   `PetPet.sln`: Visual Studio 解決方案檔案。
*   `PetPet/`: Web 應用程式的根目錄。
    *   `App_Data/`: 存放應用程式資料，包括本地資料庫檔案 `petpet.mdf`。
    *   `App_Start/`: 包含應用程式啟動時的設定檔，如路由 (`RouteConfig.cs`) 和資源打包 (`BundleConfig.cs`)。
    *   `Controllers/`: 存放控制器 (Controller)，處理使用者請求和業務邏輯。
    *   `Models/`: 存放模型 (Model)，包括 `petpet.edmx` Entity Framework 資料模型，以及由其自動生成的 C# 類別。
    *   `Views/`: 存放視圖 (View)，即 `.cshtml` 檔案，用於生成 HTML。
    *   `Scripts/`: 存放前端 JavaScript 檔案。
    *   `Content/`: 存放 CSS 檔案。
    *   `Web.config`: 應用程式的主要設定檔，包含資料庫連接字串等重要資訊。

## 4. 核心功能

根據控制器檔案的命名，可以推斷出以下核心功能：

*   **會員系統**: 註冊 (`REGController`)、登入 (`LoginController`)、會員資料管理 (`MemberController`)。
*   **寵物管理**: 使用者可以新增和管理自己的寵物資料 (`MemberController` 中的 `MyPet`, `PetCreate`, `PetEdit` 等動作)。
*   **貼文系統**: 建立和瀏覽貼文 (`PostController`)。
*   **社群互動**:
    *   **配對系統**: `MatchController` 暗示有一個配對機制。
    *   **好友系統**: `FriendController` 用於管理好友關係。
    *   **私信/郵件**: `MailController` 和 `MessageController` 提供使用者間的通訊功能。
    *   **按讚**: `LikeController` 提供按讚功能。
*   **新聞/公告**: `NewsController` 用於發布和顯示最新消息。
*   **後台管理**:
    *   `AdminController` 和 `QueenTyphoonController` 提供了管理功能。
    *   管理員可以管理檢舉 (`ReportViewsController`)、使用者、寵物種類 (`PetVarietyController`) 等。
    *   `BgStatisticalAnalysisController` 提供後台統計分析功能。

## 5. 如何建置與執行

1.  **環境**: 需要安裝 Visual Studio 並支援 .NET Framework 4.6.1 開發。同時需要安裝 SQL Server LocalDB。
2.  **開啟專案**: 使用 Visual Studio 開啟 `PetPet.sln` 檔案。
3.  **還原套件**: Visual Studio 應該會自動透過 NuGet 還原 `packages.config` 中定義的所有相依套件。
4.  **執行**: 直接在 Visual Studio 中按下 "F5" 或點擊 "執行" 按鈕。專案已設定使用 IIS Express 作為開發伺服器。資料庫檔案 `petpet.mdf` 會自動附加到 LocalDB 實例上。

## 6. 資料庫

*   **類型**: SQL Server LocalDB
*   **連接方式**: 透過 `Web.config` 中的 `petpetEntities` 連接字串進行設定。
*   **檔案位置**: `PetPet/App_Data/petpet.mdf`
*   **Schema 管理**: 資料庫的 Schema (結構) 是透過 `Models/petpet.edmx` 檔案進行視覺化管理和定義的。所有資料表對應的 C# 類別都由 T4 範本 (`.tt` 檔案) 自動產生。
