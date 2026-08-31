import { useState, useEffect, useCallback, Fragment } from 'react'
import { Building2, Wallet, TrendingDown, PieChart, RefreshCw, ChevronDown, Landmark } from 'lucide-react'
import Collapsible from '../../components/Collapsible.jsx'
import { useAuth } from '../../context/AuthContext.jsx'
import {
  listAssetCategories, listFixedAssets, createFixedAsset, runDepreciation, getDepreciationSchedule,
  listAssetDisposals, disposeAsset, approveDisposalMd, approveDisposalBoard, listTrucks, linkFixedAssetToTruck,
} from '../../services/finance.js'

// ─────────────────────────────────────────────────────────────────────────────
// Fixed Assets — Asset Register + Depreciation Schedule + Disposals, wired to
// the real FixedAssetsController (finance-service). Styled to match the Fleet
// module (KPI tiles, inline forms, expandable/collapsible rows) rather than
// the ui.jsx kit used elsewhere in Finance.
// ─────────────────────────────────────────────────────────────────────────────

const inputCls = 'w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold'
const DISPOSAL_METHODS = ['Sold', 'Scrapped', 'Donated', 'WrittenOff']
const DISPOSAL_STATUS_STYLE = {
  PendingMdApproval:    'bg-amber-100 text-amber-700',
  PendingBoardApproval: 'bg-amber-100 text-amber-700',
  Approved:             'bg-green-100 text-green-700',
  Rejected:             'bg-red-100 text-red-700',
}
const currentPeriod = () => new Date().toISOString().slice(0, 7)

function fmtKes(n) {
  if (n === null || n === undefined) return '—'
  return new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n)
}
function fmtDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return isNaN(d) ? '—' : d.toLocaleDateString('en-KE')
}

function KpiCard({ label, value, icon, tone = 'navy' }) {
  const toneCls = {
    navy:  'bg-navy/10 text-navy',
    green: 'bg-green-100 text-green-700',
    amber: 'bg-amber-100 text-amber-700',
    red:   'bg-red-100 text-red-700',
  }[tone]
  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">{label}</p>
          <p className="text-2xl font-extrabold text-gray-900 mt-1 break-words">{value}</p>
        </div>
        <div className={`w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0 ${toneCls}`}>{icon}</div>
      </div>
    </div>
  )
}

