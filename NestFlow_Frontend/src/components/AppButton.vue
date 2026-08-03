<script setup lang="ts">
withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'danger'
    disabled?: boolean
    type?: 'button' | 'submit'
  }>(),
  { variant: 'primary', disabled: false, type: 'button' },
)
</script>

<template>
  <button class="button" :class="`button--${variant}`" :type="type" :disabled="disabled">
    <slot />
  </button>
</template>

<style scoped>
.button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  /* 大型點擊區 */
  width: 100%;
  min-height: 52px;
  padding: 0 var(--space-5);
  font-size: var(--font-size-body);
  font-weight: 600;
  border-radius: var(--radius-full);
  transition:
    transform var(--duration-fast) var(--ease-out),
    opacity var(--duration-fast) var(--ease-out);
}

.button:active:not(:disabled) {
  transform: scale(0.97);
}

.button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.button--primary {
  color: var(--color-surface);
  background-color: var(--color-accent);
}

.button--secondary {
  color: var(--color-text);
  background-color: var(--color-surface);
  box-shadow: inset 0 0 0 1px var(--color-border);
}

.button--danger {
  color: var(--color-expense);
  background-color: var(--color-surface);
  box-shadow: inset 0 0 0 1px var(--color-border);
}
</style>
