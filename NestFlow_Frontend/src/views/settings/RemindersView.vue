<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import DateTimePicker from '../../components/DateTimePicker.vue'
import { api, type Reminder, type ReminderStatus } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'
import {
  dayRangeUtc,
  formatTime,
  isoToLocalInput,
  localInputToIso,
  todayInZone,
} from '../../utils/datetime'

/** 提醒清單往前後各看的天數。 */
const PAST_DAYS = 30
const FUTURE_DAYS = 365

const STATUS_LABELS: Record<ReminderStatus, string> = {
  pending: '等待發送',
  sending: '發送中',
  sent: '已發送',
  failed: '發送失敗',
}

const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const reminders = ref<Reminder[]>([])
const content = ref('')
const triggerAtLocal = ref('')

const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const isFamilyWorkspace = computed(() => active.value?.type === 'family')
const canSave = computed(() => content.value.trim().length > 0 && Boolean(triggerAtLocal.value))

const upcoming = computed(() => reminders.value.filter((x) => x.status !== 'sent'))
const done = computed(() => reminders.value.filter((x) => x.status === 'sent'))

/** 預設提醒時間：一小時後。 */
function defaultTrigger() {
  const later = new Date(Date.now() + 60 * 60 * 1000)

  return isoToLocalInput(later.toISOString(), timeZone.value)
}

function formatTrigger(iso: string) {
  const date = new Date(iso)
  const parts = new Intl.DateTimeFormat('zh-TW', {
    timeZone: timeZone.value,
    month: 'numeric',
    day: 'numeric',
    weekday: 'short',
  }).format(date)

  return `${parts} ${formatTime(iso, timeZone.value)}`
}

async function load() {
  if (!active.value) {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  const today = todayInZone(timeZone.value)

  // 從 30 天前開始，讓已發送的提醒也看得到狀態
  const past = dayRangeUtc(today.year, today.month, today.day - PAST_DAYS, 1, timeZone.value)
  const future = dayRangeUtc(today.year, today.month, today.day, FUTURE_DAYS, timeZone.value)

  try {
    reminders.value = await api.listReminders(active.value.id, past.fromUtc, future.toUtc)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

async function create() {
  if (!canSave.value || !active.value) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''
  successMessage.value = ''

  try {
    await api.createReminder({
      workspaceId: active.value.id,
      content: content.value.trim(),
      triggerAt: localInputToIso(triggerAtLocal.value, timeZone.value),
    })

    content.value = ''
    triggerAtLocal.value = defaultTrigger()
    successMessage.value = '已建立提醒，時間到會用 LINE 通知你。'

    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '建立失敗。'
  } finally {
    isSaving.value = false
  }
}

async function cancel(reminder: Reminder) {
  if (!window.confirm(`確定要取消「${reminder.content}」嗎？`)) {
    return
  }

  errorMessage.value = ''
  successMessage.value = ''

  try {
    await api.cancelReminder(reminder.id)
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '取消失敗。'
  }
}

onMounted(async () => {
  try {
    await loadWorkspaces()
    triggerAtLocal.value = defaultTrigger()
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
    isLoading.value = false
  }
})
</script>

<template>
  <AppSubPage title="提醒通知">
    <AppMessage :text="errorMessage" tone="error" />
    <AppMessage :text="successMessage" tone="success" />

    <form class="card" @submit.prevent="create">
      <h2 class="card__title">新增提醒</h2>

      <label class="field">
        <span class="field__label">提醒內容</span>
        <input
          v-model="content"
          type="text"
          maxlength="200"
          placeholder="例如：繳水電費"
          required
        />
      </label>

      <DateTimePicker v-model="triggerAtLocal" :time-zone="timeZone" label="提醒時間" />

      <p class="card__hint">時間到時會由 NestFlow 官方帳號用 LINE 通知你，需先完成 LINE 綁定。</p>

      <AppButton type="submit" :disabled="!canSave || isSaving">
        {{ isSaving ? '建立中…' : '建立提醒' }}
      </AppButton>
    </form>

    <p v-if="isLoading" class="hint">載入中…</p>

    <template v-else>
      <section>
        <h2 class="section-title">尚未發送</h2>

        <p v-if="!upcoming.length" class="hint">目前沒有排定的提醒。</p>

        <ul v-else class="list">
          <li v-for="reminder in upcoming" :key="reminder.id" class="row">
            <span class="row__body">
              <span class="row__content">{{ reminder.content }}</span>
              <span class="row__meta">
                {{ formatTrigger(reminder.triggerAt) }}
                <span class="row__status" :class="`is-${reminder.status}`">
                  {{ STATUS_LABELS[reminder.status] }}
                </span>
                <span v-if="reminder.retryCount > 0" class="row__retry">
                  已重試 {{ reminder.retryCount }} 次
                </span>
                <span v-if="isFamilyWorkspace" class="row__author">
                  {{ reminder.createdByDisplayName }}
                </span>
              </span>
            </span>

            <button
              v-if="reminder.status !== 'sending'"
              class="row__cancel"
              type="button"
              @click="cancel(reminder)"
            >
              取消
            </button>
          </li>
        </ul>
      </section>

      <section v-if="done.length">
        <h2 class="section-title">已發送</h2>

        <ul class="list">
          <li v-for="reminder in done" :key="reminder.id" class="row">
            <span class="row__body">
              <span class="row__content">{{ reminder.content }}</span>
              <span class="row__meta">
                {{ formatTrigger(reminder.triggerAt) }}
                <span class="row__status is-sent">{{ STATUS_LABELS.sent }}</span>
                <span v-if="isFamilyWorkspace" class="row__author">
                  {{ reminder.createdByDisplayName }}
                </span>
              </span>
            </span>
          </li>
        </ul>
      </section>
    </template>
  </AppSubPage>
</template>

<style scoped>
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

.field {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.field__label {
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

.section-title {
  margin: 0 var(--space-1) var(--space-2);
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
}

.hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
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

.row {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  min-height: 68px;
  padding: var(--space-3) var(--space-4);
  background-color: var(--color-surface);
}

.row__body {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.row__content {
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row__meta {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  align-items: center;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.row__status,
.row__retry,
.row__author {
  padding: 0 var(--space-2);
  font-size: 11px;
  line-height: 18px;
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.row__status.is-sent {
  color: var(--color-income);
}

.row__status.is-failed {
  color: var(--color-expense);
}

.row__cancel {
  flex-shrink: 0;
  padding: var(--space-2) var(--space-3);
  font-size: var(--font-size-caption);
  color: var(--color-expense);
  border-radius: var(--radius-full);
}

.row__cancel:active {
  background-color: var(--color-bg);
}
</style>
