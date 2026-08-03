---
# yaml-language-server: $schema=schemas\page.schema.json
Object type:
    - Page
Creation date: "2025-07-23T08:22:39Z"
Created by:
    - KaiKaiZhen
Links:
    - Items (1)
    - Items
id: bafyreia7hgik3bbpjemg2sjot7ldqivonp2vvdcwwzmcayzbulx2eifgo4
---
# 🧬 Git Flow 團隊開發規範   
本文件定義專案的 Git 分支流程與命名規範，協助團隊統一開發模式，提升協作效率與版本控管彈性。   
 --- 
## 🔱 主要分支架構   
```
main 或 master
│
│── release/customerA     ← 客戶版本 (由version去merge)
│
└── develop
    ├── feature/xxx       ← 功能開發分支
    │
    ├── bug/xxx           ← 錯誤修正分支    
    │
    └── version/vX/X.X.X  ← 正式版本歷史分支
        

```
 --- 
## 🔖 分支定義與規範   
### 1️⃣ 主分支 main / master   
- 專案的穩定版本。   
- 僅允許從 `develop/` 分支合併。   
- **禁止直接在此分支開發。**   
   
### 2️⃣ 開發分支 develop   
- 所有開發的集散地。   
- 基於主分支創建。   
- 開發與修正皆以此為基礎進行。   
   
### 3️⃣ 功能分支 feature/   
- 功能開發使用。   
- **命名規則**：`feature/功能名稱`（小寫，使用 `-` 分隔）   
- 基於 `develop` 建立，開發完成後 merge 回 `develop`。   
- 範例：`feature/user-profile-page`   
   
### 4️⃣ 錯誤修正分支 bug/   
- 用於處理非正式版本的 bug 修復。   
- 基於 `develop` 建立。   
- 修復後需合併回 `develop`。   
- 範例：`bug/fix-login-error`   
   
### 5️⃣ 版本歷史分支 version/   
- 每次部署版本建立對應的歷史紀錄。   
- 基於 `develop` 建立。   
- 命名格式：`version/v1/1.0.0`（語意化版本：MAJOR.MINOR.PATCH）   
   
### 📌 命名準則   
- `0.X.X`：開發中階段版號均為0開頭。   
- `1.0.X`：修復 bug 或微調。   
- `1.X.0`：加入新功能或改良。   
- `2.0.0`：架構大幅度調整或重大改版。   
   
### 6️⃣ 客戶產品分支 release/   
- 用於實際交付客戶的獨立版本。   
- 通常由 `version/` 分支合併產生，對應不同客戶的產品需求與維護契約。   
   
### 7️⃣ 命名風格統一原則   
- 所有分支命名皆 **小寫**。   
- 多字詞請以 中線（**`-`）** 分隔。   
- 統一使用英文，使用 **複數形式** 為佳（如 `users`, `orders`）。   
 --- 
   
### 8️⃣ 建置規範   
- **初始化 Commit**   
    - 專案初始化時，第一筆 Commit 訊息應為：`first init`。   
    - 此 Commit 主要建立 `.gitignore` 檔案，並依開發語言及系統環境自行設定，可參考：   
    ![Items (1)](files\items-1.png)    
- 若有專案特定排除規則，請統一加上註解：`#Customer`，方便團隊識別。   
- **套件導入 Commit**   
    - Commit 訊息格式統一為：`package install`   
    - 工廠尖兵等標準專案，在初始化後的第二筆 Commit，請統一導入核心套件：   
        - `TD\_WebApiHelper` (NuGet)   
        - `URPMSHelper` (NuGet)   
 --- 
   
### 9️⃣ 版本標記規範補充：功能整合與 Tag 時機   
- 每一個版本（例如 `v1.2.0`）應涵蓋對應所有功能，**避免在功能尚未整合完成前打 Tag**。   
- Tag 應該打在「**實際交付點的 commit**」，不必強制綁定在 `main`。   
   
### ✅ 兩種典型模式：   
1. 以 **`main` 作為交付分支：**   
    - `version/v1.2.0` merge → `main`   
    - 在 `main` 上打 `v1.2.0` tag ✅   
2. 以 **`version/` 作為交付基準（常見於多 release/ 客製專案）：**   
    - 測試通過後在 `version/v1.2.0` 上直接打 tag ✅   
    - 之後視情況再 merge `main` 或交由 release 分支維護   
   
> 無論哪一種模式，tag 是對 commit，不是對分支；請明確標記交付快照即可。   
> 相關語法   

## ✅ 補充建議   
- **Merge 策略**   
    - 合併時請使用 **Merge Commit** 方式，保留歷史紀錄。   
- **Commit 命名建議**   
    - 採用動詞開頭的語意化命名，如下：   
        - `add login api`   
        - `fix token refresh bug`   
        - `refactor auth middleware`   
    - 建議依類型前綴標記：   
        - `feat:` 新功能   
        - `fix:` 錯誤修正   
        - `refactor:` 重構   
        - `test:` 測試   
        - `docs:` 文件   
        - `chore:` 工具設定、環境調整   
- **分支清理策略**   
    - `feature/`、`bug/` 分支 merge 後可刪除，避免積壓。   
    - `main`、`develop`、`version/`、`release/` 為長期存在分支。   
- **version補充說明**   
    - release 客戶分支可長期維護，不強制追上最新版，需視合約與維護責任而定。   
   
## ✅ 架構圖   
![Items](files\items.png)    
