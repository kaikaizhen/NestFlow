<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import AppSubPage from '../../components/AppSubPage.vue'
import DateTimePicker from '../../components/DateTimePicker.vue'
import { api, type SaveTodoPayload, type TodoType } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'
import { isoToLocalInput, localInputToIso, todayInZone } from '../../utils/datetime'

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()
const { active, workspaces, load: loadWorkspaces } = useWorkspaces()

const todoId = computed(() => route.params.todoId as string | undefined)
const isEditing = computed(() => Boolean(todoId.value))
const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const type = ref<TodoType>('general')
const title = ref('')
const quantity = ref('1')

/** 到期時間為選填，關閉時儲存為 null。 */
const wantsDueAt = ref(false)
const dueAtLocal = ref('')

/** 要存放的資料空間。新增時可選，修改時固定為原本所屬的空間。 */
const selectedWorkspaceId = ref('')

const authorName = ref('')
const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')

// 家庭空間的既有代辦才需要標示是誰建立的
const showAuthor = computed(
  () => isEditing.value && active.value?.type === 'family' && Boolean(authorName.value),
)

const canSave = computed(
  () =>
    Boolean(title.value.trim()) &&
    (!wantsDueAt.value || Boolean(dueAtLocal.value)) &&
    (type.value !== 'shopping' || Number(quantity.value) >= 1) &&
    Boolean(isEditing.value ? active.value : selectedWorkspaceId.value),
)

/** 預設到期時間為今天稍晚，避免一打開就是已過期的時間。 */
function defaultDueAt() {
  const today = todayInZone(timeZone.value)
  const month = String(today.month).padStart(2, '0')
  const day = String(today.day).padStart(2, '0')

  return `${today.year}-${month}-${day}T18:00`
}

function enableDueAt() {
  if (!dueAtLocal.value) {
    dueAtLocal.value = defaultDueAt()
  }
}

async function loadExisting() {
  if (!todoId.value) {
    return
  }

  const found = await api.getTodo(todoId.value)

  authorName.value = found.createdByDisplayName
  type.value = found.type
  title.value = found.title
  quantity.value = String(found.quantity ?? 1)
  wantsDueAt.value = Boolean(found.dueAt)
  dueAtLocal.value = found.dueAt ? isoToLocalInput(found.dueAt, timeZone.value) : ''
}

/** 儲存或刪除後回到原本的分頁，而不是一律跳回待辦分頁。 */
function backToList() {
  return router.replace({
    name: 'life',
    query: type.value === 'general' ? {} : { tab: type.value },
  })
}

async function save() {
  const workspaceId = isEditing.value ? active.value?.id : selectedWorkspaceId.value

  if (!canSave.value || !workspaceId) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''

  const payload: SaveTodoPayload = {
    workspaceId,
    type: type.value,
    title: title.value.trim(),
    quantity: type.value === 'shopping' ? Number(quantity.value) : null,
    dueAt: wantsDueAt.value ? localInputToIso(dueAtLocal.value, timeZone.value) : null,
  }

  try {
    if (todoId.value) {
      await api.updateTodo(todoId.value, payload)
    } else {
      await api.createTodo(payload)
    }

    await backToList()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '儲存失敗。'
  } finally {
    isSaving.value = false
  }
}

async function remove() {
  if (!todoId.value || !window.confirm('確定要刪除這筆代辦嗎？')) {
    return
  }

  errorMessage.value = ''

  try {
    await api.deleteTodo(todoId.value)
    await backToList()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '刪除失敗。'
  }
}

onMounted(async () => {
  try {
    await loadWorkspaces()

    if (isEditing.value) {
      await loadExisting()
    } else {
      // 從代辦頁的哪一個分頁按新增，就預設建立哪一種類型
      type.value = route.query.type === 'shopping' ? 'shopping' : 'general'
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
  <AppSubPage :title="isEditing ? '編輯代辦' : '新增代辦'">
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
          :class="{ 'is-active': type === 'general' }"
          role="radio"
          :aria-checked="type === 'general'"
          @click="type = 'general'"
        >
          待辦事項
        </button>

        <button
          type="button"
          class="segment__item"
          :class="{ 'is-active': type === 'shopping' }"
          role="radio"
          :aria-checked="type === 'shopping'"
          @click="type = 'shopping'"
        >
          購物清單
        </button>
      </div>

      <label class="field">
        <span class="field__label">{{ type === 'shopping' ? '品項' : '內容' }}</span>
        <input
          v-model="title"
          type="text"
          maxlength="100"
          :placeholder="type === 'shopping' ? '例如：牛奶' : '例如：繳電費'"
          required
        />
      </label>

      <label v-if="type === 'shopping'" class="field">
        <span class="field__label">數量</span>
        <input v-model="quantity" type="number" inputmode="numeric" min="1" max="9999" step="1" />
      </label>

      <div class="toggle-card">
        <label class="toggle-card__row">
          <span class="toggle-card__label">設定到期時間</span>
          <input
            v-model="wantsDueAt"
            type="checkbox"
            class="toggle-card__checkbox"
            @change="enableDueAt"
          />
        </label>

        <div v-if="wantsDueAt" class="toggle-card__sub">
          <DateTimePicker v-model="dueAtLocal" :time-zone="timeZone" label="到期時間" />
        </div>
      </div>

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

.segment__item.is-active {
  color: var(--color-surface);
  background-color: var(--color-accent);
}

.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.field__label {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

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

.toggle-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.toggle-card__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.toggle-card__label {
  font-weight: 600;
}

.toggle-card__checkbox {
  width: 22px;
  height: 22px;
  accent-color: var(--color-accent);
}

.toggle-card__sub {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding-top: var(--space-3);
  border-top: 1px solid var(--color-border);
}
</style>
