# PetPet.Modern 🐾

PetPet 寵物社群平台的現代化版本，採用 **.NET 8** 與 **Clean Architecture** 重寫，並導入 **Glassmorphism** 極簡設計風格。

## ✨ 核心功能

*   **社群互動**: 發文、按讚 ❤️、留言 �。
*   **會員中心**: 註冊、登入、寵物管理 🐶。
*   **安全機制**: 檢舉違規貼文 ⚠️、Cookie 驗證。
*   **資訊公告**: 最新消息與系統通知 📰。

## �🚀 快速開始 (Quick Start)

### 先決條件
*   Windows 作業系統
*   .NET 8 SDK
*   LocalDB (隨 Visual Studio 或 SQL Server Express 附帶)

### 一鍵啟動
直接雙擊專案根目錄下的 **`run_local.bat`** 即可。

```cmd
.\run_local.bat
```

腳本將會：
1.  自動還原 NuGet 套件。
2.  編譯專案。
3.  自動建立資料庫 (`PetPetModern_Local`) 並寫入種子資料。
4.  啟動網頁伺服器 (https://localhost:7123)。

## 🏗️ 專案架構 (Clean Architecture)

*   **src/PetPet.Domain**: 核心實體 (`Member`, `Post`, `Pet`, `News`, `Report`)。無外部依賴。
*   **src/PetPet.Infrastructure**: 資料庫存取 (`PetPetDbContext`) 與 migrations。
*   **src/PetPet.Web**: MVC 控制器、Views (`Razor`)、前端資源 (`modern.css`)。
*   **tests/PetPet.UnitTests**: xUnit 單元測試專案。

## 🧪 執行測試

若要執行單元測試以驗證系統邏輯：

```cmd
dotnet test PetPet.Modern/tests/PetPet.UnitTests/PetPet.UnitTests.csproj
```

## � 開發者筆記
*   本專案使用 `LocalDB` 作為開發資料庫。
*   預設測試帳號: 系統啟動後請自行註冊新帳號即可使用。
*   設計風格定義於 `wwwroot/css/modern.css`。

---
*Modernized by Antigravity Agent Swarm* 🤖
