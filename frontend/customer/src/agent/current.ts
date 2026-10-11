import config from 'virtual:agent'
import { applyAgentColors } from './applyAgentColors'
import type { AgentConfig } from './types'

// Fixed at build time, so a plain constant rather than a hook.
export const agent: AgentConfig = config

export function initAgentTheme(): void {
  applyAgentColors(agent.color)
}
