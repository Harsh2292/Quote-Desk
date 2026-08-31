import { useSyncExternalStore } from 'react'

/**
 * The three screens plus their detail views, as a closed union. Deep links survive a refresh
 * (`#/quotes/12`) with no router dependency — six routes do not need one.
 */
export type Route =
  | { name: 'desk'; enquiryId: number | null }
  | { name: 'approvals' }
  | { name: 'quotes' }
  | { name: 'quote'; quoteId: number }

export function parseRoute(hash: string): Route {
  const path = hash.replace(/^#\/?/, '')
  const [head, tail] = path.split('/')
  const id = tail && /^\d+$/.test(tail) ? Number(tail) : null

  switch (head) {
    case 'approvals':
      return { name: 'approvals' }
    case 'quotes':
      return id === null ? { name: 'quotes' } : { name: 'quote', quoteId: id }
    case 'desk':
      return { name: 'desk', enquiryId: id }
    default:
      return { name: 'desk', enquiryId: null }
  }
}

export function toHash(route: Route): string {
  switch (route.name) {
    case 'desk':
      return route.enquiryId === null ? '#/desk' : `#/desk/${route.enquiryId}`
    case 'approvals':
      return '#/approvals'
    case 'quotes':
      return '#/quotes'
    case 'quote':
      return `#/quotes/${route.quoteId}`
  }
}

export function navigate(route: Route): void {
  window.location.hash = toHash(route)
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener('hashchange', onChange)
  return () => window.removeEventListener('hashchange', onChange)
}

export function useHashRoute(): Route {
  const hash = useSyncExternalStore(
    subscribe,
    () => window.location.hash,
    () => '#/desk',
  )
  return parseRoute(hash)
}
