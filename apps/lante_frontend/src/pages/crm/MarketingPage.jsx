import { useState, useEffect, useCallback } from 'react'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Tabs, SectionHeader, DataTable, Kpi, KPI_GRID, Modal } from '../../components/ui.jsx'
import * as crm from '../../services/crm.js'
import {
  useMarketing, CAMPAIGN_TYPES, CAMPAIGN_STATUSES, CAMPAIGN_STATUS_VARIANT,
  BRAND_ASSET_TYPES, cTypeLabel,
} from '../../hooks/crm/useMarketing.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const fmtKes = (n) => new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
const fmtDate = (d) => (d ? new Date(d).toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' }) : '—')

export default function MarketingPage() {
  const m = useMarketing()
  const [tab, setTab] = useState('overview')
  const [createOpen, setCreateOpen] = useState(false)
  const [detailId, setDetailId] = useState(null)
  const [assetOpen, setAssetOpen] = useState(false)
  const [toast, setToast] = useState('')
  const flash = (x) => { setToast(x); setTimeout(() => setToast(''), 3000) }
  const d = m.dashboard

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        {toast && <div className="fixed bottom-6 right-6 z-[1100] bg-zinc-900 text-white text-sm px-5 py-3 rounded-xl shadow-lg">{toast}</div>}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 800, color: T.navy, margin: 0 }}>Marketing</h1>
            <p style={{ fontSize: 13, color: T.mgrey, margin: '2px 0 0' }}>Campaigns, budget tracking, ROI attribution &amp; brand assets</p>
          </div>
          <Btn size="sm" variant="ghost" onClick={m.reload} disabled={m.loading}>{m.loading ? 'Refreshing…' : '↻ Refresh'}</Btn>
        </div>

        {m.error && <Card style={{ marginBottom: 16, borderColor: T.red }}><span style={{ color: T.red, fontSize: 13 }}>{m.error}</span></Card>}

        <Tabs tabs={[{ id: 'overview', label: 'Overview' }, { id: 'campaigns', label: 'Campaigns' }, { id: 'assets', label: 'Brand Assets' }]} active={tab} setActive={setTab} />

        {/* ── Overview ── */}
        {tab === 'overview' && (
          <div>
            <div style={{ ...KPI_GRID, marginBottom: 20 }}>
              <Kpi label="Campaigns" value={m.loading ? '…' : (d?.campaignCount ?? 0)} sub={`${d?.activeCampaigns ?? 0} active`} icon="📣" />
              <Kpi label="Total Spend" value={m.loading ? '…' : fmtKes(d?.totalSpend)} icon="💸" variant="blue" />
              <Kpi label="Revenue Attributed" value={m.loading ? '…' : fmtKes(d?.totalRevenueAttributed)} icon="💰" variant="green" />
              <Kpi label="Overall ROI" value={m.loading ? '…' : `${d?.overallRoi ?? 0}×`} sub={`${fmtKes(d?.costPerLead)} / lead`} icon="📈" />
            </div>
            <Card>
              <SectionHeader title="Campaign Performance" sub="Spend, attributed leads &amp; revenue per campaign" />
              <DataTable
                headers={['Campaign', 'Status', 'Budget', 'Spent', 'Leads', 'Conv.', 'Revenue', 'ROI']}
                empty={m.loading ? 'Loading…' : 'No campaigns yet.'}
                rows={(d?.campaigns ?? []).map(c => [
                  <button onClick={() => setDetailId(c.id)} style={{ fontWeight: 600, color: T.navy, background: 'none', border: 0, cursor: 'pointer', padding: 0, textAlign: 'left' }}>{c.name}</button>,
                  <Badge variant={CAMPAIGN_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.budget)}</span>,
                  <SpendCell pct={c.spendPct} alert={c.budgetAlert} amount={c.actualSpend} />,
                  `${c.leadsConverted}/${c.leadsGenerated}`,
                  `${c.conversionRate}%`,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.revenueAttributed)}</span>,
                  <strong style={{ color: c.roi >= 1 ? T.green : T.mgrey }}>{c.roi}×</strong>,
                ])} />
            </Card>
          </div>
        )}

        {/* ── Campaigns ── */}
        {tab === 'campaigns' && (
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 12, flexWrap: 'wrap' }}>
              <select value={m.statusF} onChange={e => m.setStatusF(e.target.value)} className="input" style={{ maxWidth: 200 }}>
                <option value="">All statuses</option>
                {CAMPAIGN_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
              {m.canWrite && <Btn size="sm" onClick={() => setCreateOpen(true)}>+ New Campaign</Btn>}
            </div>
            <Card>
              <DataTable
                headers={['Campaign', 'Type', 'Status', 'Dates', 'Budget', 'Spent', 'ROI']}
                empty={m.loading ? 'Loading…' : 'No campaigns match.'}
                rows={m.campaigns.map(c => [
                  <button onClick={() => setDetailId(c.id)} style={{ fontWeight: 600, color: T.navy, background: 'none', border: 0, cursor: 'pointer', padding: 0, textAlign: 'left' }}>{c.name}</button>,
                  cTypeLabel(c.campaignType),
                  <Badge variant={CAMPAIGN_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>,
                  <span style={{ fontSize: 12, whiteSpace: 'nowrap' }}>{fmtDate(c.startDate)} – {fmtDate(c.endDate)}</span>,
                  <span style={{ whiteSpace: 'nowrap' }}>{fmtKes(c.budget)}</span>,
                  <SpendCell pct={c.spendPct} alert={c.budgetAlert} amount={c.actualSpend} />,
                  <strong style={{ color: c.roi >= 1 ? T.green : T.mgrey }}>{c.roi}×</strong>,
                ])} />
            </Card>
          </div>
        )}

        {/* ── Brand Assets ── */}
        {tab === 'assets' && (
          <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, marginBottom: 12, flexWrap: 'wrap' }}>
              <select value={m.assetTypeF} onChange={e => m.setAssetTypeF(e.target.value)} className="input" style={{ maxWidth: 200 }}>
                <option value="">All types</option>
                {BRAND_ASSET_TYPES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
              {m.canWrite && <Btn size="sm" onClick={() => setAssetOpen(true)}>+ Add Asset</Btn>}
            </div>
            <Card>
              <SectionHeader title="Brand Asset Library" sub="Logos, templates &amp; brochures for all staff" />
              <DataTable
                headers={['Asset', 'Type', 'Version', 'File', 'Uploaded By', 'Added', m.canWrite ? '' : null].filter(x => x !== null)}
                empty={m.loading ? 'Loading…' : 'No assets yet.'}
                rows={m.assets.map(a => [
                  <span style={{ fontWeight: 600, color: T.navy }}>{a.assetName}</span>,
                  <Badge variant="default">{a.assetType}</Badge>,
                  a.version,
                  a.fileUrl ? <a href={a.fileUrl} target="_blank" rel="noreferrer" style={{ color: T.blue ?? '#2563eb', fontSize: 12 }}>Open ↗</a> : '—',
                  a.uploadedBy,
                  fmtDate(a.createdAt),
                  ...(m.canWrite ? [<Btn size="sm" variant="ghost" onClick={async () => {
                    if (!window.confirm(`Delete "${a.assetName}"?`)) return
                    try { await crm.deleteBrandAsset(a.id); flash('Asset deleted.'); m.reload() } catch (e) { flash(e.response?.data?.message ?? 'Failed.') }
                  }}>Delete</Btn>] : []),
                ])} />
            </Card>
          </div>
        )}
      </div>

      {createOpen && <CreateCampaignModal onClose={() => setCreateOpen(false)} onSaved={() => { setCreateOpen(false); flash('Campaign created.'); m.reload() }} onErr={flash} />}
      {assetOpen && <AddAssetModal onClose={() => setAssetOpen(false)} onSaved={() => { setAssetOpen(false); flash('Asset added.'); m.reload() }} onErr={flash} />}
      {detailId && <CampaignDetailModal id={detailId} canWrite={m.canWrite} onClose={() => setDetailId(null)} onChanged={() => { m.reload() }} onErr={flash} />}
    </>
  )
}

