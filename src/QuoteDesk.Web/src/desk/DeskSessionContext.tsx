import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import type { AgentEvent } from '../api/agentEvents'
import { createEnquiry } from '../api/endpoints'
import { useAgentStream, type UseAgentStream } from '../hooks/useAgentStream'
import { navigate } from '../routing/useHashRoute'

/**
 * Everything the Desk needs to remember while the user moves around the app. Mounted ABOVE the
 * router in `App`, so switching to Approvals and back can never unmount it and throw the run away.
 * Mirrored to `sessionStorage` so a browser refresh keeps the enquiry text and the trace too.
 *
 * Clearing rule: nothing here is wiped on navigation, on a failed run, or on a rejected decision.
 * It clears only when the user presses New enquiry (`reset`) or a run completes through an approve.
 */

type Decision = 'approve' | 'reject'

interface DeskSession {
  draftBody: string
  draftSender: string
  setDraftBody: (value: string) => void
  setDraftSender: (value: string) => void

  /** The enquiry the live stream currently pertains to, or null for a blank desk. */
  activeEnquiryId: number | null
  decided: Decision | null
  setDecided: (decision: Decision | null) => void

  stream: UseAgentStream

  submitting: boolean
  submitError: string | null

  /** Create an enquiry from the current draft and start processing it. */
  submitDraft: () => Promise<void>
  /** Re-run the pipeline for the active enquiry against the same stored body. */
  retry: () => void
  /** Move a stored enquiry body back into the draft for editing as a fresh enquiry. */
  editForRerun: (body: string) => void
  /** Clear back to a blank desk. */
  reset: () => void
}

const DeskSessionContext = createContext<DeskSession | null>(null)

const STORAGE_KEY = 'qd.desk.session'
const MAX_PERSISTED_BYTES = 400_000

interface PersistedSession {
  draftBody: string
  draftSender: string
  activeEnquiryId: number | null
  decided: Decision | null
  events: AgentEvent[]
}

function loadPersisted(): PersistedSession {
  const empty: PersistedSession = {
    draftBody: '',
    draftSender: '',
    activeEnquiryId: null,
    decided: null,
    events: [],
  }
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return empty
    const parsed = JSON.parse(raw) as Partial<PersistedSession>
    return {
      draftBody: typeof parsed.draftBody === 'string' ? parsed.draftBody : '',
      draftSender: typeof parsed.draftSender === 'string' ? parsed.draftSender : '',
      activeEnquiryId: typeof parsed.activeEnquiryId === 'number' ? parsed.activeEnquiryId : null,
      decided: parsed.decided === 'approve' || parsed.decided === 'reject' ? parsed.decided : null,
      events: Array.isArray(parsed.events) ? (parsed.events as AgentEvent[]) : [],
    }
  } catch {
    return empty
  }
}

function savePersisted(snapshot: PersistedSession): void {
  try {
    let payload = JSON.stringify(snapshot)
    if (payload.length > MAX_PERSISTED_BYTES) {
      // Keep the text and identity, drop the trace rather than overflow the quota.
      payload = JSON.stringify({ ...snapshot, events: [] })
    }
    sessionStorage.setItem(STORAGE_KEY, payload)
  } catch {
    // sessionStorage unavailable — the session just won't survive a refresh.
  }
}

export function DeskSessionProvider({ children }: { children: ReactNode }) {
  const stream = useAgentStream()

  // Read sessionStorage exactly once, as a stable state value (never re-set).
  const [restored] = useState(loadPersisted)

  const [draftBody, setDraftBody] = useState(restored.draftBody)
  const [draftSender, setDraftSender] = useState(restored.draftSender)
  const [activeEnquiryId, setActiveEnquiryId] = useState<number | null>(restored.activeEnquiryId)
  const [decided, setDecided] = useState<Decision | null>(restored.decided)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  // Replay a persisted trace once on mount so a refresh mid-run still shows what happened.
  const { replay } = stream
  useEffect(() => {
    if (restored.events.length > 0) {
      replay(restored.events)
    }
  }, [replay, restored])

  // Mirror the whole session to sessionStorage whenever any of it changes.
  useEffect(() => {
    savePersisted({ draftBody, draftSender, activeEnquiryId, decided, events: stream.events })
  }, [draftBody, draftSender, activeEnquiryId, decided, stream.events])

  const { process: startProcess, reset: resetStream } = stream

  const submitDraft = useCallback(async () => {
    if (draftBody.trim().length === 0) return
    setSubmitting(true)
    setSubmitError(null)
    try {
      const created = await createEnquiry({
        body: draftBody.trim(),
        senderId: draftSender.trim() || undefined,
      })
      setDecided(null)
      setActiveEnquiryId(created.enquiryId)
      startProcess(created.enquiryId)
      navigate({ name: 'desk', enquiryId: created.enquiryId })
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Could not submit the enquiry.')
    } finally {
      setSubmitting(false)
    }
  }, [draftBody, draftSender, startProcess])

  const retry = useCallback(() => {
    if (activeEnquiryId === null) return
    setDecided(null)
    startProcess(activeEnquiryId)
  }, [activeEnquiryId, startProcess])

  const editForRerun = useCallback(
    (body: string) => {
      resetStream()
      setActiveEnquiryId(null)
      setDecided(null)
      setDraftBody(body)
      navigate({ name: 'desk', enquiryId: null })
    },
    [resetStream],
  )

  const reset = useCallback(() => {
    resetStream()
    setActiveEnquiryId(null)
    setDecided(null)
    setDraftBody('')
    setDraftSender('')
    navigate({ name: 'desk', enquiryId: null })
  }, [resetStream])

  const value = useMemo<DeskSession>(
    () => ({
      draftBody,
      draftSender,
      setDraftBody,
      setDraftSender,
      activeEnquiryId,
      decided,
      setDecided,
      stream,
      submitting,
      submitError,
      submitDraft,
      retry,
      editForRerun,
      reset,
    }),
    [
      draftBody,
      draftSender,
      activeEnquiryId,
      decided,
      stream,
      submitting,
      submitError,
      submitDraft,
      retry,
      editForRerun,
      reset,
    ],
  )

  return <DeskSessionContext.Provider value={value}>{children}</DeskSessionContext.Provider>
}

export function useDeskSession(): DeskSession {
  const ctx = useContext(DeskSessionContext)
  if (!ctx) {
    throw new Error('useDeskSession must be used within a DeskSessionProvider')
  }
  return ctx
}
