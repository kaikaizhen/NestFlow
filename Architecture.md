# Architecture Overview

本文件是給人與 AI Agent 共用的架構速查表，目的是讓不熟悉本專案的人能在最短時間內判斷「某段程式碼應該放在哪裡」「某個功能由誰負責」。

- 專案：NestFlow — 以手機 PWA 與 LINE 為入口的個人／家庭管家（記帳、行事曆、提醒、待辦、儲藏庫）。
- 核心原則：**Thin Backend 主導、Dify 輔助**。後端握有全部資料、權限與執行控制權；Dify 只做自然語言解析。
- 相關文件：[docs/計畫.md](docs/計畫.md)（MVP 規格與模組順序）、[docs/三層式架構.md](docs/三層式架構.md)（後端分層與命名規則）、[docs/AGENTS.md](docs/AGENTS.md)（分支、文件規範）、[README.md](README.md)（安裝與啟動）。

## 1. Project Structure

本節說明目錄配置與**檔案落點規則**。新增檔案前請先對照 1.2 的規則表，由檔名即可判斷歸屬資料夾。

### 1.1 目錄樹

```
NestFlow/                                  # [Project Root]
├── NestFlow_Backend/                      # .NET 8 Solution（後端全部程式碼）
│   ├── NestFlow_Backend.sln
│   ├── NestFlow_Backend/                  # ASP.NET Core Web API（主要可執行專案）
│   │   ├── Controllers/                   # 表現層：API 端點，只做轉換與傳遞
│   │   ├── Services/                      # 服務層：商業邏輯，收發 DtoModel
│   │   │   └── External/                  # 對外系統用戶端（LINE Login／LINE Messaging／Dify）與其契約
│   │   ├── Repositories/                  # 資料存取層：EF Core CRUD，收發 Entity；含 IUnitOfWork
│   │   ├── Models/                        # 所有模型定義
│   │   │   ├── Entities/                  # 資料庫映射實體（PascalCase，對應 snake_case 資料表）
│   │   │   ├── Dtos/                      # 層與層之間傳遞用（含 SaveXxxCommand）
│   │   │   ├── ParamModels/               # 前端送入的參數（含驗證屬性）
│   │   │   └── ViewModels/                # 回傳給前端的輸出格式
│   │   ├── Profiles/                      # AutoMapper Profile，一個業務模組一個 Profile
│   │   ├── Helpers/                       # 無商業邏輯的工具（加解密、簽章驗證、固定格式解析）
│   │   ├── Common/                        # 全域靜態定義：Enums、常數、Options、例外、共用抽象
│   │   ├── Middlewares/                   # 全域例外處理、Session 認證
│   │   ├── Filters/                       # MVC Filter（RequireSessionAttribute）
│   │   ├── Data/                          # EF Core DbContext
│   │   │   ├── Configurations/            # IEntityTypeConfiguration：Entity ↔ 資料表對應
│   │   │   └── Migrations/                # EF Core Migration（一個模組一個 Migration）
│   │   ├── Properties/launchSettings.json
│   │   ├── appsettings.json               # 非機密預設值；機密值一律留空
│   │   ├── appsettings.Development.json.example  # 本機設定範本（實際檔不進版控）
│   │   └── Dockerfile
│   ├── NestFlow_Worker/                   # 提醒排程 Worker（BackgroundService）
│   │   ├── ReminderWorker.cs
│   │   ├── Program.cs                     # 只註冊派送提醒所需的 Repository／Service
│   │   └── Dockerfile
│   └── NestFlow_Backend.Tests/            # xUnit 測試（單元 + 整合）
│       ├── *Tests.cs                      # 測試類別，一個業務模組一個檔案
│       ├── Fakes/                         # 測試替身（FakeDifyClient、TestTimeProvider…）
│       └── Infrastructure/                # 測試共用基礎（NestFlowApiFactory、HttpClient 擴充）
├── NestFlow_Frontend/                     # Vue 3 + TypeScript + Vite PWA
│   ├── src/
│   │   ├── views/                         # 路由頁面；子資料夾對應底部導航分頁下的子頁
│   │   ├── components/                    # 可重用 UI 元件（icons/ 放 SVG 元件）
│   │   ├── stores/                        # 全域狀態（auth／workspace／theme），以 composable 實作
│   │   ├── services/                      # 後端 API 呼叫（apiClient.ts）
│   │   ├── composables/                   # 可重用邏輯（useAutoRefresh）
│   │   ├── utils/                         # 純函式工具（日期、分類、事件）
│   │   ├── styles/                        # 全域樣式與設計權杖（明亮／深色）
│   │   ├── router/                        # vue-router 路由與登入守衛
│   │   ├── App.vue / main.ts
│   ├── public/                            # 直接複製的靜態檔（PWA 圖示）
│   ├── nginx.conf                         # 容器內靜態檔服務設定
│   ├── vite.config.ts                     # Vite + PWA manifest 設定
│   └── Dockerfile
├── docs/                                  # 規格與規範文件
├── docker-compose.yml                     # 四個服務：sqlserver／api／worker／pwa
├── .env.example                           # Compose 機密值範本（.env 不進版控）
├── README.md                              # 專案簡介與快速開始
└── Architecture.md                        # 本文件
```

