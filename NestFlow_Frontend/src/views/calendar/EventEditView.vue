<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import DateTimePicker from '../../components/DateTimePicker.vue'
import {
  api,
  type RecurrenceEndType,
  type SaveCalendarEventPayload,
} from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'
import { isoToLocalInput, localInputToIso, todayInZone } from '../../utils/datetime'

/** 新增時的預設長度：一小時。 */
const DEFAULT_DURATION_MINUTES = 60

/** 提前多久通知的固定選單，需與後端 CalendarReminderOptions 一致。 */
const REMINDER_OPTIONS: { minutes: number; label: string }[] = [
  { minutes: 0, label: '準時' },
  { minutes: 5, label: '5 分鐘前' },
  { minutes: 10, label: '10 分鐘前' },
  { minutes: 30, label: '30 分鐘前' },
  { minutes: 60, label: '1 小時前' },
  { minutes: 1440, label: '1 天前' },
]

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()
const { active, workspaces, load: loadWorkspaces } = useWorkspaces()

const eventId = computed(() => route.params.eventId as string | undefined)
const isEditing = computed(() => Boolean(eventId.value))
const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const title = ref('')
const description = ref('')
const startAtLocal = ref('')
const endAtLocal = ref('')

/** 要存放的資料空間。新增時可選，修改時固定為原本所屬的空間。 */
const selectedWorkspaceId = ref('')

const wantsReminder = ref(false)
const reminderMinutes = ref(30)

/** 週期設定，只在新增時使用。 */
const repeat = ref(false)
const repeatEndType = ref<RecurrenceEndType>('count')
const repeatCount = ref(4)
const repeatUntilLocal = ref('')

const authorName = ref('')
const isRecurring = ref(false)
const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')

// 家庭空間的既有行程才需要標示是誰建立的
const showAuthor = computed(
  () => isEditing.value && active.value?.type === 'family' && Boolean(authorName.value),
)

const isPeriodValid = computed(
  () => Boolean(startAtLocal.value) && Boolean(endAtLocal.value) && endAtLocal.value > startAtLocal.value,
)

const canSave = computed(
  () =>
    title.value.trim().length > 0 &&
    isPeriodValid.value &&
    Boolean(isEditing.value ? active.value : selectedWorkspaceId.value),
)

function pad(value: number) {
  return String(value).padStart(2, '0')
}

function shift(local: string, minutes: number) {
  const [datePart, timePart] = local.split('T')
  const [year, month, day] = datePart.split('-').map(Number)
  const [hour, minute] = timePart.split(':').map(Number)

  const moved = new Date(Date.UTC(year, month - 1, day, hour, minute + minutes))

  return (
    `${moved.getUTCFullYear()}-${pad(moved.getUTCMonth() + 1)}-${pad(moved.getUTCDate())}` +
    `T${pad(moved.getUTCHours())}:${pad(moved.getUTCMinutes())}`
  )
}

/** 新增時的預設開始時間：從月曆點進來就用那天，否則用今天的下一個整點。 */
function defaultStart() {
  const dayParam = route.query.day as string | undefined
  const now = todayInZone(timeZone.value)

  const hour = Number(
    new Intl.DateTimeFormat('en-GB', {
      timeZone: timeZone.value,
      hour12: false,
      hour: '2-digit',
    }).format(new Date()),
  )

  if (dayParam && /^\d{4}-\d{2}-\d{2}$/.test(dayParam)) {
    return `${dayParam}T09:00`
  }

  const nextHour = Math.min(hour + 1, 23)

  return `${now.year}-${pad(now.month)}-${pad(now.day)}T${pad(nextHour)}:00`
}

async function loadExisting() {
  if (!eventId.value) {
    return
  }

  const found = await api.getEvent(eventId.value)

  authorName.value = found.createdByDisplayName
  title.value = found.title
  description.value = found.description ?? ''
  startAtLocal.value = isoToLocalInput(found.startAt, timeZone.value)
  endAtLocal.value = isoToLocalInput(found.endAt, timeZone.value)
  wantsReminder.value = found.hasReminder
  reminderMinutes.value = found.reminderMinutesBeforeStart ?? 30
  isRecurring.value = found.isRecurring
}

