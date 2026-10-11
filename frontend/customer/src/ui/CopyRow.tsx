import { useState } from 'react'
import { copyText } from '../lib/clipboard'

export function CopyRow({
  label,
  value,
  display,
}: {
  label: string
  value: string
  display?: string
}) {
  const [state, setState] = useState<'idle' | 'copied' | 'failed'>('idle')

  const copy = async () => {
    const ok = await copyText(value)
    setState(ok ? 'copied' : 'failed')
    setTimeout(() => setState('idle'), 2000)
  }

  return (
    <div className="flex items-center gap-12 rounded-14 border border-hairline bg-surface-raised px-14 py-12">
      <span className="min-w-0 flex-1">
        <span className="block text-11 font-semibold text-ink-faint">{label}</span>
        <span className="mt-2 block truncate text-17 font-extrabold tracking-loose text-ink">
          {display ?? value}
        </span>
      </span>
      <button
        type="button"
        onClick={() => void copy()}
        className="flex shrink-0 items-center gap-5 rounded-10 bg-pri-tint px-11 py-8 text-12 font-extrabold text-pri"
      >
        <i className={`ph ${state === 'copied' ? 'ph-check' : 'ph-copy'} text-14`} />
        {state === 'copied' ? 'Tersalin' : 'Salin'}
      </button>
      {state === 'failed' && (
        <span className="text-11 font-semibold text-danger">Gagal menyalin</span>
      )}
    </div>
  )
}
