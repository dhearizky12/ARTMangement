import { useNavigate } from 'react-router-dom'
import { IconButton } from '../ui'

export function BackButton({ tone }: { tone: 'onHero' | 'onSurface' }) {
  const navigate = useNavigate()
  return (
    <IconButton icon="ph-arrow-left" label="Kembali" tone={tone} onClick={() => navigate(-1)} />
  )
}
