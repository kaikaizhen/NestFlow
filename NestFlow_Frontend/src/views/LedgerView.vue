<script setup lang="ts">
import AppPage from '../components/AppPage.vue'
import PlaceholderCard from '../components/PlaceholderCard.vue'
</script>

<template>
  <AppPage title="記帳">
    <PlaceholderCard
      title="月收入、月支出與結餘"
      description="收入以綠色顯示，支出以紅色顯示。"
      module-label="Module 3 實作"
    />
    <PlaceholderCard
      title="最近交易"
      description="列出最近的收支紀錄，可修改與軟刪除。"
      module-label="Module 3 實作"
    />
    <PlaceholderCard
      title="新增記帳"
      description="以表單建立收支，不經過自然語言解析。"
      module-label="Module 3 實作"
    />
  </AppPage>
</template>