async function save() {
  const workspaceId = isEditing.value ? active.value?.id : selectedWorkspaceId.value

  if (!canSave.value || !workspaceId) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''

  const payload: SaveCalendarEventPayload = {
    workspaceId,
    title: title.value.trim(),
    description: description.value.trim() || null,
    startAt: localInputToIso(startAtLocal.value, timeZone.value),
    endAt: localInputToIso(endAtLocal.value, timeZone.value),
    wantsReminder: wantsReminder.value,
    reminderMinutesBeforeStart: wantsReminder.value ? reminderMinutes.value : null,
  }

  if (!isEditing.value && repeat.value) {
    payload.repeat = true
    payload.repeatEndType = repeatEndType.value

    if (repeatEndType.value === 'count') {
      payload.repeatCount = repeatCount.value
    } else if (repeatEndType.value === 'until') {
      payload.repeatUntil = localInputToIso(repeatUntilLocal.value, timeZone.value)
    }
  }

  try {
    if (eventId.value) {
      await api.updateEvent(eventId.value, payload)
    } else {
      await api.createEvent(payload)
    }

    await router.replace({ name: 'calendar' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '儲存失敗。'
  } finally {
    isSaving.value = false
  }
}

/** 僅此次取消：週期行程只刪除這一場，其他場次不受影響。 */
async function removeOccurrence() {
  if (!eventId.value) {
    return
  }

  const confirmText = isRecurring.value
    ? '確定要跳過這一場嗎？其他場次不會受影響。'
    : '確定要刪除這筆行程嗎？'

  if (!window.confirm(confirmText)) {
    return
  }

  errorMessage.value = ''

  try {
    await api.deleteEvent(eventId.value)
    await router.replace({ name: 'calendar' })
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '刪除失敗。'
  }
}

/** 刪除整個系列：移除這場與所有尚未發生的場次，已過去的場次保留。 */
async function removeSeries() {
  if (!eventId.value || !window.confirm('確定要刪除整個週期系列嗎？尚未發生的場次都會一併移除。')) {
    return
  }

  errorMessage.value = ''

  try {
    await api.deleteEventSeries(eventId.value)
    await router.replace({ name: 'calendar' })
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
      startAtLocal.value = defaultStart()
      endAtLocal.value = shift(startAtLocal.value, DEFAULT_DURATION_MINUTES)
      repeatUntilLocal.value = startAtLocal.value
      selectedWorkspaceId.value = active.value?.id ?? workspaces.value[0]?.id ?? ''
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
})

// 調整開始時間時，結束時間一併順移，避免每次都要改兩個欄位
watch(startAtLocal, (next, previous) => {
  if (!previous || !next || !endAtLocal.value || isLoading.value) {
    return
  }

  const [datePart, timePart] = previous.split('T')
  const [py, pm, pd] = datePart.split('-').map(Number)
  const [ph, pmin] = timePart.split(':').map(Number)

  const [nDatePart, nTimePart] = next.split('T')
  const [ny, nm, nd] = nDatePart.split('-').map(Number)
  const [nh, nmin] = nTimePart.split(':').map(Number)

  const deltaMinutes =
    (Date.UTC(ny, nm - 1, nd, nh, nmin) - Date.UTC(py, pm - 1, pd, ph, pmin)) / 60000

  endAtLocal.value = shift(endAtLocal.value, deltaMinutes)
})
</script>

<template>
  <AppSubPage :title="isEditing ? '編輯行程' : '新增行程'">
    <AppMessage :text="errorMessage" tone="error" />

    <p v-if="isLoading" class="hint">載入中…</p>

    <p v-else-if="showAuthor" class="author">由 {{ authorName }} 建立</p>

    <p v-if="!isLoading && isRecurring" class="recurring-note">
      🔁 這是週期行程的其中一場。修改內容會套用到「尚未發生」的所有場次，且不能單獨改日期；
      要跳過某一場請用下方「僅此次取消」。
    </p>

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
        <span class="field__label">標題</span>
        <input
          v-model="title"
          type="text"
          maxlength="100"
          placeholder="例如：專案進度會議"
          required
        />
      </label>

      <DateTimePicker v-model="startAtLocal" :time-zone="timeZone" label="開始" />
      <DateTimePicker v-model="endAtLocal" :time-zone="timeZone" label="結束" />

      <p v-if="startAtLocal && endAtLocal && !isPeriodValid" class="warning">
        結束時間必須晚於開始時間。
      </p>

      <label class="field">
        <span class="field__label">說明</span>
        <input v-model="description" type="text" maxlength="500" placeholder="選填，例如地點" />
      </label>

      <div class="toggle-card">
        <label class="toggle-card__row">
          <span class="toggle-card__label">需要提醒</span>
          <input v-model="wantsReminder" type="checkbox" class="toggle-card__checkbox" />
        </label>

        <label v-if="wantsReminder" class="toggle-card__sub">
          <span class="field__label">提前多久通知</span>
          <select v-model.number="reminderMinutes">
            <option v-for="option in REMINDER_OPTIONS" :key="option.minutes" :value="option.minutes">
              {{ option.label }}
            </option>
          </select>
        </label>
      </div>

      <div v-if="!isEditing" class="toggle-card">
        <label class="toggle-card__row">
          <span class="toggle-card__label">每週重複</span>
          <input v-model="repeat" type="checkbox" class="toggle-card__checkbox" />
        </label>

        <div v-if="repeat" class="toggle-card__sub">
          <span class="field__label">結束方式</span>

          <div class="segment" role="radiogroup" aria-label="結束方式">
            <button
              type="button"
              class="segment__item"
              :class="{ 'is-active': repeatEndType === 'count' }"
              role="radio"
              :aria-checked="repeatEndType === 'count'"
              @click="repeatEndType = 'count'"
            >
              次數
            </button>

            <button
              type="button"
              class="segment__item"
              :class="{ 'is-active': repeatEndType === 'until' }"
              role="radio"
              :aria-checked="repeatEndType === 'until'"
              @click="repeatEndType = 'until'"
            >
              指定日期
            </button>

            <button
              type="button"
              class="segment__item"
              :class="{ 'is-active': repeatEndType === 'forever' }"
              role="radio"
              :aria-checked="repeatEndType === 'forever'"
              @click="repeatEndType = 'forever'"
            >
              不設結束
            </button>
          </div>

          <label v-if="repeatEndType === 'count'" class="field">
            <span class="field__label">重複次數</span>
            <input v-model.number="repeatCount" type="number" inputmode="numeric" min="1" max="104" />
          </label>

          <DateTimePicker
            v-if="repeatEndType === 'until'"
            v-model="repeatUntilLocal"
            :time-zone="timeZone"
            label="重複到"
          />

          <p v-if="repeatEndType === 'forever'" class="hint">
            最多自動產生 2 年份的場次，之後如需延續請再手動新增。
          </p>
        </div>
      </div>

      <AppButton type="submit" :disabled="!canSave || isSaving">
        {{ isSaving ? '儲存中…' : '儲存' }}
      </AppButton>

      <template v-if="isEditing">
        <AppButton variant="danger" @click="removeOccurrence">
          {{ isRecurring ? '僅此次取消' : '刪除' }}
        </AppButton>

        <AppButton v-if="isRecurring" variant="danger" @click="removeSeries">
          刪除整個系列
        </AppButton>
      </template>
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

.recurring-note {
  margin: 0;
  padding: var(--space-3) var(--space-4);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.warning {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-expense);
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

.toggle-card__sub select,
.toggle-card__sub input {
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
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-2);
}

.segment__item {
  min-height: 44px;
  font-size: var(--font-size-caption);
  font-weight: 600;
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