### 1.2 檔案落點規則（依檔名判斷）

後端（`NestFlow_Backend/NestFlow_Backend/`）：

| 檔名樣式 | 應放位置 | 說明 |
|:--|:--|:--|
| `XxxController.cs` | `Controllers/` | 只做 ParamModel → DtoModel → Service → ViewModel 的轉換與傳遞 |
| `XxxService.cs` / `IXxxService.cs` | `Services/` | 商業邏輯；跨 Repository 的流程控制 |
| `XxxClient.cs` / `IXxxClient.cs` | `Services/External/` | 呼叫外部系統（HTTP）的用戶端與其請求／回應契約 |
| `IAssistantProvider.cs` | `Services/External/` | 自然語言解析提供者抽象，屬對外整合契約 |
| `XxxRepository.cs` / `IXxxRepository.cs` | `Repositories/` | 只做資料 CRUD，收發 Entity |
| `XxxDtoModel.cs`、`XxxParamModels.cs`、`XxxViewModels.cs` | `Models/` 對應子資料夾 | 檔名後綴即決定子資料夾 |
| 無後綴的名詞（`User.cs`、`Todo.cs`） | `Models/Entities/` | 資料庫實體 |
| `XxxProfile.cs` | `Profiles/` | AutoMapper 對應 |
| `XxxHelper.cs`、`XxxParser.cs`、`XxxValidator.cs`、`XxxGenerator.cs`、`XxxMapper.cs` | `Helpers/` | 純函式工具，不得存取資料庫或呼叫 API |
| `XxxOptions.cs`、`Enums.cs`、`GlobalConstants.cs`、`AppException.cs`、`XxxCategories.cs` | `Common/` | 全域靜態定義與設定綁定類別 |
| `XxxMiddleware.cs` | `Middlewares/` | 進入 MVC 前的管線處理 |
| `XxxAttribute.cs`（Filter） | `Filters/` | MVC 授權／動作過濾器 |
| `XxxConfiguration.cs` | `Data/Configurations/` | `IEntityTypeConfiguration<T>` |
| `NestFlowDbContext.cs` | `Data/` | EF Core DbContext |

命名慣例：`CreateUserParamModel`、`UserViewModel`、`UserDtoModel`、`User`（Entity）、`UserService`、`UserRepository`、`IUserService`。詳見 [docs/三層式架構.md](docs/三層式架構.md)。

前端（`NestFlow_Frontend/src/`）：

| 檔名樣式 | 應放位置 |
|:--|:--|
| `XxxView.vue`（有對應路由） | `views/`；子頁放 `views/<父頁>/` |
| 其他 `.vue` | `components/`；純圖示放 `components/icons/` |
| 全域狀態 `useXxx` 且跨頁共用 | `stores/` |
| 可重用邏輯 `useXxx` 且不持有全域狀態 | `composables/` |
| 呼叫後端 API | `services/` |
| 純函式（無 Vue 相依） | `utils/` |

測試（`NestFlow_Backend.Tests/`）：`*Tests.cs` 放根目錄、測試替身放 `Fakes/`、共用測試基礎放 `Infrastructure/`；三者共用 `NestFlow_Backend.Tests` 命名空間，資料夾只作分組用途。

## 2. High-Level System Diagram

