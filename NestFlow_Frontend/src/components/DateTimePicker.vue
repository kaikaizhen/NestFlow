<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { todayInZone } from '../utils/datetime'

const WEEKDAYS = ['日', '一', '二', '三', '四', '五', '六']
const MINUTE_STEP = 5

/** 滾輪每一格的高度，需與 CSS 的 .wheel__item 一致。 */
const ITEM_HEIGHT = 44

/** 停止滑動多久後才視為選定，避免滑動過程一直改值。 */
const SETTLE_DELAY = 120

const props = defineProps<{
  /** 使用者當地時間，格式為 YYYY-MM-DDTHH:mm。 */
  modelValue: string
  timeZone: string
  label?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const isOpen = ref(false)

// 草稿狀態，按下完成才寫回，取消時不影響原值
const draftYear = ref(2026)
const draftMonth = ref(1)
const draftDay = ref(1)
const draftHour = ref(0)
const draftMinute = ref(0)

const hours = Array.from({ length: 24 }, (_, i) => i)
const minutes = Array.from({ length: 60 / MINUTE_STEP }, (_, i) => i * MINUTE_STEP)

const hourWheel = ref<HTMLElement | null>(null)
const minuteWheel = ref<HTMLElement | null>(null)

let hourTimer: number | undefined
let minuteTimer: number | undefined

function parse(value: string) {
  const [datePart, timePart = '00:00'] = value.split('T')
  const [year, month, day] = datePart.split('-').map(Number)
  const [hour, minute] = timePart.split(':').map(Number)

  return { year, month, day, hour, minute }
}

function pad(value: number) {
  return String(value).padStart(2, '0')
}

function compose() {
  return `${draftYear.value}-${pad(draftMonth.value)}-${pad(draftDay.value)}T${pad(draftHour.value)}:${pad(draftMinute.value)}`
}

/** 顯示文字：2026年8月16日（週日）12:30 */
const displayText = computed(() => {
  if (!props.modelValue) {
    return '請選擇時間'
  }

  const { year, month, day, hour, minute } = parse(props.modelValue)
  const weekday = WEEKDAYS[new Date(Date.UTC(year, month - 1, day)).getUTCDay()]

  return `${year}年${month}月${day}日（週${weekday}）${pad(hour)}:${pad(minute)}`
})

/** 月曆格子。補滿前後月份，使每列都是完整七天。 */
const cells = computed(() => {
  const first = new Date(Date.UTC(draftYear.value, draftMonth.value - 1, 1))
  const leading = first.getUTCDay()
  const daysInMonth = new Date(Date.UTC(draftYear.value, draftMonth.value, 0)).getUTCDate()

  const result: { day: number; inMonth: boolean; date: Date }[] = []

  for (let i = 0; i < leading; i++) {
    const date = new Date(Date.UTC(draftYear.value, draftMonth.value - 1, i - leading + 1))
    result.push({ day: date.getUTCDate(), inMonth: false, date })
  }

  for (let day = 1; day <= daysInMonth; day++) {
    result.push({
      day,
      inMonth: true,
      date: new Date(Date.UTC(draftYear.value, draftMonth.value - 1, day)),
    })
  }

  while (result.length % 7 !== 0) {
    const next = result.length - leading - daysInMonth + 1
    const date = new Date(Date.UTC(draftYear.value, draftMonth.value - 1, daysInMonth + next))
    result.push({ day: date.getUTCDate(), inMonth: false, date })
  }

  return result
})

const todayKey = computed(() => {
  const t = todayInZone(props.timeZone)
  return `${t.year}-${pad(t.month)}-${pad(t.day)}`
})

function cellKey(date: Date) {
  return `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`
}

const selectedKey = computed(
  () => `${draftYear.value}-${pad(draftMonth.value)}-${pad(draftDay.value)}`,
)

function open() {
  const source = props.modelValue || `${todayKey.value}T09:00`
  const parsed = parse(source)

  draftYear.value = parsed.year
  draftMonth.value = parsed.month
  draftDay.value = parsed.day
  draftHour.value = parsed.hour
  // 分鐘對齊到選項刻度，避免選單沒有對應項目
  draftMinute.value = Math.round(parsed.minute / MINUTE_STEP) * MINUTE_STEP % 60

  isOpen.value = true
}

function shiftMonth(delta: number) {
  const next = new Date(Date.UTC(draftYear.value, draftMonth.value - 1 + delta, 1))
  draftYear.value = next.getUTCFullYear()
  draftMonth.value = next.getUTCMonth() + 1
}

function selectCell(date: Date) {
  draftYear.value = date.getUTCFullYear()
  draftMonth.value = date.getUTCMonth() + 1
  draftDay.value = date.getUTCDate()
}

function selectToday() {
  const t = todayInZone(props.timeZone)
  draftYear.value = t.year
  draftMonth.value = t.month
  draftDay.value = t.day
}

function confirm() {
  emit('update:modelValue', compose())
  isOpen.value = false
}

// ---------------------------------------------------------------
// 時間滾輪：左邊小時、右邊分鐘，垂直滑動選取
// ---------------------------------------------------------------

/** 把滾輪捲到指定值所在的位置。 */
function scrollToValue(el: HTMLElement | null, list: number[], value: number, smooth: boolean) {
  if (!el) {
    return
  }

  const index = Math.max(list.indexOf(value), 0)

  el.scrollTo({ top: index * ITEM_HEIGHT, behavior: smooth ? 'smooth' : 'auto' })
}

/** 滑動停止後，取中央那一格作為選定值。 */
function settle(el: HTMLElement, list: number[]): number {
  const index = Math.round(el.scrollTop / ITEM_HEIGHT)

  return list[Math.min(Math.max(index, 0), list.length - 1)]
}

function onHourScroll(event: Event) {
  const el = event.target as HTMLElement

  window.clearTimeout(hourTimer)
  hourTimer = window.setTimeout(() => {
    draftHour.value = settle(el, hours)
  }, SETTLE_DELAY)
}

function onMinuteScroll(event: Event) {
  const el = event.target as HTMLElement

  window.clearTimeout(minuteTimer)
  minuteTimer = window.setTimeout(() => {
    draftMinute.value = settle(el, minutes)
  }, SETTLE_DELAY)
}

/** 直接點某一格時也捲到中央，維持與滑動一致的操作感。 */
function selectHour(hour: number) {
  draftHour.value = hour
  scrollToValue(hourWheel.value, hours, hour, true)
}

function selectMinute(minute: number) {
  draftMinute.value = minute
  scrollToValue(minuteWheel.value, minutes, minute, true)
}

// 開啟面板時鎖住背景捲動，並把滾輪對到目前的時間
watch(isOpen, async (value) => {
  document.body.style.overflow = value ? 'hidden' : ''

  if (!value) {
    window.clearTimeout(hourTimer)
    window.clearTimeout(minuteTimer)
    return
  }

  await nextTick()

  scrollToValue(hourWheel.value, hours, draftHour.value, false)
  scrollToValue(minuteWheel.value, minutes, draftMinute.value, false)
})
</script>

<template>
  <div class="picker">
    <span class="picker__label">{{ label ?? '時間' }}</span>

    <button class="picker__trigger" type="button" @click="open">
      <span :class="{ 'is-placeholder': !modelValue }">{{ displayText }}</span>
      <svg
        viewBox="0 0 24 24"
        width="20"
        height="20"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <rect x="3" y="5" width="18" height="16" rx="4" />
        <path d="M3 10h18M8 3v4M16 3v4" />
      </svg>
    </button>

    <Teleport to="body">
      <Transition name="sheet">
        <div v-if="isOpen" class="sheet" role="dialog" aria-modal="true" aria-label="選擇時間">
          <div class="sheet__backdrop" @click="isOpen = false" />

          <div class="sheet__panel">
            <div class="sheet__grabber" aria-hidden="true" />

            <div class="month">
              <button class="month__btn" type="button" aria-label="上個月" @click="shiftMonth(-1)">
                <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="m15 6-6 6 6 6" />
                </svg>
              </button>

              <span class="month__label">{{ draftYear }}年{{ draftMonth }}月</span>

              <button class="month__btn" type="button" aria-label="下個月" @click="shiftMonth(1)">
                <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="m9 6 6 6-6 6" />
                </svg>
              </button>
            </div>

            <div class="weekdays">
              <span v-for="day in WEEKDAYS" :key="day">{{ day }}</span>
            </div>

            <div class="days">
              <button
                v-for="(cell, index) in cells"
                :key="index"
                type="button"
                class="days__cell"
                :class="{
                  'is-outside': !cell.inMonth,
                  'is-today': cellKey(cell.date) === todayKey,
                  'is-selected': cellKey(cell.date) === selectedKey,
                }"
                @click="selectCell(cell.date)"
              >
                {{ cell.day }}
              </button>
            </div>

            <button class="today" type="button" @click="selectToday">回到今天</button>

            <div class="time">
              <span class="time__label">時間</span>

              <div class="wheels">
                <span class="wheels__highlight" aria-hidden="true" />

                <div
                  ref="hourWheel"
                  class="wheel"
                  role="listbox"
                  aria-label="小時"
                  @scroll.passive="onHourScroll"
                >
                  <button
                    v-for="hour in hours"
                    :key="hour"
                    type="button"
                    class="wheel__item"
                    :class="{ 'is-active': hour === draftHour }"
                    role="option"
                    :aria-selected="hour === draftHour"
                    @click="selectHour(hour)"
                  >
                    {{ pad(hour) }}
                  </button>
                </div>

                <span class="wheels__colon" aria-hidden="true">:</span>

                <div
                  ref="minuteWheel"
                  class="wheel"
                  role="listbox"
                  aria-label="分鐘"
                  @scroll.passive="onMinuteScroll"
                >
                  <button
                    v-for="minute in minutes"
                    :key="minute"
                    type="button"
                    class="wheel__item"
                    :class="{ 'is-active': minute === draftMinute }"
                    role="option"
                    :aria-selected="minute === draftMinute"
                    @click="selectMinute(minute)"
                  >
                    {{ pad(minute) }}
                  </button>
                </div>
              </div>
            </div>

            <div class="sheet__actions">
              <button class="sheet__cancel" type="button" @click="isOpen = false">取消</button>
              <button class="sheet__confirm" type="button" @click="confirm">完成</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.picker {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.picker__label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.picker__trigger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  min-height: 48px;
  padding: 0 var(--space-3);
  font-weight: 500;
  color: var(--color-text);
  background-color: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  text-align: left;
}

