import {
  Activity,
  EyeOff,
  FileClock,
  Network,
  ShieldAlert,
  ShieldBan,
  ShieldCheck,
  Users,
  type LucideIcon,
} from 'lucide-react'

export interface SystemSection {
  key: string
  icon: LucideIcon
  /** 額外說明的翻譯鍵 */
  noteKey?: string
}

/** 系統頁區塊與順序（contracts/ui-routes.md「系統頁區塊順序」、FR-022） */
export const SYSTEM_SECTIONS: SystemSection[] = [
  { key: 'accounts', icon: Users },
  { key: 'auditLogs', icon: FileClock },
  { key: 'endpoints', icon: Network, noteKey: 'system.endpoints.note' },
  { key: 'certificates', icon: ShieldCheck },
  { key: 'ddos', icon: ShieldAlert },
  { key: 'ipRules', icon: ShieldBan },
  { key: 'blockedPaths', icon: EyeOff },
  { key: 'tracing', icon: Activity },
]
