import { Link } from 'react-router-dom'

export function NotFound() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-10 p-28 text-center">
      <i className="ph ph-compass text-40 text-ink-ghost" />
      <h1 className="m-0 text-17 font-extrabold">Halaman tidak ditemukan</h1>
      <Link to="/" className="text-13 font-bold">
        Kembali
      </Link>
    </div>
  )
}
