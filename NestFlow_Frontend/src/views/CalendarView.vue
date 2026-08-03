<script setup lang="ts">
import AppPage from '../components/AppPage.vue'
import PlaceholderCard from '../components/PlaceholderCard.vue'
</script>

<template>
  <AppPage title="行事曆">
    <PlaceholderCard
      title="月曆"
      description="以月為單位檢視行程，有行程的日期會顯示標記。"
      module-label="Module 5 實作"
    />
    <PlaceholderCard
      title="即將到來"
      description="依時間排序列出今天與本週的行程。"
      module-label="Module 5 實作"
    />
    <PlaceholderCard
      title="新增行程"
      description="以表單建立行程，不經過自然語言解析。"
      module-label="Module 5 實作"
    />
  </AppPage>
</template>
