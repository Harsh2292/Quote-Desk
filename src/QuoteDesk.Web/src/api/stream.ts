import type { AgentEvent } from './agentEvents'
import { apiUrl, getToken } from './client'

/**
 * A non-2xx that arrived as a JSON body instead of a stream — e.g. `POST /api/approvals/{id}` with a
 * decision the run cannot take answers with RFC 9457 ProblemDetails, not SSE frames.
 */
export class StreamProblem extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'StreamProblem'
    this.status = status
  }
}

/**
 * POST to one of the Api's SSE endpoints and yield each {@link AgentEvent} as it arrives.
 *
 * The Api writes data-only frames — `data: {json}\n\n`, one flush per event, no `event:` names, no
 * `id:` lines, no retry hint, and no terminal sentinel: the stream simply ends. There is no
 * server-side resume, so recovery is a fresh `GET /api/enquiries/{id}` for its stored `trace`.
 */
export async function* openAgentStream(
  path: string,
  signal: AbortSignal,
  body?: unknown,
): AsyncGenerator<AgentEvent, void, unknown> {
  const headers = new Headers({ Accept: 'text/event-stream' })
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (body !== undefined) headers.set('Content-Type', 'application/json')

  const response = await fetch(apiUrl(path), {
    method: 'POST',
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  })

  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('text/event-stream')) {
    let message = `Stream request failed with ${response.status}.`
    if (contentType.includes('json')) {
      const problem = (await response.json().catch(() => null)) as
        | { detail?: string; title?: string }
        | null
      message = problem?.detail ?? problem?.title ?? message
    }
    throw new StreamProblem(response.status, message)
  }

  if (!response.body) {
    throw new StreamProblem(response.status, 'The response carried no stream.')
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''

  try {
    for (;;) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += value

      let boundary = buffer.indexOf('\n\n')
      while (boundary !== -1) {
        const frame = buffer.slice(0, boundary)
        buffer = buffer.slice(boundary + 2)
        const event = parseFrame(frame)
        if (event) yield event
        boundary = buffer.indexOf('\n\n')
      }
    }
  } finally {
    void reader.cancel().catch(() => undefined)
  }
}

/** One SSE frame → one event. Frames are single-line JSON in practice, but tolerate folded `data:`. */
function parseFrame(frame: string): AgentEvent | null {
  const data = frame
    .split('\n')
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice('data:'.length).trimStart())
    .join('')
  if (!data) return null
  try {
    return JSON.parse(data) as AgentEvent
  } catch {
    return null
  }
}