export default function FixedAssetsPage() {
  const { hasPermission } = useAuth()
  const canWrite = hasPermission('finance.write')
  const canApprove = hasPermission('finance.approve')

  const [tab, setTab] = useState('register')
  const [categories, setCategories] = useState([])
  const [assets, setAssets] = useState([])
  const [schedule, setSchedule] = useState([])
  const [disposals, setDisposals] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [runningDep, setRunningDep] = useState(false)

  // Asset Register — inline add form + per-row expand (depreciation history)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ categoryId: '', description: '', cost: '', date: '', serial: '', location: '', linkedTruckId: '' })
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')
  const [trucks, setTrucks] = useState([])
  const [expandedAssetId, setExpandedAssetId] = useState(null)

  // Depreciation Schedule — collapsible period groups
  const [expandedPeriods, setExpandedPeriods] = useState(() => new Set())

  // Disposals — per-row expand (approval trail) + dispose modal
  const [expandedDisposalId, setExpandedDisposalId] = useState(null)
  const [disposeFor, setDisposeFor] = useState(null)
  const [disposeForm, setDisposeForm] = useState({ date: '', method: DISPOSAL_METHODS[0], proceeds: '' })
  const [disposing, setDisposing] = useState(false)
  const [disposeErr, setDisposeErr] = useState('')

  const notify = (text) => { setSuccess(text); setError('') }

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([listAssetCategories(), listFixedAssets(), getDepreciationSchedule(), listAssetDisposals()])
      .then(([c, a, s, d]) => {
        setCategories(c ?? []); setAssets(a ?? []); setSchedule(s ?? []); setDisposals(d ?? [])
        setForm(f => ({ ...f, categoryId: f.categoryId || (c ?? [])[0]?.id || '' }))
      })
      .catch(() => setError('Failed to load fixed assets.'))
      .finally(() => setLoading(false))
  }, [])
  useEffect(() => { load() }, [load])

  const category = categories.find(c => c.id === form.categoryId)
  const isMotorVehicle = category?.name === 'Motor Vehicles'
  useEffect(() => {
    if (isMotorVehicle && trucks.length === 0) listTrucks().then(t => setTrucks(t ?? [])).catch(() => {})
  }, [isMotorVehicle, trucks.length])

  const activeAssets = assets.filter(a => a.status === 'Active')
  const originalCost = activeAssets.reduce((s, a) => s + a.acquisitionCost, 0)
  const accumulated = activeAssets.reduce((s, a) => s + a.accumulatedDepreciation, 0)
  const nbv = originalCost - accumulated

  async function handleRunDepreciation() {
    const period = window.prompt('Run depreciation for which period? (yyyy-MM)', currentPeriod())
    if (!period) return
    setRunningDep(true); setError('')
    try {
      const result = await runDepreciation(period)
      notify(result.alreadyRun
        ? `Depreciation for ${period} was already run — no changes made.`
        : `Depreciation posted: ${result.assetsProcessed} asset(s), ${fmtKes(result.totalCharge)}.`)
      await load()
      setTab('schedule')
    } catch (e) { setError(e.response?.data?.message ?? 'Failed to run depreciation.') }
    finally { setRunningDep(false) }
  }

  function set(field, val) { setForm(f => ({ ...f, [field]: val })) }

  async function handleCreateAsset(e) {
    e.preventDefault()
    setFormErr('')
    if (!form.categoryId || !form.description || !form.cost || !form.date) {
      setFormErr('Category, description, cost, and acquisition date are required.')
      return
    }
    setSaving(true)
    try {
      const asset = await createFixedAsset({
        categoryId: form.categoryId, description: form.description, acquisitionCost: +form.cost || 0,
        acquisitionDate: form.date, serialNumber: form.serial || null, location: form.location || null,
        linkedTruckId: isMotorVehicle && form.linkedTruckId ? form.linkedTruckId : null,
      })
      if (isMotorVehicle && form.linkedTruckId) {
        const truck = trucks.find(t => t.id === form.linkedTruckId)
        if (truck) await linkFixedAssetToTruck(truck, asset.id).catch(() => setError('Asset registered, but linking the vehicle record failed — link it manually.'))
      }
      setShowForm(false)
      setForm({ categoryId: categories[0]?.id ?? '', description: '', cost: '', date: '', serial: '', location: '', linkedTruckId: '' })
      await load()
      notify(`Asset ${asset.assetTag} registered.`)
    } catch (e) { setFormErr(e.response?.data?.message ?? 'Failed to register asset.') }
    finally { setSaving(false) }
  }

  function openDispose(asset) {
    setDisposeFor(asset)
    setDisposeForm({ date: new Date().toISOString().slice(0, 10), method: DISPOSAL_METHODS[0], proceeds: '' })
    setDisposeErr('')
  }

  async function handleDispose(e) {
    e.preventDefault()
    setDisposeErr('')
    setDisposing(true)
    try {
      await disposeAsset(disposeFor.id, { disposalDate: disposeForm.date, method: disposeForm.method, proceeds: disposeForm.proceeds ? +disposeForm.proceeds : null })
      setDisposeFor(null)
      await load()
      notify('Disposal submitted for MD approval.')
    } catch (e) { setDisposeErr(e.response?.data?.message ?? 'Failed to submit disposal.') }
    finally { setDisposing(false) }
  }

  async function handleApproveMd(id) {
    try { await approveDisposalMd(id); await load(); notify('Disposal approved by MD.') }
    catch (e) { setError(e.response?.data?.message ?? 'Failed to approve.') }
  }
  async function handleApproveBoard(id) {
    try { await approveDisposalBoard(id); await load(); notify('Board approval recorded — asset disposed.') }
    catch (e) { setError(e.response?.data?.message ?? 'Failed to record.') }
  }

  const periodGroups = Object.values(schedule.reduce((acc, e) => {
    (acc[e.period] ??= { period: e.period, entries: [], total: 0 }).entries.push(e)
    acc[e.period].total += e.amount
    return acc
  }, {})).sort((a, b) => b.period.localeCompare(a.period))

  function togglePeriod(period) {
    setExpandedPeriods(prev => {
      const next = new Set(prev)
      next.has(period) ? next.delete(period) : next.add(period)
      return next
    })
  }

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-8">
        <Collapsible title="About the Fixed Asset Register" dismissKey="finance.pageInfo.fixedAssets.dismissed">
          <p className="text-sm text-gray-700">
            Every asset costing Kshs 10,000 or more must be registered here (ASSET-001). Depreciation posts
            monthly on a straight-line basis to the General Ledger; disposals above Kshs 200,000 net book
            value require Board sign-off after MD approval.
          </p>
        </Collapsible>

        <div className="flex items-center justify-between mb-6 flex-wrap gap-3">
          <div>
            <h1 className="text-2xl font-extrabold text-navy">Fixed Assets</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {loading ? 'Loading…' : `${activeAssets.length} active asset${activeAssets.length !== 1 ? 's' : ''}`}
            </p>
          </div>
          {canWrite && (
            <div className="flex items-center gap-3">
              <button
                onClick={handleRunDepreciation}
                disabled={runningDep}
                className="inline-flex items-center gap-2 px-4 py-2 bg-white border border-gray-300 hover:bg-gray-50 text-gray-700 text-sm font-semibold rounded-xl transition-colors disabled:opacity-50"
              >
                <RefreshCw size={14} className={runningDep ? 'animate-spin' : ''} /> {runningDep ? 'Running…' : 'Run Depreciation'}
              </button>
              <button
                onClick={() => setShowForm(v => !v)}
                className="inline-flex items-center gap-2 px-4 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl transition-colors shadow"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                </svg>
                Add Asset
              </button>
            </div>
          )}
        </div>

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
          <KpiCard label="Active Assets"              value={activeAssets.length}     icon={<Building2 size={20} />} tone="navy" />
          <KpiCard label="Original Cost"               value={fmtKes(originalCost)}     icon={<Wallet size={20} />} tone="navy" />
          <KpiCard label="Accumulated Depreciation"    value={fmtKes(accumulated)}      icon={<TrendingDown size={20} />} tone="amber" />
          <KpiCard label="Net Book Value"              value={fmtKes(nbv)}              icon={<PieChart size={20} />} tone="green" />
        </div>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-4 text-sm mb-4">{error}</div>}
        {success && <div className="bg-green-50 border border-green-200 text-green-700 rounded-xl px-5 py-4 text-sm mb-4">{success}</div>}

        <div className="bg-white rounded-xl border border-gray-200 overflow-hidden mb-5">
          <div className="flex border-b border-gray-100">
            {[
              { id: 'register', label: 'Asset Register' },
              { id: 'schedule', label: `Depreciation Schedule${schedule.length ? ` (${schedule.length})` : ''}` },
              { id: 'disposals', label: `Disposals${disposals.length ? ` (${disposals.length})` : ''}` },
            ].map(t => (
              <button
                key={t.id}
                onClick={() => setTab(t.id)}
                className={`px-5 py-3 text-sm font-semibold transition-colors ${tab === t.id ? 'text-navy border-b-2 border-gold' : 'text-gray-500 hover:text-gray-700'}`}
              >
                {t.label}
              </button>
            ))}
          </div>
        </div>

        {tab === 'register' && showForm && (
          <form onSubmit={handleCreateAsset} className="bg-white rounded-2xl border border-gray-200 p-5 mb-5 shadow-sm">
            <h2 className="text-sm font-semibold text-gray-700 mb-1">Register New Asset</h2>
            <p className="text-xs text-gray-400 mb-4">Minimum: Kshs 10,000 — items below this threshold must be expensed directly.</p>
            {formErr && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2 mb-3">{formErr}</div>}
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Category *</label>
                <select value={form.categoryId} onChange={e => set('categoryId', e.target.value)} className={inputCls}>
                  {categories.map(c => <option key={c.id} value={c.id}>{c.name} ({c.usefulLifeLabel})</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Description *</label>
                <input value={form.description} onChange={e => set('description', e.target.value)} placeholder="e.g. Toyota Hilux — KDA 123B" className={inputCls} required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Cost (Kshs) *</label>
                <input type="number" min="0" value={form.cost} onChange={e => set('cost', e.target.value)} className={inputCls} required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Acquisition Date *</label>
                <input type="date" value={form.date} onChange={e => set('date', e.target.value)} className={inputCls} required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Serial Number</label>
                <input value={form.serial} onChange={e => set('serial', e.target.value)} className={inputCls} />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Location</label>
                <input value={form.location} onChange={e => set('location', e.target.value)} placeholder="Office/site/vehicle" className={inputCls} />
              </div>
              {isMotorVehicle && (
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1">Link to Fleet Vehicle (ASSET-008)</label>
                  <select value={form.linkedTruckId} onChange={e => set('linkedTruckId', e.target.value)} className={inputCls}>
                    <option value="">— None —</option>
                    {trucks.map(t => <option key={t.id} value={t.id}>{t.licensePlate} — {t.model}</option>)}
                  </select>
                </div>
              )}
            </div>
            <div className="flex gap-3 mt-4">
              <button type="submit" disabled={saving} className="px-5 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                {saving ? 'Saving…' : 'Register Asset'}
              </button>
              <button type="button" onClick={() => setShowForm(false)} className="px-5 py-2.5 text-sm border border-gray-200 rounded-xl hover:bg-gray-50">Cancel</button>
            </div>
          </form>
        )}

        {loading ? (
          <div className="space-y-3">{[1, 2, 3].map(i => <div key={i} className="h-16 bg-white rounded-xl border animate-pulse" />)}</div>
        ) : tab === 'register' ? (
          assets.length === 0 ? (
            <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
              <div className="flex justify-center mb-3 text-gray-400"><Building2 size={40} /></div>
              <h3 className="font-semibold text-gray-700">No assets registered</h3>
              <p className="text-sm text-gray-400 mt-1">Add your first fixed asset to the register.</p>
            </div>
          ) : (
            <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-gray-50 border-b border-gray-100">
                    <th className="text-left px-5 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Tag No</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Description</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden md:table-cell">Category</th>
                    <th className="text-right px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Cost</th>
                    <th className="text-right px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden lg:table-cell">NBV</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider hidden xl:table-cell">Location</th>
                    <th className="text-left px-4 py-3 text-xs font-semibold text-gray-600 uppercase tracking-wider">Status</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {assets.map(a => {
                    const expanded = expandedAssetId === a.id
                    const history = schedule.filter(e => e.assetId === a.id)
                    return (
                      <Fragment key={a.id}>
                        <tr onClick={() => setExpandedAssetId(id => id === a.id ? null : a.id)} className="hover:bg-gray-50 transition-colors cursor-pointer">
                          <td className="px-5 py-4 font-mono text-xs font-semibold text-gray-900">{a.assetTag}</td>
                          <td className="px-4 py-4 font-medium text-gray-800">{a.description}</td>
                          <td className="px-4 py-4 text-gray-600 hidden md:table-cell">{a.categoryName}</td>
                          <td className="px-4 py-4 text-right text-gray-600">{fmtKes(a.acquisitionCost)}</td>
                          <td className="px-4 py-4 text-right font-semibold text-green-700 hidden lg:table-cell">{fmtKes(a.netBookValue)}</td>
                          <td className="px-4 py-4 text-gray-600 hidden xl:table-cell">{a.location || '—'}</td>
                          <td className="px-4 py-4">
                            <span className={`inline-flex px-2.5 py-1 rounded-full text-xs font-semibold ${a.status === 'Active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                              {a.status}
                            </span>
                          </td>
                          <td className="px-4 py-4 text-right">
                            <div className="flex items-center justify-end gap-2" onClick={e => e.stopPropagation()}>
                              {canWrite && a.status === 'Active' && (
                                <button onClick={() => openDispose(a)} className="text-xs px-3 py-1.5 border border-red-200 rounded-lg hover:bg-red-50 text-red-600 font-medium">Dispose</button>
                              )}
                              <ChevronDown size={16} className={`text-gray-400 transition-transform ${expanded ? 'rotate-180' : ''}`} />
                            </div>
                          </td>
                        </tr>
                        {expanded && (
                          <tr className="bg-gray-50/60">
                            <td colSpan={8} className="px-5 py-3">
                              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-2">Depreciation History</p>
                              {history.length === 0 ? (
                                <p className="text-sm text-gray-400">No depreciation has been run for this asset yet.</p>
                              ) : (
                                <div className="space-y-1.5">
                                  {history.map(h => (
                                    <div key={h.id} className="flex items-center justify-between text-sm px-3 py-1.5 bg-white rounded-lg border border-gray-100">
                                      <span className="text-gray-600">{h.period}</span>
                                      <span className="font-semibold text-gray-800">{fmtKes(h.amount)}</span>
                                      <span className="text-gray-400 text-xs">Acc. {fmtKes(h.accumulatedAfter)} · NBV {fmtKes(h.netBookValueAfter)}</span>
                                    </div>
                                  ))}
                                </div>
                              )}
                              {a.acquisitionCost > 0 && a.annualRate > 0 && (
                                <p className="text-xs text-gray-400 mt-2">Rate: {(a.annualRate * 100).toFixed(2)}% p.a. straight-line — {a.usefulLifeLabel}</p>
                              )}
                            </td>
                          </tr>
                        )}
                      </Fragment>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )
        ) : tab === 'schedule' ? (
          periodGroups.length === 0 ? (
            <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
              <div className="flex justify-center mb-3 text-gray-400"><TrendingDown size={40} /></div>
              <h3 className="font-semibold text-gray-700">No depreciation has been run yet</h3>
              <p className="text-sm text-gray-400 mt-1">Use "Run Depreciation" on the Asset Register tab.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {periodGroups.map(g => {
                const open = expandedPeriods.has(g.period)
                return (
                  <div key={g.period} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                    <button onClick={() => togglePeriod(g.period)} className="w-full px-5 py-3.5 flex items-center justify-between text-left hover:bg-gray-50 transition-colors">
                      <div>
                        <p className="text-sm font-bold text-gray-800">{g.period}</p>
                        <p className="text-xs text-gray-400">{g.entries.length} asset{g.entries.length !== 1 ? 's' : ''}</p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm font-extrabold text-amber-700">{fmtKes(g.total)}</span>
                        <ChevronDown size={16} className={`text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`} />
                      </div>
                    </button>
                    {open && (
                      <div className="px-5 pb-4 pt-1 border-t border-gray-100 space-y-1.5">
                        {g.entries.map(e => (
                          <div key={e.id} className="flex items-center justify-between text-sm px-3 py-2 bg-gray-50 rounded-lg">
                            <span><span className="font-mono text-xs text-gray-500">{e.assetTag}</span> — {e.assetDescription}</span>
                            <span className="font-semibold text-gray-800">{fmtKes(e.amount)}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )
        ) : (
          disposals.length === 0 ? (
            <div className="flex flex-col items-center justify-center bg-white rounded-2xl border border-dashed border-gray-300 py-20 text-center">
              <div className="flex justify-center mb-3 text-gray-400"><Landmark size={40} /></div>
              <h3 className="font-semibold text-gray-700">No disposals recorded</h3>
              <p className="text-sm text-gray-400 mt-1">Assets above Kshs 200,000 NBV require Board sign-off after MD approval.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {disposals.map(d => {
                const expanded = expandedDisposalId === d.id
                return (
                  <div key={d.id} className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                    <button onClick={() => setExpandedDisposalId(id => id === d.id ? null : d.id)} className="w-full px-5 py-3.5 flex items-center justify-between text-left hover:bg-gray-50 transition-colors">
                      <div>
                        <p className="text-sm font-semibold text-gray-800"><span className="font-mono text-xs text-gray-500">{d.assetTag}</span> — {d.assetDescription}</p>
                        <p className="text-xs text-gray-400">{d.method} · {fmtDate(d.disposalDate)} · NBV {fmtKes(d.closingNbv)}</p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className={`inline-flex px-2.5 py-1 rounded-full text-xs font-semibold ${DISPOSAL_STATUS_STYLE[d.status] ?? 'bg-gray-100 text-gray-600'}`}>{d.status}</span>
                        <ChevronDown size={16} className={`text-gray-400 transition-transform ${expanded ? 'rotate-180' : ''}`} />
                      </div>
                    </button>
                    {expanded && (
                      <div className="px-5 pb-4 pt-1 border-t border-gray-100 space-y-2">
                        <div className="flex justify-between text-sm"><span className="text-gray-400">Proceeds</span><span className="font-semibold text-gray-800">{d.proceeds != null ? fmtKes(d.proceeds) : '—'}</span></div>
                        <div className="flex justify-between text-sm"><span className="text-gray-400">MD Approval</span><span className="font-semibold text-gray-800">{d.mdApprovedBy ? `${d.mdApprovedBy} · ${fmtDate(d.mdApprovedAt)}` : 'Pending'}</span></div>
                        {d.requiresBoardApproval && (
                          <div className="flex justify-between text-sm"><span className="text-gray-400">Board Approval</span><span className="font-semibold text-gray-800">{d.boardApprovedBy ? `${d.boardApprovedBy} · ${fmtDate(d.boardApprovedAt)}` : 'Pending (required, NBV over Kshs 200,000)'}</span></div>
                        )}
                        {canApprove && d.status === 'PendingMdApproval' && (
                          <button onClick={() => handleApproveMd(d.id)} className="mt-2 px-4 py-2 bg-navy hover:bg-navy-dark text-white text-xs font-bold rounded-lg transition-colors">Approve (MD)</button>
                        )}
                        {canApprove && d.status === 'PendingBoardApproval' && (
                          <button onClick={() => handleApproveBoard(d.id)} className="mt-2 px-4 py-2 bg-navy hover:bg-navy-dark text-white text-xs font-bold rounded-lg transition-colors">Record Board Approval</button>
                        )}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )
        )}
      </main>

      {disposeFor && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
          <form onSubmit={handleDispose} className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <h3 className="text-base font-bold text-gray-900">Dispose Asset — {disposeFor.assetTag}</h3>
            {disposeErr && <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-4 py-2">{disposeErr}</div>}
            <div className="bg-gray-50 rounded-lg px-4 py-2.5 text-sm">
              Net Book Value: <span className="font-semibold text-amber-700">{fmtKes(disposeFor.netBookValue)}</span>
              {disposeFor.netBookValue > 200000 && <p className="text-xs text-gray-500 mt-1">This exceeds Kshs 200,000 — Board approval will be required after MD sign-off.</p>}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Disposal Date</label>
                <input type="date" value={disposeForm.date} onChange={e => setDisposeForm(f => ({ ...f, date: e.target.value }))} className={inputCls} required />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-500 mb-1">Method</label>
                <select value={disposeForm.method} onChange={e => setDisposeForm(f => ({ ...f, method: e.target.value }))} className={inputCls}>
                  {DISPOSAL_METHODS.map(m => <option key={m} value={m}>{m}</option>)}
                </select>
              </div>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">Proceeds (Kshs, if any)</label>
              <input type="number" min="0" value={disposeForm.proceeds} onChange={e => setDisposeForm(f => ({ ...f, proceeds: e.target.value }))} className={inputCls} />
            </div>
            <div className="flex gap-3 pt-2">
              <button type="button" onClick={() => setDisposeFor(null)} className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors">Cancel</button>
              <button type="submit" disabled={disposing} className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-xl disabled:opacity-50 transition-colors">
                {disposing ? 'Submitting…' : 'Submit for MD Approval'}
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  )
}
