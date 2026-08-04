<script setup lang="ts">
import type { StorageItem } from '../services/apiClient'

defineProps<{
  item: StorageItem
  /** 家庭資料空間才顯示建立者，個人空間顯示自己的名字沒有意義。 */
  showAuthor?: boolean
}>()

defineEmits<{ select: [item: StorageItem] }>()
</script>

<template>
  <button class="row" type="button" @click="$emit('select', item)">
    <span class="row__icon" aria-hidden="true">
      <svg
        viewBox="0 0 24 24"
        width="20"
        height="20"
        fill="none"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path d="M3 8.5 12 4l9 4.5v7L12 20l-9-4.5z" />
        <path d="M3 8.5 12 13l9-4.5M12 13v7" />
      </svg>
    </span>

    <span class="row__body">
      <span class="row__title">{{ item.name }}</span>

      <span class="row__meta">
        <span class="row__location">{{ item.location }}</span>
        <span v-if="showAuthor" class="row__author">{{ item.createdByDisplayName }}</span>
      </span>

      <span v-if="item.note" class="row__note">{{ item.note }}</span>
    </span>
  </button>
</template>

<style scoped>
.row {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  width: 100%;
  min-height: 68px;
  padding: var(--space-3) var(--space-4);
  background-color: var(--color-surface);
  text-align: left;
}

.row:active {
  background-color: var(--color-bg);
}

.row__icon {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  color: var(--color-text);
  background-color: var(--color-neutral-tint);
  border-radius: var(--radius-full);
}

.row__body {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.row__title {
  overflow: hidden;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row__meta {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  min-width: 0;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
}

/* 存放位置是這個功能的重點，太長時截斷但不換行 */
.row__location {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row__author {
  flex-shrink: 0;
  padding: 0 var(--space-2);
  font-size: 11px;
  line-height: 18px;
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}

.row__note {
  overflow: hidden;
  font-size: var(--font-size-caption);
  color: var(--color-text-muted);
  text-overflow: ellipsis;
  white-space: nowrap;
  opacity: 0.8;
}
</style>
