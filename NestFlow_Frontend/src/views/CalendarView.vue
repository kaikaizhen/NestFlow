<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppMessage from '../components/AppMessage.vue'
import EventRow from '../components/EventRow.vue'
import { useAutoRefresh } from '../composables/useAutoRefresh'
import { api, type CalendarEvent, type WorkspaceMember } from '../services/apiClient'
import { useAuth } from '../stores/auth'
import { useWorkspaces } from '../stores/workspace'
import {
  dayKeyInZone,
  dayRangeUtc,
  formatYearMonth,
  monthRangeUtc,
  toDayKey,
  todayInZone,
} from '../utils/datetime'
import { colorByIndex, eventColor } from '../utils/events'

const WEEKDAYS = ['日', '一', '二', '三', '四', '五', '六']

/** 即將到來一次最多往後看的天數與筆數。 */
const UPCOMING_DAYS = 60
const UPCOMING_LIMIT = 20

type RangeKey = 'today' | 'week' | 'upcoming'

const router = useRouter()
const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const today = todayInZone(timeZone.value)
const year = ref(today.year)
const month = ref(today.month)

const todayKey = toDayKey(today.year, today.month, today.day)

const tomorrowKey = computed(() => {
  const next = new Date(Date.UTC(today.year, today.month - 1, today.day + 1))

  return toDayKey(next.getUTCFullYear(), next.getUTCMonth() + 1, next.getUTCDate())
})

/** 本週的最後一天，以週六為結尾，與月曆的欄位順序一致。 */
const weekEndKey = computed(() => {
  const base = new Date(Date.UTC(today.year, today.month - 1, today.day))
  const end = new Date(Date.UTC(today.year, today.month - 1, today.day + (6 - base.getUTCDay())))

  return toDayKey(end.getUTCFullYear(), end.getUTCMonth() + 1, end.getUTCDate())
})

const monthEvents = ref<CalendarEvent[]>([])
const upcomingEvents = ref<CalendarEvent[]>([])
const members = ref<WorkspaceMember[]>([])
const rangeKey = ref<RangeKey>('upcoming')

/** 點選月曆上的某一天時，畫面改為只顯示那一天的行程。 */
const selectedDayKey = ref<string | null>(null)

/** 家庭空間點選某位成員時，下方列表只顯示他建立的行程。 */
const selectedMemberId = ref<string | null>(null)

const isLoading = ref(true)
const errorMessage = ref('')

const monthLabel = computed(() => formatYearMonth(year.value, month.value))
const isFamilyWorkspace = computed(() => active.value?.type === 'family')

const selectedDayLabel = computed(() => {
  if (!selectedDayKey.value) {
    return ''
  }

  const [, m, d] = selectedDayKey.value.split('-').map(Number)
  return `${m}月${d}日`
})

/** 家庭成員依加入順序配色，建立者為第一位。 */
const memberColors = computed(
  () => new Map(members.value.map((member, index) => [member.userId, colorByIndex(index)])),
)

/**
 * 行程顏色。家庭空間依建立者上色，同一位成員所有行程同色，
 * 月曆上就能一眼看出是誰的；個人空間依行程上色，同一天的多筆才分得開。
 */
function colorOf(event: CalendarEvent) {
  if (!isFamilyWorkspace.value) {
    return eventColor(event.id)
  }

  // 建立者已離開資料空間時不在成員清單中，退回以 id 推導避免沒有顏色
  return memberColors.value.get(event.createdByUserId) ?? eventColor(event.createdByUserId)
}

/** 家庭空間的成員色票，讓月曆上的圓點看得懂，點選可篩選下方列表。 */
const legend = computed(() =>
  isFamilyWorkspace.value
    ? members.value.map((member, index) => ({
        id: member.userId,
        name: member.displayName,
        color: colorByIndex(index),
      }))
    : [],
)

function selectMember(memberId: string) {
  selectedMemberId.value = selectedMemberId.value === memberId ? null : memberId
}

