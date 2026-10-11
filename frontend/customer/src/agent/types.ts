// What a build needs to know about one agency. Everything else (rating, city,
// vetting, promo) comes from GET /agency so it can change without a rebuild.
export interface AgentConfig {
  slug: string
  appName: string
  agencyName: string
  initials: string
  // Seed colour, the whole --pri ramp is derived from it
  color: string
  // Square logo in agents/<slug>/public/, rasterised into the app icons
  logo: string
  // Used for <meta name="description"> and the web manifest
  desc: string
}
