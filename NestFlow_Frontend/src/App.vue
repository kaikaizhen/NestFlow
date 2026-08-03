<script setup lang="ts">
import BottomNav from './components/BottomNav.vue'
</script>

<template>
  <div class="app-shell">
    <main class="app-shell__main">
      <RouterView v-slot="{ Component }">
        <Transition name="page-fade" mode="out-in">
          <component :is="Component" />
        </Transition>
      </RouterView>
    </main>

    <BottomNav />
  </div>
</template>

<style scoped>
.app-shell {
  /* 手機優先，桌機瀏覽時維持單欄手機寬度置中 */
  max-width: 480px;
  min-height: 100vh;
  min-height: 100dvh;
  margin: 0 auto;
  background-color: var(--color-bg);
}

.page-fade-enter-active,
.page-fade-leave-active {
  transition:
    opacity var(--duration-base) var(--ease-out),
    transform var(--duration-base) var(--ease-out);
}

.page-fade-enter-from {
  opacity: 0;
  transform: translateY(6px);
}

.page-fade-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}
</style>
