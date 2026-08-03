<script setup lang="ts">
defineProps<{ title: string }>()
</script>

<template>
  <section class="page">
    <header class="page__header">
      <h1 class="page__title">{{ title }}</h1>
      <slot name="header-actions" />
    </header>

    <div class="page__body">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.page {
  /* 預留底部導航高度與 iOS 安全區，內容不會被導航列蓋住 */
  padding: calc(var(--space-6) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + var(--space-6) + env(safe-area-inset-bottom));
}

.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  margin-bottom: var(--space-5);
}

.page__title {
  margin: 0;
  font-size: var(--font-size-page-title);
  font-weight: 700;
  letter-spacing: -0.02em;
}

.page__body {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
</style>
