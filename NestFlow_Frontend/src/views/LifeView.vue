<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppMessage from '../components/AppMessage.vue'
import StoragePanel from '../components/StoragePanel.vue'
import TodoRow from '../components/TodoRow.vue'
import { useAutoRefresh } from '../composables/useAutoRefresh'
import { api, type Todo, type TodoType } from '../services/apiClient'
import { useAuth } from '../stores/auth'
import { useWorkspaces } from '../stores/workspace'

/** 前兩個分頁是代辦的兩種類型，第三個是儲藏庫（資料模型完全不同，另以元件呈現）。 */
type TabKey = TodoType | 'storage'

const TABS: { key: TabKey; label: string }[] = [
  { key: 'general', label: '待辦' },
  { key: 'shopping', label: '購物' },
  { key: 'storage', label: '儲藏庫' },
]

const route = useRoute()
const router = useRouter()
const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const activeTab = ref<TabKey>(readTabFromQuery())
const todos = ref<Todo[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const isStorage = computed(() => activeTab.value === 'storage')

// 家庭空間才需要區分是誰加的
const isFamilyWorkspace = computed(() => active.value?.type === 'family')

const pending = computed(() => todos.value.filter((t) => !t.isCompleted))
const completed = computed(() => todos.value.filter((t) => t.isCompleted))

const emptyText = computed(() =>
  activeTab.value === 'shopping' ? '購物清單是空的。' : '目前沒有待辦事項。',
)

/** 分頁記在網址上，從新增或編輯頁返回時才會回到原本那一頁。 */
function readTabFromQuery(): TabKey {
  const tab = route.query.tab

  return tab === 'shopping' || tab === 'storage' ? tab : 'general'
}

async function load() {
  const workspace = active.value
  const tab = activeTab.value

  // 儲藏庫的資料由 StoragePanel 自行載入，這裡只負責代辦
  if (!workspace || tab === 'storage') {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  try {
    todos.value = await api.listTodos(workspace.id, tab)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

/**
 * 先在畫面上切換狀態再送出，讓勾選立即有反應。
 * 失敗時把整份清單重新載回來，避免畫面與後端不一致。
 */
async function toggle(todo: Todo) {
  const next = !todo.isCompleted
  const index = todos.value.findIndex((t) => t.id === todo.id)

  if (index < 0) {
    return
  }

  const previous = todos.value[index]
  todos.value[index] = { ...previous, isCompleted: next }
  errorMessage.value = ''

  try {
    todos.value[index] = await api.setTodoCompletion(todo.id, next)
  } catch (error) {
    todos.value[index] = previous
    errorMessage.value = error instanceof Error ? error.message : '更新失敗。'
    await load()
  }
}

function openTodo(todo: Todo) {
  router.push({ name: 'todo-edit', params: { todoId: todo.id } })
}

/** 浮動按鈕依目前分頁決定要新增代辦還是儲藏庫物品。 */
function create() {
  if (isStorage.value) {
    router.push({ name: 'storage-create' })
    return
  }

  router.push({ name: 'todo-create', query: { type: activeTab.value } })
}

onMounted(async () => {
  try {
    await loadWorkspaces()
    await load()
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
    isLoading.value = false
  }
})

// 切換資料空間或分頁時重新載入。
// 從新增或編輯頁返回時本元件會重新掛載，因此 onMounted 已涵蓋重新整理。
watch([active, activeTab], load)

// 分頁狀態同步到網址，重新整理或從子頁返回時才不會跳回第一個分頁
watch(activeTab, (tab) => {
  router.replace({ query: tab === 'general' ? {} : { tab } })
})

// 家庭成員新增或完成代辦時，這裡不會即時收到通知，
// 靠定時輪詢與切回頁面時補抓一次來縮短看到最新資料的延遲
useAutoRefresh(load)
</script>

<template>
  <section class="life">
    <header class="life__header">
      <h1 class="life__title">生活</h1>
    </header>

    <div class="tabs" role="tablist" aria-label="待辦、購物與儲藏庫">
      <button
        v-for="tab in TABS"
        :key="tab.key"
        class="tabs__item"
        :class="{ 'is-active': activeTab === tab.key }"
        type="button"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </div>

    <StoragePanel v-if="isStorage" :workspace="active" />

    <template v-else>
      <AppMessage :text="errorMessage" tone="error" />

      <div class="life__scroll">
        <p v-if="isLoading" class="life__hint">載入中…</p>

        <template v-else>
          <p v-if="!todos.length" class="life__hint">{{ emptyText }}</p>

          <ul v-if="pending.length" class="life__list">
            <li v-for="todo in pending" :key="todo.id">
              <TodoRow
                :todo="todo"
                :time-zone="timeZone"
                :show-author="isFamilyWorkspace"
                @select="openTodo"
                @toggle="toggle"
              />
            </li>
          </ul>

          <template v-if="completed.length">
            <h2 class="life__section">已完成（{{ completed.length }}）</h2>

            <ul class="life__list">
              <li v-for="todo in completed" :key="todo.id">
                <TodoRow
                  :todo="todo"
                  :time-zone="timeZone"
                  :show-author="isFamilyWorkspace"
                  @select="openTodo"
                  @toggle="toggle"
                />
              </li>
            </ul>
          </template>
        </template>
      </div>
    </template>

    <button
      class="life__fab"
      type="button"
      :aria-label="isStorage ? '新增物品' : '新增代辦'"
      @click="create"
    >
      <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
    </button>
  </section>
</template>

<style scoped>
.life {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  /* 佔滿視窗並扣掉底部導航，讓標題與分頁固定、只有清單區塊捲動 */
  height: 100vh;
  height: 100dvh;
  padding: calc(var(--space-6) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + env(safe-area-inset-bottom));
}

.life__scroll {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-4);
  /* flex 子項預設 min-height 為 auto，不歸零就不會出現捲軸 */
  min-height: 0;
  /* 捲到底時的留白，最後一筆才不會被浮動按鈕蓋住 */
  padding-bottom: calc(var(--space-4) + 56px + var(--space-3));
  overflow-y: auto;
}

.life__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.life__title {
  margin: 0;
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.tabs {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: var(--space-1);
  padding: var(--space-1);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
}

.tabs__item {
  min-height: 44px;
  font-weight: 600;
  color: var(--color-text-muted);
  border-radius: var(--radius-full);
  transition:
    color var(--duration-fast) var(--ease-out),
    background-color var(--duration-fast) var(--ease-out);
}

.tabs__item.is-active {
  color: var(--color-surface);
  background-color: var(--color-accent);
}

.life__section {
  margin: var(--space-1) 0 0;
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
}

.life__hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.life__list {
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

/* 用 absolute 而非 fixed：頁面本身已是視窗高度且不捲動，效果相同，
   但定位基準是內容欄而不是整個視窗，桌機時才會貼齊內容右緣；
   也不會在頁面切換動畫（祖先有 transform）期間跳位。 */
.life__fab {
  position: absolute;
  right: var(--space-4);
  bottom: calc(var(--nav-height) + var(--space-4) + env(safe-area-inset-bottom));
  display: flex;
  align-items: center;
  justify-content: center;
  width: 56px;
  height: 56px;
  color: var(--color-surface);
  background-color: var(--color-accent);
  border-radius: var(--radius-full);
  box-shadow: 0 6px 20px rgb(17 17 19 / 24%);
  transition: transform var(--duration-fast) var(--ease-out);
}

.life__fab:active {
  transform: scale(0.94);
}
</style>
