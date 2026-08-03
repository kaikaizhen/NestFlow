<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppPage from '../components/AppPage.vue'
import AppButton from '../components/AppButton.vue'
import AppMessage from '../components/AppMessage.vue'
import SettingsGroup from '../components/SettingsGroup.vue'
import SettingsRow from '../components/SettingsRow.vue'
import { api, type Workspace } from '../services/apiClient'
import { useAuth } from '../stores/auth'
import { useWorkspaces } from '../stores/workspace'

const router = useRouter()
const { currentUser, logout } = useAuth()
const { reset: resetWorkspaces } = useWorkspaces()

const workspaces = ref<Workspace[]>([])
const errorMessage = ref('')

const defaultWorkspaceName = computed(() => {
  const id = currentUser.value?.defaultWorkspaceId
  return workspaces.value.find((w) => w.id === id)?.name ?? '尚未設定'
})

const hasFamilyWorkspace = computed(() => workspaces.value.some((w) => w.type === 'family'))

const familyWorkspaceId = computed(
  () => workspaces.value.find((w) => w.type === 'family')?.id ?? null,
)

async function load() {
  try {
    workspaces.value = await api.listWorkspaces()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  }
}

async function signOut() {
  if (!window.confirm('確定要登出嗎？')) {
    return
  }

  try {
    await logout()
    // 清除記憶的資料空間，避免下一位使用者沿用到無權存取的空間
    resetWorkspaces()
    await router.replace({ name: 'login' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '登出失敗。'
  }
}

function goToMembers() {
  if (familyWorkspaceId.value) {
    router.push({ name: 'workspace-members', params: { workspaceId: familyWorkspaceId.value } })
  } else {
    router.push({ name: 'workspaces' })
  }
}

onMounted(load)
</script>

<template>
  <AppPage title="設定">
    <AppMessage :text="errorMessage" tone="error" />

    <div v-if="currentUser" class="profile">
      <img v-if="currentUser.pictureUrl" class="profile__avatar" :src="currentUser.pictureUrl" alt="" />
      <span v-else class="profile__avatar profile__avatar--empty" aria-hidden="true">
        {{ currentUser.displayName.slice(0, 1) }}
      </span>

      <span class="profile__info">
        <span class="profile__name">{{ currentUser.displayName }}</span>
        <span class="profile__meta">
          {{ currentUser.isLineLinked ? '已透過 LINE 登入' : '開發登入' }}
        </span>
      </span>
    </div>

    <SettingsGroup label="偏好設定">
      <SettingsRow label="深色模式" :chevron="false">
        <template #icon>
          <svg
            viewBox="0 0 24 24"
            width="22"
            height="22"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
          </svg>
        </template>

        <template #trailing>
          <!-- 第一版只保留入口，樣式待後續版本實作 -->
          <span class="toggle" aria-disabled="true">
            <span class="toggle__knob" />
          </span>
        </template>
      </SettingsRow>

      <RouterLink class="link" :to="{ name: 'line-binding' }">
        <SettingsRow
          label="LINE 綁定"
          :value="currentUser?.isLineMessagingLinked ? '已綁定' : '未綁定'"
          :disabled="false"
        >
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <circle cx="12" cy="12" r="9" />
              <path d="M8 10.5h8M8 14h5" />
            </svg>
          </template>
        </SettingsRow>
      </RouterLink>

      <RouterLink class="link" :to="{ name: 'workspaces' }">
        <SettingsRow label="預設資料空間" :value="defaultWorkspaceName" :disabled="false">
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <ellipse cx="12" cy="6" rx="8" ry="3" />
              <path d="M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3" />
            </svg>
          </template>
        </SettingsRow>
      </RouterLink>

      <RouterLink class="link" :to="{ name: 'timezone' }">
        <SettingsRow label="時區" :value="currentUser?.timeZone ?? ''" :disabled="false">
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <circle cx="12" cy="12" r="9" />
              <path d="M12 7v5l3 2" />
            </svg>
          </template>
        </SettingsRow>
      </RouterLink>

      <SettingsRow label="提醒通知" value="Module 6">
        <template #icon>
          <svg
            viewBox="0 0 24 24"
            width="22"
            height="22"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M18 9a6 6 0 1 0-12 0c0 5-2 6-2 6h16s-2-1-2-6M10.5 20a2 2 0 0 0 3 0" />
          </svg>
        </template>
      </SettingsRow>
    </SettingsGroup>

    <SettingsGroup label="家庭">
      <button class="link link--button" type="button" @click="goToMembers">
        <SettingsRow
          label="家庭成員"
          :value="hasFamilyWorkspace ? '' : '尚未建立家庭'"
          :disabled="false"
        >
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <circle cx="9" cy="8" r="3.2" />
              <path d="M3.5 19a5.5 5.5 0 0 1 11 0M16 6.2a3.2 3.2 0 0 1 0 6M17 14.2a5.5 5.5 0 0 1 3.5 4.8" />
            </svg>
          </template>
        </SettingsRow>
      </button>

      <button class="link link--button" type="button" @click="goToMembers">
        <SettingsRow label="產生邀請碼" :disabled="false">
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <circle cx="8.5" cy="12" r="3.5" />
              <path d="M12 12h9M17.5 12v3M20 12v2.4" />
            </svg>
          </template>
        </SettingsRow>
      </button>

      <RouterLink class="link" :to="{ name: 'join-family' }">
        <SettingsRow label="加入家庭" :disabled="false">
          <template #icon>
            <svg
              viewBox="0 0 24 24"
              width="22"
              height="22"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4 11 12 4l8 7M6 10v9h12v-9M12 12v4M10 14h4" />
            </svg>
          </template>
        </SettingsRow>
      </RouterLink>
    </SettingsGroup>

    <SettingsGroup label="其他">
      <SettingsRow label="關於" value="v0.2.0" :chevron="false">
        <template #icon>
          <svg
            viewBox="0 0 24 24"
            width="22"
            height="22"
            fill="none"
            stroke="currentColor"
            stroke-width="1.8"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="9" />
            <path d="M12 11v5M12 8h.01" />
          </svg>
        </template>
      </SettingsRow>
    </SettingsGroup>

    <AppButton variant="danger" @click="signOut">登出</AppButton>
  </AppPage>
</template>

<style scoped>
.profile {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-card);
}

.profile__avatar {
  flex-shrink: 0;
  width: 48px;
  height: 48px;
  object-fit: cover;
  border-radius: var(--radius-full);
}

.profile__avatar--empty {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--font-size-title);
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-bg);
}

.profile__info {
  display: flex;
  flex-direction: column;
}

.profile__name {
  font-size: var(--font-size-title);
  font-weight: 600;
}

.profile__meta {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.link {
  display: block;
  color: inherit;
  text-decoration: none;
}

.link--button {
  width: 100%;
  text-align: left;
}

.link:active {
  background-color: var(--color-bg);
}

.toggle {
  display: inline-flex;
  align-items: center;
  width: 48px;
  height: 28px;
  padding: 3px;
  background-color: var(--color-border);
  border-radius: var(--radius-full);
}

.toggle__knob {
  width: 22px;
  height: 22px;
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
  box-shadow: 0 1px 3px rgb(17 17 19 / 20%);
}
</style>
