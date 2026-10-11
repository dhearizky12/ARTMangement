import { Link, useLocation } from 'react-router-dom'

interface NavItem {
  label: string
  icon: string
  to: string
  // Other paths that keep this tab lit
  alsoActive?: string[]
}

const NAV: NavItem[] = [
  { label: 'Beranda', icon: 'ph-house', to: '' },
  { label: 'Cari', icon: 'ph-magnifying-glass', to: 'browse', alsoActive: ['profile', 'agency'] },
  { label: 'Pesanan', icon: 'ph-clipboard-text', to: 'bookings' },
  { label: 'Akun', icon: 'ph-user', to: 'akun' },
]

export function BottomNav() {
  const { pathname } = useLocation()
  const rel = pathname.replace(/^\/|\/$/g, '')

  return (
    <nav className="fixed right-0 bottom-0 left-0 z-30 flex items-stretch border-t border-hairline bg-card pt-tabbar-pad pb-tabbar-bottom">
      {NAV.map((item) => {
        const paths = [item.to, ...(item.alsoActive ?? [])]
        const on =
          item.to === '' ? rel === '' : paths.some((p) => rel === p || rel.startsWith(`${p}/`))
        const ink = on ? 'text-pri' : 'text-ink-dim'

        return (
          <Link
            key={item.label}
            to={`/${item.to}`}
            className="flex flex-1 flex-col items-center justify-center gap-3 no-underline select-none"
          >
            <i className={`${on ? 'ph-fill' : 'ph'} ${item.icon} text-22 ${ink}`} />
            <span className={`text-10-5 font-bold ${ink}`}>{item.label}</span>
          </Link>
        )
      })}
    </nav>
  )
}
