<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import AppPage from '../components/AppPage.vue'
import SettingsGroup from '../components/SettingsGroup.vue'
import SettingsRow from '../components/SettingsRow.vue'
import { getApiHealth } from '../services/apiClient'

type ApiState = 'loading' | 'online' | 'offline'

const apiState = ref<ApiState>('loading')
const apiStateText = {
  loading: '檢查中…',
  online: '已連線',
  offline: '無法連線',
}

const controller = new AbortController()

onMounted(async () => {
  try {
    const health = await getApiHealth(controller.signal)
    apiState.value = health.status === 'ok' ? 'online' : 'offline'
  } catch {
    apiState.value = 'offline'
  }
})

onBeforeUnmount(() => controller.abort())
</script>

<template>
  <AppPage title="設定">
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

      <SettingsRow label="LINE 綁定" value="未綁定">
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

      <SettingsRow label="預設資料空間" value="尚未設定">
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

      <SettingsRow label="提醒通知">
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
      <SettingsRow label="家庭成員">
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

      <SettingsRow label="產生邀請碼">
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

      <SettingsRow label="加入家庭">
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
    </SettingsGroup>

    <SettingsGroup label="其他">
      <SettingsRow label="關於" value="v0.1.0">
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

      <SettingsRow label="後端連線" :value="apiStateText[apiState]" :chevron="false">
        <template #icon>
          <span class="status-dot" :class="`status-dot--${apiState}`" />
        </template>
      </SettingsRow>
    </SettingsGroup>

    <p class="note">第一版只完成明亮模式，各項設定將於後續模組逐步開放。</p>
  </AppPage>
</template>

<style scoped>
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

.status-dot {
  width: 10px;
  height: 10px;
  border-radius: var(--radius-full);
  background-color: var(--color-text-muted);
}

.status-dot--online {
  background-color: var(--color-income);
}

.status-dot--offline {
  background-color: var(--color-expense);
}

.note {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}
</style>