function matchesMember(event: CalendarEvent) {
  return !selectedMemberId.value || event.createdByUserId === selectedMemberId.value
}

/** 每一天有哪些行程，供月曆顯示彩色標記。跨日行程會標記所有經過的日期。 */
const marksByDay = computed(() => {
  const map = new Map<string, string[]>()

  for (const event of monthEvents.value) {
    const endKey = dayKeyInZone(event.endAt, timeZone.value)
    let cursor = dayKeyInZone(event.startAt, timeZone.value)

    // 逐日推進到結束日，單筆行程最長 30 天由後端限制
    while (cursor <= endKey) {
      const colors = map.get(cursor) ?? []

      // 同一天最多顯示三個標記，避免格子被塞滿。
      // 家庭空間同一位成員當天有多筆時只留一顆點，否則整排都是同色。
      const color = colorOf(event)
      const isDuplicate = isFamilyWorkspace.value && colors.includes(color)

      if (colors.length < 3 && !isDuplicate) {
        colors.push(color)
        map.set(cursor, colors)
      }

      const [y, m, d] = cursor.split('-').map(Number)
      const next = new Date(Date.UTC(y, m - 1, d + 1))
      cursor = toDayKey(next.getUTCFullYear(), next.getUTCMonth() + 1, next.getUTCDate())
    }
  }

  return map
})

/** 月曆格子。補滿前後月份，使每列都是完整七天。 */
const cells = computed(() => {
  const first = new Date(Date.UTC(year.value, month.value - 1, 1))
  const leading = first.getUTCDay()
  const total = new Date(Date.UTC(year.value, month.value, 0)).getUTCDate()

  const result: { key: string; day: number; inMonth: boolean; marks: string[] }[] = []

  for (let i = 0; i < leading; i += 1) {
    const date = new Date(Date.UTC(year.value, month.value - 1, i - leading + 1))
    result.push({ key: `lead-${i}`, day: date.getUTCDate(), inMonth: false, marks: [] })
  }

  for (let day = 1; day <= total; day += 1) {
    const key = toDayKey(year.value, month.value, day)
    result.push({ key, day, inMonth: true, marks: marksByDay.value.get(key) ?? [] })
  }

  // 補到整列七格
  let trailing = 1
  while (result.length % 7 !== 0) {
    result.push({ key: `trail-${trailing}`, day: trailing, inMonth: false, marks: [] })
    trailing += 1
  }

  return result
})

/** 選定某一天時，從當月已載入的行程中篩選出當天（含跨日）的行程。 */
const dayEvents = computed(() => {
  if (!selectedDayKey.value) {
    return []
  }

  const key = selectedDayKey.value

  return monthEvents.value
    .filter((event) => {
      const startKey = dayKeyInZone(event.startAt, timeZone.value)
      const endKey = dayKeyInZone(event.endAt, timeZone.value)

      return startKey <= key && endKey >= key && matchesMember(event)
    })
    .sort((a, b) => a.startAt.localeCompare(b.startAt))
})

/** 依選取的區間過濾清單。今天與本週都以是否與該區間重疊判斷。 */
const visibleEvents = computed(() => {
  if (selectedDayKey.value) {
    return dayEvents.value
  }

  const memberFiltered = upcomingEvents.value.filter(matchesMember)

  if (rangeKey.value === 'upcoming') {
    return memberFiltered.slice(0, UPCOMING_LIMIT)
  }

  const limitKey = rangeKey.value === 'today' ? todayKey : weekEndKey.value

  return memberFiltered.filter((event) => {
    const startKey = dayKeyInZone(event.startAt, timeZone.value)
    const endKey = dayKeyInZone(event.endAt, timeZone.value)

    return startKey <= limitKey && endKey >= todayKey
  })
})

const emptyText = computed(() => {
  if (selectedDayKey.value) {
    return `${selectedDayLabel.value}沒有行程。`
  }

  if (rangeKey.value === 'today') {
    return '今天沒有行程。'
  }

  return rangeKey.value === 'week' ? '本週沒有行程。' : '接下來沒有行程。'
})

