<script setup lang="ts">
import { computed } from 'vue'
import type { CalendarEvent } from '../services/apiClient'
import { dayKeyInZone, formatTime, formatWeekday } from '../utils/datetime'

const props = defineProps<{
  event: CalendarEvent
  timeZone: string
  /** 左側色條顏色。家庭空間依建立者、個人空間依行程決定，由上層算好傳入。 */
  color: string
  /** 使用者時區下的今天，用來標示「今天」與「明天」。 */
  todayKey: string
  tomorrowKey: string
  /** 家庭資料空間才顯示建立者。 */
  showAuthor?: boolean
}>()

defineEmits<{ select: [event: CalendarEvent] }>()

const startKey = computed(() => dayKeyInZone(props.event.startAt, props.timeZone))

/** 日期標籤：今天／明天／M/D，第二行補星期。 */
const dayLabel = computed(() => {
  if (startKey.value === props.todayKey) {
    return '今天'
  }

  if (startKey.value === props.tomorrowKey) {
    return '明天'
  }

  return formatWeekday(props.event.startAt, props.timeZone)
})

const dayNumber = computed(() => {
  const [, month, day] = startKey.value.split('-')

  return `${Number(month)}/${Number(day)}`
})

const timeRange = computed(
  () =>
    `${formatTime(props.event.startAt, props.timeZone)} – ${formatTime(props.event.endAt, props.timeZone)}`,
)

/** 跨日行程要標出結束日，只顯示時間會誤導。 */
const isMultiDay = computed(
  () => startKey.value !== dayKeyInZone(props.event.endAt, props.timeZone),
)
</script>

<template>
  <button class="row" type="button" @click="$emit('select', event)">
    <span class="row__day">
      <span class="row__day-label">{{ dayLabel }}</span>
      <span class="row__day-number">{{ dayNumber }}</span>
    </span>

    <span class="row__bar" :style="{ backgroundColor: color }" aria-hidden="true" />

    <span class="row__body">
      <span class="row__title-row">
        <span class="row__title">{{ event.title }}</span>
        <svg
          v-if="event.hasReminder"
          class="row__reminder-icon"
          viewBox="0 0 24 24"
          width="15"
          height="15"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-label="已設定提醒"
        >
          <path d="M18 9a6 6 0 1 0-12 0c0 5-2 6-2 6h16s-2-1-2-6M10.5 20a2 2 0 0 0 3 0" />
        </svg>
      </span>
      <span class="row__meta">
        <span>{{ timeRange }}</span>
        <span v-if="isMultiDay" class="row__tag">跨日</span>
        <span v-if="event.description" class="row__desc">{{ event.description }}</span>
        <span v-if="showAuthor" class="row__author">{{ event.createdByDisplayName }}</span>
      </span>
    </span>

    <svg
      class="row__chevron"
      viewBox="0 0 24 24"
      width="18"
      height="18"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <path d="m9 6 6 6-6 6" />
    </svg>
  </button>
</template>

<style scoped>
.row {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  width: 100%;
  min-height: 72px;
  padding: var(--space-3) var(--space-4);
  background-color: var(--color-surface);
  text-align: left;
}

.row:active {
  background-color: var(--color-bg);
}

.row__day {
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  align-items: center;
  width: 44px;
}

.row__day-label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.row__day-number {
  font-weight: 700;
}

.row__bar {
  flex-shrink: 0;
  width: 3px;
  align-self: stretch;
  margin: var(--space-1) 0;
  border-radius: var(--radius-full);
}

.row__body {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.row__title-row {
  display: flex;
  gap: var(--space-1);
  align-items: center;
  min-width: 0;
}

.row__title {
  overflow: hidden;
  min-width: 0;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row__reminder-icon {
  flex-shrink: 0;
  color: var(--color-text-muted);
}

.row__meta {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  min-width: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.row__desc {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row__tag,
.row__author {
  flex-shrink: 0;
  padding: 0 var(--space-2);
  font-size: 11px;
  line-height: 18px;
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.row__chevron {
  flex-shrink: 0;
  color: var(--color-text-muted);
}
</style>
