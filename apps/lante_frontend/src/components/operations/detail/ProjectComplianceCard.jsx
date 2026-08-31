import { Card, Badge, Progress, Loading } from '../../ui.jsx'
import { useProjectCompliance, burnPercent } from '../../../hooks/operations/useProjectCompliance.js'

const money = (n) => (Number(n) || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

// O11.1 — surfaces the O2 budget-burn (bar + alert log + hard-lock) and the O8 HSE summary on the
// project overview. Self-contained: loads its own data from the service layer via useProjectCompliance.
export default function ProjectComplianceCard({ project, stacked = false }) {
  const { alerts, hse, loading } = useProjectCompliance(project?.id)
  const burn = burnPercent(project)
  const burnVariant = burn >= 100 ? 'red' : burn >= 90 ? 'amber' : 'green'

  return (
    <div style={{ display: 'grid', gridTemplateColumns: stacked ? '1fr' : 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
      {/* Budget burn */}
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: '#1b3a5c', margin: 0 }}>Budget Burn</h3>
          {project?.budgetLocked
            ? <Badge variant="red">Locked · 100%</Badge>
            : <Badge variant={burnVariant}>{burn}%</Badge>}
        </div>
        <Progress value={burn} height={8} />
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#5b6b7c', marginTop: 8 }}>
          <span>Spent {money(project?.actualCost)}{Number(project?.committed) > 0 ? ` + committed ${money(project?.committed)}` : ''}</span>
          <span>of {money(project?.plannedBudget)}</span>
        </div>
        {project?.budgetLocked && (
          <p style={{ fontSize: 12, color: '#c0392b', marginTop: 8 }}>Budget exhausted — further expenditure is blocked until the budget is revised.</p>
        )}

        <div style={{ marginTop: 14 }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: '#5b6b7c', marginBottom: 6 }}>Alert history</div>
          {loading ? <Loading /> : alerts.length === 0
            ? <p style={{ fontSize: 12, color: '#9aa7b4' }}>No budget alerts.</p>
            : (
              <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
                {alerts.map(a => (
                  <li key={a.id} style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 12, color: '#5b6b7c' }}>
                    <Badge variant={a.thresholdPct >= 100 ? 'red' : a.thresholdPct >= 90 ? 'amber' : 'blue'}>{a.thresholdPct}%</Badge>
                    <span>{a.burnPct}% burn</span>
                    <span style={{ marginLeft: 'auto', color: '#9aa7b4' }}>{a.createdAt?.slice(0, 10)}</span>
                  </li>
                ))}
              </ul>
            )}
        </div>
      </Card>

      {/* HSE summary (seam) */}
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: '#1b3a5c', margin: 0 }}>Health &amp; Safety</h3>
          {hse
            ? <Badge variant={hse.clearForCloseOut ? 'green' : 'red'}>{hse.clearForCloseOut ? 'Clear for close-out' : 'Open serious incident'}</Badge>
            : <Badge variant="default">—</Badge>}
        </div>
        {loading ? <Loading /> : (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <Metric label="Open incidents" value={hse?.openIncidents ?? '—'} />
            <Metric label="Serious / LTI" value={hse?.openSeriousIncidents ?? '—'} variant={hse?.openSeriousIncidents ? 'red' : undefined} />
            <Metric label="TRIR" value={hse?.trir ?? '—'} />
          </div>
        )}
        {hse?.message && <p style={{ fontSize: 11, color: '#9aa7b4', marginTop: 10 }}>{hse.message}</p>}
      </Card>
    </div>
  )
}

function Metric({ label, value, variant }) {
  return (
    <div>
      <div style={{ fontSize: 20, fontWeight: 800, color: variant === 'red' ? '#c0392b' : '#1b3a5c' }}>{value}</div>
      <div style={{ fontSize: 11, color: '#9aa7b4' }}>{label}</div>
    </div>
  )
}
