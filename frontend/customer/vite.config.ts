import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { agentPlugin } from './tooling/agentPlugin'
import { AGENTS_DIR, loadAgent, resolveAgentSlug } from './tooling/agents'

const root = __dirname

// One build per agency: AGENT=<slug> picks agents/<slug>/agent.json.
export default defineConfig(() => {
  const slug = resolveAgentSlug(root)
  const agent = loadAgent(root, slug)

  return {
    plugins: [react(), tailwindcss(), agentPlugin(agent, root)],
    // Relative assets, so the bundle works under any mount path.
    base: './',
    publicDir: path.join(root, AGENTS_DIR, slug, 'public'),
    build: { outDir: path.join('dist', slug), emptyOutDir: true },
    server: { port: 5174 },
  }
})
