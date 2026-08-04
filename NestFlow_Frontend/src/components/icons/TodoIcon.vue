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
    <!-- 選中時第一項呈現已勾選，勾勾改用底色畫在實心方塊上 -->
    <rect x="3" y="4" width="8" height="8" rx="2.5" :fill="active ? 'currentColor' : 'none'" />
    <path d="m5.4 8 1.5 1.5L9 6.8" :stroke="active ? 'var(--color-surface)' : 'currentColor'" />
    <path d="M14 8h7" />

    <rect x="3" y="13" width="8" height="8" rx="2.5" />
    <path d="M14 17h7" />
  </svg>
</template>