.picker__trigger:active {
  background-color: var(--color-border);
}

.picker__trigger .is-placeholder {
  color: var(--color-text-muted);
}

/* --------------------------------------------------------------- */
/* 底部面板                                                          */
/* --------------------------------------------------------------- */
.sheet {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: flex;
  align-items: flex-end;
  justify-content: center;
}

.sheet__backdrop {
  position: absolute;
  inset: 0;
  background-color: rgb(17 17 19 / 35%);
}

.sheet__panel {
  position: relative;
  width: 100%;
  max-width: 480px;
  max-height: 90dvh;
  overflow-y: auto;
  padding: var(--space-3) var(--space-4) calc(var(--space-5) + env(safe-area-inset-bottom));
  background-color: var(--color-surface);
  border-radius: var(--radius-lg) var(--radius-lg) 0 0;
}

.sheet__grabber {
  width: 40px;
  height: 4px;
  margin: 0 auto var(--space-4);
  background-color: var(--color-border);
  border-radius: var(--radius-full);
}

.month {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-3);
}

.month__btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  color: var(--color-text-muted);
  border-radius: var(--radius-full);
}

.month__btn:active {
  background-color: var(--color-bg);
}

.month__label {
  font-size: var(--font-size-title);
  font-weight: 700;
}

.weekdays,
.days {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
}

