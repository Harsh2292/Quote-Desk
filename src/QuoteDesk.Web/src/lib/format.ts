const IST = 'Asia/Kolkata'

/** ₹ with Indian digit grouping and two decimals — the invoicing convention the domain rounds to. */
export function money(amount: number): string {
  return (
    '₹' +
    amount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  )
}

/** "31 Aug 2026" from an ISO date or datetime string, shown in IST. Display only. */
export function shortDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: IST,
  })
}

/** "31 Aug 2026, 08:47" in IST. */
export function shortDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return date.toLocaleString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: IST,
  })
}

export function relativeTime(iso: string): string {
  const then = new Date(iso).getTime()
  if (Number.isNaN(then)) return ''
  const minutes = Math.round((Date.now() - then) / 60_000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours} h ago`
  return `${Math.round(hours / 24)} d ago`
}

/** "340ms" / "1.08s" for a trace row. */
export function duration(ms: number): string {
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(2)}s`
}
