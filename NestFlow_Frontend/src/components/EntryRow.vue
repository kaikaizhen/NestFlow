<script setup lang="ts">
import { computed } from 'vue'
import type { AccountEntry } from '../services/apiClient'
import { categoryStyle } from '../utils/categories'
import { formatAmount, formatMonthDay } from '../utils/datetime'

const props = defineProps<{
  entry: AccountEntry
  categoryLabel: string
  timeZone: string
  /** 家庭資料空間才顯示記帳者，個人空間顯示自己的名字沒有意義。 */
  showAuthor?: boolean
}>()

defineEmits<{ select: [entry: AccountEntry] }>()

const style = computed(() => categoryStyle(props.entry.category))
const isIncome = computed(() => props.entry.type === 'income')
</script>

<template>
  <button class="row" type="button" @click="$emit('select', entry)">
    <span class="row__icon" :style="{ backgroundColor: style.tint }" aria-hidden="true">
      <svg
        viewBox="0 0 24 24"
        width="20"
        height="20"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path :d="style.path" />
      </svg>
    </span>

    <span class="row__body">
      <span class="row__title">{{ entry.note || categoryLabel }}</span>
      <span class="row__meta">
        {{ formatMonthDay(entry.occurredAt, timeZone) }}
        <template v-if="entry.note">・{{ categoryLabel }}</template>
        <span v-if="showAuthor" class="row__author">{{ entry.createdByDisplayName }}</span>
      </span>
    </span>

    <span class="row__amount" :class="isIncome ? 'is-income' : 'is-expense'">
      {{ isIncome ? '' : '−' }}{{ formatAmount(entry.amount) }}
      <span class="row__currency">{{ entry.currency }}</span>
    </span>
  </button>
</template>

<style scoped>
.row {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  width: 100%;
  min-height: 68px;
  padding: var(--space-3) var(--space-4);
  background-color: var(--color-surface);
  text-align: left;
}

.row:active {
  background-color: var(--color-bg);
}

.row__icon {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  color: var(--color-text);
  border-radius: var(--radius-full);
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
}

.row__meta {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.row__author {
  padding: 0 var(--space-2);
  font-size: 11px;
  line-height: 18px;
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.row__amount {
  flex-shrink: 0;
  font-weight: 700;
  letter-spacing: -0.01em;
}

.row__amount.is-income {
  color: var(--color-income);
}

.row__amount.is-expense {
  color: var(--color-expense);
}

.row__currency {
  font-size: 11px;
  font-weight: 500;
  color: var(--color-text-muted);
}
</style>
