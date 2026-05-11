import type { Directive, DirectiveBinding } from 'vue'

function hasPermission(code: string | string[]): boolean {
  const userStr = localStorage.getItem('user')
  if (!userStr) return false

  try {
    const user = JSON.parse(userStr)
    if (user.roleName === '管理员') return true
    if (!Array.isArray(user.permissions)) return false

    if (user.permissions.includes('*')) return true

    const codes = Array.isArray(code) ? code : [code]
    return codes.some(c => user.permissions.includes(c) || user.permissions.some((p: string) => p.startsWith(`${c}.`)))
  } catch {
    return false
  }
}

export const permission: Directive = {
  mounted(el: HTMLElement, binding: DirectiveBinding) {
    const { value } = binding
    if (!value) return

    if (!hasPermission(value)) {
      el.parentNode?.removeChild(el)
    }
  }
}

export { hasPermission }
