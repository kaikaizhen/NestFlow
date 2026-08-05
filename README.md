# NestFlow

> 以手機 PWA 與 LINE 為入口的個人與家庭生活管理工具，整合記帳、行事曆、提醒、待辦、購物清單與儲藏庫。

## 專案介紹

NestFlow 用於集中管理個人與家庭的日常資訊。使用者可透過手機瀏覽器安裝 PWA，也可使用 LINE 輸入固定格式或自然語言指令，快速新增記帳與生活事項。

系統支援個人與家庭 Workspace，可依成員管理資料存取範圍。即使 AI 自然語言服務暫時無法使用，PWA 與固定格式指令仍可正常操作。

## 主要功能

| 模組        | 功能                                |
| :-------- | :-------------------------------- |
| 登入        | 使用 LINE 帳號登入                      |
| Workspace | 建立與切換個人或家庭空間、邀請成員及管理權限            |
| 記帳        | 新增與管理收支、查看月收支、結餘及最近交易             |
| 行事曆       | 建立行程、重複行程與行程提醒                    |
| 待辦        | 管理個人與家庭待辦事項                       |
| 購物清單      | 建立購物項目並記錄數量                       |
| 儲藏庫       | 記錄物品名稱、數量及存放位置                    |
| 提醒        | 透過 LINE 接收行程與待辦提醒                 |
| 設定        | 深色模式、LINE 綁定、預設 Workspace、家庭成員與時區 |

介面設計稿請參考 [docs/UI設計.png](docs/UI設計.png)。

## 安裝需求

安裝前請先準備：

* Docker
* Docker Compose
* 可供 Docker 使用的至少 4 GB 記憶體
* 可用的連接埠：

  * `5173`：NestFlow PWA
  * `8080`：NestFlow 服務
  * `1433`：SQL Server

LINE 與 Dify 設定為選用項目。未設定時，部分 LINE 或自然語言功能將無法使用，但 PWA 仍可啟動。

## 快速安裝

### 1. 建立環境設定檔

Linux、macOS 或 Git Bash：

```bash
cp .env.example .env
```

Windows PowerShell：

```powershell
Copy-Item .env.example .env
```

開啟 `.env`，至少設定：

```dotenv
MSSQL_SA_PASSWORD=請設定高強度密碼
ENCRYPTION_KEY=請填入Base64格式金鑰
```

SQL Server 密碼應包含：

* 大寫英文
* 小寫英文
* 數字
* 特殊符號

### 2. 產生加密金鑰

使用 PowerShell 產生 AES-256 加密金鑰：

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

將輸出結果填入 `.env` 的 `ENCRYPTION_KEY`。

### 3. 啟動 NestFlow

在專案根目錄執行：

```bash
docker compose up -d --build
```

首次啟動需要下載映像檔並建立資料庫，所需時間取決於網路與電腦效能。

### 4. 確認服務狀態

```bash
docker compose ps
```

所有服務應顯示為執行中或健康狀態。

## 使用 NestFlow

啟動完成後，使用瀏覽器開啟：

* NestFlow PWA：http://localhost:5173

在支援 PWA 的瀏覽器中，可將 NestFlow 安裝至手機或電腦主畫面。

## 選用設定

### LINE Login

使用 LINE 登入時，請在 `.env` 設定：

```dotenv
LINE_CHANNEL_ID=
LINE_CHANNEL_SECRET=
```

### LINE Messaging

使用 LINE 訊息與提醒功能時，請設定：

```dotenv
LINE_MESSAGING_CHANNEL_ID=
LINE_MESSAGING_CHANNEL_SECRET=
LINE_MESSAGING_CHANNEL_ACCESS_TOKEN=
```

### Dify

使用自然語言解析功能時，請設定：

```dotenv
DIFY_BASE_URL=
DIFY_APP_KEY=
```

未設定 Dify 時，固定格式指令及 PWA 功能仍可使用。

## 常用設定

| 變數                                    |  必填 | 說明                                  |
| :------------------------------------ | :-: | :---------------------------------- |
| `MSSQL_SA_PASSWORD`                   |  是  | SQL Server 管理員密碼                    |
| `ENCRYPTION_KEY`                      |  是  | Base64 格式的 AES-256 加密金鑰             |
| `LINE_CHANNEL_ID`                     |  否  | LINE Login Channel ID               |
| `LINE_CHANNEL_SECRET`                 |  否  | LINE Login Channel Secret           |
| `LINE_MESSAGING_CHANNEL_ID`           |  否  | LINE Messaging Channel ID           |
| `LINE_MESSAGING_CHANNEL_SECRET`       |  否  | LINE Messaging Channel Secret       |
| `LINE_MESSAGING_CHANNEL_ACCESS_TOKEN` |  否  | LINE Messaging Channel Access Token |
| `DIFY_BASE_URL`                       |  否  | Dify 服務網址                           |
| `DIFY_APP_KEY`                        |  否  | Dify Workflow API Key               |
| `API_PORT`                            |  否  | 服務連接埠，預設為 `8080`                    |
| `PWA_PORT`                            |  否  | PWA 連接埠，預設為 `5173`                  |
| `SESSION_REQUIRE_HTTPS`               |  否  | 使用 HTTPS 反向代理時設為 `true`             |
| `FORWARDED_HEADERS_ENABLED`           |  否  | 使用反向代理時設為 `true`                    |

請勿將包含密碼、Token 或金鑰的 `.env` 提交至版本控制系統。

## 更新服務

取得新版程式後，在專案根目錄執行：

```bash
docker compose down
docker compose up -d --build
```

若只需要重新建立特定服務，可使用：

```bash
docker compose up -d --build <service-name>
```

## 查看執行紀錄

查看所有服務：

```bash
docker compose logs -f
```

查看特定服務：

```bash
docker compose logs -f <service-name>
```

按下 `Ctrl+C` 可停止查看，不會停止服務。

## 停止服務

停止服務並保留資料：

```bash
docker compose down
```

停止服務並刪除資料庫儲存空間：

```bash
docker compose down -v
```

執行 `docker compose down -v` 會刪除既有資料，除非確定不需要保留資料，否則請勿使用。
