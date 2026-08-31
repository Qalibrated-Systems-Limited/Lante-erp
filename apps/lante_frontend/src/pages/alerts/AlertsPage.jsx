import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import { useAlerts } from '../../context/AlertContext.jsx'
import { Tabs, Badge, Btn, Card, Loading, EmptyState } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'

const SEVERITY_VARIANTS = {
  Critical: 'red',
  Warning:  'amber',
  Info:     'blue',
}

function fmtRelative(dateStr) {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  const days = Math.floor(hrs / 24)
  return `${days}d ago`
}

function AlertRow({ alert, onMarkSeen, onAcknowledge }) {
  const navigate = useNavigate()
  return (
    <Card style={{ padding: '14px 18px', marginBottom: 10 }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginBottom: 5 }}>
            <Badge variant={SEVERITY_VARIANTS[alert.severity] || 'default'}>{alert.severity}</Badge>
            <span style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5 }}>{alert.source}</span>
            <span style={{ fontSize: 11, color: T.mgrey, marginLeft: 'auto' }}>{fmtRelative(alert.createdAt)}</span>
          </div>
          <p style={{ fontSize: 14, fontWeight: alert.isSeen ? 500 : 700, color: T.navy, margin: 0 }}>{alert.title}</p>
          <p style={{ fontSize: 12.5, color: T.dgrey, margin: '4px 0 0' }}>{alert.message}</p>
          {alert.ticketId && (
            <button
              onClick={() => navigate(`/modules/ticketing/${alert.ticketId}`)}
              style={{ background: 'none', border: 'none', padding: 0, marginTop: 6, fontSize: 12, fontWeight: 600, color: T.blue, cursor: 'pointer' }}
            >
              Ticket: {alert.ticketTitle || alert.ticketId} →
            </button>
          )}
          {alert.isAcknowledged && (
            <p style={{ fontSize: 11, fontWeight: 600, color: T.green, margin: '6px 0 0' }}>
              Acknowledged{alert.acknowledgedBy ? ` by ${alert.acknowledgedBy}` : ''}{alert.acknowledgedAt ? ` · ${fmtRelative(alert.acknowledgedAt)}` : ''}
            </p>
          )}
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'stretch', gap: 6, flexShrink: 0 }}>
          {!alert.isSeen && <Btn variant="ghost" size="sm" onClick={() => onMarkSeen(alert.id)}>Mark seen</Btn>}
          {!alert.isAcknowledged && <Btn variant="gold" size="sm" onClick={() => onAcknowledge(alert.id)}>Acknowledge</Btn>}
        </div>
      </div>
    </Card>
  )
}

export default function AlertsPage() {
  const { openAlerts, openCount, markSeen, acknowledge } = useAlerts()
  const [tab, setTab] = useState('open')
  const [history, setHistory] = useState([])
  const [historyLoaded, setHistoryLoaded] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)

  async function loadHistory() {
    setHistoryLoading(true)
    try {
      const res = await api.get('/api/v1/alerts/history')
      setHistory(res.data?.data ?? [])
      setHistoryLoaded(true)
    } catch {
      // Silently fail — alerts are non-critical
    } finally {
      setHistoryLoading(false)
    }
  }

  function switchTab(id) {
    setTab(id)
    if (id === 'history' && !historyLoaded && !historyLoading) loadHistory()
  }

  function markSeenFromHistory(id) {
    markSeen(id)
    setHistory(prev => prev.map(a => a.id === id ? { ...a, isSeen: true } : a))
  }

  async function acknowledgeFromHistory(id) {
    await acknowledge(id)
    loadHistory()
  }

  return (
    <>
      <div style={{ maxWidth: 860, margin: '0 auto', padding: '28px 20px' }}>
        <div style={{ marginBottom: 18 }}>
          <h1 style={{ fontSize: 22, fontWeight: 700, color: T.navy, margin: 0 }}>Alerts</h1>
          {openCount > 0 && (
            <p style={{ fontSize: 13, color: T.mgrey, margin: '4px 0 0' }}>{openCount} open alert{openCount === 1 ? '' : 's'}</p>
          )}
        </div>

        <Tabs
          tabs={[
            { id: 'open',    label: openCount > 0 ? `Open (${openCount})` : 'Open' },
            { id: 'history', label: 'History' },
          ]}
          active={tab}
          setActive={switchTab}
        />

        {tab === 'open' && (
          openAlerts.length === 0
            ? <EmptyState icon="🚨" title="No open alerts" sub="You're all caught up — new alerts will appear here as they're raised." />
            : openAlerts.map(a => <AlertRow key={a.id} alert={a} onMarkSeen={markSeen} onAcknowledge={acknowledge} />)
        )}

        {tab === 'history' && (
          historyLoading
            ? <Loading />
            : history.length === 0
              ? <EmptyState icon="🗂️" title="No alert history" sub="Open and acknowledged alerts will show up here." />
              : history.map(a => <AlertRow key={a.id} alert={a} onMarkSeen={markSeenFromHistory} onAcknowledge={acknowledgeFromHistory} />)
        )}
      </div>
    </>
  )
}
