<script setup lang="ts">
import type { CurrencySummary } from '../services/apiClient'
import { formatAmount } from '../utils/datetime'

defineProps<{ summaries: CurrencySummary[] }>()
</script>

<template>
  <div class="summary">
    <!-- 第一版不做匯率換算，主要幣別放大顯示，其他幣別列在下方 -->
    <div v-if="summaries.length" class="summary__primary">
      <div class="summary__cell">
        <span class="summary__label">總收入</span>
        <span class="summary__value summary__value--income">
          {{ formatAmount(summaries[0].income) }}
        </span>
      </div>

      <div class="summary__cell">
        <span class="summary__label">總支出</span>
        <span class="summary__value summary__value--expense">
          {{ formatAmount(summaries[0].expense) }}
        </span>
      </div>

      <div class="summary__cell">
        <span class="summary__label">結餘</span>
        <span class="summary__value">{{ formatAmount(summaries[0].balance) }}</span>
      </div>
    </div>

    <p v-if="summaries.length" class="summary__currency">{{ summaries[0].currency }}</p>

    <ul v-if="summaries.length > 1" class="summary__others">
      <li v-for="item in summaries.slice(1)" :key="item.currency">
        <span class="summary__others-currency">{{ item.currency }}</span>
        <span class="summary__others-value summary__value--income">
          +{{ formatAmount(item.income) }}
        </span>
        <span class="summary__others-value summary__value--expense">
          −{{ formatAmount(item.expense) }}
        </span>
        <span class="summary__others-value">{{ formatAmount(item.balance) }}</span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.summary {
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-card);
}

.summary__primary {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
}

.summary__cell {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: center;
  text-align: center;
}

.summary__cell + .summary__cell {
  border-left: 1px solid var(--color-border);
}

.summary__label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.summary__value {
  font-size: var(--font-size-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.summary__value--income {
  color: var(--color-income);
}

.summary__value--expense {
  color: var(--color-expense);
}

.summary__currency {
  margin: var(--space-3) 0 0;
  font-size: 11px;
  color: var(--color-text-muted);
  text-align: center;
}

.summary__others {
  margin: var(--space-3) 0 0;
  padding: var(--space-3) 0 0;
  list-style: none;
  border-top: 1px solid var(--color-border);
}

.summary__others li {
  display: grid;
  grid-template-columns: 44px 1fr 1fr 1fr;
  gap: var(--space-2);
  align-items: center;
  padding: var(--space-1) 0;
  font-size: var(--font-size-caption);
}

.summary__others-currency {
  font-weight: 600;
  color: var(--color-text-muted);
}

.summary__others-value {
  text-align: right;
}
</style>
