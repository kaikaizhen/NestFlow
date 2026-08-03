<script setup lang="ts">
withDefaults(
  defineProps<{
    label: string
    value?: string
    /** 尚未實作的項目維持可見但不可點，供使用者知道功能位置。 */
    disabled?: boolean
    /** 顯示右側箭頭，代表可進入下一層。 */
    chevron?: boolean
  }>(),
  { disabled: true, chevron: true },
)
</script>

<template>
  <div class="row" :class="{ 'is-disabled': disabled }">
    <span class="row__icon">
      <slot name="icon" />
    </span>

    <span class="row__label">{{ label }}</span>

    <span v-if="value" class="row__value">{{ value }}</span>

    <slot name="trailing">
      <svg
        v-if="chevron"
        class="row__chevron"
        viewBox="0 0 24 24"
        width="20"
        height="20"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path d="m9 6 6 6-6 6" />
      </svg>
    </slot>
  </div>
</template>

<style scoped>
.row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  /* 大型點擊區 */
  min-height: 56px;
  padding: var(--space-3) var(--space-4);
}

.row.is-disabled {
  color: var(--color-text-muted);
}

.row__icon {
  display: flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
}

.row__label {
  flex: 1;
  font-weight: 500;
}

.row__value {
  color: var(--color-text-muted);
}

.row__chevron {
  flex-shrink: 0;
  color: var(--color-text-muted);
}
</style>
