<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import { api } from '../../services/apiClient'

const router = useRouter()

const code = ref('')
const isJoining = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const isValid = computed(() => code.value.trim().length === 8)

/** 邀請碼只有大寫英數，輸入時即時轉換，避免使用者因大小寫失敗。 */
function normalize(event: Event) {
  const input = event.target as HTMLInputElement
  code.value = input.value.toUpperCase().replace(/[^0-9A-Z]/g, '').slice(0, 8)
  input.value = code.value
}

async function join() {
  if (!isValid.value) {
    return
  }

  isJoining.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    const workspace = await api.joinWorkspace(code.value)
    successMessage.value = `已加入「${workspace.name}」。`

    setTimeout(() => router.replace({ name: 'workspaces' }), 900)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '加入失敗。'
  } finally {
    isJoining.value = false
  }
}
</script>

<template>
  <AppSubPage title="加入家庭">
    <form class="join" @submit.prevent="join">
      <p class="join__hint">請輸入家庭建立者提供的 8 碼邀請碼。</p>

      <input
        class="join__input"
        :value="code"
        type="text"
        inputmode="text"
        autocapitalize="characters"
        autocomplete="off"
        spellcheck="false"
        maxlength="8"
        placeholder="XXXXXXXX"
        aria-label="邀請碼"
        @input="normalize"
      />

      <AppMessage :text="errorMessage" tone="error" />
      <AppMessage :text="successMessage" tone="success" />

      <AppButton type="submit" :disabled="!isValid || isJoining">
        {{ isJoining ? '加入中…' : '加入' }}
      </AppButton>
    </form>

    <p class="note">邀請碼 24 小時內有效，且只能成功使用一次。</p>
  </AppSubPage>
</template>

<style scoped>
.join {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-5) var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.join__hint {
  margin: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.join__input {
  min-height: 64px;
  padding: 0 var(--space-3);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 28px;
  font-weight: 700;
  color: var(--color-text);
  text-align: center;
  letter-spacing: 0.16em;
  background-color: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
}

.join__input:focus {
  outline: 2px solid var(--color-accent);
  outline-offset: 1px;
}

.note {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}
</style>
