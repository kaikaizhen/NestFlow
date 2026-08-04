<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import DateTimePicker from '../../components/DateTimePicker.vue'
import {
  api,
  type Category,
  type EntryType,
  type SaveAccountEntryPayload,
} from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'
import { categoryStyle } from '../../utils/categories'
import { isoToLocalInput, localInputToIso, todayInZone } from '../../utils/datetime'

const CURRENCIES = ['TWD', 'USD', 'JPY', 'EUR', 'CNY']

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()
const { active, workspaces, load: loadWorkspaces } = useWorkspaces()

const entryId = computed(() => route.params.entryId as string | undefined)
const isEditing = computed(() => Boolean(entryId.value))
const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const type = ref<EntryType>('expense')
const amount = ref('')
const currency = ref('TWD')
const category = ref('')
const note = ref('')
const occurredAtLocal = ref('')

/** 要存放的資料空間。新增時可選，修改時固定為原本所屬的空間。 */
const selectedWorkspaceId = ref('')

const authorName = ref('')
const categories = ref<Category[]>([])
const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')

const visibleCategories = computed(() => categories.value.filter((c) => c.type === type.value))

// 家庭空間的既有記帳才需要標示是誰建立的
const showAuthor = computed(
  () => isEditing.value && active.value?.type === 'family' && Boolean(authorName.value),
)

const canSave = computed(
  () =>
    Number(amount.value) > 0 &&
    Boolean(category.value) &&
    Boolean(occurredAtLocal.value) &&
    Boolean(isEditing.value ? active.value : selectedWorkspaceId.value),
)

/** 切換收支時，原分類若不屬於新類型就改選第一個。 */
function switchType(next: EntryType) {
  type.value = next

  if (!visibleCategories.value.some((c) => c.code === category.value)) {
    category.value = visibleCategories.value[0]?.code ?? ''
  }
}