function SpendCell({ pct, alert, amount }) {
  const p = Math.min(pct ?? 0, 100)
  const color = alert ? T.red : (p >= 60 ? T.gold : T.green)
  return (
    <div style={{ minWidth: 90 }}>
      <div style={{ fontSize: 12, fontWeight: 600, color, whiteSpace: 'nowrap' }}>{fmtKes(amount)} · {pct ?? 0}%</div>
      <div style={{ height: 5, background: T.lgrey, borderRadius: 3, marginTop: 3, overflow: 'hidden' }}>
        <div style={{ width: `${p}%`, height: '100%', background: color }} />
      </div>
    </div>
  )
}

function CreateCampaignModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ name: '', campaignType: 'Digital', description: '', targetAudience: '', startDate: '', endDate: '', budget: '' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.name.trim()) return onErr('Campaign name is required.')
    setBusy(true)
    try {
      await crm.createCampaign({
        name: f.name.trim(), campaignType: f.campaignType, description: f.description || undefined,
        targetAudience: f.targetAudience || undefined, startDate: f.startDate || undefined,
        endDate: f.endDate || undefined, budget: Number(f.budget) || 0,
      })
      onSaved()
    } catch (e) { onErr(e.response?.data?.message ?? 'Failed to create campaign.') } finally { setBusy(false) }
  }
  return (
    <Modal title="New Campaign" onClose={onClose} width={480}>
      <Field label="Name *"><input value={f.name} onChange={e => upd('name', e.target.value)} className="input" /></Field>
      <Field label="Type"><select value={f.campaignType} onChange={e => upd('campaignType', e.target.value)} className="input">{CAMPAIGN_TYPES.map(t => <option key={t} value={t}>{cTypeLabel(t)}</option>)}</select></Field>
      <Field label="Target audience"><input value={f.targetAudience} onChange={e => upd('targetAudience', e.target.value)} className="input" /></Field>
      <Field label="Description"><textarea value={f.description} onChange={e => upd('description', e.target.value)} className="input" rows={2} /></Field>
      <div style={{ display: 'flex', gap: 10 }}>
        <Field label="Start date" flex><input type="date" value={f.startDate} onChange={e => upd('startDate', e.target.value)} className="input" /></Field>
        <Field label="End date" flex><input type="date" value={f.endDate} onChange={e => upd('endDate', e.target.value)} className="input" /></Field>
      </div>
      <Field label="Budget (KES)"><input type="number" value={f.budget} onChange={e => upd('budget', e.target.value)} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Create'}</Btn></div>
    </Modal>
  )
}

