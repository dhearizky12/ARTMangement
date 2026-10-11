import type { ReactNode } from 'react'

interface Shared {
  placeholder: string
  trailing?: ReactNode
  tone?: 'plain' | 'raised'
}

type SearchFieldProps = Shared &
  (
    | {
        onActivate: () => void
        value?: never
        onChange?: never
        onSubmit?: never
        onClear?: never
        inputRef?: never
      }
    | {
        onActivate?: never
        value: string
        onChange: (v: string) => void
        onSubmit?: () => void
        onClear?: () => void
        inputRef?: React.Ref<HTMLInputElement>
      }
  )

export function SearchField({
  value,
  onChange,
  placeholder,
  onSubmit,
  onClear,
  trailing,
  onActivate,
  tone = 'plain',
  inputRef,
}: SearchFieldProps) {
  const shell = `flex w-full items-center gap-11 ${
    tone === 'raised'
      ? 'rounded-16 bg-white px-16 py-13 shadow-float'
      : 'rounded-field border border-hairline bg-card px-13 py-11'
  }`
  const icon = `ph ph-magnifying-glass ${
    tone === 'raised' ? 'text-20 text-pri' : 'text-18 text-ink-faint'
  }`

  if (onActivate) {
    return (
      <button type="button" onClick={onActivate} className={`${shell} text-left`}>
        <i className={icon} />
        <span className="min-w-0 flex-1 truncate text-14 font-medium text-ink-faint">
          {placeholder}
        </span>
        {trailing}
      </button>
    )
  }

  return (
    <form
      role="search"
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit?.()
      }}
      className={shell}
    >
      <i className={icon} />
      <input
        ref={inputRef}
        type="search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={placeholder}
        // "Cari" on the action key, and none of the capitalisation or correction
        // a name or a service label would fight with.
        enterKeyHint="search"
        autoCapitalize="none"
        autoCorrect="off"
        spellCheck={false}
        className="min-w-0 flex-1 bg-transparent text-14 font-medium text-ink placeholder:text-ink-faint"
      />
      {value && onClear && (
        <button
          type="button"
          aria-label="Kosongkan kolom pencarian"
          onClick={onClear}
          className="flex h-22 w-22 shrink-0 items-center justify-center rounded-half bg-surface-muted text-ink-soft"
        >
          <i className="ph ph-x text-12" />
        </button>
      )}
      {trailing}
    </form>
  )
}
