<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppMessage from '../components/AppMessage.vue'
import EntryRow from '../components/EntryRow.vue'
import SummaryCard from '../components/SummaryCard.vue'
import { useAutoRefresh } from '../composables/useAutoRefresh'
import {
  api,
  type AccountEntry,
  type Category,
  type CurrencySummary,
  type SettlementSummary,
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
const settlement = ref<SettlementSummary>({ toReceive: [], toPay: [] })
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
    const [entryList, summaryList, settlementResult] = await Promise.all([
      api.listEntries(active.value.id, fromUtc, toUtc, 20),
      api.summarize(active.value.id, fromUtc, toUtc),
      isFamilyWorkspace.value
        ? api.getSettlement(active.value.id, fromUtc, toUtc)
        : Promise.resolve({ toReceive: [], toPay: [] }),
    ])

    entries.value = entryList
    summaries.value = summaryList
    settlement.value = settlementResult
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

async function closeSettlement() {
  if (!active.value || !window.confirm('確認已依照以上金額完成收付款，並將本月分攤標記為已結清？')) return
  const { fromUtc, toUtc } = monthRangeUtc(year.value, month.value, timeZone.value)
  try {
    await api.closeSettlement(active.value.id, fromUtc, toUtc)
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '結清失敗，請稍後再試。'
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

// 家庭成員新增或修改記帳時，這裡不會即時收到通知，
// 靠定時輪詢與切回頁面時補抓一次來縮短看到最新資料的延遲
useAutoRefresh(load)
</script>

<template>
  <section class="ledger">
    <header class="ledger__header">
      <h1 class="ledger__title">記帳</h1>
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

    <section v-if="isFamilyWorkspace" class="settlement">
      <div class="settlement__head">
        <h2>本月分攤結算</h2>
        <button
          v-if="settlement.toReceive.length || settlement.toPay.length"
          type="button"
          @click="closeSettlement"
        >標記已結清</button>
      </div>
      <p v-if="!settlement.toReceive.length && !settlement.toPay.length" class="settlement__empty">目前沒有未結清的分攤款項。</p>
      <template v-else>
        <div v-if="settlement.toReceive.length" class="settlement__group receive">
          <strong>我應收</strong>
          <p v-for="item in settlement.toReceive" :key="`receive-${item.counterpartyName}-${item.currency}`">{{ item.counterpartyName }} 應付我 {{ item.currency }} {{ item.amount }}</p>
        </div>
        <div v-if="settlement.toPay.length" class="settlement__group pay">
          <strong>我應付</strong>
          <p v-for="item in settlement.toPay" :key="`pay-${item.counterpartyName}-${item.currency}`">我應付 {{ item.counterpartyName }} {{ item.currency }} {{ item.amount }}</p>
        </div>
      </template>
      <p class="settlement__note">已先抵銷彼此款項，轉帳金額採四捨五入。</p>
    </section>

    <div class="ledger__section-head">
      <h2 class="ledger__section-title">最近交易</h2>
    </div>

    <div class="ledger__scroll">
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
    </div>

    <button class="ledger__fab" type="button" aria-label="新增記帳" @click="createEntry">
      <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
    </button>
  </section>
</template>

<style scoped>
.ledger {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  /* 佔滿視窗並扣掉底部導航，讓月份與收支摘要固定、只有交易清單捲動 */
  height: 100vh;
  height: 100dvh;
  padding: calc(var(--space-6) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + env(safe-area-inset-bottom));
}

.ledger__scroll {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-4);
  /* flex 子項預設 min-height 為 auto，不歸零就不會出現捲軸 */
  min-height: 0;
  /* 捲到底時的留白，最後一筆才不會被浮動按鈕蓋住 */
  padding-bottom: calc(var(--space-4) + 56px + var(--space-3));
  overflow-y: auto;
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

.settlement {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
  background: var(--color-surface);
  border-radius: var(--radius-md);
}

.settlement__head { display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); }
.settlement__head h2 { margin: 0; font-size: var(--font-size-body); }
.settlement__head button { padding: var(--space-2) var(--space-3); color: var(--color-surface); background: var(--color-accent); border-radius: var(--radius-sm); }
.settlement__group { padding: var(--space-2) var(--space-3); border-radius: var(--radius-sm); }
.settlement__group p, .settlement__empty, .settlement__note { margin: var(--space-1) 0; font-size: var(--font-size-caption); }
.receive { background: color-mix(in srgb, var(--color-income) 12%, var(--color-surface)); }
.pay { background: color-mix(in srgb, var(--color-expense) 10%, var(--color-surface)); }
.settlement__note, .settlement__empty { color: var(--color-text-muted); }

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
  /* overflow 非 visible 的 flex 子項，min-height: auto 會解析為 0，
     清單會被壓縮成剛好塞滿捲動容器而不是撐出高度，導致捲不動、
     且塞不下的項目被直接裁掉。flex-shrink: 0 保住內容高度。 */
  flex-shrink: 0;
  overflow: hidden;
  list-style: none;
  background-color: var(--color-border);
  border-radius: var(--radius-md);
}

/* 用 absolute 而非 fixed：頁面本身已是視窗高度且不捲動，效果相同，
   但定位基準是內容欄而不是整個視窗，桌機時才會貼齊內容右緣；
   也不會在頁面切換動畫（祖先有 transform）期間跳位。 */
.ledger__fab {
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

.ledger__fab:active {
  transform: scale(0.94);
}
</style>
