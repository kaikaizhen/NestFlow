<script setup lang="ts">
import { useRouter } from 'vue-router'

defineProps<{ title: string }>()

const router = useRouter()
</script>

<template>
  <section class="subpage">
    <header class="subpage__header">
      <button class="subpage__back" type="button" aria-label="返回" @click="router.back()">
        <svg
          viewBox="0 0 24 24"
          width="24"
          height="24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="m15 6-6 6 6 6" />
        </svg>
      </button>

      <h1 class="subpage__title">{{ title }}</h1>
    </header>

    <div class="subpage__body">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.subpage {
  padding: calc(var(--space-4) + env(safe-area-inset-top)) var(--space-4)
    calc(var(--nav-height) + var(--space-6) + env(safe-area-inset-bottom));
}

.subpage__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-5);
}

.subpage__back {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  margin-left: calc(var(--space-2) * -1);
  border-radius: var(--radius-full);
  transition: background-color var(--duration-fast) var(--ease-out);
}

.subpage__back:active {
  background-color: var(--color-border);
}

.subpage__title {
  margin: 0;
  font-size: var(--font-size-title);
  font-weight: 700;
}

.subpage__body {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
</style>
