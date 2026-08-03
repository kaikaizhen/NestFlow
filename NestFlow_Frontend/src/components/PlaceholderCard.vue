<script setup lang="ts">
defineProps<{
  title: string
  description: string
  moduleLabel: string
}>()
</script>

<template>
  <article class="placeholder">
    <h2 class="placeholder__title">{{ title }}</h2>
    <p class="placeholder__description">{{ description }}</p>
    <span class="placeholder__badge">{{ moduleLabel }}</span>
  </article>
</template>

<style scoped>
.placeholder {
  padding: var(--space-5);
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-card);
}

.placeholder__title {
  margin: 0 0 var(--space-2);
  font-size: var(--font-size-title);
  font-weight: 600;
}

.placeholder__description {
  margin: 0 0 var(--space-4);
  color: var(--color-text-muted);
}

.placeholder__badge {
  display: inline-block;
  padding: var(--space-1) var(--space-3);
  font-size: var(--font-size-caption);
  font-weight: 500;
  color: var(--color-text-muted);
  background-color: var(--color-bg);
  border-radius: var(--radius-full);
}
</style>