```text
[使用者]
   │
   ├── 手機瀏覽器／PWA ──────────► [NestFlow_Frontend (Vue 3 PWA / nginx)]
   │                                        │ REST + Session Cookie (credentials: include)
   │                                        ▼
   └── LINE App ──► LINE Platform ──► [NestFlow_Backend (ASP.NET Core Web API)]
                         ▲   Webhook       │  │  │
                         │                 │  │  └──► [Dify Workflow]  自然語言解析（固定 JSON）
                         │                 │  │           只在固定格式解析失敗時呼叫
                         │                 │  └──► [LINE Login / OIDC]  身分驗證與 JWKS
                         │                 ▼
                         │           [SQL Server]  單一資料庫，以 workspace_id 隔離
                         │                 ▲
                         └── Push ── [NestFlow_Worker (BackgroundService)]  掃描到期提醒
```

資料流重點：

1. PWA 所有請求都帶 Session Cookie，`workspace_id` 一律由後端重新驗證 Membership。
2. LINE 訊息先驗證 Signature → 事件冪等檢查 → 固定格式解析；未命中才呼叫 Dify。
3. Dify 回傳的 JSON 一律視為不可信，經 `DifyResultMapper` 轉為既有 `FixedCommand` 後才進入既有流程。
4. 自然語言寫入前必須建立 Pending Action，由使用者於 LINE 回覆「確認」後才真正寫入。
5. Worker 與 API 共用同一份 Entity、DbContext 與 LINE 用戶端（專案參考），不重複實作提醒邏輯。

## 3. Core Components

### 3.1. Frontend

- **Name**：NestFlow PWA（`NestFlow_Frontend`）
- **Description**：手機優先的單頁應用，底部導航四個分頁 — 行事曆／生活（待辦・購物・儲藏庫）／記帳／設定。負責表單輸入、列表瀏覽與設定管理；**不呼叫 Dify**，所有寫入都走後端 REST API。支援明亮／深色模式（預設跟隨系統，可於設定頁覆蓋並記住）與 PWA 安裝。
- **Technologies**：Vue 3（`<script setup>`）、TypeScript、Vite、vue-router、vite-plugin-pwa（Workbox autoUpdate）。狀態管理未使用 Pinia，改以 `src/stores/` 下的 composable 自行封裝。
- **Deployment**：多階段 Docker build，產出靜態檔由 nginx 提供；`VITE_API_BASE_URL` 與 `VITE_BASE_PATH` 於建置時注入，支援反向代理子路徑部署。

### 3.2. Backend Services

#### 3.2.1. NestFlow API

- **Name**：NestFlow_Backend（ASP.NET Core Web API）
- **Description**：Thin Backend 主體，採 Controller / Service / Repository 三層式架構。負責 LINE Login、Server-side Session、Workspace 與家庭成員機制、記帳、行事曆、提醒、待辦與儲藏庫的 CRUD，以及 LINE Webhook 的固定格式解析、Pending Action 與 Dify 後援。
- **主要端點**：`/api/auth`、`/api/workspaces`、`/api/account-entries`、`/api/calendar-events`、`/api/reminders`、`/api/todos`、`/api/storage-items`、`/api/webhooks/line`、`/health/live`、`/health/ready`。`/api/dev/*`（DevAuth／DevLine／DevReminder）僅在 Development 環境生效，供尚未取得 LINE Channel 時測試。
- **Technologies**：.NET 8、EF Core 8（SQL Server）、AutoMapper、Swashbuckle、`Microsoft.IdentityModel.Protocols.OpenIdConnect`（ID Token 驗證與 JWKS 快取）、`IHttpClientFactory`。
- **Deployment**：Docker 容器 `nestflow-api`，對外 `${API_PORT:-8080}`；Development 環境啟動時自動套用 Migration。

#### 3.2.2. NestFlow Reminder Worker

- **Name**：NestFlow_Worker（.NET Worker Service）
- **Description**：`BackgroundService` 定期掃描到期提醒，透過 LINE Messaging API 推播；具備失敗重試與重複發送防護（提醒狀態機）。以專案參考共用 API 的 Entity、Repository 與 `LineMessagingClient`。
- **Technologies**：.NET 8 Generic Host、EF Core 8、`ILineMessagingClient`。
- **Deployment**：Docker 容器 `nestflow-worker`，不對外開埠，與 API 使用相同的設定鍵名（由環境變數注入）。

## 4. Data Stores

### 4.1. 主資料庫

