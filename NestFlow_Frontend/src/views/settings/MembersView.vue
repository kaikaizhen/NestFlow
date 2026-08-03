<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSubPage from '../../components/AppSubPage.vue'
import AppButton from '../../components/AppButton.vue'
import AppMessage from '../../components/AppMessage.vue'
import { api, type Invitation, type WorkspaceMember } from '../../services/apiClient'
import { useAuth } from '../../stores/auth'

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()

const workspaceId = computed(() => route.params.workspaceId as string)

const members = ref<WorkspaceMember[]>([])
const isOwner = ref(false)
const isLoading = ref(true)
const errorMessage = ref('')

const invitation = ref<Invitation | null>(null)
const isGenerating = ref(false)
const copyMessage = ref('')

async function load() {
  isLoading.value = true
  errorMessage.value = ''

  try {
    members.value = await api.listMembers(workspaceId.value)
    isOwner.value = members.value.some(
      (m) => m.userId === currentUser.value?.id && m.membershipType === 'owner',
    )
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

async function generateInvitation() {
  isGenerating.value = true
  errorMessage.value = ''
  copyMessage.value = ''

  try {
    invitation.value = await api.createInvitation(workspaceId.value)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '產生失敗。'
  } finally {
    isGenerating.value = false
  }
}

async function copyCode() {
  if (!invitation.value) {
    return
  }

  try {
    await navigator.clipboard.writeText(invitation.value.code)
    copyMessage.value = '已複製邀請碼。'
  } catch {
    copyMessage.value = '無法自動複製，請手動選取。'
  }
}

async function removeMember(member: WorkspaceMember) {
  const isSelf = member.userId === currentUser.value?.id
  const confirmText = isSelf
    ? '確定要離開這個資料空間嗎？離開後將無法存取其中的記帳與行程。'
    : `確定要移除「${member.displayName}」嗎？`

  if (!window.confirm(confirmText)) {
    return
  }

  errorMessage.value = ''

  try {
    await api.removeMember(workspaceId.value, member.userId)

    if (isSelf) {
      await router.replace({ name: 'workspaces' })
      return
    }

    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '操作失敗。'
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
  <AppSubPage title="家庭成員">
    <AppMessage :text="errorMessage" tone="error" />

    <p v-if="isLoading" class="hint">載入中…</p>

    <ul v-else class="list">
      <li v-for="member in members" :key="member.userId" class="member">
        <img v-if="member.pictureUrl" class="member__avatar" :src="member.pictureUrl" alt="" />
        <span v-else class="member__avatar member__avatar--empty" aria-hidden="true">
          {{ member.displayName.slice(0, 1) }}
        </span>

        <span class="member__info">
          <span class="member__name">{{ member.displayName }}</span>
          <span class="member__role">
            {{ member.membershipType === 'owner' ? '建立者' : '成員' }}
            <template v-if="member.userId === currentUser?.id">・你</template>
          </span>
        </span>

        <button
          v-if="member.membershipType !== 'owner' && (isOwner || member.userId === currentUser?.id)"
          class="member__remove"
          type="button"
          @click="removeMember(member)"
        >
          {{ member.userId === currentUser?.id ? '離開' : '移除' }}
        </button>
      </li>
    </ul>

    <section v-if="isOwner" class="invite">
      <h2 class="invite__title">邀請成員</h2>
      <p class="invite__hint">邀請碼 24 小時內有效，且只能成功使用一次。</p>

      <div v-if="invitation" class="invite__code">
        <code>{{ invitation.code }}</code>
        <span class="invite__expiry">有效至 {{ formatExpiry(invitation.expiresAt) }}</span>
      </div>

      <AppMessage :text="copyMessage" tone="success" />

      <AppButton :disabled="isGenerating" @click="generateInvitation">
        {{ isGenerating ? '產生中…' : invitation ? '重新產生邀請碼' : '產生邀請碼' }}
      </AppButton>

      <AppButton v-if="invitation" variant="secondary" @click="copyCode">複製邀請碼</AppButton>
    </section>
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

.member {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  min-height: 64px;
  padding: var(--space-3) var(--space-4);
  background-color: var(--color-surface);
}

.member__avatar {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  object-fit: cover;
  border-radius: var(--radius-full);
}

.member__avatar--empty {
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  color: var(--color-text-muted);
  background-color: var(--color-bg);
}

.member__info {
  display: flex;
  flex: 1;
  flex-direction: column;
}

.member__name {
  font-weight: 600;
}

.member__role {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.member__remove {
  padding: var(--space-2) var(--space-3);
  font-size: var(--font-size-caption);
  color: var(--color-expense);
  border-radius: var(--radius-full);
}

.member__remove:active {
  background-color: var(--color-bg);
}

.hint {
  margin: 0 var(--space-1);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.invite {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-4);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.invite__title {
  margin: 0;
  font-size: var(--font-size-body);
  font-weight: 600;
}

.invite__hint {
  margin: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

.invite__code {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: center;
  padding: var(--space-4);
  background-color: var(--color-bg);
  border-radius: var(--radius-sm);
}

.invite__code code {
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
  font-size: 30px;
  font-weight: 700;
  letter-spacing: 0.16em;
}

.invite__expiry {
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}
</style>
