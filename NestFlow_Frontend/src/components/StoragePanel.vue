<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppMessage from './AppMessage.vue'
import StorageRow from './StorageRow.vue'
import { useAutoRefresh } from '../composables/useAutoRefresh'
import { api, type StorageItem, type Workspace } from '../services/apiClient'

/** 停止輸入多久後才送出搜尋，避免每按一個字就打一次 API。 */
const SEARCH_DELAY = 300

const props = defineProps<{ workspace: Workspace | null }>()

const router = useRouter()

const keyword = ref('')
const items = ref<StorageItem[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

let searchTimer: ReturnType<typeof setTimeout> | undefined

const isFamilyWorkspace = computed(() => props.workspace?.type === 'family')
const isSearching = computed(() => Boolean(keyword.value.trim()))

const emptyText = computed(() =>
  isSearching.value ? '找不到符合的物品。' : '還沒有記錄任何物品。',
)

async function load() {
  if (!props.workspace) {
    return
  }

  isLoading.value = true
  errorMessage.value = ''

  try {
    items.value = await api.listStorageItems(props.workspace.id, keyword.value.trim() || undefined)
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : '載入失敗。'
  } finally {
    isLoading.value = false
  }
}

function openItem(item: StorageItem) {
  router.push({ name: 'storage-edit', params: { itemId: item.id } })
}

function clearKeyword() {
  keyword.value = ''
}

onMounted(load)

// 打字時延遲送出，切換資料空間則立即重新載入
watch(keyword, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(load, SEARCH_DELAY)
})

watch(() => props.workspace, load)

onUnmounted(() => clearTimeout(searchTimer))

// 家庭成員新增或修改物品時，這裡不會即時收到通知，
// 靠定時輪詢與切回頁面時補抓一次來縮短看到最新資料的延遲
useAutoRefresh(load)
</script>

<template>
  <div class="storage">
    <div class="search">
      <svg
        class="search__icon"
        viewBox="0 0 24 24"
        width="20"
        height="20"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <circle cx="11" cy="11" r="7" />
        <path d="m20 20-3.5-3.5" />
      </svg>

      <input
        v-model="keyword"
        class="search__input"
        type="search"
        inputmode="search"
        maxlength="100"
        placeholder="搜尋物品或存放位置"
        aria-label="搜尋物品或存放位置"
      />

      <button
        v-if="keyword"
        class="search__clear"
        type="button"
        aria-label="清除搜尋"
        @click="clearKeyword"
      >
        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
          <path d="M6 6l12 12M18 6 6 18" />
        </svg>
      </button>
    </div>

    <AppMessage :text="errorMessage" tone="error" />

    <div class="storage__scroll">
      <p v-if="isLoading" class="storage__hint">載入中…</p>

      <p v-else-if="!items.length" class="storage__hint">{{ emptyText }}</p>

      <template v-else>
        <h2 v-if="!isSearching" class="storage__section">最近更新</h2>

        <ul class="storage__list">
          <li v-for="item in items" :key="item.id">
            <StorageRow :item="item" :show-author="isFamilyWorkspace" @select="openItem" />
          </li>
        </ul>
      </template>
    </div>
  </div>
</template>

<style scoped>
/* 佔滿分頁剩下的高度，讓搜尋列固定、只有清單捲動 */
.storage {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-4);
  min-height: 0;
}

.search {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  padding: 0 var(--space-3);
  background-color: var(--color-surface);
  border-radius: var(--radius-full);
}

.search__icon {
  flex-shrink: 0;
  color: var(--color-text-muted);
}

.search__input {
  flex: 1;
  min-width: 0;
  min-height: 48px;
  font: inherit;
  color: var(--color-text);
  background: none;
  border: none;
  outline: none;
}

/* 移除瀏覽器內建的清除鈕，改用自己的按鈕統一外觀 */
.search__input::-webkit-search-cancel-button {
  display: none;
}

.search__clear {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  color: var(--color-text-muted);
  border-radius: var(--radius-full);
}

.search__clear:active {
  background-color: var(--color-bg);
}

.storage__scroll {
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

.storage__section {
  margin: 0;
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
}

.storage__hint {
  margin: 0;
  padding: var(--space-5);
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-align: center;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
}

.storage__list {
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
</style>
