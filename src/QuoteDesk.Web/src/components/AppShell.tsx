import type { ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { toHash, type Route } from '../routing/useHashRoute'
import { cn } from '../lib/cn'

const TABS: { label: string; route: Route; matches: Route['name'][] }[] = [
  { label: 'Desk', route: { name: 'desk', enquiryId: null }, matches: ['desk'] },
  { label: 'Approvals', route: { name: 'approvals' }, matches: ['approvals'] },
  { label: 'Quotes', route: { name: 'quotes' }, matches: ['quotes', 'quote'] },
]

function initials(name: string): string {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('') || '?'
  )
}

export function AppShell({ active, children }: { active: Route['name']; children: ReactNode }) {
  const { user, signOut } = useAuth()

  return (
    <div className="flex min-h-screen flex-col bg-slate-50">
      <header className="flex h-12 shrink-0 items-center gap-6 border-b border-slate-200 bg-white px-5">
        <div className="flex items-center gap-2.5 text-[13.5px] font-semibold text-slate-900">
          <span className="flex size-5 items-center justify-center rounded-[5px] bg-slate-900">
            <svg
              width="11"
              height="11"
              viewBox="0 0 24 24"
              fill="none"
              stroke="#fff"
              strokeWidth="2.6"
              aria-hidden="true"
            >
              <path d="M5 12l5 5L20 7" />
            </svg>
          </span>
          QuoteDesk
        </div>

        <nav className="flex items-center">
          {TABS.map((tab) => (
            <a
              key={tab.label}
              href={toHash(tab.route)}
              className={cn(
                'border-b-2 px-2.5 py-[15px] text-[12.5px] font-medium transition-colors',
                tab.matches.includes(active)
                  ? 'border-slate-900 text-slate-900'
                  : 'border-transparent text-slate-500 hover:text-slate-700',
              )}
            >
              {tab.label}
            </a>
          ))}
        </nav>

        <div className="flex-1" />

        {user && (
          <div className="flex items-center gap-2.5">
            {user.pictureUrl ? (
              <img
                src={user.pictureUrl}
                alt=""
                className="size-6 rounded-full"
                referrerPolicy="no-referrer"
              />
            ) : (
              <span className="flex size-6 items-center justify-center rounded-full bg-slate-200 text-[10px] font-semibold text-slate-600">
                {initials(user.name)}
              </span>
            )}
            <div className="leading-tight">
              <div className="text-[12px] font-semibold text-slate-900">{user.name}</div>
              <div className="text-[10px] text-slate-400">{user.role}</div>
            </div>
            <button
              type="button"
              onClick={signOut}
              className="ml-2 text-[11.5px] text-slate-500 hover:text-slate-700"
            >
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="flex min-h-0 flex-1 flex-col">{children}</main>
    </div>
  )
}
