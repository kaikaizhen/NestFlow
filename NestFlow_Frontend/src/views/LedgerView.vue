<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppMessage from '../components/AppMessage.vue'
import EntryRow from '../components/EntryRow.vue'
import SummaryCard from '../components/SummaryCard.vue'
import WorkspaceSwitcher from '../components/WorkspaceSwitcher.vue'
import {
  api,
  type AccountEntry,
  type Category,
  type CurrencySummary,
} from '../services/apiClient'
import { useAuth } from '../stores/auth'
import { useWorkspaces } from '../stores/workspace'
import { formatYearMonth, monthRangeUtc, todayInZone } from '../utils/datetime'

const router = useRouter()
const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const today = todayInZone(timeZone.value)
const year = ref(today.year)
const month = ref(today.month)

const entries = ref<AccountEntry[]>([])
const summaries = ref<CurrencySummary[]>([])
const categories = ref<Category[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const monthLabel = computed(() => formatYearMonth(year.value, month.value))

// 家庭空間才需要區分是誰記的帳
const isFamilyWorkspace = computed(() => active.value?.type === 'family')

function categoryLabel(code: string) {
  return categories.value.find((c) => c.code === code)?.label ?? code
}

function shiftMonth(delta: number) {
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

async function load() {
  if (!active.value) {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  const { fromUtc, toUtc } = monthRangeUtc(year.value, month.value, timeZone.value)

  try {
    const [entryList, summaryList] = await Promise.all([
      api.listEntries(active.value.id, fromUtc, toUtc, 20),
      api.summarize(active.value.id, fromUtc, toUtc),
    ])

    entries.value = entryList
    summaries.value = summaryList
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

function openEntry(entry: AccountEntry) {
  router.push({ name: 'entry-edit', params: { entryId: entry.id } })
}

function createEntry() {
  router.push({ name: 'entry-create' })
}

onMounted(async () => {
  try {
    await loadWorkspaces()
    categories.value = await api.listCategories()
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
    isLoading.value = false
  }
})

// 切換資料空間或月份時重新載入。
// 從新增或編輯頁返回時本元件會重新掛載，因此 onMounted 已涵蓋重新整理。
watch([active, year, month], load)
</script>

<template>
  <section class="ledger">
    <header class="ledger__header">
      <h1 class="ledger__title">記帳</h1>
      <WorkspaceSwitcher />
    </header>

    <div class="ledger__month">
      <button class="ledger__month-btn" type="button" aria-label="上個月" @click="shiftMonth(-1)">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="m15 6-6 6 6 6" />
        </svg>
      </button>

      <span class="ledger__month-label">{{ monthLabel }}</span>

      <button class="ledger__month-btn" type="button" aria-label="下個月" @click="shiftMonth(1)">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="m9 6 6 6-6 6" />
        </svg>
      </button>
    </div>

    <AppMessage :text="errorMessage" tone="error" />

    <SummaryCard :summaries="summaries" />

    <div class="ledger__section-head">
      <h2 class="ledger__section-title">最近交易</h2>
    </div>

    <p v-if="isLoading" class="ledger__hint">載入中…</p>

    <p v-else-if="!entries.length" class="ledger__hint">這個月還沒有記帳紀錄。</p>

    <ul v-else class="ledger__list">
      <li v-for="entry in entries" :key="entry.id">
        <EntryRow
          :entry="entry"
          :category-label="categoryLabel(entry.category)"
          :time-zone="timeZone"
          :show-author="isFamilyWorkspace"
          @select="openEntry"
        />
      </li>
    </ul>

    <button class="ledger__create" type="button" @click="createEntry">
      <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
      新增記帳
    </button>
  </section>
</template>

<style scoped>
.ledger {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: calc(var(--space-6) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + var(--space-6) + env(safe-area-inset-bottom));
}

.ledger__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.ledger__title {
  margin: 0;
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.ledger__month {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  justify-content: center;
}

.ledger__month-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  color: var(--color-text-muted);
  border-radius: var(--radius-full);
}

.ledger__month-btn:active {
  background-color: var(--color-border);
}

.ledger__month-label {
  min-width: 110px;
  font-weight: 600;
  text-align: center;
}

.ledger__section-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--space-1);
}

.ledger__section-title {
  margin: 0;
  font-size: var(--font-size-body);
  font-weight: 600;
}

.ledger__hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.ledger__list {
  display: flex;
  flex-direction: column;
  gap: 1px;
  margin: 0;
  padding: 0;
  overflow: hidden;
  list-style: none;
  background-color: var(--color-border);
  border-radius: var(--radius-md);
}

.ledger__create {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  justify-content: center;
  min-height: 56px;
  margin-top: var(--space-1);
  font-weight: 600;
  color: var(--color-surface);
  background-color: var(--color-accent);
  border-radius: var(--radius-full);
  transition: transform var(--duration-fast) var(--ease-out);
}

.ledger__create:active {
  transform: scale(0.98);
}
</style>
