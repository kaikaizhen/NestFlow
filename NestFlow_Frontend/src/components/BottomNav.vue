<script setup lang="ts">
import { computed, type Component } from 'vue'
import { useRoute } from 'vue-router'
import CalendarIcon from './icons/CalendarIcon.vue'
import LedgerIcon from './icons/LedgerIcon.vue'
import SettingsIcon from './icons/SettingsIcon.vue'

interface NavItem {
  name: string
  label: string
  icon: Component
}

const items: NavItem[] = [
  { name: 'calendar', label: '行事曆', icon: CalendarIcon },
  { name: 'ledger', label: '記帳', icon: LedgerIcon },
  { name: 'settings', label: '設定', icon: SettingsIcon },
]

const route = useRoute()
const currentName = computed(() => route.name)
</script>

<template>
  <nav class="bottom-nav" aria-label="主要導航">
    <RouterLink
      v-for="item in items"
      :key="item.name"
      class="bottom-nav__item"
      :class="{ 'is-active': currentName === item.name }"
      :to="{ name: item.name }"
      :aria-current="currentName === item.name ? 'page' : undefined"
    >
      <span class="bottom-nav__icon">
        <component :is="item.icon" :active="currentName === item.name" />
      </span>
      <span class="bottom-nav__label">{{ item.label }}</span>
    </RouterLink>
  </nav>
</template>

<style scoped>
.bottom-nav {
  position: fixed;
  right: 0;
  bottom: 0;
  left: 0;
  z-index: 20;
  /* 與 app-shell 同寬並置中，桌機瀏覽時不會拉滿整個視窗 */
  max-width: 480px;
  margin-inline: auto;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  background-color: var(--color-surface);
  border-top: 1px solid var(--color-border);
  /* 讓導航列避開 iOS 底部 Home Indicator */
  padding-bottom: env(safe-area-inset-bottom);
}

.bottom-nav__item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-1);
  /* 大型點擊區，長者也能輕鬆按到 */
  min-height: var(--nav-height);
  padding: var(--space-2) 0;
  color: var(--color-text-muted);
  text-decoration: none;
  transition: color var(--duration-base) var(--ease-out);
}

.bottom-nav__item.is-active {
  color: var(--color-accent);
}

.bottom-nav__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 56px;
  height: 30px;
  border-radius: var(--radius-full);
  background-color: transparent;
  transition:
    background-color var(--duration-base) var(--ease-out),
    transform var(--duration-fast) var(--ease-out);
}

.bottom-nav__item.is-active .bottom-nav__icon {
  background-color: var(--color-bg);
}

.bottom-nav__item:active .bottom-nav__icon {
  transform: scale(0.92);
}

.bottom-nav__label {
  font-size: var(--font-size-caption);
  font-weight: 500;
}

.bottom-nav__item.is-active .bottom-nav__label {
  font-weight: 600;
}
</style>
