<script setup lang="ts">
import { ref } from 'vue'
import { useWorkspaces } from '../stores/workspace'

const { workspaces, active, setActive } = useWorkspaces()
const isOpen = ref(false)

function choose(id: string) {
  setActive(id)
  isOpen.value = false
}
</script>

<template>
  <div class="switcher">
    <button
      class="switcher__trigger"
      type="button"
      :aria-expanded="isOpen"
      @click="isOpen = !isOpen"
    >
      <span>{{ active?.name ?? '選擇資料空間' }}</span>
      <svg
        class="switcher__caret"
        :class="{ 'is-open': isOpen }"
        viewBox="0 0 24 24"
        width="18"
        height="18"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path d="m6 9 6 6 6-6" />
      </svg>
    </button>

    <template v-if="isOpen">
      <div class="switcher__backdrop" @click="isOpen = false" />

      <ul class="switcher__menu">
        <li v-for="workspace in workspaces" :key="workspace.id">
          <button
            class="switcher__option"
            :class="{ 'is-active': workspace.id === active?.id }"
            type="button"
            @click="choose(workspace.id)"
          >
            <span>{{ workspace.name }}</span>
            <span class="switcher__tag">{{ workspace.type === 'family' ? '家庭' : '個人' }}</span>
          </button>
        </li>
      </ul>
    </template>
  </div>
</template>

<style scoped>
.switcher {
  position: relative;
}

.switcher__trigger {
  display: flex;
  gap: var(--space-1);
  align-items: center;
  min-height: 40px;
  padding: 0 var(--space-2);
  font-size: var(--font-size-body);
  font-weight: 600;
  border-radius: var(--radius-sm);
}

.switcher__trigger:active {
  background-color: var(--color-border);
}

.switcher__caret {
  transition: transform var(--duration-fast) var(--ease-out);
}

.switcher__caret.is-open {
  transform: rotate(180deg);
}

.switcher__backdrop {
  position: fixed;
  inset: 0;
  z-index: 30;
}

.switcher__menu {
  position: absolute;
  top: calc(100% + var(--space-1));
  left: 0;
  z-index: 31;
  min-width: 200px;
  margin: 0;
  padding: var(--space-1);
  list-style: none;
  background-color: var(--color-surface);
  border-radius: var(--radius-sm);
  box-shadow: 0 8px 28px rgb(17 17 19 / 12%);
}

.switcher__option {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 44px;
  padding: 0 var(--space-3);
  border-radius: var(--radius-sm);
  text-align: left;
}

.switcher__option.is-active {
  font-weight: 600;
  background-color: var(--color-bg);
}

.switcher__tag {
  font-size: 11px;
  color: var(--color-text-muted);
}
</style>
