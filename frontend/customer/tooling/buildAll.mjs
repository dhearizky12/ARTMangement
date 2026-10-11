#!/usr/bin/env node
// Builds every agency's app into dist/<slug>/.
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'

const agents = fs
  .readdirSync('agents', { withFileTypes: true })
  .filter((e) => e.isDirectory() && fs.existsSync(`agents/${e.name}/agent.json`))
  .map((e) => e.name)
  .sort()

if (agents.length === 0) {
  console.error('No agents configured under agents/<slug>/agent.json')
  process.exit(1)
}

console.log(`Building ${agents.length} app(s): ${agents.join(', ')}\n`)

const failed = []
for (const slug of agents) {
  console.log(`-- ${slug}`)
  try {
    execFileSync('npx', ['vite', 'build'], {
      stdio: 'inherit',
      env: { ...process.env, AGENT: slug },
    })
  } catch {
    // Keep going so one bad config doesn't hide whether the others build.
    failed.push(slug)
    console.error(`\n${slug} failed\n`)
  }
}

if (failed.length) {
  console.error(`Failed: ${failed.join(', ')}`)
  process.exit(1)
}
console.log(`\nBuilt ${agents.length} app(s) into dist/`)