function shiftMonth(delta: number) {
  selectedDayKey.value = null

  const next = month.value + delta

  if (next < 1) {
    year.value -= 1
    month.value = 12
  } else if (next > 12) {
    year.value += 1
    month.value = 1
  } else {
    month.value = next
  }
}

function selectTab(key: RangeKey) {
  selectedDayKey.value = null
  rangeKey.value = key
}

/** 點月曆上的某一天：查看當天的行程，而不是直接跳去新增。 */
function selectDay(dayKey: string) {
  selectedDayKey.value = selectedDayKey.value === dayKey ? null : dayKey
}

async function load() {
  if (!active.value) {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  const monthRange = monthRangeUtc(year.value, month.value, timeZone.value)

  // 即將到來一律從今天起算，與目前檢視的月份無關
  const upcomingRange = dayRangeUtc(
    today.year,
    today.month,
    today.day,
    UPCOMING_DAYS,
    timeZone.value,
  )

  try {
    const [monthList, upcomingList, memberList] = await Promise.all([
      api.listEvents(active.value.id, monthRange.fromUtc, monthRange.toUtc),
      api.listEvents(active.value.id, upcomingRange.fromUtc, upcomingRange.toUtc, 100),
      // 個人空間不需要成員色票，省一次請求
      isFamilyWorkspace.value ? api.listMembers(active.value.id) : Promise.resolve([]),
    ])

    monthEvents.value = monthList
    upcomingEvents.value = upcomingList
    members.value = memberList
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

function openEvent(event: CalendarEvent) {
  router.push({ name: 'event-edit', params: { eventId: event.id } })
}

/** 新增行程：若目前有選定日期就帶入當作預設開始時間。 */
function createEvent() {
  router.push({
    name: 'event-create',
    query: selectedDayKey.value ? { day: selectedDayKey.value } : undefined,
  })
}

onMounted(async () => {
  try {
    await loadWorkspaces()
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
    isLoading.value = false
  }
})

// 切換資料空間或月份時重新載入。
// 從新增或編輯頁返回時本元件會重新掛載，因此 onMounted 已涵蓋重新整理。
watch([active, year, month], load)

// 換資料空間時清掉成員篩選，避免帶著上一個空間的篩選條件
watch(active, () => {
  selectedMemberId.value = null
})

// 家庭成員新增或修改行程時，這裡不會即時收到通知，
// 靠定時輪詢與切回頁面時補抓一次來縮短看到最新資料的延遲
useAutoRefresh(load)
</script>

<template>
  <section class="calendar">
    <header class="calendar__header">
      <h1 class="calendar__title">行事曆</h1>
    </header>

    <div class="calendar__month">
      <span class="calendar__month-label">{{ monthLabel }}</span>

      <span class="calendar__month-nav">
        <button class="calendar__month-btn" type="button" aria-label="上個月" @click="shiftMonth(-1)">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="m15 6-6 6 6 6" />
          </svg>
        </button>

        <button class="calendar__month-btn" type="button" aria-label="下個月" @click="shiftMonth(1)">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="m9 6 6 6-6 6" />
          </svg>
        </button>
      </span>
    </div>

    <AppMessage :text="errorMessage" tone="error" />

    <div class="grid">
      <span v-for="label in WEEKDAYS" :key="label" class="grid__weekday">{{ label }}</span>

      <template v-for="cell in cells" :key="cell.key">
        <span v-if="!cell.inMonth" class="grid__cell grid__cell--outside">{{ cell.day }}</span>

        <button
          v-else
          class="grid__cell"
          :class="{ 'is-today': cell.key === todayKey, 'is-selected': cell.key === selectedDayKey }"
          type="button"
          @click="selectDay(cell.key)"
        >
          <span class="grid__number">{{ cell.day }}</span>
          <span class="grid__marks">
            <span
              v-for="(color, index) in cell.marks"
              :key="index"
              class="grid__mark"
              :style="{ backgroundColor: color }"
            />
          </span>
        </button>
      </template>
    </div>

    <ul v-if="legend.length" class="legend" role="group" aria-label="依成員篩選">
      <li v-for="member in legend" :key="member.id">
        <button
          class="legend__item"
          :class="{ 'is-active': selectedMemberId === member.id }"
          type="button"
          :aria-pressed="selectedMemberId === member.id"
          @click="selectMember(member.id)"
        >
          <span class="legend__dot" :style="{ backgroundColor: member.color }" aria-hidden="true" />
          {{ member.name }}
        </button>
      </li>
    </ul>

    <div v-if="selectedDayKey" class="selected-day">
      <span class="selected-day__label">{{ selectedDayLabel }}</span>
      <button class="selected-day__clear" type="button" @click="selectedDayKey = null">
        回到列表
      </button>
    </div>

    <div v-else class="tabs" role="tablist" aria-label="行程區間">
      <button
        class="tabs__item"
        :class="{ 'is-active': rangeKey === 'today' }"
        type="button"
        role="tab"
        :aria-selected="rangeKey === 'today'"
        @click="selectTab('today')"
      >
        今天
      </button>

      <button
        class="tabs__item"
        :class="{ 'is-active': rangeKey === 'week' }"
        type="button"
        role="tab"
        :aria-selected="rangeKey === 'week'"
        @click="selectTab('week')"
      >
        本週
      </button>

      <button
        class="tabs__item"
        :class="{ 'is-active': rangeKey === 'upcoming' }"
        type="button"
        role="tab"
        :aria-selected="rangeKey === 'upcoming'"
        @click="selectTab('upcoming')"
      >
        即將到來
      </button>
    </div>

    <div class="calendar__scroll">
      <p v-if="isLoading" class="calendar__hint">載入中…</p>

      <p v-else-if="!visibleEvents.length" class="calendar__hint">{{ emptyText }}</p>

      <ul v-else class="calendar__list">
        <li v-for="event in visibleEvents" :key="event.id">
          <EventRow
            :event="event"
            :color="colorOf(event)"
            :time-zone="timeZone"
            :today-key="todayKey"
            :tomorrow-key="tomorrowKey"
            :show-author="isFamilyWorkspace"
            @select="openEvent"
          />
        </li>
      </ul>
    </div>

    <button class="calendar__fab" type="button" aria-label="新增行程" @click="createEvent()">
      <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
    </button>
  </section>
</template>

<style scoped>
.calendar {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  /* 佔滿視窗並扣掉底部導航，讓月曆與篩選固定、只有行程清單捲動。
     overflow: hidden 避免小螢幕上內容略微超出 100dvh 時外溢成整頁捲動
     ——外溢應該被清單自己的 overflow-y: auto 吸收，而不是變成頁面捲動。 */
  height: 100vh;
  height: 100dvh;
  overflow: hidden;
  padding: calc(var(--space-5) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + env(safe-area-inset-bottom));
}

.calendar__scroll {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-4);
  /* 保底高度，小螢幕上方月曆與按鈕擠壓版面時，
     清單至少保留可視與可捲動的高度 */
  min-height: 88px;
  /* 不再為浮動按鈕保留清單底部留白：小螢幕上空間本就緊繃，
     寧可讓＋按鈕疊在最後一筆行程上，也不要讓清單少一大截可視高度。 */
  padding-bottom: var(--space-4);
  overflow-y: auto;
}

.calendar__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.calendar__title {
  margin: 0;
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.calendar__month {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.calendar__month-label {
  font-weight: 600;
}

.calendar__month-nav {
  display: flex;
  gap: var(--space-2);
}

.calendar__month-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
  box-shadow: var(--shadow-card);
}

.calendar__month-btn:active {
  background-color: var(--color-border);
}

.grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: var(--space-1);
  padding: var(--space-2);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-card);
}

.grid__weekday {
  padding-bottom: var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
}

.grid__cell {
  display: flex;
  flex-direction: column;
  gap: 3px;
  align-items: center;
  justify-content: center;
  min-height: 38px;
  border-radius: var(--radius-sm);
}

.grid__cell--outside {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--color-border);
}

