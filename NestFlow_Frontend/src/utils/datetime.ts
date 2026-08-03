/**
 * 時區換算工具。
 * 後端一律以 UTC 保存，所有「使用者看到的日期時間」都在此換算。
 */

/** 取得某個時間點在指定時區的 UTC 偏移量（分鐘）。 */
function offsetMinutes(date: Date, timeZone: string): number {
  // 以 en-CA 取得可解析的 YYYY-MM-DD 格式，再回推該時區當下的偏移量
  const formatter = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    hour12: false,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })

  const parts = Object.fromEntries(
    formatter.formatToParts(date).map((part) => [part.type, part.value]),
  )

  const asUtc = Date.UTC(
    Number(parts.year),
    Number(parts.month) - 1,
    Number(parts.day),
    Number(parts.hour === '24' ? '0' : parts.hour),
    Number(parts.minute),
    Number(parts.second),
  )

  return (asUtc - date.getTime()) / 60000
}

/** 把「指定時區的當地時間」轉為 UTC 時間點。 */
function zonedToUtc(
  year: number,
  month: number,
  day: number,
  hour: number,
  minute: number,
  timeZone: string,
): Date {
  const guess = new Date(Date.UTC(year, month - 1, day, hour, minute))
  const offset = offsetMinutes(guess, timeZone)

  // 以初次偏移量修正後再算一次，處理日光節約時間切換的邊界
  const adjusted = new Date(guess.getTime() - offset * 60000)
  const finalOffset = offsetMinutes(adjusted, timeZone)

  return new Date(guess.getTime() - finalOffset * 60000)
}

/** 使用者時區下「今天」的年月日。 */
export function todayInZone(timeZone: string): { year: number; month: number; day: number } {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    })
      .formatToParts(new Date())
      .map((part) => [part.type, part.value]),
  )

  return {
    year: Number(parts.year),
    month: Number(parts.month),
    day: Number(parts.day),
  }
}

/**
 * 取得某個月份在使用者時區下的 UTC 起訖時間（前閉後開）。
 * 例如 Asia/Taipei 的 2026 年 8 月會得到 2026-07-31T16:00Z ~ 2026-08-31T16:00Z。
 */
export function monthRangeUtc(
  year: number,
  month: number,
  timeZone: string,
): { fromUtc: string; toUtc: string } {
  const from = zonedToUtc(year, month, 1, 0, 0, timeZone)

  const nextYear = month === 12 ? year + 1 : year
  const nextMonth = month === 12 ? 1 : month + 1
  const to = zonedToUtc(nextYear, nextMonth, 1, 0, 0, timeZone)

  return { fromUtc: from.toISOString(), toUtc: to.toISOString() }
}

/** 把使用者輸入的當地日期時間（datetime-local 值）轉為帶時區的 ISO 字串。 */
export function localInputToIso(value: string, timeZone: string): string {
  const [datePart, timePart = '00:00'] = value.split('T')
  const [year, month, day] = datePart.split('-').map(Number)
  const [hour, minute] = timePart.split(':').map(Number)

  return zonedToUtc(year, month, day, hour, minute, timeZone).toISOString()
}

/** 把 UTC 時間轉為 datetime-local 輸入框需要的當地時間字串。 */
export function isoToLocalInput(iso: string, timeZone: string): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-CA', {
      timeZone,
      hour12: false,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
      .formatToParts(new Date(iso))
      .map((part) => [part.type, part.value]),
  )

  const hour = parts.hour === '24' ? '00' : parts.hour

  return `${parts.year}-${parts.month}-${parts.day}T${hour}:${parts.minute}`
}

/** 顯示用：M月D日。 */
export function formatMonthDay(iso: string, timeZone: string): string {
  return new Intl.DateTimeFormat('zh-TW', {
    timeZone,
    month: 'numeric',
    day: 'numeric',
  }).format(new Date(iso))
}

/** 顯示用：YYYY年M月。 */
export function formatYearMonth(year: number, month: number): string {
  return `${year}年${month}月`
}

/** 顯示用：千分位金額，最多兩位小數。 */
export function formatAmount(value: number): string {
  return new Intl.NumberFormat('zh-TW', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value)
}
