/**
 * Mirrors QuoteDesk.Agents.Pipeline.AgentEvent (src/QuoteDesk.Agents/Pipeline/AgentEvent.cs) exactly
 * — the two must change in the same commit (CLAUDE.md, Frontend section; docs/SPEC.md §8). The type
 * only, for now: `useAgentStream` (the fetch + ReadableStream hook that parses SSE frames into these)
 * is task 08's job, once the screens that consume it exist.
 *
 * `token` is declared here because the C# union declares it, but no pipeline stage emits one today —
 * Price's narration runs as one non-streaming call (see AgentEvent.cs's remarks, and
 * tasks/task-07-api.md's Notes on completion). A consumer should treat it as always-possible per the
 * type, not assume it never arrives.
 */
export type AgentEvent =
  | { type: 'stage'; stage: 'extract' | 'resolve' | 'price'; at: string }
  | { type: 'tool_start'; name: string; args: unknown; at: string }
  | { type: 'tool_end'; name: string; ms: number; ok: boolean; result: unknown }
  | { type: 'token'; text: string }
  | { type: 'approval_required'; approvalId: string; action: string; payload: unknown }
  | { type: 'done'; usage: { promptTokens: number; completionTokens: number } }
  | { type: 'error'; code: 'provider_rate_limited' | 'budget_exceeded' | 'internal'; message: string }
