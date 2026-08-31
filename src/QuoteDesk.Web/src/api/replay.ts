import type { AgentEvent } from './agentEvents'
import { cleanRun } from '../fixtures/cleanRun'
import { unknownSenderRun } from '../fixtures/unknownSender'
import { workedExampleRun } from '../fixtures/workedExample'

/** One of the runs offered on the `provider_rate_limited` screen — replayable with the Api stopped. */
export interface RecordedRun {
  id: string
  title: string
  summary: string
  events: AgentEvent[]
}

export const RECORDED_RUNS: readonly RecordedRun[] = [
  {
    id: 'worked-example',
    title: 'Shreeji Textiles — the worked example',
    summary: '3 lines · 1 ambiguous · 1 date conflict · 8% within policy',
    events: workedExampleRun,
  },
  {
    id: 'clean-run',
    title: 'Ramdev Mills — clean run',
    summary: '3 lines · all resolved · Tier A · no overrides',
    events: cleanRun,
  },
  {
    id: 'unknown-sender',
    title: 'Unknown sender — new customer',
    summary: 'no customer match · list price + slab only · flagged for verification',
    events: unknownSenderRun,
  },
]