.weekdays {
  margin-bottom: var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
}

.days__cell {
  display: flex;
  align-items: center;
  justify-content: center;
  /* 大型點擊區 */
  aspect-ratio: 1;
  margin: 2px auto;
  width: 40px;
  font-size: var(--font-size-body);
  border-radius: var(--radius-full);
  transition:
    background-color var(--duration-fast) var(--ease-out),
    color var(--duration-fast) var(--ease-out);
}

.days__cell.is-outside {
  color: var(--color-border);
}

.days__cell.is-today {
  font-weight: 700;
  box-shadow: inset 0 0 0 1px var(--color-border);
}

.days__cell.is-selected {
  font-weight: 700;
  color: var(--color-surface);
  background-color: var(--color-accent);
  box-shadow: none;
}

.today {
  display: block;
  margin: var(--space-2) auto var(--space-4);
  padding: var(--space-2) var(--space-4);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.time__label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

/* 左邊小時、右邊分鐘的垂直滾輪。高度為 5 格，中央那格即為選取值。 */
.wheels {
  position: relative;
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  height: 220px;
  margin-top: var(--space-2);
  overflow: hidden;
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
}

.wheels__highlight {
  position: absolute;
  top: 50%;
  right: var(--space-3);
  left: var(--space-3);
  height: 44px;
  transform: translateY(-50%);
  background-color: var(--color-surface);
  border-radius: var(--radius-sm);
  pointer-events: none;
}

.wheels__colon {
  z-index: 1;
  font-size: var(--font-size-title);
  font-weight: 700;
  color: var(--color-text-muted);
  pointer-events: none;
}

.wheel {
  z-index: 1;
  height: 100%;
  overflow-y: auto;
  /* 上下各補 (220 - 44) / 2，讓第一格與最後一格也能停在中央 */
  padding-block: 88px;
  scroll-snap-type: y mandatory;
  scrollbar-width: none;
  -webkit-overflow-scrolling: touch;
}

.wheel::-webkit-scrollbar {
  display: none;
}

.wheel__item {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 44px;
  font-size: var(--font-size-title);
  font-variant-numeric: tabular-nums;
  color: var(--color-text-muted);
  scroll-snap-align: center;
  scroll-snap-stop: always;
  transition:
    color var(--duration-fast) var(--ease-out),
    opacity var(--duration-fast) var(--ease-out);
  opacity: 0.45;
}

.wheel__item.is-active {
  font-weight: 700;
  color: var(--color-text);
  opacity: 1;
}

/* 關閉動態效果時不做透明度變化，避免辨識困難 */
@media (prefers-reduced-motion: reduce) {
  .wheel__item {
    opacity: 1;
  }
}

.sheet__actions {
  display: grid;
  grid-template-columns: 1fr 2fr;
  gap: var(--space-2);
  margin-top: var(--space-5);
}

.sheet__cancel,
.sheet__confirm {
  min-height: 52px;
  font-weight: 600;
  border-radius: var(--radius-full);
}

.sheet__cancel {
  color: var(--color-text);
  background-color: var(--color-bg);
}

.sheet__confirm {
  color: var(--color-surface);
  background-color: var(--color-accent);
}

/* --------------------------------------------------------------- */
/* 動畫                                                              */
/* --------------------------------------------------------------- */
.sheet-enter-active,
.sheet-leave-active {
  transition: opacity var(--duration-base) var(--ease-out);
}

.sheet-enter-active .sheet__panel,
.sheet-leave-active .sheet__panel {
  transition: transform var(--duration-base) var(--ease-out);
}

.sheet-enter-from,
.sheet-leave-to {
  opacity: 0;
}

.sheet-enter-from .sheet__panel,
.sheet-leave-to .sheet__panel {
  transform: translateY(100%);
}
</style>
