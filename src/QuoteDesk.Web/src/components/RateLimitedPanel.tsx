import { useState } from 'react'
import { RECORDED_RUNS } from '../api/replay'
import { cn } from '../lib/cn'
import { Button, Card, Eyebrow } from './ui'

/**
 * The `provider_rate_limited` fallback — a recruiter clicking the live demo must never see a blank
 * error. Picks one of three runs recorded as `AgentEvent[]` and replays it with no network.
 */
export function RateLimitedPanel({ onReplay }: { onReplay: (events: (typeof RECORDED_RUNS)[number]['events']) => void }) {
  const [selected, setSelected] = useState(RECORDED_RUNS[0]?.id ?? '')

  return (
    <Card className="mx-auto w-[520px] max-w-full overflow-hidden">
      <div className="flex items-start gap-3 border-b border-slate-100 px-[22px] py-5">
        <span className="flex size-[34px] shrink-0 items-center justify-center rounded-[9px] bg-amber-50">
          <svg
            className="text-amber-600"
            width="17"
            height="17"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
          >
            <path d="M12 9v4M12 17h.01M10.3 3.9 2 18a1.7 1.7 0 0 0 1.5 2.5h17A1.7 1.7 0 0 0 22 18L13.7 3.9a1.7 1.7 0 0 0-3 0Z" />
          </svg>
        </span>
        <div>
          <div className="text-[13.5px] font-semibold text-slate-900">
            The AI provider is rate limited
          </div>
          <p className="mt-1 text-[12.5px] leading-relaxed text-slate-500">
            The free tier is capped at a small number of requests per day and that limit is reached.
            Live processing resumes after the daily reset — until then, replay a recorded run to see
            the whole pipeline end to end.
          </p>
        </div>
      </div>

      <div className="flex flex-col gap-2.5 px-[22px] py-4">
        <Eyebrow>Replay a saved run</Eyebrow>
        {RECORDED_RUNS.map((run) => {
          const active = selected === run.id
          return (
            <button
              key={run.id}
              type="button"
              onClick={() => setSelected(run.id)}
              className={cn(
                'flex items-center gap-3 rounded-[9px] border px-[15px] py-3 text-left transition-colors',
                active ? 'border-slate-900 ring-1 ring-slate-900' : 'border-slate-200 hover:bg-slate-50',
              )}
            >
              <span
                className={cn(
                  'flex size-[15px] shrink-0 items-center justify-center rounded-full border-[1.5px]',
                  active ? 'border-slate-900' : 'border-slate-300',
                )}
              >
                {active && <span className="size-[7px] rounded-full bg-slate-900" />}
              </span>
              <span className="flex-1">
                <span className="block text-[12.5px] font-semibold text-slate-900">{run.title}</span>
                <span className="block text-[11.5px] text-slate-400">{run.summary}</span>
              </span>
            </button>
          )
        })}
      </div>

      <div className="flex items-center justify-end border-t border-slate-200 bg-slate-50 px-[22px] py-3.5">
        <Button
          onClick={() => {
            const run = RECORDED_RUNS.find((r) => r.id === selected)
            if (run) onReplay(run.events)
          }}
          disabled={!selected}
        >
          Replay this run
        </Button>
      </div>
    </Card>
  )
}
