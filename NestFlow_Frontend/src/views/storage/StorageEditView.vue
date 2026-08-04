<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import AppSubPage from '../../components/AppSubPage.vue'
import { api, type SaveStorageItemPayload } from '../../services/apiClient'
import { useWorkspaces } from '../../stores/workspace'

const route = useRoute()
const router = useRouter()
const { active, workspaces, load: loadWorkspaces } = useWorkspaces()

const itemId = computed(() => route.params.itemId as string | undefined)
const isEditing = computed(() => Boolean(itemId.value))

const name = ref('')
const location = ref('')
const note = ref('')

/** 要存放的資料空間。新增時可選，修改時固定為原本所屬的空間。 */
const selectedWorkspaceId = ref('')

const authorName = ref('')
const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')

// 家庭空間的既有紀錄才需要標示是誰建立的
const showAuthor = computed(
  () => isEditing.value && active.value?.type === 'family' && Boolean(authorName.value),
)

const canSave = computed(
  () =>
    Boolean(name.value.trim()) &&
    Boolean(location.value.trim()) &&
    Boolean(isEditing.value ? active.value : selectedWorkspaceId.value),
)

async function loadExisting() {
  if (!itemId.value) {
    return
  }

  const found = await api.getStorageItem(itemId.value)

  authorName.value = found.createdByDisplayName
  name.value = found.name
  location.value = found.location
  note.value = found.note ?? ''
}

/** 儲存或刪除後都回到儲藏庫分頁，而不是預設的待辦分頁。 */
function backToStorage() {
  return router.replace({ name: 'life', query: { tab: 'storage' } })
}

async function save() {
  const workspaceId = isEditing.value ? active.value?.id : selectedWorkspaceId.value

  if (!canSave.value || !workspaceId) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''

  const payload: SaveStorageItemPayload = {
    workspaceId,
    name: name.value.trim(),
    location: location.value.trim(),
    note: note.value.trim() || null,
  }

  try {
    if (itemId.value) {
      await api.updateStorageItem(itemId.value, payload)
    } else {
      await api.createStorageItem(payload)
    }

    await backToStorage()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '儲存失敗。'
  } finally {
    isSaving.value = false
  }
}

async function remove() {
  if (!itemId.value || !window.confirm('確定要刪除這筆物品紀錄嗎？')) {
    return
  }

  errorMessage.value = ''

  try {
    await api.deleteStorageItem(itemId.value)
    await backToStorage()
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
  <AppSubPage :title="isEditing ? '編輯物品' : '新增物品'">
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

      <label class="field">
        <span class="field__label">物品名稱</span>
        <input v-model="name" type="text" maxlength="100" placeholder="例如：電鑽" required />
      </label>

      <label class="field">
        <span class="field__label">存放位置</span>
        <input
          v-model="location"
          type="text"
          maxlength="100"
          placeholder="例如：陽台工具箱"
          required
        />
      </label>

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
</style>
