import { useState, useEffect, useCallback } from 'react'
import Chart from 'react-apexcharts'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID, Modal } from '../../components/ui.jsx'
import * as crm from '../../services/crm.js'
import { useAuth } from '../../context/AuthContext.jsx'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const RAG_VARIANT = { Green: 'green', Amber: 'amber', Red: 'red', Grey: 'default' }

export default function SalesDashboardPage() {
  const { hasPermission } = useAuth()
  const canSetTarget = hasPermission?.('crm.approve.bd')
  const [tab, setTab] = useState('pipeline')
  const [md, setMd] = useState(null)
  const [se, setSe] = useState([])
  const [loading, setLoading] = useState(true)
  const [refreshedAt, setRefreshedAt] = useState(null)
  const [modal, setModal] = useState(false)
  const [tgt, setTgt] = useState({ employeeId: '', employeeName: '', revenueTarget: '' })
  const [toast, setToast] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [m, s] = await Promise.allSettled([crm.getMdPipeline(), crm.getSePerformance()])
      if (m.status === 'fulfilled') setMd(m.value)
      if (s.status === 'fulfilled') setSe(s.value ?? [])
      setRefreshedAt(new Date())
    } finally { setLoading(false) }
  }, [])
  useEffect(() => { load() }, [load])

  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }
  const dash = (v) => (loading ? '…' : (v ?? 0))
  const saveTarget = async () => {
    if (!tgt.employeeId.trim()) return flash('Employee id required.')
    try { await crm.saveSalesTarget({ ...tgt, periodType: 'Annual', periodLabel: String(new Date().getFullYear()), revenueTarget: Number(tgt.revenueTarget) || 0 }); setModal(false); flash('Target saved.'); load() }
    catch (e) { flash(e.response?.data?.message ?? 'Failed.') }
  }

  const stageNames = md?.byStage?.map(s => s.stageName) ?? []
  const stageWeighted = md?.byStage?.map(s => Math.round(s.weighted)) ?? []

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Sales Dashboard</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>{refreshedAt ? `Last refreshed ${refreshedAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : 'Loading…'}</p>
          </div>
          <Btn size="sm" variant="ghost" onClick={load} disabled={loading}>{loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
        </div>

        <Tabs tabs={[{ id: 'pipeline', label: 'MD Pipeline' }, { id: 'performance', label: 'SE Performance' }]} active={tab} setActive={setTab} />

        {tab === 'pipeline' && (
          <div>
            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Open Opportunities" value={dash(md?.openCount)} icon="📈" />
              <Kpi label="Pipeline Value" value={loading ? '…' : fmtKes(md?.totalPipelineValue)} icon="💰" variant="blue" />
              <Kpi label="Weighted Pipeline" value={loading ? '…' : fmtKes(md?.weightedPipelineValue)} icon="🎯" variant="green" />
              <Kpi label="Win Rate (12mo)" value={loading ? '…' : `${md?.winRate ?? 0}%`} sub={`${md?.wonLast12mo ?? 0}W / ${md?.lostLast12mo ?? 0}L`} icon="🏆" />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 20, marginBottom: 20 }}>
              <Card>
                <SectionHeader title="Weighted Pipeline by Stage" sub="Estimated value × win probability" />
                {stageWeighted.every(v => v === 0) ? <p style={{ color: T.mgrey, fontSize: 13 }}>No open pipeline.</p> : (
                  <Chart type="bar" height={280} series={[{ name: 'Weighted', data: stageWeighted }]}
                    options={{ chart: { toolbar: { show: false } }, colors: [T.gold], plotOptions: { bar: { borderRadius: 4, columnWidth: '50%', distributed: true } }, dataLabels: { enabled: false }, legend: { show: false }, xaxis: { categories: stageNames, labels: { style: { fontSize: '10px' }, rotate: -30 } }, grid: { borderColor: T.lgrey } }} />
                )}
              </Card>
              <Card>
                <SectionHeader title="Pipeline Risk" sub="Needs attention" />
                <div style={{ display: 'grid', gap: 10 }}>
                  <RiskLine label="Stale opportunities (>14d)" value={md?.risk?.staleOpportunities} />
                  <RiskLine label="Overdue tasks" value={md?.risk?.overdueTasks} />
                  <RiskLine label="Bid bonds expiring (≤14d)" value={md?.risk?.expiringBidBonds} />
                  <RiskLine label="Dormant clients" value={md?.risk?.dormantClients} />
                </div>
              </Card>
            </div>

            <Card>
              <SectionHeader title="Top 10 Clients by Open Pipeline" />
              <DataTable headers={['Client', 'Opportunities', 'Open Value']} empty={loading ? 'Loading…' : 'No open pipeline.'}
                rows={(md?.topClients ?? []).map(c => [c.customerName, c.opportunityCount, <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.openValue)}</span>])} />
            </Card>
          </div>
        )}

        {tab === 'performance' && (
          <div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
              {canSetTarget && <Btn size="sm" onClick={() => { setTgt({ employeeId: '', employeeName: '', revenueTarget: '' }); setModal(true) }}>Set Annual Target</Btn>}
            </div>
            <Card>
              <SectionHeader title="SE Performance" sub={`Attainment vs ${new Date().getFullYear()} target · RAG`} />
              <DataTable headers={['Sales Engineer', 'Leads', 'Won', 'Lost', 'Win Rate', 'Revenue', 'Target', 'Attainment', 'RAG']}
                empty={loading ? 'Loading…' : 'No sales activity yet.'}
                rows={se.map(r => [
                  <span style={{ fontWeight: 600, color: T.navy }}>{r.employeeName ?? r.employeeId}</span>,
                  `${r.leadsConverted}/${r.leadsGenerated}`,
                  r.oppsWon, r.oppsLost, `${r.winRate}%`,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(r.revenue)}</span>,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(r.target)}</span>,
                  `${r.attainmentPct}%`,
                  <Badge variant={RAG_VARIANT[r.rag] ?? 'default'}>{r.rag}</Badge>,
                ])} />
            </Card>
          </div>
        )}
      </div>

      {modal && (
        <Modal title="Set Annual Revenue Target" onClose={() => setModal(false)} width={420}>
          <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">Employee id *</span><input value={tgt.employeeId} onChange={e => setTgt(t => ({ ...t, employeeId: e.target.value }))} className="input" /></label>
          <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">Employee name</span><input value={tgt.employeeName} onChange={e => setTgt(t => ({ ...t, employeeName: e.target.value }))} className="input" /></label>
          <label className="block mb-3"><span className="block text-xs font-medium text-gray-500 mb-1">Annual revenue target (KES)</span><input type="number" value={tgt.revenueTarget} onChange={e => setTgt(t => ({ ...t, revenueTarget: e.target.value }))} className="input" /></label>
          <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn><Btn onClick={saveTarget}>Save</Btn></div>
        </Modal>
      )}
    </>
  )
}

function RiskLine({ label, value }) {
  const v = value ?? 0
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
      <span style={{ fontSize: 13, color: T.mgrey }}>{label}</span>
      <strong style={{ fontSize: 16, color: v > 0 ? T.red : T.green }}>{v}</strong>
    </div>
  )
}
