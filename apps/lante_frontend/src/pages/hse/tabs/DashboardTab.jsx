import { useState } from 'react'
import api from '../../../api/axios.js'
import { T, fmt } from '../../../theme/tokens.js'
import { KPI_GRID, Kpi, Btn, HelpPanel, SectionHeader, worstVariant } from '../../../components/ui.jsx'

export default function DashboardTab({ dashboard, setDashboard, setMsg, setTab }) {
  const [hoursWorkedYtd, setHoursWorkedYtd] = useState('')

  async function recalcDashboard() {
    try {
      const res = await api.get('/api/v1/hse-dashboard', { params: { hoursWorkedYtd: Number(hoursWorkedYtd) || 0 } })
      setDashboard(res.data?.data ?? null)
    } catch {
      setMsg({ type: 'error', text: 'Failed to recompute KPIs.' })
    }
  }

  const openCorrectiveVariant = (dashboard?.openCorrectiveActions ?? 0) > 0 ? 'amber' : 'green'
  const overdueCorrectiveVariant = (dashboard?.overdueCorrectiveActions ?? 0) > 0 ? 'red' : 'green'
  const ramsPendingVariant = (dashboard?.ramsPendingApproval ?? 0) > 0 ? 'amber' : 'green'
  const trainingExpiringVariant = (dashboard?.trainingCertificatesExpiringSoon ?? 0) > 0 ? 'amber' : 'green'
  const inspectionsDueVariant = (dashboard?.statutoryInspectionsDueSoon ?? 0) > 0 ? 'amber' : 'green'

  return (
    <>
      <HelpPanel title="About the HSE KPI Dashboard" variant={worstVariant(
        'amber', openCorrectiveVariant, overdueCorrectiveVariant, ramsPendingVariant,
        trainingExpiringVariant, inspectionsDueVariant,
      )}>
        A rollup of leading and lagging safety indicators across every register in this module.
        TRIR (Total Recordable Incident Rate) and LTIF (Lost Time Injury Frequency) are computed
        against hours worked YTD — enter that figure below and hit Recalculate when Operations/HR
        gives you an updated number. The rest of the tiles (near-misses, open/overdue corrective
        actions, RAMS pending approval, expiring training certs, upcoming inspections) update
        automatically whenever a record changes on any tab.
      </HelpPanel>

      <SectionHeader
        title="HSE KPI Dashboard"
        sub="TRIR / LTIF are computed against hours worked YTD — supply the figure from Operations/HR when available."
        action={
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input
              type="number" min="0" placeholder="Hours worked YTD" value={hoursWorkedYtd}
              onChange={e => setHoursWorkedYtd(e.target.value)}
              style={{ width: 160, padding: '8px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13 }}
            />
            <Btn size="sm" onClick={recalcDashboard}>Recalculate</Btn>
          </div>
        }
      />
      <div style={{ ...KPI_GRID }}>
        <Kpi label="TRIR" value={dashboard?.trir ?? 0} sub="Total Recordable Incident Rate" icon="📊" />
        <Kpi label="LTIF" value={dashboard?.ltif ?? 0} sub="Lost Time Injury Frequency" icon="📉" />
        <Kpi label="Near-Miss Frequency" value={dashboard?.nearMissCount ?? 0} icon="⚠️" variant="amber" onClick={() => setTab?.('incidents')} />
        <Kpi label="Open Corrective Actions" value={dashboard?.openCorrectiveActions ?? 0} icon="🔧" variant={openCorrectiveVariant} onClick={() => setTab?.('incidents')} />
        <Kpi label="Overdue Corrective Actions" value={dashboard?.overdueCorrectiveActions ?? 0} icon="⏰" variant={overdueCorrectiveVariant} onClick={() => setTab?.('incidents')} />
        <Kpi label="RAMS Pending Approval" value={dashboard?.ramsPendingApproval ?? 0} icon="📄" variant={ramsPendingVariant} onClick={() => setTab?.('rams')} />
        <Kpi label="Training Certs Expiring Soon" value={dashboard?.trainingCertificatesExpiringSoon ?? 0} icon="🎓" variant={trainingExpiringVariant} onClick={() => setTab?.('training')} />
        <Kpi label="Inspections Due Soon" value={dashboard?.statutoryInspectionsDueSoon ?? 0} icon="🔍" variant={inspectionsDueVariant} onClick={() => setTab?.('inspections')} />
      </div>
      {dashboard?.generatedAt && <p style={{ fontSize: 11, color: T.mgrey, marginTop: 12 }}>Last computed {fmt.date(dashboard.generatedAt)}</p>}
    </>
  )
}
