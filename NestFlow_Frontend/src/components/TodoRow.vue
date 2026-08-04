<script setup lang="ts">
import { computed } from 'vue'
import type { Todo } from '../services/apiClient'
import { formatMonthDay, formatTime } from '../utils/datetime'

const props = defineProps<{
  todo: Todo
  timeZone: string
  /** 家庭資料空間才顯示建立者，個人空間顯示自己的名字沒有意義。 */
  showAuthor?: boolean
}>()

defineEmits<{ select: [todo: Todo]; toggle: [todo: Todo] }>()

const dueLabel = computed(() => {
  if (!props.todo.dueAt) {
    return ''
  }

  return `${formatMonthDay(props.todo.dueAt, props.timeZone)} ${formatTime(props.todo.dueAt, props.timeZone)}`
})

// 已完成的就算過了期限也不必再提醒，避免整片紅字
const isOverdue = computed(
  () => !props.todo.isCompleted && Boolean(props.todo.dueAt) && new Date(props.todo.dueAt!) < new Date(),
)
</script>

<template>
  <div class="row" :class="{ 'is-done': todo.isCompleted }">
    <button
      class="row__check"
      type="button"
      role="checkbox"
      :aria-checked="todo.isCompleted"
      :aria-label="todo.isCompleted ? `取消完成 ${todo.title}` : `完成 ${todo.title}`"
      @click="$emit('toggle', todo)"
    >
      <span class="row__box" :class="{ 'is-checked': todo.isCompleted }">
        <svg
          v-if="todo.isCompleted"
          viewBox="0 0 24 24"
          width="16"
          height="16"
          fill="none"
          stroke="currentColor"
          stroke-width="3"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="m5 12 4.5 4.5L19 7" />
        </svg>
      </span>
    </button>

    <button class="row__main" type="button" @click="$emit('select', todo)">
      <span class="row__body">
        <span class="row__title">{{ todo.title }}</span>

        <span v-if="dueLabel || showAuthor" class="row__meta">
          <span v-if="dueLabel" :class="{ 'is-overdue': isOverdue }">{{ dueLabel }}</span>
          <span v-if="showAuthor" class="row__author">{{ todo.createdByDisplayName }}</span>
        </span>
      </span>

      <span v-if="todo.quantity !== null" class="row__quantity">×{{ todo.quantity }}</span>
    </button>
  </div>
</template>

<style scoped>
.row {
  display: flex;
  align-items: center;
  width: 100%;
  min-height: 64px;
  background-color: var(--color-surface);
}

/* 勾選框是獨立的點擊區，點文字才是進入編輯 */
.row__check {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 56px;
  align-self: stretch;
}

.row__box {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  color: var(--color-surface);
  border: 2px solid var(--color-border);
  border-radius: var(--radius-full);
  transition:
    background-color var(--duration-fast) var(--ease-out),
    border-color var(--duration-fast) var(--ease-out),
    transform var(--duration-fast) var(--ease-out);
}

.row__box.is-checked {
  background-color: var(--color-accent);
  border-color: var(--color-accent);
}

.row__check:active .row__box {
  transform: scale(0.88);
}

.row__main {
  display: flex;
  flex: 1;
  gap: var(--space-3);
  align-items: center;
  min-width: 0;
  align-self: stretch;
  padding: var(--space-3) var(--space-4) var(--space-3) 0;
  text-align: left;
}

.row__main:active {
  background-color: var(--color-bg);
}

.row__body {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.row__title {
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
  transition: color var(--duration-base) var(--ease-out);
}

.row.is-done .row__title {
  color: var(--color-text-muted);
  text-decoration: line-through;
}

.row__meta {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.row__meta .is-overdue {
  font-weight: 600;
  color: var(--color-expense);
}

.row__author {
  padding: 0 var(--space-2);
  font-size: 11px;
  line-height: 18px;
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.row__quantity {
  flex-shrink: 0;
  font-weight: 700;
  letter-spacing: -0.01em;
}

.row.is-done .row__quantity {
  color: var(--color-text-muted);
}
</style>
