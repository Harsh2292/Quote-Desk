import { useMemo, useState } from 'react'
import type { AgentEvent } from '../api/agentEvents'
import { stageBadge, stageLabel, toolLabel } from '../api/traceLabels'
import { duration } from '../lib/format'
import { cn } from '../lib/cn'
import { Badge, Eyebrow, StatusDot } from './ui'

/**
 * The Agent Trace panel — "the product" per CLAUDE.md. Stage badge, a plain-language label for each
 * step, what it looked at and returned (collapsible), duration, ok/fail. Raw tool names are never
 * shown. Driven entirely by the `AgentEvent[]` it is handed, whether that is a live SSE stream or a
 * trace replayed from the database.
 */

interface ToolItem {
  kind: 'tool'
  key: number
  name: string
  args: unknown
  end?: { ms: number; ok: boolean; result: unknown }
}

interface StageItem {
  kind: 'stage'
  key: number
  stage: string
  at: string
  endsAt: string | null
}

type TraceItem =
  | StageItem
  | ToolItem
  | { kind: 'approval'; key: number }
  | { kind: 'error'; key: number; code: string; message: string }
  | { kind: 'done'; key: number; promptTokens: number; completionTokens: number }

interface BuiltTrace {
  items: TraceItem[]
  narration: string
}

function buildTrace(events: AgentEvent[]): BuiltTrace {
  const items: TraceItem[] = []
  const openTools = new Map<string, ToolItem>()
  let narration = ''

  events.forEach((event, index) => {
    switch (event.type) {
      case 'stage': {
        items.push({ kind: 'stage', key: index, stage: event.stage, at: event.at, endsAt: null })
        break
      }
      case 'tool_start': {
        const item: ToolItem = { kind: 'tool', key: index, name: event.name, args: event.args }
        items.push(item)
        openTools.set(event.name, item)
        break
      }
      case 'tool_end': {
        const item = openTools.get(event.name)
        if (item) {
          item.end = { ms: event.ms, ok: event.ok, result: event.result }
          openTools.delete(event.name)
        }
        break
      }
      case 'token': {
        narration += event.text
        break
      }
      case 'approval_required': {
        items.push({ kind: 'approval', key: index })
        break
      }
      case 'error': {
        items.push({ kind: 'error', key: index, code: event.code, message: event.message })
        break
      }
      case 'done': {
        items.push({
          kind: 'done',
          key: index,
          promptTokens: event.usage.promptTokens,
          completionTokens: event.usage.completionTokens,
        })
        break
      }
    }
  })

  // Give each stage an end time from the next stage's start, so a duration can be shown.
  const stages = items.filter((i): i is StageItem => i.kind === 'stage')
  stages.forEach((stage, i) => {
    stage.endsAt = stages[i + 1]?.at ?? null
  })

  return { items, narration }
}

function stageDuration(item: StageItem): string | null {
  if (!item.endsAt) return null
  const ms = new Date(item.endsAt).getTime() - new Date(item.at).getTime()
  return ms >= 0 && Number.isFinite(ms) ? duration(ms) : null
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return String(value)
  if (typeof value === 'string') return value
  try {
    return JSON.stringify(value, null, 2)
  } catch {
    return String(value)
  }
}

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      className={cn('size-3 shrink-0 text-slate-400 transition-transform', open && 'rotate-90')}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.4"
      aria-hidden="true"
    >
      <path d="M9 6l6 6-6 6" />
    </svg>
  )
}

function ToolRow({ item, live }: { item: ToolItem; live: boolean }) {
  const [open, setOpen] = useState(false)
  const running = !item.end
  const failed = item.end?.ok === false

  return (
    <div className="border-b border-slate-50 last:border-0">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center gap-2.5 py-2 pl-11 pr-5 text-left hover:bg-slate-50"
      >
        <Chevron open={open} />
        <span className="text-[12px] font-medium text-slate-900">{toolLabel(item.name)}</span>
        <span className="flex-1" />
        {item.end && (
          <span className="font-mono text-[11.5px] tabular-nums text-slate-500">
            {duration(item.end.ms)}
          </span>
        )}
        {running && live && <StatusDot tone="running" />}
        {running && live && <span className="text-[11px] font-semibold text-amber-700">running</span>}
        {item.end && <StatusDot tone={failed ? 'bad' : 'ok'} />}
        {item.end && (
          <span
            className={cn(
              'text-[11px] font-semibold',
              failed ? 'text-red-600' : 'text-emerald-600',
            )}
          >
            {failed ? 'failed' : 'ok'}
          </span>
        )}
      </button>

      {open && (
        <div className="space-y-2 px-5 pb-3 pl-11">
          <TraceBlock label="Looked at" value={formatValue(item.args)} />
          {item.end && (
            <TraceBlock
              label={failed ? 'Error' : 'Result'}
              value={formatValue(item.end.result)}
              tone={failed ? 'bad' : 'plain'}
            />
          )}
        </div>
      )}
    </div>
  )
}

