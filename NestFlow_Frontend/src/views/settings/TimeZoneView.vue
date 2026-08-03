<script setup lang="ts">
import { computed, ref } from 'vue'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import { api } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'

/** 常用時區。資料一律以 UTC 保存，此設定只影響顯示與月份切分。 */
const OPTIONS = [
  { id: 'Asia/Taipei', label: '台北（UTC+8）' },
  { id: 'Asia/Tokyo', label: '東京（UTC+9）' },
  { id: 'Asia/Shanghai', label: '上海（UTC+8）' },
  { id: 'Asia/Singapore', label: '新加坡（UTC+8）' },
  { id: 'America/Los_Angeles', label: '洛杉磯（UTC-8/-7）' },
  { id: 'America/New_York', label: '紐約（UTC-5/-4）' },
  { id: 'Europe/London', label: '倫敦（UTC+0/+1）' },
  { id: 'UTC', label: 'UTC' },
]

const { currentUser, refresh } = useAuth()

const selected = ref(currentUser.value?.timeZone ?? 'Asia/Taipei')
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const isDirty = computed(() => selected.value !== currentUser.value?.timeZone)

async function save() {
  isSaving.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await api.updateTimeZone(selected.value)
    await refresh()
    successMessage.value = '已更新時區。'
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '更新失敗。'
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <AppSubPage title="時區">
    <p class="hint">
      記帳與行程一律以世界標準時間保存，此設定只影響顯示的時間與月份的切分方式。
    </p>

    <ul class="list">
      <li v-for="option in OPTIONS" :key="option.id">
        <button class="option" type="button" @click="selected = option.id">
          <span class="option__label">{{ option.label }}</span>
          <svg
            v-if="selected === option.id"
            viewBox="0 0 24 24"
            width="20"
            height="20"
            fill="none"
            stroke="currentColor"
            stroke-width="2.2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="m5 13 4 4L19 7" />
          </svg>
        </button>
      </li>
    </ul>

    <AppMessage :text="errorMessage" tone="error" />
    <AppMessage :text="successMessage" tone="success" />

    <AppButton :disabled="!isDirty || isSaving" @click="save">
      {{ isSaving ? '儲存中…' : '儲存' }}
    </AppButton>
  </AppSubPage>
</template>

<style scoped>
.hint {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

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

.option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 52px;
  padding: 0 var(--space-4);
  background-color: var(--color-surface);
  text-align: left;
}

.option:active {
  background-color: var(--color-bg);
}

.option__label {
  font-weight: 500;
}
</style>
