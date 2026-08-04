# NestFlow Dify Workflow 串接規格

## 目的

當 NestFlow 既有固定格式解析器無法解析 LINE 訊息時，呼叫 Dify Workflow，將自然語言解析為：

- `entry`：記帳
- `event`：行程
- `none`：無法判斷

Dify 只負責解析，不直接寫入資料庫。解析結果仍由 NestFlow 轉成既有 `FixedCommand`，並沿用原本的待確認流程。

---

## Dify API

### Base URL

```text
http://100.64.195.42/v1
```

### Endpoint

```http
POST /workflows/run
```

完整路由：

```text
http://100.64.195.42/v1/workflows/run
```

### Header

```http
Authorization: Bearer <DIFY_APP_KEY>
Content-Type: application/json
```

App Key 應由設定或環境變數提供，不要寫死在程式碼。

---

## Request

### 外層格式

```json
{
  "inputs": {
    "message": "今天午餐和同學吃牛肉麵花了 180 元",
    "current_date": "2026-08-04",
    "current_time": "11:45",
    "time_zone": "Asia/Taipei",
    "expense_categories": "[...]",
    "income_categories": "[...]"
  },
  "response_mode": "blocking",
  "user": "內部使用者識別值"
}
```

### inputs 參數

| 參數 | 型別 | 說明 |
|---|---|---|
| `message` | string | LINE 使用者原始訊息 |
| `current_date` | string | 使用者當地日期，格式 `yyyy-MM-dd` |
| `current_time` | string | 使用者當地時間，格式 `HH:mm` |
| `time_zone` | string | 使用者時區，例如 `Asia/Taipei` |
| `expense_categories` | string | 支出分類定義的 JSON 字串 |
| `income_categories` | string | 收入分類定義的 JSON 字串 |

注意參數名稱是：

```text
time_zone
```

不是：

```text
timezone
```

`response_mode` 固定使用：

```text
blocking
```

`user` 建議使用 NestFlow 內部 `User.Id`，不要傳明文 LINE User ID。

---

## 分類資料格式

`expense_categories` 與 `income_categories` 都是 JSON 字串，其內容為分類陣列。

### 分類物件

```json
{
  "code": "food",
  "label": "餐飲",
  "keywords": ["餐飲", "吃飯", "早餐", "午餐"]
}
```

欄位：

- `code`：NestFlow 實際保存的分類代碼
- `label`：顯示名稱
- `keywords`：提供 Dify 判斷分類的關鍵字

### 支出分類範例

```json
[
  {
    "code": "food",
    "label": "餐飲",
    "keywords": ["餐飲", "吃飯", "早餐", "午餐", "晚餐", "宵夜", "飲料", "咖啡", "點心", "外送"]
  },
  {
    "code": "transport",
    "label": "交通",
    "keywords": ["交通", "捷運", "公車", "火車", "高鐵", "計程車", "加油", "停車", "油錢"]
  },
  {
    "code": "shopping",
    "label": "購物",
    "keywords": ["購物", "超市", "網購", "衣服", "日用品", "賣場"]
  },
  {
    "code": "home",
    "label": "居家",
    "keywords": ["居家", "房租", "水費", "電費", "瓦斯", "網路費", "家具"]
  },
  {
    "code": "medical",
    "label": "醫療",
    "keywords": ["醫療", "看病", "買藥", "藥局", "健檢", "牙醫"]
  },
  {
    "code": "entertainment",
    "label": "娛樂",
    "keywords": ["娛樂", "電影", "遊戲", "旅遊", "訂閱", "運動"]
  },
  {
    "code": "other",
    "label": "其他",
    "keywords": []
  }
]
```

### 收入分類範例

```json
[
  {
    "code": "salary",
    "label": "薪資",
    "keywords": ["薪資", "薪水", "月薪"]
  },
  {
    "code": "bonus",
    "label": "獎金",
    "keywords": ["獎金", "紅利", "年終"]
  },
  {
    "code": "investment",
    "label": "投資",
    "keywords": ["投資", "股利", "股息", "利息"]
  },
  {
    "code": "other",
    "label": "其他",
    "keywords": []
  }
]
```

分類內容應由 NestFlow 後端現有分類定義產生，避免後端與 Dify 維護兩份不同規則。

---

## Response 外層結構

可參考：

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class DifyWorkflowResponse
{
    [JsonPropertyName("workflow_run_id")]
    public string? WorkflowRunId { get; init; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; init; }

    [JsonPropertyName("data")]
    public DifyWorkflowData? Data { get; init; }
}

public sealed class DifyWorkflowData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("outputs")]
    public Dictionary<string, JsonElement>? Outputs { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("elapsed_time")]
    public double? ElapsedTime { get; init; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; init; }
}
```

Request 可參考：

```csharp
using System.Text.Json.Serialization;

public sealed class DifyWorkflowRequest
{
    [JsonPropertyName("inputs")]
    public required Dictionary<string, object> Inputs { get; init; }

    [JsonPropertyName("response_mode")]
    public string ResponseMode { get; init; } = "blocking";