function AddAssetModal({ onClose, onSaved, onErr }) {
  const [f, setF] = useState({ assetName: '', assetType: 'Logo', fileUrl: '', version: '1.0' })
  const [busy, setBusy] = useState(false)
  const upd = (k, v) => setF(s => ({ ...s, [k]: v }))
  const save = async () => {
    if (!f.assetName.trim()) return onErr('Asset name is required.')
    setBusy(true)
    try { await crm.saveBrandAsset({ ...f, assetName: f.assetName.trim(), fileUrl: f.fileUrl || undefined }); onSaved() }
    catch (e) { onErr(e.response?.data?.message ?? 'Failed to add asset.') } finally { setBusy(false) }
  }
  return (
    <Modal title="Add Brand Asset" onClose={onClose} width={440}>
      <Field label="Asset name *"><input value={f.assetName} onChange={e => upd('assetName', e.target.value)} className="input" /></Field>
      <Field label="Type"><select value={f.assetType} onChange={e => upd('assetType', e.target.value)} className="input">{BRAND_ASSET_TYPES.map(t => <option key={t} value={t}>{t}</option>)}</select></Field>
      <Field label="File URL"><input value={f.fileUrl} onChange={e => upd('fileUrl', e.target.value)} className="input" placeholder="https://…" /></Field>
      <Field label="Version"><input value={f.version} onChange={e => upd('version', e.target.value)} className="input" /></Field>
      <div className="flex justify-end gap-2 pt-1"><Btn variant="ghost" onClick={onClose}>Cancel</Btn><Btn onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Add'}</Btn></div>
    </Modal>
  )
}

