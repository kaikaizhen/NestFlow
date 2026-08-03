/**
 * 行程標記顏色。計畫第 12.3 節只要求「少量特殊顏色」，
 * 因此不在資料表存顏色，改由傳入的識別碼穩定推導，同一個來源每次都是同一色。
 *
 * 家庭資料空間依成員在 Workspace 的加入順序配色（見 colorByIndex），
 * 成員數在色盤範圍內時保證不撞色；個人空間傳入行程 id，
 * 讓同一天的多筆行程在月曆上仍可區分。
 */
const EVENT_COLORS = ['#2563eb', '#f97316', '#16a34a', '#a855f7', '#ec4899', '#0891b2', '#ca8a04']

/** 依序取色。成員數超過色盤長度才會循環，前幾位一定不同色。 */
export function colorByIndex(index: number): string {
  return EVENT_COLORS[index % EVENT_COLORS.length]
}

export function eventColor(seed: string): string {
  let hash = 0

  for (let i = 0; i < seed.length; i += 1) {
    hash = (hash * 31 + seed.charCodeAt(i)) % 100000
  }

  return EVENT_COLORS[hash % EVENT_COLORS.length]
}