.grid__number {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  border-radius: var(--radius-full);
}

.grid__cell.is-today .grid__number {
  font-weight: 700;
  color: var(--color-surface);
  background-color: var(--color-accent);
}

.grid__cell.is-selected:not(.is-today) .grid__number {
  font-weight: 700;
  box-shadow: inset 0 0 0 2px var(--color-accent);
}

.grid__cell:active .grid__number {
  background-color: var(--color-border);
}

.grid__cell.is-today:active .grid__number {
  background-color: var(--color-accent);
}

.grid__marks {
  display: flex;
  gap: 3px;
  height: 5px;
}

.grid__mark {
  width: 5px;
  height: 5px;
  border-radius: var(--radius-full);
}

.legend {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin: 0;
  padding: 0 var(--space-1);
  list-style: none;
}

.legend__item {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  padding: var(--space-1) var(--space-3) var(--space-1) var(--space-2);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
  transition:
    color var(--duration-fast) var(--ease-out),
    background-color var(--duration-fast) var(--ease-out);
}

.legend__item.is-active {
  color: var(--color-text);
  background-color: var(--color-border);
  font-weight: 600;
}

.legend__item:active {
  background-color: var(--color-border);
}

.legend__dot {
  width: 8px;
  height: 8px;
  border-radius: var(--radius-full);
}