- **Name**：NestFlow 主資料庫
- **Type**：SQL Server 2022（容器 `nestflow-sqlserver`，資料存於具名磁碟區 `nestflow-mssql-data`）
- **Purpose**：保存全部業務資料與身分資料。單一資料庫、單一 Schema，以每筆資料的 `workspace_id` 做租戶隔離。
- **Key Tables**：`users`、`sessions`、`external_identities`、`workspaces`、`workspace_memberships`、`workspace_invitations`、`account_entries`、`calendar_events`、`reminders`、`todos`、`storage_items`、`pending_actions`、`processed_events`。
- **慣例**：資料表與欄位一律 snake_case，C# Entity 為 PascalCase，兩者由 `IEntityTypeConfiguration` 明確對應；建表一律透過 EF Core Migration，Migration 以模組命名（`Module3_AccountEntries` 等）。

### 4.2. 快取

- **Name**：處理程序內記憶體快取
- **Type**：`IMemoryCache`（無 Redis）
- **Purpose**：快取 LINE OIDC 設定／JWKS 等短期資料。第一版不使用外部快取或訊息佇列；提醒排程由 Worker 直接輪詢資料庫，不引入 MQ。

## 5. External Integrations / APIs

| 服務 | 用途 | 整合方式 | 缺少設定時的行為 |
|:--|:--|:--|:--|
| LINE Login（OIDC） | 唯一登入方式；`state`／`nonce`／ID Token 驗證後建立本地 User | REST + OpenID Connect Metadata／JWKS | `/api/auth/line/login` 回傳提示；可改用 `/api/dev/auth` |
| LINE Messaging API | 接收使用者訊息（Webhook）與推播提醒（Push） | REST + Channel Access Token；請求以 Channel Secret 驗簽 | Webhook 停用，可用 `/api/dev/line` 模擬 |
| Dify Workflow | 固定格式解析不到時的自然語言後援，輸出固定 JSON | REST（`Dify:BaseUrl` + `Dify:AppKey`，逾時 15 秒） | 直接回覆既有用法提示，固定格式不受影響 |
| Discord | 規劃中的最小整合（Module 14），尚未實作 | — | — |

Dify 的邊界：不得回傳或決定 `user_id`／`workspace_id`、不得存取資料庫、不得執行 SQL／URL／Script、不得決定權限。低信心、未知 Intent、格式錯誤的輸出一律丟棄。

## 6. Deployment & Infrastructure

- **Cloud Provider**：無雲端相依，設計為自架（開發機或 NAS）。
- **Key Services Used**：Docker Compose 四服務 — `sqlserver`、`api`、`worker`、`pwa`。`api` 依賴 `sqlserver` healthcheck、`pwa` 依賴 `api` healthcheck。
- **設定策略**：同一份 `docker-compose.yml` 支援「直接執行」與「反向代理後方」兩種情境，差異只靠 `.env` 覆蓋（`CORS_ORIGIN_*`、`SESSION_REQUIRE_HTTPS`、`FORWARDED_HEADERS_ENABLED`、`VITE_BASE_PATH`、`VITE_API_BASE_URL`）。機密值只存在 `.env` 或環境變數，不寫入 compose 檔與映像檔。
- **CI/CD Pipeline**：尚未建立（無 `.github/workflows`），目前以本機 `docker compose build` 部署。
- **Monitoring & Logging**：ASP.NET Core 內建 `ILogger` 輸出至容器 stdout；健康檢查 `/health/live`（行程存活）與 `/health/ready`（資料庫就緒），回應只輸出狀態名稱以避免洩漏連線資訊。

## 7. Security Considerations

- **Authentication**：僅 LINE Login（OAuth2／OIDC），驗證 `state`、`nonce` 與 ID Token 簽章。登入後建立 Server-side Session，以 HttpOnly、SameSite=Lax、可設定 Secure 的 Cookie（`nestflow_session`）傳遞；資料庫只保存 `token_hash`。
- **Authorization**：以 Workspace Membership 為授權邊界。`user_id` 只由 Session 解析（`ICurrentUserAccessor`），永不接受前端傳入；前端傳入的 `workspace_id` 一律重新驗證。非成員存取一律回傳 **404** 而非 403，避免洩漏資源是否存在。`RequireSessionAttribute` 標記需登入的端點。
- **Data Encryption**：高敏感欄位以 AES-256-GCM 加密後入庫（LINE ID Token／Access Token、`external_identities.external_subject`）；`external_subject_hash` 作為可查詢的確定性鍵。邀請碼與 Session Token 只存雜湊。一般業務欄位（金額、分類、備註、行程標題）不加密以保留查詢與彙總能力。主金鑰由 `IOptions<EncryptionOptions>` 注入並在啟動時驗證，不進版控、不入映像檔。
- **其他實務**：LINE Webhook 強制簽章驗證；以 `processed_events`（`provider + external_event_id` 唯一鍵）做事件冪等；自然語言寫入必須經 Pending Action 二次確認；CORS 僅允許設定檔列出的來源並允許憑證；全域 `ExceptionHandlingMiddleware` 將 `AppException` 轉為對應狀態碼，不外洩堆疊。

