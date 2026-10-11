import type { ReactNode } from 'react'
import { FieldLabel } from './Section'

export function Field({
  label,
  hint,
  children,
}: {
  label: string
  hint?: string
  children: ReactNode
}) {
  return (
    <div>
      <FieldLabel>{label}</FieldLabel>
      {children}
      {hint && <div className="mt-7 text-11 font-semibold text-ink-faint">{hint}</div>}
    </div>
  )
}

export function FieldBox({ children }: { children: ReactNode }) {
  return (
    <div className="flex items-center gap-9 rounded-field border-[1.5px] border-hairline bg-card p-13">
      {children}
    </div>
  )
}

export function FieldInput(props: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className="flex-1 text-13-5 font-semibold" />
}

export function PhoneInput({
  value,
  onChange,
  label,
  hint,
  readOnly = false,
  enterKeyHint = 'done',
}: {
  value: string
  onChange: (v: string) => void
  label?: string
  hint?: string
  readOnly?: boolean
  enterKeyHint?: 'done' | 'send' | 'next'
}) {
  const box = (
    <FieldBox>
      <span className="text-13-5 font-bold text-ink-soft">+62</span>
      <span className="h-16 w-1 bg-control-idle" />
      <FieldInput
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="812 3456 7890"
        inputMode="tel"
        autoComplete="tel-national"
        enterKeyHint={enterKeyHint}
        readOnly={readOnly}
      />
    </FieldBox>
  )
  if (!label) return box
  return (
    <div>
      <div className="mb-8 text-13-5 font-extrabold text-ink">{label}</div>
      {box}
      {hint && <div className="mt-7 text-11 font-semibold text-ink-faint">{hint}</div>}
    </div>
  )
}

export function LabelledInput({
  label,
  value,
  onChange,
  placeholder,
  optional = false,
  multiline = false,
}: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder: string
  optional?: boolean
  multiline?: boolean
}) {
  const box = 'w-full rounded-field border-[1.5px] border-hairline bg-card p-13 text-13'
  return (
    <div>
      <div className="mb-8 text-13-5 font-extrabold text-ink">
        {label} {optional && <span className="font-medium text-ink-faint">(opsional)</span>}
      </div>
      {multiline ? (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={`${box} h-78 resize-none leading-relaxed`}
        />
      ) : (
        <input
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={box}
        />
      )}
    </div>
  )
}