    [JsonPropertyName("user")]
    public required string User { get; init; }
}
```

---

## Dify outputs.result

Dify 的解析結果位於：

```text
data.outputs.result
```

目前 `result` 是一段 JSON 字串，不是直接的 JSON Object。

實際外層回應範例：

```json
{
  "workflow_run_id": "xxx",
  "task_id": "xxx",
  "data": {
    "status": "succeeded",
    "outputs": {
      "result": "{\"kind\":\"entry\",\"type\":\"expense\",\"amount\":180,\"category\":\"food\",\"note\":\"今天午餐和同學去吃牛肉麵\",\"categoryIsFallback\":false,\"event\":null,\"confidence\":0.98,\"reason\":null}"
    }
  }
}
```

Claude 實作時應：

1. 解析 Dify Workflow 外層回應
2. 取得 `data.outputs.result`
3. 將 `result` 字串再次反序列化成 NestFlow 使用的 Result 模型

---

## Result 結構

### 記帳 entry

```json
{
  "kind": "entry",
  "type": "expense",
  "amount": 180,
  "category": "food",
  "note": "今天午餐和同學去吃牛肉麵",
  "categoryIsFallback": false,
  "event": null,
  "confidence": 0.98,
  "reason": null
}
```

規則：

- `kind` 固定為 `entry`
- `type` 只能是 `expense` 或 `income`
- `amount` 必須大於 0
- `category` 必須是後端提供的分類 `code`
- 找不到明確分類時使用 `other`
- 使用 `other` 時，`categoryIsFallback = true`
- `event` 必須為 `null`
- `reason` 必須為 `null`

### 行程 event

```json
{
  "kind": "event",
  "type": null,
  "amount": null,
  "category": null,
  "note": null,
  "categoryIsFallback": false,
  "event": {
    "title": "跟教授討論論文",
    "month": null,
    "day": null,
    "dayOffset": 1,
    "startHour": 14,
    "startMinute": 0,
    "endHour": 15,
    "endMinute": 30
  },
  "confidence": 0.97,
  "reason": null
}
```

行程日期規則：

- 明確日期：
  - `month`、`day` 有值
  - `dayOffset = null`
- 相對日期：
  - `month = null`
  - `day = null`
  - `dayOffset` 有值
- 今天：`dayOffset = 0`
- 明天：`dayOffset = 1`
- 後天：`dayOffset = 2`

時間使用 24 小時制。

未提供結束時間時：

```json
{
  "endHour": null,
  "endMinute": null
}
```

### 無法判斷 none

```json
{
  "kind": "none",
  "type": null,
  "amount": null,
  "category": null,
  "note": null,
  "categoryIsFallback": false,
  "event": null,
  "confidence": 0.2,
  "reason": "不是記帳或行程"
}
```

以下情況通常回傳 `none`：

- 不是記帳或行程
- 記帳缺少金額
- 行程缺少日期、時間或標題
- 同時包含多筆操作
- 同時包含記帳與行程
- 確認、取消、綁定或提醒
- 必須猜測關鍵資料

---

## Result 模型參考

```csharp
using System.Text.Json.Serialization;

public sealed class DifyCommandResult
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("categoryIsFallback")]
    public bool CategoryIsFallback { get; init; }

    [JsonPropertyName("event")]
    public DifyEventResult? Event { get; init; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

public sealed class DifyEventResult
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("month")]
    public int? Month { get; init; }

    [JsonPropertyName("day")]
    public int? Day { get; init; }

    [JsonPropertyName("dayOffset")]
    public int? DayOffset { get; init; }

    [JsonPropertyName("startHour")]
    public int? StartHour { get; init; }

    [JsonPropertyName("startMinute")]
    public int? StartMinute { get; init; }

    [JsonPropertyName("endHour")]
    public int? EndHour { get; init; }

    [JsonPropertyName("endMinute")]
    public int? EndMinute { get; init; }
}
```

---

## NestFlow 串接規則

Claude 應理解以下既有流程限制：

```text
LINE 訊息
→ 先使用既有固定格式 Parser
→ 固定格式無法解析時才呼叫 Dify
→ Dify Result 轉成既有 FixedCommand
→ 建立 PendingAction
→ 使用者回覆「確認」
→ 才真正寫入記帳或行程
```

必要規則：

- 固定格式解析器優先
- 只有已綁定使用者才呼叫 Dify
- Dify 目前只處理 `entry`、`event`、`none`
- Dify 不處理確認、取消、綁定或提醒
- Dify 不直接寫入資料庫
- Dify 結果必須轉成現有 `FixedCommand`
- 記帳與行程仍須等待使用者確認
- Dify 呼叫失敗或資料無效時，視為 `FixedCommandKind.None`
- 後端仍需驗證金額、分類、日期、時間及 `confidence`
- 不得信任 Dify 回傳的未知分類代碼
- App Key 不得寫入程式碼或版本控制

---

## 給 Claude 的任務描述

```text
請閱讀此文件，先理解 NestFlow 使用的 Dify Workflow API、路由、輸入參數、分類格式、外層回應及 outputs.result 結構。

請自行依照現有 NestFlow_Backend 專案架構完成串接，不必完全照搬文件中的類別名稱或檔案配置，但必須遵守：

1. 固定格式 Parser 優先。
2. 僅固定格式無法解析且使用者已綁定時呼叫 Dify。
3. 呼叫 POST {BaseUrl}/workflows/run。
4. inputs 名稱必須完全符合：
   message、current_date、current_time、time_zone、
   expense_categories、income_categories。
5. response_mode 使用 blocking。
6. 從 data.outputs.result 取得 JSON 字串，並轉成 Result。
7. 將有效 Result 轉成現有 FixedCommand。
8. 後續沿用既有 PendingAction 與確認流程。
9. Dify 不得直接寫入記帳或行程資料。
10. Dify 錯誤、逾時、無效 JSON 或無效欄位時，安全退回 None。
11. App Key 使用設定或環境變數，不得硬編碼。
12. 依現有專案慣例自行決定 DTO、Service、HttpClient、DI、驗證與測試實作。
```
