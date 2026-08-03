<script setup lang="ts">
defineProps<{
  text: string
  tone?: 'error' | 'success' | 'muted'
}>()
</script>

<template>
  <p v-if="text" class="message" :class="`message--${tone ?? 'muted'}`" role="status">
    {{ text }}
  </p>
</template>

<style scoped>
.message {
  margin: 0;
  padding: var(--space-3) var(--space-4);
  font-size: var(--font-size-caption);
  border-radius: var(--radius-sm);
}

.message--error {
  color: var(--color-expense);
  background-color: rgb(220 38 38 / 8%);
}

.message--success {
  color: var(--color-income);
  background-color: rgb(22 163 74 / 8%);
}

.message--muted {
  color: var(--color-text-muted);
  background-color: var(--color-surface);
}
</style>
