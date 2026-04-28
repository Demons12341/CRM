<template>
  <router-view />
</template>

<script setup lang="ts">
// App.vue - 根组件
</script>

<style>
html, body, #app {
  margin: 0;
  padding: 0;
  height: 100%;
  font-family: 'Helvetica Neue', Helvetica, 'PingFang SC', 'Hiragino Sans GB', 'Microsoft YaHei', Arial, sans-serif;
}

:root {
  --app-dialog-radius: 14px;
  --app-dialog-border: #e5e7eb;
  --app-dialog-header-bg: #f8fafc;
  --app-dialog-header-text: #1f2937;
}

.el-dialog {
  border-radius: var(--app-dialog-radius);
  border: 1px solid var(--app-dialog-border);
  box-shadow: 0 14px 40px rgba(15, 23, 42, 0.14);
  overflow: hidden;
}

.el-dialog__header {
  margin: 0;
  padding: 14px 18px;
  background: var(--app-dialog-header-bg);
  border-bottom: 1px solid #edf0f5;
}

.el-dialog__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--app-dialog-header-text);
}

.el-dialog__headerbtn {
  top: 14px;
  right: 14px;
}

.el-dialog__body {
  padding: 18px;
}

.el-dialog__footer {
  padding: 12px 18px 16px;
  border-top: 1px solid #edf0f5;
  background: #ffffff;
}

.el-message-box {
  border-radius: 12px;
  border: 1px solid var(--app-dialog-border);
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.16);
}

.el-message-box__header {
  padding: 14px 16px;
  border-bottom: 1px solid #edf0f5;
}

.el-message-box__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--app-dialog-header-text);
}

.el-message-box__content {
  padding: 16px;
}

.el-message-box__btns {
  padding: 0 16px 14px;
}

@media (max-width: 768px) {
  .el-dialog {
    width: calc(100vw - 24px) !important;
    margin: 0 auto;
  }
}
</style>
