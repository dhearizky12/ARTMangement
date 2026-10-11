/// <reference types="vite/client" />

declare module 'virtual:agent' {
  import type { AgentConfig } from './agent/types'
  const config: AgentConfig
  export default config
}