function TraceBlock({
  label,
  value,
  tone = 'plain',
}: {
  label: string
  value: string
  tone?: 'plain' | 'bad'
}) {
  return (
    <div>
      <div className="mb-1 text-[10px] font-semibold uppercase tracking-[0.05em] text-slate-400">
        {label}
      </div>
      <pre
        className={cn(
          'overflow-x-auto whitespace-pre-wrap rounded-md border px-3 py-2 font-mono text-[11px] leading-relaxed',
          tone === 'bad'
            ? 'border-red-200 bg-red-50 text-red-700'
            : 'border-slate-200 bg-slate-50 text-slate-600',
        )}
      >
        {value}
      </pre>
    </div>
  )
}

export function TracePanel({
  events,
  live = false,
  meta,
  className,
}: {
  events: AgentEvent[]
  live?: boolean
  meta?: string
  className?: string
}) {
  const { items, narration } = useMemo(() => buildTrace(events), [events])

  return (
    <section className={cn('flex min-h-0 flex-col', className)}>
      <header className="flex items-center justify-between border-b border-slate-200 bg-white px-5 py-3.5">
        <Eyebrow>Agent trace</Eyebrow>
        <span className="font-mono text-[11px] tabular-nums text-slate-400">
          {meta ?? (live ? 'running' : 'idle')}
        </span>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {items.length === 0 ? (
          <EmptyTrace />
        ) : (
          items.map((item) => {
            switch (item.kind) {
              case 'stage': {
                const dur = stageDuration(item)
                return (
                  <div
                    key={item.key}
                    className="flex items-center gap-2.5 border-b border-slate-100 bg-white px-5 py-2.5"
                  >
                    <Badge tone="neutral">{stageBadge(item.stage)}</Badge>
                    <span className="text-[12px] font-medium text-slate-600">
                      {stageLabel(item.stage)}
                    </span>
                    <span className="flex-1" />
                    {dur && (
                      <span className="font-mono text-[11.5px] tabular-nums text-slate-500">
                        {dur}
                      </span>
                    )}
                    <StatusDot tone={item.endsAt || !live ? 'ok' : 'running'} />
                  </div>
                )
              }
              case 'tool':
                return <ToolRow key={item.key} item={item} live={live} />
              case 'approval':
                return (
                  <div
                    key={item.key}
                    className="flex items-center gap-2.5 border-b border-slate-100 bg-amber-50 px-5 py-2.5"
                  >
                    <Badge tone="warn">Approval</Badge>
                    <span className="text-[12px] font-medium text-amber-800">
                      Waiting for a human decision
                    </span>
                  </div>
                )
              case 'error':
                return (
                  <div
                    key={item.key}
                    className="flex items-start gap-2.5 border-b border-slate-100 bg-red-50 px-5 py-2.5"
                  >
                    <Badge tone="bad">Error</Badge>
                    <span className="text-[12px] text-red-700">{item.message}</span>
                  </div>
                )
              case 'done':
                return (
                  <div
                    key={item.key}
                    className="px-5 py-2.5 font-mono text-[11px] tabular-nums text-slate-400"
                  >
                    done · {item.promptTokens.toLocaleString()} in ·{' '}
                    {item.completionTokens.toLocaleString()} out
                  </div>
                )
            }
          })
        )}

        {narration && (
          <p className="whitespace-pre-wrap px-5 py-3 text-[12.5px] leading-relaxed text-slate-600">
            {narration}
          </p>
        )}
      </div>
    </section>
  )
}

function EmptyTrace() {
  return (
    <div className="flex h-full items-center justify-center p-10">
      <div className="max-w-xs text-center">
        <svg
          className="mx-auto text-slate-300"
          width="30"
          height="30"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.5"
          aria-hidden="true"
        >
          <path d="M4 7h16M4 12h10M4 17h7" />
        </svg>
        <div className="mt-3.5 text-[13px] font-semibold text-slate-600">No run yet</div>
        <div className="mt-1.5 text-[12px] leading-relaxed text-slate-400">
          Each step the agent takes — what it looked at, how long it took, whether it succeeded —
          streams in here.
        </div>
      </div>
    </div>
  )
}
