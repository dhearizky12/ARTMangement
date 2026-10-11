import type { ReactNode } from 'react'

export function Centered({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-full flex-col items-center justify-center p-40 text-center">
      {children}
    </div>
  )
}

export function Spinner() {
  return (
    <div className="h-64 w-64 animate-spin-slow rounded-half border-[5px] border-pri-tint border-t-pri" />
  )
}