## 8. Development & Testing Environment

- **Local Setup**：見 [README.md](README.md)。最短路徑為填好 `.env` 後 `docker compose up -d --build`；純後端開發則複製 `appsettings.Development.json.example` 為 `appsettings.Development.json`。
- **Testing Frameworks**：後端 xUnit + `Microsoft.AspNetCore.Mvc.Testing`（`NestFlowApiFactory` 以 SQLite 取代 SQL Server 跑整合測試）、`coverlet.collector`。前端目前無自動化測試。
- **測試原則**：每模組一個成功單元測試、一個失敗單元測試、一個核心整合測試；權限隔離、邀請碼失效、簽章錯誤、事件冪等、Dify 非法輸出、提醒不重複發送等情境不可省略（見 [docs/計畫.md](docs/計畫.md) 第 14 節）。
- **Code Quality Tools**：`Nullable` 與 `ImplicitUsings` 全專案啟用；前端以 `vue-tsc` 於 build 時型別檢查。尚未導入 ESLint／Prettier／分析器規則。
- **分支規範**：`main` ← `develop` ← `feature/xxx`、`bug/xxx`，命名一律小寫並以 `-` 分隔（見 [docs/gitFlow規範.md](docs/gitFlow規範.md)）。

## 9. Future Considerations / Roadmap

- Module 14：Discord 最小整合（Binding、`/expense`、`/today`、提醒通知），共用既有記帳與提醒邏輯。
- Module 15：MVP 封版（錯誤訊息整理、PWA 安裝、日誌遮罩、Dify Workflow 匯出、備份說明）。
- 已知架構債：
  - `IAssistantProvider` 為 Module 1 留下的抽象骨架，實際 Dify 串接改以 `IDifyClient` + `DifyResultMapper` 完成，兩者尚未收斂。
  - `FixedCommand`／`ParsedEvent` 等解析結果模型定義於 `Helpers/IFixedFormatParser.cs`，未依規範集中於 `Models/`。
  - `Helpers/` 內 `CodeGenerator`、`FixedFormatParser`、`LineSignatureValidator`、`DifyResultMapper` 未採 `XxxHelper` 後綴。
  - 前端無自動化測試，專案亦無 CI（`.github/workflows`）與部署腳本（`scripts/`）。
  - `NestFlow_Frontend/README.md` 仍為 Vite 範本內容。
- 擴充空間（本階段不設計）：Dify 詢問物品位置、重複代辦與成員指派、細粒度權限、每 Workspace 獨立資料庫、KMS、Google Calendar、銀行串接。

## 10. Project Identification

- **Project Name**：NestFlow
- **Repository URL**：git@github.com:kaikaizhen/NestFlow.git
- **Primary Contact/Team**：Kash（KaiKaiZhen）
- **Date of Last Update**：2026-08-05

## 11. Glossary / Acronyms

- **Thin Backend**：後端保有全部資料、權限與執行控制權，AI 只負責解析的架構原則。
- **Workspace**：資料隔離單位，型別為 `personal` 或 `family`；每筆業務資料都帶 `workspace_id`。
- **Membership**：使用者與 Workspace 的關聯，第一版只有 `owner` 與 `member` 兩種。
- **Pending Action**：自然語言解析後尚待使用者於 LINE 確認的暫存動作，確認後才真正寫入。
- **固定格式（Fixed Format）**：如「記帳 午餐 120」「確認」「取消」等免呼叫 Dify 的指令，優先於自然語言解析。
- **DtoModel / ParamModel / ViewModel / Entity**：分別為層間傳遞模型／前端傳入參數／回傳輸出格式／資料庫映射實體。
- **Idempotency（冪等）**：同一外部事件（LINE Webhook）重送時不重複處理，以 `processed_events` 保證。
- **PWA**：Progressive Web App，可安裝於手機主畫面並離線快取靜態資源的網頁應用。
- **Dify**：外部 LLM Workflow 平台，本專案僅用於自然語言解析並輸出固定 JSON。
