import fs from 'node:fs'
import path from 'node:path'
import type { AgentConfig } from '../src/agent/types'

export const AGENTS_DIR = 'agents'

function fail(message: string): never {
  console.error(`\n  agents: ${message.replace(/\n/g, '\n  ')}\n`)
  process.exit(1)
}

export function listAgents(root: string): string[] {
  const dir = path.join(root, AGENTS_DIR)
  if (!fs.existsSync(dir)) return []
  return fs
    .readdirSync(dir, { withFileTypes: true })
    .filter((e) => e.isDirectory() && fs.existsSync(path.join(dir, e.name, 'agent.json')))
    .map((e) => e.name)
    .sort()
}

const STRINGS = ['slug', 'appName', 'agencyName', 'initials', 'color', 'logo', 'desc'] as const

export function loadAgent(root: string, slug: string): AgentConfig {
  const dir = path.join(root, AGENTS_DIR, slug)
  const file = path.join(dir, 'agent.json')
  const where = path.relative(root, file)

  if (!fs.existsSync(file)) {
    const known = listAgents(root)
    fail(`No agent config at ${where}.\nKnown agents: ${known.join(', ') || '(none)'}`)
  }

  let cfg: Partial<AgentConfig>
  try {
    cfg = JSON.parse(fs.readFileSync(file, 'utf8')) as Partial<AgentConfig>
  } catch (e) {
    fail(`${where} is not valid JSON: ${(e as Error).message}`)
  }

  const bad = (msg: string): never => fail(`${where}: ${msg}`)

  for (const k of STRINGS) {
    const v = cfg[k]
    if (typeof v !== 'string' || v.trim() === '') bad(`"${k}" must be a non-empty string`)
  }
  if (cfg.slug !== slug) bad(`"slug" is "${cfg.slug}" but the directory is "${slug}"`)
  if (!/^#[0-9a-fA-F]{6}$/.test(cfg.color as string)) {
    bad(`"color" must be a 6-digit hex colour like "#2f6a4f", got "${cfg.color}"`)
  }

  const logoPath = path.join(dir, 'public', cfg.logo as string)
  if (!fs.existsSync(logoPath)) {
    bad(`"logo" is "${cfg.logo}" but ${path.relative(root, logoPath)} does not exist`)
  }
  assertSquare(logoPath, bad)

  return cfg as AgentConfig
}

// A wide logo would be letterboxed into every app icon.
function assertSquare(logoPath: string, bad: (m: string) => never): void {
  if (path.extname(logoPath).toLowerCase() !== '.svg') return

  const svg = fs.readFileSync(logoPath, 'utf8')
  const viewBox = /viewBox\s*=\s*["']\s*([\d.-]+)\s+([\d.-]+)\s+([\d.-]+)\s+([\d.-]+)/.exec(svg)
  if (!viewBox) return

  const w = Number(viewBox[3])
  const h = Number(viewBox[4])
  if (w && h && Math.abs(w - h) / Math.max(w, h) > 0.02) {
    bad(`"logo" must be square, ${path.basename(logoPath)} has a ${w}x${h} viewBox.`)
  }
}

// No default agency: building the wrong one silently is worse than failing.
export function resolveAgentSlug(root: string): string {
  const slug = process.env.AGENT?.trim()
  const known = listAgents(root)

  if (!slug) {
    fail(
      `AGENT is not set, every build is for one agency.\n` +
        `  AGENT=${known[0] ?? '<slug>'} npm run dev\n` +
        `Available: ${known.join(', ') || '(none)'}`,
    )
  }
  if (!known.includes(slug)) {
    fail(`Unknown agent "${slug}". Available: ${known.join(', ') || '(none)'}`)
  }
  return slug
}
