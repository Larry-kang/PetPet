# 🐾 PetPet Evolution: Legacy to Modern Architecture

> **From Monolith to Clean Architecture**: A demonstration of refactoring a legacy ASP.NET MVC application into a modern, event-driven .NET 8 solution using AI-augmented engineering.
>
> **從單體到現代化架構**：展示如何利用 AI 輔助與架構思維，將 2019 年的職訓局專案重構為符合現代標準的 .NET 8 微服務前導架構。

---

## 📖 專案背景 (The Refactoring Story)

這個 Repository 包含了我職涯兩個重要階段的代碼，展示了從「功能實作為主」到「系統設計為主」的思維轉變：

| 📂 資料夾 (Folder) | 📅 年份 (Year) | 🏗️ 架構 (Architecture) | 🛠️ 技術棧 (Tech Stack) | 描述 (Description) |
| :--- | :--- | :--- | :--- | :--- |
| **[`/PetPet`](./PetPet)** | **2019** | **Legacy Monolith** | .NET Framework 4.7, MVC 5, MSSQL (EDMX) | **(職訓局時期)** 典型的義大利麵式代碼 (Spaghetti Code)，邏輯高度耦合於 Controller，無單元測試。 |
| **[`/PetPet.Modern`](./PetPet.Modern)** | **2025** | **Modular Monolith** | .NET 8, Clean Architecture, Docker, RabbitMQ | **(架構師時期)** 運用 DDD 思維重構。導入依賴注入 (DI)、事件驅動 (Event-Driven) 與自動化測試。 |

---

## 🚀 PetPet.Modern (.NET 8 Event-Driven Core)

這是不僅是一個寵物配對平台，更是一個 **「還技術債」** 的實戰演練。重點展示如何解決高耦合問題並導入現代化工程實踐。

### 🌟 核心架構亮點 (Architecture Highlights)

* **Clean Architecture (整潔架構)**: 嚴格遵循 `Domain` -> `Application` -> `Infrastructure` -> `Web` 的依賴反轉原則。
* **Event-Driven (事件驅動)**: 使用 **RabbitMQ** + **MassTransit** 解耦核心業務。例如：「配對成功」後，透過 Event Bus 非同步觸發系統通知，避免阻塞主執行緒。
* **Modern Database**: 從舊版 EDMX 遷移至 **Entity Framework Core (Code First)**，並使用 **Dockerized SQL Server 2022**。
* **AI-Augmented**: 整合 AI 智能伴侶 (Auto-Reply) 於聊天室功能中。

### 🏗️ 系統架構圖 (System Diagram)

```mermaid
graph TD
    User[用戶 User] -->|HTTP/SignalR| Web[PetPet.Web (.NET 8)]
    Web -->|Read/Write| DB[(SQL Server)]
    
    subgraph "Legacy Refactoring"
        Old[.NET 4.7 MVC] -.->|Replaced by| Web
    end

    subgraph "Event-Driven Layer"
        Web -->|Publish MatchSuccess| MQ[RabbitMQ]
        MQ -->|Consume| Worker[NotificationConsumer]
        Worker -->|Write System Msg| DB
    end

```

### ✨ 業務功能 (Features)

1. **Tinder-style Matching**: 實作雙向喜歡 (Double Opt-in) 配對邏輯，運用 Redis (Optional) 優化高併發滑動判定。
2. **Ziwei Matching (紫微斗數)**: 結合東方命理演算法的趣味配對機制 (保留自舊版並優化演算法)。
3. **Real-time Chat**: 基於 SignalR 的即時聊天室。
4. **RBAC Security**: 區分 `Admin` (後台數據看板) 與 `User` 權限，實作 JWT/Cookie 混合驗證。

---

## 🛠️ 快速開始 (Quick Start for Modern Version)

本專案支援 **Docker Compose** 一鍵啟動，無需安裝本地 SQL Server 或 RabbitMQ。

### 前置需求

* Docker Desktop
* .NET 8 SDK (Optional, for local development)

### 🚀 一鍵啟動

```powershell
# 進入現代化專案目錄
cd PetPet.Modern

# 啟動容器 (Web + DB + MQ)
docker-compose up -d --build

```

### 🔐 測試帳號 (Default Accounts)

系統啟動時會自動 Seed 測試資料：

| 角色 | Email | Password | 備註 |
| --- | --- | --- | --- |
| **Admin** | `admin@petpet.com` | `admin` | 可存取後台 Dashboard |
| **User** | `alice@test.com` | `password` | 模擬用戶 A |
| **User** | `bob@test.com` | `password` | 模擬用戶 B |

---

## 📂 目錄結構說明 (Project Structure)

```text
PetPet.Modern/
├── src/
│   ├── PetPet.Domain/          # 核心實體, Value Objects, Domain Events (無依賴)
│   ├── PetPet.Application/     # 業務邏輯, DTOs, Interfaces (依賴 Domain)
│   ├── PetPet.Infrastructure/  # EF Core, Repository 實作, 外部服務 (依賴 Application)
│   └── PetPet.Web/             # API & MVC Controllers, Views
├── tests/                      # xUnit 單元測試
└── docker-compose.yml          # 容器編排配置

```

---

*Maintained by Larry Kang - Focused on High Concurrency & System Reliability*

```