.selected-day {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--space-1);
}

.selected-day__label {
  font-size: var(--font-size-title);
  font-weight: 700;
}

.selected-day__clear {
  padding: var(--space-2) var(--space-3);
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
}

.selected-day__clear:active {
  background-color: var(--color-border);
}

.tabs {
  display: flex;
  gap: var(--space-2);
  margin-top: var(--space-1);
}

.tabs__item {
  padding: var(--space-2) var(--space-4);
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
  transition:
    color var(--duration-fast) var(--ease-out),
    background-color var(--duration-fast) var(--ease-out);
}

.tabs__item.is-active {
  color: var(--color-surface);
  background-color: var(--color-accent);
}

.calendar__hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.calendar__list {
  display: flex;
  flex-direction: column;
  gap: 1px;
  margin: 0;
  padding: 0;
  /* 本元素同時是 .calendar__scroll（column flex）的子項，且設了 overflow: hidden。
     依 flexbox 規範，overflow 非 visible 的 flex 子項，其 min-height: auto 會解析為 0，
     於是清單會被壓縮成剛好塞滿容器、而不是撐出高度，捲動容器因此永遠沒有溢出內容
     ——結果就是捲不動也沒有捲軸，且塞不下的行程被 overflow: hidden 直接裁掉。
     flex-shrink: 0 讓清單保持內容高度，把溢出交還給 .calendar__scroll 處理。 */
  flex-shrink: 0;
  overflow: hidden;
  list-style: none;
  background-color: var(--color-border);
  border-radius: var(--radius-md);
}

/* 用 absolute 而非 fixed：頁面本身已是視窗高度且不捲動，效果相同，
   但定位基準是內容欄而不是整個視窗，桌機時才會貼齊內容右緣；
   也不會在頁面切換動畫（祖先有 transform）期間跳位。 */
.calendar__fab {
  position: absolute;
  right: var(--space-4);
  bottom: calc(var(--nav-height) + var(--space-4) + env(safe-area-inset-bottom));
  display: flex;
  align-items: center;
  justify-content: center;
  width: 56px;
  height: 56px;
  color: var(--color-surface);
  background-color: var(--color-accent);
  border-radius: var(--radius-full);
  box-shadow: 0 6px 20px rgb(17 17 19 / 24%);
  transition: transform var(--duration-fast) var(--ease-out);
}

.calendar__fab:active {
  transform: scale(0.94);
}
</style>
