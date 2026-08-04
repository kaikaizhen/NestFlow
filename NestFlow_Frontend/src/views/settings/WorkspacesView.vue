<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import { api, type Workspace, type WorkspaceType } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'

const router = useRouter()
const { setDefaultWorkspaceId, refresh: refreshCurrentUser } = useAuth()
const { load: reloadActiveWorkspace } = useWorkspaces()

const workspaces = ref<Workspace[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const isCreating = ref(false)
const newName = ref('')
const newType = ref<WorkspaceType>('family')

const deletingId = ref<string | null>(null)

async function load() {
  isLoading.value = true
  errorMessage.value = ''

  try {
    workspaces.value = await api.listWorkspaces()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

async function setDefault(workspace: Workspace) {
  if (workspace.isDefault) {
    return
  }

  errorMessage.value = ''

  try {
    await api.setDefaultWorkspace(workspace.id)
    setDefaultWorkspaceId(workspace.id)
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '設定失敗。'
  }
}

async function create() {
  const name = newName.value.trim()
  if (!name) {
    return
  }

  isCreating.value = true
  errorMessage.value = ''

  try {
    await api.createWorkspace(name, newType.value)
    newName.value = ''
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '建立失敗。'
  } finally {
    isCreating.value = false
  }
}

function openMembers(workspace: Workspace) {
  router.push({ name: 'workspace-members', params: { workspaceId: workspace.id } })
}

async function remove(workspace: Workspace) {
  const confirmText = workspace.type === 'family'
    ? `確定要刪除「${workspace.name}」嗎？所有成員將立即失去存取權，記帳與行程資料不會再顯示。`
    : `確定要刪除「${workspace.name}」嗎？其中的記帳與行程資料不會再顯示。`

  if (!window.confirm(confirmText)) {
    return
  }

  deletingId.value = workspace.id
  errorMessage.value = ''

  try {
    await api.deleteWorkspace(workspace.id)

    // 後端可能已把使用者的預設資料空間改指向其他空間，這裡一併同步，
    // 避免刪除後畫面仍顯示已不存在的預設值。
    await refreshCurrentUser()
    await reloadActiveWorkspace(true)
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '刪除失敗。'
  } finally {
    deletingId.value = null
  }
}

onMounted(load)
</script>

<template>
  <AppSubPage title="資料空間">
    <AppMessage :text="errorMessage" tone="error" />

    <p v-if="isLoading" class="hint">載入中…</p>

    <ul v-else class="list">
      <li v-for="workspace in workspaces" :key="workspace.id" class="item">
        <button class="item__main" type="button" @click="setDefault(workspace)">
          <span class="item__name">
            {{ workspace.name }}
            <span class="item__tag">{{ workspace.type === 'family' ? '家庭' : '個人' }}</span>
          </span>

          <span class="item__meta">
            {{ workspace.membershipType === 'owner' ? '建立者' : '成員' }}
            <template v-if="workspace.isDefault"> ・預設</template>
          </span>
        </button>

        <button
          v-if="workspace.type === 'family'"
          class="item__members"
          type="button"
          aria-label="家庭成員"
          @click="openMembers(workspace)"
        >
          成員
        </button>

        <button
          v-if="workspace.membershipType === 'owner'"
          class="item__delete"
          type="button"
          aria-label="刪除資料空間"
          :disabled="deletingId === workspace.id"
          @click="remove(workspace)"
        >
          <svg
            viewBox="0 0 24 24"
            width="18"
            height="18"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M4 7h16M9 7V5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2m-8 0 1 13a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1l1-13" />
          </svg>
        </button>
      </li>
    </ul>

    <p class="hint">點選任一資料空間即可設為 LINE 訊息的預設寫入位置。</p>

    <form class="create" @submit.prevent="create">
      <h2 class="create__title">新增資料空間</h2>

      <label class="field">
        <span>名稱</span>
        <input v-model="newName" type="text" maxlength="50" placeholder="例如：我們家" />
      </label>

      <div class="segment" role="radiogroup" aria-label="類型">
        <button
          type="button"
          class="segment__item"
          :class="{ 'is-active': newType === 'personal' }"
          role="radio"
          :aria-checked="newType === 'personal'"
          @click="newType = 'personal'"
        >
          個人
        </button>

        <button
          type="button"
          class="segment__item"
          :class="{ 'is-active': newType === 'family' }"
          role="radio"
          :aria-checked="newType === 'family'"
          @click="newType = 'family'"
        >
          家庭
        </button>
      </div>

      <AppButton type="submit" :disabled="isCreating || !newName.trim()">
        {{ isCreating ? '建立中…' : '建立' }}
      </AppButton>
    </form>
  </AppSubPage>
</template>

<style scoped>
.list {
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

.item {
  display: flex;
  align-items: stretch;
  background-color: var(--color-surface);
}

.item__main {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 2px;
  align-items: flex-start;
  min-height: 60px;
  padding: var(--space-3) var(--space-4);
  text-align: left;
}

.item__main:active {
  background-color: var(--color-bg);
}

.item__name {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  font-weight: 600;
}

.item__tag {
  padding: 1px var(--space-2);
  font-size: 11px;
  font-weight: 500;
  color: var(--color-text-muted);
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.item__meta {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.item__members {
  padding: 0 var(--space-4);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  border-left: 1px solid var(--color-border);
}

.item__delete {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 52px;
  color: var(--color-text-muted);
  border-left: 1px solid var(--color-border);
  transition: color var(--duration-fast) var(--ease-out);
}

.item__delete:active {
  color: var(--color-expense);
}

.item__delete:disabled {
  opacity: 0.5;
}

.hint {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.create {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.create__title {
  margin: 0;
  font-size: var(--font-size-body);
  font-weight: 600;
}

.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.field input {
  min-height: 48px;
  padding: 0 var(--space-3);
  font: inherit;
  font-size: var(--font-size-body);
  color: var(--color-text);
  background-color: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
}

.segment {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--space-2);
}

.segment__item {
  min-height: 48px;
  font-weight: 500;
  color: var(--color-text-muted);
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
  transition:
    color var(--duration-fast) var(--ease-out),
    background-color var(--duration-fast) var(--ease-out);
}

.segment__item.is-active {
  color: var(--color-surface);
  background-color: var(--color-accent);
}
</style>
