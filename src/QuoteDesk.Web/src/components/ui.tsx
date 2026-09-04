import type { ButtonHTMLAttributes, HTMLAttributes, ReactNode } from 'react'
import { cn } from '../lib/cn'

/**
 * The small set of hand-rolled primitives the three screens share. Deliberately not a component
 * library — five endpoints and three screens do not need one, and the trace panel and approval card
 * (the parts that matter) are bespoke regardless.
 *
 * Visual language, lifted from the design canvas: slate ground, system UI font, `font-mono` +
 * `tabular-nums` for anything numeric, and emerald / amber / red reserved for line status only.
 */

// ── Button ───────────────────────────────────────────────────────────────────

type ButtonVariant = 'primary' | 'ghost' | 'danger'

const BUTTON_VARIANTS: Record<ButtonVariant, string> = {
  primary: 'bg-slate-900 text-white hover:bg-slate-800',
  ghost: 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50',
  danger: 'border border-red-300 bg-white text-red-700 hover:bg-red-50',
}

export function Button({
  variant = 'primary',
  className,
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  return (
    <button
      type={type}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-[12.5px] font-semibold transition-colors',
        'disabled:cursor-default disabled:opacity-55',
        BUTTON_VARIANTS[variant],
        className,
      )}
      {...props}
    />
  )
}

// ── Badge ────────────────────────────────────────────────────────────────────

type BadgeTone = 'ok' | 'warn' | 'bad' | 'neutral' | 'info'

const BADGE_TONES: Record<BadgeTone, string> = {
  ok: 'bg-emerald-50 text-emerald-700',
  warn: 'bg-amber-50 text-amber-700',
  bad: 'bg-red-50 text-red-700',
  neutral: 'bg-slate-100 text-slate-600',
  info: 'bg-blue-50 text-blue-700',
}

export function Badge({
  tone = 'neutral',
  className,
  ...props
}: HTMLAttributes<HTMLSpanElement> & { tone?: BadgeTone }) {
  return (
    <span
      className={cn(
        'inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.04em]',
        BADGE_TONES[tone],
        className,
      )}
      {...props}
    />
  )
}

// ── Card ─────────────────────────────────────────────────────────────────────

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('rounded-[10px] border border-slate-200 bg-white', className)} {...props} />
}

// ── Mono ─────────────────────────────────────────────────────────────────────

export function Mono({ className, ...props }: HTMLAttributes<HTMLSpanElement>) {
  return <span className={cn('font-mono tabular-nums tracking-tight', className)} {...props} />
}

// ── Eyebrow ──────────────────────────────────────────────────────────────────

export function Eyebrow({ className, ...props }: HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      className={cn(
        'text-[10.5px] font-semibold uppercase tracking-[0.07em] text-slate-400',
        className,
      )}
      {...props}
    />
  )
}

// ── Field ────────────────────────────────────────────────────────────────────

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-[10px] font-semibold uppercase tracking-[0.07em] text-slate-400">
        {label}
      </span>
      {children}
    </label>
  )
}

// ── StatusDot ────────────────────────────────────────────────────────────────

type DotTone = 'ok' | 'warn' | 'bad' | 'running' | 'idle'

const DOT_TONES: Record<DotTone, string> = {
  ok: 'bg-emerald-500',
  warn: 'bg-amber-500',
  bad: 'bg-red-500',
  running: 'bg-amber-500 animate-pulse',
  idle: 'bg-slate-300',
}

export function StatusDot({ tone }: { tone: DotTone }) {
  return <span className={cn('inline-block size-[7px] shrink-0 rounded-full', DOT_TONES[tone])} />
}

// ── Logo ─────────────────────────────────────────────────────────────────────

/** The one QuoteDesk mark, used at two sizes (the header's small badge, the sign-in card's larger
 * one) — previously the header used a checkmark and the sign-in screen a "Q" glyph, so the two
 * screens a visitor sees first showed two different brand marks. "Q" is the one both now render, tied
 * directly to the product name. `size` is the outer square's side in pixels; the letter's font size
 * scales proportionally so it reads correctly at either size. */
export function Logo({ size = 20, className }: { size?: number; className?: string }) {
  return (
    <span
      className={cn(
        'flex shrink-0 items-center justify-center rounded-[5px] bg-slate-900 font-semibold text-white',
        className,
      )}
      style={{ width: size, height: size, fontSize: size * 0.45 }}
    >
      Q
    </span>
  )
}

// ── IconButton ───────────────────────────────────────────────────────────────

type IconButtonTone = 'neutral' | 'warn'

const ICON_BUTTON_TONES: Record<IconButtonTone, string> = {
  neutral: 'text-slate-500 hover:bg-slate-100 hover:text-slate-900',
  warn: 'text-amber-700 hover:bg-amber-50',
}

/** A compact icon-only action button with a hover tooltip standing in for its visible label — for a
 * row of actions (Retry / Edit & re-run / New enquiry) where three text buttons packed together read
 * as one run-on phrase rather than three distinct actions. `label` is both the tooltip text and the
 * accessible name (there is no visible text otherwise). CSS-only tooltip, no library: this app has no
 * component dependency for anything else, so adding one for a single hover bubble would be the odd
 * one out. */
export function IconButton({
  label,
  tone = 'neutral',
  className,
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  label: string
  tone?: IconButtonTone
  children: ReactNode
}) {
  return (
    <span className="group relative inline-flex">
      <button
        type="button"
        aria-label={label}
        className={cn(
          'flex size-7 items-center justify-center rounded-md transition-colors',
          ICON_BUTTON_TONES[tone],
          className,
        )}
        {...props}
      >
        {children}
      </button>
      <span
        role="tooltip"
        className="pointer-events-none absolute left-1/2 top-full z-10 mt-1.5 -translate-x-1/2 whitespace-nowrap rounded-md bg-slate-900 px-2 py-1 text-[10.5px] font-medium text-white opacity-0 transition-opacity delay-300 group-hover:opacity-100"
      >
        {label}
      </span>
    </span>
  )
}

// ── Spinner ──────────────────────────────────────────────────────────────────

export function Spinner({ className }: { className?: string }) {
  return (
    <svg
      className={cn('size-4 animate-spin text-slate-400', className)}
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" opacity="0.25" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  )
}

// ── AsyncBoundary ────────────────────────────────────────────────────────────

/** Renders the loading / empty / error scaffolding so each screen only writes its ready branch. */
export function AsyncBoundary({
  status,
  error,
  onRetry,
  empty,
  children,
}: {
  status: 'loading' | 'empty' | 'ready' | 'error'
  error?: Error
  onRetry?: () => void
  empty?: ReactNode
  children: ReactNode
}) {
  if (status === 'loading') {
    return (
      <div className="flex items-center justify-center gap-2 py-16 text-sm text-slate-500">
        <Spinner /> Loading…
      </div>
    )
  }
  if (status === 'error') {
    return (
      <div className="flex flex-col items-center gap-3 py-16 text-center">
        <p className="max-w-sm text-sm text-slate-600">
          {error?.message ?? 'Something went wrong.'}
        </p>
        {onRetry && (
          <Button variant="ghost" onClick={onRetry}>
            Try again
          </Button>
        )}
      </div>
    )
  }
  if (status === 'empty') {
    return (
      <div className="py-16 text-center text-sm text-slate-500">{empty ?? 'Nothing here yet.'}</div>
    )
  }
  return <>{children}</>
}
