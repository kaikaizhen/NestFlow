<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppMessage from '../components/AppMessage.vue'
import TodoRow from '../components/TodoRow.vue'
import { useAutoRefresh } from '../composables/useAutoRefresh'
import { api, type Todo, type TodoType } from '../services/apiClient'
import { useAuth } from '../stores/auth'
import { useWorkspaces } from '../stores/workspace'

const TABS: { type: TodoType; label: string }[] = [
  { type: 'general', label: '待辦事項' },
  { type: 'shopping', label: '購物清單' },
]

const router = useRouter()
const { currentUser } = useAuth()
const { active, load: loadWorkspaces } = useWorkspaces()

const timeZone = computed(() => currentUser.value?.timeZone || 'Asia/Taipei')

const activeType = ref<TodoType>('general')
const todos = ref<Todo[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

// 家庭空間才需要區分是誰加的
const isFamilyWorkspace = computed(() => active.value?.type === 'family')

const pending = computed(() => todos.value.filter((t) => !t.isCompleted))
const completed = computed(() => todos.value.filter((t) => t.isCompleted))

const emptyText = computed(() =>
  activeType.value === 'shopping' ? '購物清單是空的。' : '目前沒有待辦事項。',
)

async function load() {
  if (!active.value) {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  try {
    todos.value = await api.listTodos(active.value.id, activeType.value)
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

function createTodo() {
  router.push({ name: 'todo-create', query: { type: activeType.value } })
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
watch([active, activeType], load)

// 家庭成員新增或完成代辦時，這裡不會即時收到通知，
// 靠定時輪詢與切回頁面時補抓一次來縮短看到最新資料的延遲
useAutoRefresh(load)
</script>

<template>
  <section class="todos">
    <header class="todos__header">
      <h1 class="todos__title">代辦</h1>
    </header>

    <div class="tabs" role="tablist" aria-label="代辦類型">
      <button
        v-for="tab in TABS"
        :key="tab.type"
        class="tabs__item"
        :class="{ 'is-active': activeType === tab.type }"
        type="button"
        role="tab"
        :aria-selected="activeType === tab.type"
        @click="activeType = tab.type"
      >
        {{ tab.label }}
      </button>
    </div>

    <AppMessage :text="errorMessage" tone="error" />

    <div class="todos__scroll">
      <p v-if="isLoading" class="todos__hint">載入中…</p>

      <template v-else>
        <p v-if="!todos.length" class="todos__hint">{{ emptyText }}</p>

        <ul v-if="pending.length" class="todos__list">
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
          <h2 class="todos__section">已完成（{{ completed.length }}）</h2>

          <ul class="todos__list">
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

    <button class="todos__fab" type="button" aria-label="新增代辦" @click="createTodo">
      <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
    </button>
  </section>
</template>

<style scoped>
.todos {
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

.todos__scroll {
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

.todos__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.todos__title {
  margin: 0;
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.tabs {
  display: grid;
  grid-template-columns: 1fr 1fr;
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

.todos__section {
  margin: var(--space-1) 0 0;
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
}

.todos__hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.todos__list {
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
.todos__fab {
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

.todos__fab:active {
  transform: scale(0.94);
}
</style>
