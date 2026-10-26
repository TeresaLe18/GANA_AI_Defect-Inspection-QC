import { useCallback, useRef, useState } from 'react'
import Dashboard from './components/Dashboard.jsx'
import UploadReview from './components/UploadReview.jsx'
import History from './components/History.jsx'
import Login from './components/Login.jsx'

const TABS = [
  ['dashboard', 'QC Dashboard'],
  ['upload', 'Upload & Review'],
  ['history', 'Inspection History'],
]

export default function App() {
  const [tab, setTab] = useState('dashboard')
  const [toasts, setToasts] = useState([])
  const [refreshKey, setRefreshKey] = useState(0)
  const [user, setUser] = useState(() => {
    try { return JSON.parse(localStorage.getItem('qc-user')) } catch { return null }
  })
  const [resume, setResume] = useState(null) // draft chosen in History, to be reviewed in the Upload & Review tab
  const nextId = useRef(1)

  // Real-time feedback: transient toasts + a refresh signal that makes dashboard/history reload.
  const notify = useCallback((type, text) => {
    const id = nextId.current++
    setToasts((t) => [...t, { id, type, text }])
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 5000)
  }, [])
  const refresh = useCallback(() => setRefreshKey((k) => k + 1), [])
  const reviewDraft = useCallback((report) => { setResume(report); setTab('upload') }, [])
  const clearResume = useCallback(() => setResume(null), [])

  const login = (inspector) => {
    try { localStorage.setItem('qc-user', JSON.stringify(inspector)) } catch { /* storage unavailable */ }
    setUser(inspector); setTab('dashboard')
    notify('success', `Welcome, ${inspector.inspectorName}.`)
  }
  const logout = () => {
    try { localStorage.removeItem('qc-user') } catch { /* storage unavailable */ }
    setUser(null); setResume(null)
    notify('info', 'Signed out.')
  }

  return (
    <div className="app">
      <header>
        <h1>AI Defect Inspection &amp; QC</h1>
        {user && (
          <>
            <nav>
              {TABS.map(([key, label]) => (
                <button key={key} className={tab === key ? 'active' : ''} onClick={() => setTab(key)}>
                  {label}
                </button>
              ))}
            </nav>
            <div className="session">
              <span className="muted">{user.inspectorName}</span>
              <button onClick={logout}>Log out</button>
            </div>
          </>
        )}
      </header>
      <main>
        {!user && <Login onLogin={login} notify={notify} />}
        {user && tab === 'dashboard' && <Dashboard refreshKey={refreshKey} />}
        {user && tab === 'upload' && (
          <UploadReview notify={notify} onConfirmed={refresh} resume={resume} onResumed={clearResume} inspector={user} />
        )}
        {user && tab === 'history' && <History refreshKey={refreshKey} notify={notify} onReview={reviewDraft} />}
      </main>
      <div className="toasts" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`toast ${t.type}`}>{t.text}</div>
        ))}
      </div>
    </div>
  )
}
