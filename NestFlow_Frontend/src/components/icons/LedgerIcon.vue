<script setup lang="ts">
defineProps<{ active?: boolean }>()
</script>

<template>
  <svg
    viewBox="0 0 24 24"
    width="24"
    height="24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.8"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <circle cx="12" cy="12" r="9" />
    <path v-if="!active" d="M12 3v9l6.4 4.2" />
    <path v-else d="M12 3a9 9 0 0 1 9 9h-9z" fill="currentColor" stroke="none" />
  </svg>
</template>
