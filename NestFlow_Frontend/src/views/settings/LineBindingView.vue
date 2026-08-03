<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import { api, type Invitation, type LineBot } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'

const { currentUser, refresh } = useAuth()

const bot = ref<LineBot | null>(null)
const binding = ref<Invitation | null>(null)

/** 未綁定時分兩步：先加好友，確認加過之後才給綁定碼。 */
const hasAddedFriend = ref(false)

const isLoading = ref(true)
const isGenerating = ref(false)
const errorMessage = ref('')
const copyMessage = ref('')

const isLinked = computed(() => currentUser.value?.isLineMessagingLinked === true)
const isConfigured = computed(() => currentUser.value?.isLineMessagingConfigured === true)

async function load() {
  isLoading.value = true

  // 可能剛在 LINE 完成綁定，進頁面先更新一次狀態
  await refresh()

  if (!isLinked.value && isConfigured.value) {
    try {
      bot.value = await api.getLineBot()
    } catch {
      // 取不到官方帳號資訊時仍可手動加好友，不擋住流程
      bot.value = null
    }
  }

  isLoading.value = false
}

/** 開啟加好友連結，並讓畫面進到下一步。 */
function addFriend() {
  if (bot.value) {
    window.open(bot.value.addFriendUrl, '_blank', 'noopener')
  }

  hasAddedFriend.value = true
}

async function generate() {
  isGenerating.value = true
  errorMessage.value = ''
  copyMessage.value = ''

  try {
    binding.value = await api.createBindingCode()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '產生失敗。'
  } finally {
    isGenerating.value = false
  }
}

async function copyCode() {
  if (!binding.value) {
    return
  }

  try {
    await navigator.clipboard.writeText(binding.value.code)
    copyMessage.value = '已複製綁定碼。'
  } catch {
    copyMessage.value = '無法自動複製，請手動選取。'
  }
}

/** 使用者在 LINE 傳完綁定碼後回到這頁，重新取得狀態確認是否已綁定。 */
async function recheck() {
  errorMessage.value = ''
  copyMessage.value = ''

  await refresh()

  if (isLinked.value) {
    binding.value = null
    copyMessage.value = '已完成綁定。'
  } else {
    errorMessage.value = '尚未偵測到綁定，請確認已在 LINE 傳出綁定碼。'
  }
}

function formatExpiry(value: string) {
  return new Date(value).toLocaleString('zh-TW', {
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(load)
</script>

<template>
  <AppSubPage title="LINE 綁定">
    <p v-if="isLoading" class="note">載入中…</p>

    <template v-else>
      <section class="status" :class="{ 'status--linked': isLinked }">
        <span class="status__dot" aria-hidden="true" />
        <span class="status__text">
          {{ isLinked ? '已綁定 LINE 官方帳號' : '尚未綁定 LINE 官方帳號' }}
        </span>
      </section>

      <!-- 已綁定：只需要說明怎麼用 -->
      <section v-if="isLinked" class="card">
        <h2 class="card__title">可以這樣記帳</h2>
        <pre class="card__sample">記帳 午餐 120
支出 交通 60 捷運
收入 薪資 50000</pre>
        <p class="card__hint">
          傳出後官方帳號會回覆內容摘要，再回覆「確認」才會寫入，回覆「取消」則放棄。
        </p>
      </section>

      <!-- 後端沒設定 Channel：不給假的入口 -->
      <section v-else-if="!isConfigured" class="card">
        <h2 class="card__title">尚未開放</h2>
        <p class="card__hint">這個環境還沒有設定 LINE 官方帳號，暫時無法綁定。</p>
      </section>

      <!-- 第一步：加好友 -->
      <template v-else-if="!hasAddedFriend">
        <section class="card">
          <h2 class="card__title">第一步：加官方帳號好友</h2>

          <div v-if="bot" class="bot">
            <img v-if="bot.pictureUrl" class="bot__avatar" :src="bot.pictureUrl" alt="" />
            <span v-else class="bot__avatar bot__avatar--empty" aria-hidden="true">
              {{ bot.displayName.slice(0, 1) }}
            </span>
            <span class="bot__name">{{ bot.displayName }}</span>
          </div>

          <p class="card__hint">
            加好友後才能用 LINE 記帳。手機會直接開啟 LINE，電腦則會顯示 QR Code 供掃描。
          </p>

          <AppButton :disabled="!bot" @click="addFriend">加入好友</AppButton>

          <button class="skip" type="button" @click="hasAddedFriend = true">
            我已經是好友了
          </button>
        </section>
      </template>

      <!-- 第二步：綁定碼 -->
      <template v-else>
        <section class="card">
          <h2 class="card__title">第二步：把綁定碼傳給官方帳號</h2>
          <p class="card__hint">綁定碼 10 分鐘內有效，且只能成功使用一次。</p>

          <div v-if="binding" class="code">
            <code>{{ binding.code }}</code>
            <span class="code__expiry">有效至 {{ formatExpiry(binding.expiresAt) }}</span>
          </div>

          <AppMessage :text="errorMessage" tone="error" />
          <AppMessage :text="copyMessage" tone="success" />

          <AppButton :disabled="isGenerating" @click="generate">
            {{ isGenerating ? '產生中…' : binding ? '重新產生綁定碼' : '產生綁定碼' }}
          </AppButton>

          <template v-if="binding">
            <AppButton variant="secondary" @click="copyCode">複製綁定碼</AppButton>
            <AppButton variant="secondary" @click="recheck">我已傳出，檢查綁定狀態</AppButton>
          </template>

          <button v-if="bot" class="skip" type="button" @click="hasAddedFriend = false">
            還沒加好友？回上一步
          </button>
        </section>
      </template>
    </template>
  </AppSubPage>
</template>

<style scoped>
.status {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.status__dot {
  flex-shrink: 0;
  width: 10px;
  height: 10px;
  background-color: var(--color-text-muted);
  border-radius: var(--radius-full);
}

.status--linked .status__dot {
  background-color: var(--color-income);
}

.status__text {
  font-weight: 600;
}

.card {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.card__title {
  margin: 0;
  font-size: var(--font-size-body);
  font-weight: 600;
}

.card__hint {
  margin: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.card__sample {
  margin: 0;
  padding: var(--space-3);
  overflow-x: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: var(--font-size-body);
  line-height: 1.7;
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
}

.bot {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-3);
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
}

.bot__avatar {
  flex-shrink: 0;
  width: 44px;
  height: 44px;
  object-fit: cover;
  border-radius: var(--radius-full);
}

.bot__avatar--empty {
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-surface);
}

.bot__name {
  font-weight: 600;
}

.code {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: center;
  padding: var(--space-4);
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
}

.code code {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 30px;
  font-weight: 700;
  letter-spacing: 0.16em;
}

.code__expiry {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.skip {
  align-self: center;
  padding: var(--space-2);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-decoration: underline;
}

.note {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}
</style>
