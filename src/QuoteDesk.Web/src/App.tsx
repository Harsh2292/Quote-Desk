import { useAuth } from './auth/AuthContext'
import { SignInScreen } from './auth/SignInScreen'
import { AppShell } from './components/AppShell'
import { useHashRoute } from './routing/useHashRoute'
import { DeskScreen } from './screens/DeskScreen'
import { ApprovalsScreen } from './screens/ApprovalsScreen'
import { QuotesScreen } from './screens/QuotesScreen'
import { QuoteDetailScreen } from './screens/QuoteDetailScreen'

function App() {
  const { user, status } = useAuth()
  const route = useHashRoute()

  if (status !== 'signedIn' || !user) {
    return <SignInScreen />
  }

  return (
    <AppShell active={route.name}>
      {route.name === 'desk' && <DeskScreen route={route} />}
      {route.name === 'approvals' && <ApprovalsScreen />}
      {route.name === 'quotes' && <QuotesScreen />}
      {route.name === 'quote' && <QuoteDetailScreen quoteId={route.quoteId} />}
    </AppShell>
  )
}

export default App
