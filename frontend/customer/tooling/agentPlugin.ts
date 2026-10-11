import fs from 'node:fs'
import path from 'node:path'
import sharp from 'sharp'
import type { Plugin } from 'vite'
import { AGENTS_DIR } from './agents'
import type { AgentConfig } from '../src/agent/types'

const VIRTUAL_ID = 'virtual:agent'
const RESOLVED_ID = `\0${VIRTUAL_ID}`
const MANIFEST = 'manifest.webmanifest'

// PNG sizes rendered from the logo. Android needs a raster icon to offer install,
// and Capacitor's asset generator wants the 1024.
const ICON_SIZES = [180, 192, 512, 1024]

// Maskable icons are cropped to a circle, so the mark stays inside 80%.
const MASKABLE_SAFE_ZONE = 0.8

// Bakes one agency's config into the bundle as `virtual:agent`, and generates the
// manifest and icons from its logo.
export function agentPlugin(agent: AgentConfig, root: string): Plugin {
  const logoPath = path.join(root, AGENTS_DIR, agent.slug, 'public', agent.logo)

  let icons: Promise<GeneratedIcon[]> | null = null
  const getIcons = () => (icons ??= generateIcons(agent, logoPath))

  return {
    name: 'agent',

    resolveId(id) {
      return id === VIRTUAL_ID ? RESOLVED_ID : null
    },

    load(id) {
      if (id === RESOLVED_ID) {
        return `export default JSON.parse(${JSON.stringify(JSON.stringify(agent))})`
      }
      return null
    },

    transformIndexHtml(html) {
      return html
        .replaceAll('%AGENT_APP_NAME%', escapeHtml(agent.appName))
        .replaceAll('%AGENT_COLOR%', agent.color)
        .replaceAll('%AGENT_DESC%', escapeHtml(agent.desc))
    },

    async generateBundle() {
      this.emitFile({ type: 'asset', fileName: MANIFEST, source: manifestJson(agent) })
      for (const icon of await getIcons()) {
        this.emitFile({ type: 'asset', fileName: icon.fileName, source: icon.buffer })
      }
    },

    // The dev server doesn't run generateBundle, so serve the same files here.
    configureServer(server) {
      server.watcher.add(logoPath)
      server.watcher.on('change', (file) => {
        if (file === logoPath) icons = null
      })

      server.middlewares.use((req, res, next) => {
        const url = req.url?.split('?')[0] ?? ''

        if (url.endsWith(`/${MANIFEST}`)) {
          res.setHeader('content-type', 'application/manifest+json')
          res.end(manifestJson(agent))
          return
        }

        const match = /\/(logo-[\w-]+\.png)$/.exec(url)
        if (!match) return next()

        void getIcons().then((set) => {
          const icon = set.find((i) => i.fileName === match[1])
          if (!icon) return next()
          res.setHeader('content-type', 'image/png')
          res.end(icon.buffer)
        }, next)
      })
    },
  }
}

interface GeneratedIcon {
  fileName: string
  buffer: Buffer
}

async function generateIcons(agent: AgentConfig, logoPath: string): Promise<GeneratedIcon[]> {
  const src = fs.readFileSync(logoPath)
  // Render the SVG at 4x density so the 1024 icon isn't an upscaled 512.
  const render = (input: Buffer) => sharp(input, { density: 288 })
  const clear = { r: 0, g: 0, b: 0, alpha: 0 }

  const icons: GeneratedIcon[] = await Promise.all(
    ICON_SIZES.map(async (size) => ({
      fileName: `logo-${size}.png`,
      buffer: await render(src)
        .resize(size, size, { fit: 'contain', background: clear })
        .png()
        .toBuffer(),
    })),
  )

  const canvas = 512
  const inner = Math.round(canvas * MASKABLE_SAFE_ZONE)
  const innerPng = await render(src)
    .resize(inner, inner, { fit: 'contain', background: clear })
    .png()
    .toBuffer()

  icons.push({
    fileName: 'logo-maskable-512.png',
    buffer: await sharp({
      create: { width: canvas, height: canvas, channels: 4, background: agent.color },
    })
      .composite([{ input: innerPng, gravity: 'centre' }])
      .png()
      .toBuffer(),
  })

  return icons
}

// URLs are relative so the manifest works under any deployment prefix.
function manifestJson(agent: AgentConfig): string {
  return JSON.stringify(
    {
      name: agent.appName,
      short_name: agent.appName,
      description: agent.desc,
      start_url: './',
      scope: './',
      display: 'standalone',
      background_color: '#faf6f0',
      theme_color: agent.color,
      lang: 'id',
      icons: [
        { src: './logo.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
        { src: './logo-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
        { src: './logo-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
        { src: './logo-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
      ],
    },
    null,
    2,
  )
}

function escapeHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')
}
