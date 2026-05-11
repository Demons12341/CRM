<template>
  <div class="dashboard-card pending-contract-card">
    <div class="card-header">
      <h3 class="card-title">
        <el-icon><Document /></el-icon>
        待签合同项目
        <span v-if="items.length > 0" class="item-count">{{ items.length }}</span>
      </h3>
    </div>
    <div class="card-body">
      <div v-if="items.length > 0" class="project-list">
        <div
          v-for="item in items"
          :key="item.id"
          class="project-item"
          @click="$emit('select', item)"
        >
          <div class="project-info">
            <div class="project-name">{{ item.name }}</div>
            <div class="project-meta">
              <span class="manager">{{ item.managerName }}</span>
              <span v-if="item.budget" class="budget">¥{{ formatBudget(item.budget) }}</span>
            </div>
          </div>
          <div class="project-time">{{ formatDate(item.updatedAt) }}</div>
        </div>
      </div>
      <el-empty v-else description="暂无待签合同项目" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import dayjs from 'dayjs'

defineEmits<{
  select: [item: any]
}>()

defineProps<{
  items: Array<{
    id: number
    name: string
    businessLine?: string
    managerName: string
    budget: number | null
    updatedAt: string
  }>
}>()

const formatDate = (date: string) => {
  if (!date) return '-'
  return dayjs(date).format('MM-DD')
}

const formatBudget = (budget: number) => {
  if (budget >= 10000) {
    return (budget / 10000).toFixed(1) + '万'
  }
  return budget.toLocaleString()
}
</script>

<style scoped>
.dashboard-card {
  background: #ffffff;
  border-radius: 14px;
  border: 1px solid #dce8fb;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 20px rgba(31, 59, 115, 0.08);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  border-bottom: 1px solid #e4ecfa;
}

.card-title {
  font-size: 14px;
  font-weight: 700;
  color: #1d3250;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-title .el-icon {
  color: #3b82f6;
}

.item-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  background: #eff6ff;
  color: #3b82f6;
  font-size: 12px;
  font-weight: 500;
  border-radius: 10px;
  margin-left: 4px;
}

.card-body {
  flex: 1;
  padding: 12px;
  min-height: 286px;
  max-height: 340px;
  overflow-y: auto;
}

.project-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.project-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px;
  border-radius: 10px;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
  border: 1px solid transparent;
  gap: 12px;
}

.project-item:hover {
  background: #f3f8ff;
  border-color: #dce8fb;
}

.project-info {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.project-name {
  font-size: 14px;
  color: #1d3250;
  font-weight: 600;
  margin-bottom: 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #71839b;
}

.budget {
  color: #f59e0b;
  font-weight: 600;
}

.project-time {
  font-size: 12px;
  color: #94a3b8;
  flex-shrink: 0;
}
</style>
