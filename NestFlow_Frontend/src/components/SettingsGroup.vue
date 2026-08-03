<script setup lang="ts">
defineProps<{ label: string }>()
</script>

<template>
  <section class="group">
    <h2 class="group__label">{{ label }}</h2>
    <div class="group__list">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.group__label {
  margin: 0 0 var(--space-2) var(--space-1);
  font-size: var(--font-size-caption);
  font-weight: 600;
  color: var(--color-text-muted);
}

.group__list {
  overflow: hidden;
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-card);
}

.group__list > * + * {
  border-top: 1px solid var(--color-border);
}
</style>
