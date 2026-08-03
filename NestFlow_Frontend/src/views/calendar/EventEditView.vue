<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import DateTimePicker from '../../components/DateTimePicker.vue'
import { api, type SaveCalendarEventPayload } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'
import { useWorkspaces } from '../../stores/workspace'
import { isoToLocalInput, localInputToIso, todayInZone } from '../../utils/datetime'

/** 新增時的預設長度：一小時。 */
const DEFAULT_DURATION_MINUTES = 60

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const eventId = computed(() => route.params.eventId as string | undefined)
const isEditing = computed(() => Boolean(eventId.value))
const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const title = ref('')
const description = ref('')
const startAtLocal = ref('')
const endAtLocal = ref('')

const authorName = ref('')
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

const canSave = computed(() => title.value.trim().length > 0 && isPeriodValid.value)

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
}

async function save() {
  if (!canSave.value || !active.value) {
    return
  }

  isSaving.value = true
  errorMessage.value = ''

  const payload: SaveCalendarEventPayload = {
    workspaceId: active.value.id,
    title: title.value.trim(),
    description: description.value.trim() || null,
    startAt: localInputToIso(startAtLocal.value, timeZone.value),
    endAt: localInputToIso(endAtLocal.value, timeZone.value),
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

async function remove() {
  if (!eventId.value || !window.confirm('確定要刪除這筆行程嗎？')) {
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

onMounted(async () => {
  try {
    await loadWorkspaces()

    if (isEditing.value) {
      await loadExisting()
    } else {
      startAtLocal.value = defaultStart()
      endAtLocal.value = shift(startAtLocal.value, DEFAULT_DURATION_MINUTES)
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

    <form v-if="!isLoading" class="form" @submit.prevent="save">
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
</style>