function defaultOccurredAt() {
  const now = todayInZone(timeZone.value)
  const time = new Intl.DateTimeFormat('en-GB', {
    timeZone: timeZone.value,
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date())

  const month = String(now.month).padStart(2, '0')
  const day = String(now.day).padStart(2, '0')

  return `${now.year}-${month}-${day}T${time}`
}

async function loadExisting() {
  if (!entryId.value) {
    return
  }

  const found = await api.getEntry(entryId.value)

  authorName.value = found.createdByDisplayName
  type.value = found.type
  amount.value = String(found.amount)
  currency.value = found.currency
  category.value = found.category
  note.value = found.note ?? ''
  occurredAtLocal.value = isoToLocalInput(found.occurredAt, timeZone.value)
}

async function save() {
  const workspaceId = isEditing.value ? active.value?.id : selectedWorkspaceId.value

  if (!canSave.value || !workspaceId) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''

  const payload: SaveAccountEntryPayload = {
    workspaceId,
    type: type.value,
    amount: Number(amount.value),
    currency: currency.value,
    category: category.value,
    note: note.value.trim() || null,
    occurredAt: localInputToIso(occurredAtLocal.value, timeZone.value),
  }

  try {
    if (entryId.value) {
      await api.updateEntry(entryId.value, payload)
    } else {
      await api.createEntry(payload)
    }

    await router.replace({ name: 'ledger' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '儲存失敗。'
  } finally {
    isSaving.value = false
  }
}

async function remove() {
  if (!entryId.value || !window.confirm('確定要刪除這筆記帳嗎？')) {
    return
  }

  errorMessage.value = ''

  try {
    await api.deleteEntry(entryId.value)
    await router.replace({ name: 'ledger' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '刪除失敗。'
  }
}

onMounted(async () => {
  try {
    await loadWorkspaces()
    categories.value = await api.listCategories()

    if (isEditing.value) {
      await loadExisting()
    } else {
      category.value = visibleCategories.value[0]?.code ?? ''
      occurredAtLocal.value = defaultOccurredAt()
      selectedWorkspaceId.value = active.value?.id ?? workspaces.value[0]?.id ?? ''
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <AppSubPage :title="isEditing ? '編輯記帳' : '新增記帳'">
    <AppMessage :text="errorMessage" tone="error" />

    <p v-if="isLoading" class="hint">載入中…</p>

    <p v-else-if="showAuthor" class="author">由 {{ authorName }} 建立</p>

    <form v-if="!isLoading" class="form" @submit.prevent="save">
      <label v-if="!isEditing" class="field">
        <span class="field__label">資料空間</span>
        <select v-model="selectedWorkspaceId">
          <option v-for="ws in workspaces" :key="ws.id" :value="ws.id">
            {{ ws.name }}（{{ ws.type === 'family' ? '家庭' : '個人' }}）
          </option>
        </select>
      </label>

      <div class="segment" role="radiogroup" aria-label="類型">
        <button
          type="button"
          class="segment__item"
          :class="{ 'is-active is-expense': type === 'expense' }"
          role="radio"
          :aria-checked="type === 'expense'"
          @click="switchType('expense')"
        >
          支出
        </button>

        <button
          type="button"
          class="segment__item"
          :class="{ 'is-active is-income': type === 'income' }"
          role="radio"
          :aria-checked="type === 'income'"
          @click="switchType('income')"
        >
          收入
        </button>
      </div>

      <label class="amount">
        <span class="amount__label">金額</span>
        <span class="amount__row">
          <input
            v-model="amount"
            class="amount__input"
            type="number"
            inputmode="decimal"
            step="0.01"
            min="0.01"
            placeholder="0"
            required
          />
          <select v-model="currency" class="amount__currency" aria-label="幣別">
            <option v-for="item in CURRENCIES" :key="item" :value="item">{{ item }}</option>
          </select>
        </span>
      </label>

      <fieldset class="categories">
        <legend class="field__label">分類</legend>

        <div class="categories__grid">
          <button
            v-for="item in visibleCategories"
            :key="item.code"
            type="button"
            class="categories__item"
            :class="{ 'is-active': category === item.code }"
            @click="category = item.code"
          >
            <span
              class="categories__icon"
              :style="{ backgroundColor: categoryStyle(item.code).tint }"
              aria-hidden="true"
            >
              <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <path :d="categoryStyle(item.code).path" />
              </svg>
            </span>
            <span class="categories__label">{{ item.label }}</span>
          </button>
        </div>
      </fieldset>

      <DateTimePicker v-model="occurredAtLocal" :time-zone="timeZone" label="時間" />

      <label class="field">
        <span class="field__label">備註</span>
        <input v-model="note" type="text" maxlength="200" placeholder="選填" />
      </label>

      <AppButton type="submit" :disabled="!canSave || isSaving">
        {{ isSaving ? '儲存中…' : '儲存' }}
      </AppButton>

      <AppButton v-if="isEditing" variant="danger" @click="remove">刪除</AppButton>
    </form>
  </AppSubPage>
</template>

<style scoped>
.form {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.hint {
  margin: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.author {
  margin: 0;
  padding: var(--space-3) var(--space-4);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.segment {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-2);
}

.segment__item {
  min-height: 48px;
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-sm);
  transition:
    color var(--duration-fast) var(--ease-out),
    background-color var(--duration-fast) var(--ease-out);
}

.segment__item.is-active.is-expense {
  color: var(--color-surface);
  background-color: var(--color-expense);
}

.segment__item.is-active.is-income {
  color: var(--color-surface);
  background-color: var(--color-income);
}

.amount,
.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.amount__label,
.field__label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.amount__row {
  display: flex;
  gap: var(--space-3);
  align-items: center;
}

.amount__input {
  flex: 1;
  min-width: 0;
  font: inherit;
  font-size: 32px;
  font-weight: 700;
  color: var(--color-text);
  background: none;
  border: none;
  outline: none;
}

.amount__currency,
.field input,
.field select {
  min-height: 48px;
  padding: 0 var(--space-3);
  font: inherit;
  font-size: var(--font-size-body);
  color: var(--color-text);
  background-color: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
}

.amount__currency {
  flex-shrink: 0;
}

.categories {
  margin: 0;
  padding: var(--space-4);
  background-color: var(--color-surface);
  border: none;
  border-radius: var(--radius-md);
}

.categories__grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--space-3);
  margin-top: var(--space-3);
}

.categories__item {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: center;
  padding: var(--space-2) 0;
  border-radius: var(--radius-sm);
  transition: background-color var(--duration-fast) var(--ease-out);
}

.categories__item.is-active {
  background-color: var(--color-bg);
}

.categories__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: var(--radius-full);
}

.categories__item.is-active .categories__icon {
  box-shadow: 0 0 0 2px var(--color-accent);
}

.categories__label {
  font-size: 12px;
  color: var(--color-text-muted);
}

.categories__item.is-active .categories__label {
  font-weight: 600;
  color: var(--color-text);
}
</style>