function CampaignDetailModal({ id, canWrite, onClose, onChanged, onErr }) {
  const [c, setC] = useState(null)
  const [busy, setBusy] = useState(false)
  const [spend, setSpend] = useState('')
  const reload = useCallback(async () => {
    try { setC(await crm.getCampaign(id)) } catch (e) { onErr(e.response?.data?.message ?? 'Failed to load.') }
  }, [id])
  useEffect(() => { reload() }, [reload])

  const act = async (fn, ...args) => {
    setBusy(true)
    try { await fn(id, ...args); await reload(); onChanged() }
    catch (e) { onErr(e.response?.data?.message ?? 'Action failed.') } finally { setBusy(false) }
  }
  const doSpend = async () => {
    const amt = Number(spend)
    if (!amt || amt <= 0) return onErr('Enter a positive amount.')
    await act(crm.recordCampaignSpend, { amount: amt }); setSpend('')
  }

  if (!c) return <Modal title="Campaign" onClose={onClose} width={520}><p style={{ color: T.mgrey, fontSize: 13 }}>Loading…</p></Modal>

  return (
    <Modal title={c.name} onClose={onClose} width={560}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 12, flexWrap: 'wrap' }}>
        <Badge variant={CAMPAIGN_STATUS_VARIANT[c.status] ?? 'default'}>{c.status}</Badge>
        <span style={{ fontSize: 13, color: T.mgrey }}>{cTypeLabel(c.campaignType)}</span>
        {c.budgetAlert && <Badge variant="red">≥80% budget</Badge>}
      </div>
      {c.description && <p style={{ fontSize: 13, color: T.dgrey ?? '#374151', margin: '0 0 12px' }}>{c.description}</p>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit,minmax(120px,1fr))', gap: 12, marginBottom: 16 }}>
        <Stat label="Budget" value={fmtKes(c.budget)} />
        <Stat label="Spent" value={`${fmtKes(c.actualSpend)} (${c.spendPct}%)`} alert={c.budgetAlert} />
        <Stat label="Leads (conv/gen)" value={`${c.leadsConverted}/${c.leadsGenerated}`} />
        <Stat label="Conversion" value={`${c.conversionRate}%`} />
        <Stat label="Revenue" value={fmtKes(c.revenueAttributed)} />
        <Stat label="ROI" value={`${c.roi}×`} />
        <Stat label="Cost / lead" value={fmtKes(c.costPerLead)} />
        <Stat label="Window" value={`${fmtDate(c.startDate)} – ${fmtDate(c.endDate)}`} />
      </div>

      {canWrite && c.status !== 'Cancelled' && c.status !== 'Completed' && (
        <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14 }}>
          <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'flex-end' }}>
            <div style={{ flex: 1 }}><Field label="Record spend (KES)"><input type="number" value={spend} onChange={e => setSpend(e.target.value)} className="input" /></Field></div>
            <Btn size="sm" variant="ghost" onClick={doSpend} disabled={busy}>Add Spend</Btn>
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {c.status === 'Planned' && <Btn size="sm" onClick={() => act(crm.launchCampaign)} disabled={busy}>Launch</Btn>}
            {c.status === 'Active' && <Btn size="sm" onClick={() => act(crm.completeCampaign)} disabled={busy}>Complete</Btn>}
            <Btn size="sm" variant="danger" onClick={() => { if (window.confirm('Cancel this campaign?')) act(crm.cancelCampaign) }} disabled={busy}>Cancel</Btn>
          </div>
        </div>
      )}
    </Modal>
  )
}

function Field({ label, children, flex }) {
  return (
    <label className="block mb-3" style={flex ? { flex: 1 } : undefined}>
      <span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>
      {children}
    </label>
  )
}
function Stat({ label, value, alert }) {
  return (
    <div>
      <div style={{ fontSize: 11, color: T.mgrey, textTransform: 'uppercase', letterSpacing: 0.4 }}>{label}</div>
      <div style={{ fontSize: 14, fontWeight: 700, color: alert ? T.red : T.navy }}>{value}</div>
    </div>
  )
}
