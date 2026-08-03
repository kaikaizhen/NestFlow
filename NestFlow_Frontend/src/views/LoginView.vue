<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import AppButton from '../components/AppButton.vue'
import AppMessage from '../components/AppMessage.vue'
import { api } from '../services/apiClient'
import { useAuth } from '../stores/auth'

const router = useRouter()
const { refresh } = useAuth()

const errorMessage = ref('')

// 開發登入僅在本機開發時顯示，正式建置不會出現
const isDev = import.meta.env.DEV
const devSubject = ref('Udev_user_01')
const devName = ref('開發測試者')
const isDevLoggingIn = ref(false)

function loginWithLine() {
  errorMessage.value = ''
  api.gotoLineLogin()
}

async function loginAsDeveloper() {
  errorMessage.value = ''
  isDevLoggingIn.value = true

  try {
    await api.devLogin(devSubject.value.trim(), devName.value.trim())
    await refresh()
    await router.replace({ name: 'calendar' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '登入失敗。'
  } finally {
    isDevLoggingIn.value = false
  }
}
</script>

<template>
  <section class="login">
    <div class="login__brand">
      <div class="login__mark" aria-hidden="true">N</div>
      <h1 class="login__title">NestFlow</h1>
      <p class="login__subtitle">記帳、行程與提醒的個人管家</p>
    </div>

    <div class="login__actions">
      <AppButton @click="loginWithLine">使用 LINE 登入</AppButton>
      <p class="login__hint">登入後會自動建立你的個人資料空間。</p>
      <AppMessage :text="errorMessage" tone="error" />
    </div>

    <details v-if="isDev" class="login__dev">
      <summary>開發登入（僅本機）</summary>

      <form class="login__dev-form" @submit.prevent="loginAsDeveloper">
        <label class="login__field">
          <span>識別碼</span>
          <input v-model="devSubject" type="text" autocomplete="off" />
        </label>

        <label class="login__field">
          <span>顯示名稱</span>
          <input v-model="devName" type="text" autocomplete="off" />
        </label>

        <AppButton type="submit" variant="secondary" :disabled="isDevLoggingIn">
          {{ isDevLoggingIn ? '登入中…' : '以此身分登入' }}
        </AppButton>
      </form>
    </details>
  </section>
</template>

<style scoped>
.login {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: var(--space-6);
  min-height: 100dvh;
  padding: var(--space-6) var(--space-5) calc(var(--space-6) + env(safe-area-inset-bottom));
}

.login__brand {
  text-align: center;
}

.login__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 72px;
  height: 72px;
  margin: 0 auto var(--space-4);
  font-size: 36px;
  font-weight: 700;
  color: var(--color-surface);
  background-color: var(--color-accent);
  border-radius: 22px;
}

.login__title {
  margin: 0 0 var(--space-2);
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.login__subtitle {
  margin: 0;
  color: var(--color-text-muted);
}

.login__actions {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.login__hint {
  margin: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
}

.login__dev {
  padding: var(--space-4);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.login__dev summary {
  cursor: pointer;
}

.login__dev-form {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-top: var(--space-4);
}

.login__field {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.login__field input {
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
