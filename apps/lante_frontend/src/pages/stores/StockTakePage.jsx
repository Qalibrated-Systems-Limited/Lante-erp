import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import SelectWithAdd from '../../components/SelectWithAdd.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'
import { Download } from 'lucide-react'
import { exportToPdf, exportToExcel, STOCK_TAKE_COLUMNS } from '../../utils/export.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const EXPORT_CAP = 100

// Approvals are attributed to whoever is logged in — never manually typed.
function currentUserName(user) {
  return [user?.firstName, user?.lastName].filter(Boolean).join(' ') || user?.email || 'Unknown user'
}
const BLANK = { itemId: '', locationId: '', physicalCount: '', reasonCode: 'CountCorrection' }
const REASONS = [
  { value: 'CountCorrection', label: 'Count Correction' },
  { value: 'Damage', label: 'Damage' },
  { value: 'Expiry', label: 'Expiry' },
  { value: 'Theft', label: 'Theft' },
  { value: 'WriteOff', label: 'Write-Off' },
  { value: 'Other', label: 'Other' },
]
const PHYSICAL_COUNT_HELP = 'The quantity you actually counted at this location right now — not an adjustment amount. The system compares this to its own running total and reconciles the difference.'
const REASON_CODE_HELP = 'What caused the physical count to differ from the system quantity — Count Correction for a routine recount, Damage/Expiry/Theft/Write-Off when stock was actually lost, or Other.'

export default function StockTakePage() {
  const { user } = useAuth()
  const [stockTakes, setStockTakes] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [items, setItems] = useState([])
  const [suppliers, setSuppliers] = useState([])
  const [categories, setCategories] = useState([])
  const [unitsOfMeasure, setUnitsOfMeasure] = useState([])
  const [locations, setLocations] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const [modal, setModal] = useState(false)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [approveModal, setApproveModal] = useState(null) // null | stockTake
  const [approving, setApproving] = useState(false)
  const [approveErr, setApproveErr] = useState('')

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/items', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/locations', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/suppliers', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/categories', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/units-of-measure', { params: { pageSize: 200, isActive: true } }),
    ]).then(([itemsRes, locationsRes, suppliersRes, categoriesRes, unitsRes]) => {
      setItems(itemsRes.data?.data?.items ?? [])
      setLocations(locationsRes.data?.data?.items ?? [])
      setSuppliers(suppliersRes.data?.data?.items ?? [])
      setCategories(categoriesRes.data?.data?.items ?? [])
      setUnitsOfMeasure(unitsRes.data?.data?.items ?? [])
    }).catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/stock-take', { params: { page, pageSize: PAGE_SIZE } })
      setStockTakes(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load stock-take records.')
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function fetchExportRows() {
    const res = await api.get('/api/v1/stock-take', { params: { page: 1, pageSize: EXPORT_CAP } })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Stock-Take Reconciliation',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: STOCK_TAKE_COLUMNS, rows, filename: 'stock-take', theme: 'navy',
      })
    } catch {
      setError('Failed to export PDF.')
    } finally {
      setExporting(false)
    }
  }

  async function handleExportExcel() {
    setExporting(true)
    try {
      const { rows } = await fetchExportRows()
      exportToExcel({ title: 'Stock-Take Reconciliation', columns: STOCK_TAKE_COLUMNS, rows, filename: 'stock-take' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  function openCreate() {
    setForm({ ...BLANK, itemId: items[0]?.id ?? '', locationId: locations[0]?.id ?? '' })
    setFormErr('')
    setModal(true)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.itemId || !form.locationId || form.physicalCount === '') {
      setFormErr('Item, location and physical count are required.')
      return
    }
    if (Number(form.physicalCount) < 0) {
      setFormErr('Physical Count cannot be negative.')
      return
    }
    setSaving(true)
    try {
      await api.post('/api/v1/stock-take', {
        itemId: form.itemId,
        locationId: form.locationId,
        physicalCount: Number(form.physicalCount),
        reasonCode: form.reasonCode,
      })
      setModal(false)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to record stock take.')
    } finally {
      setSaving(false)
    }
  }

  function openApprove(st) {
    setApproveErr('')
    setApproveModal(st)
  }

  async function handleApprove() {
    setApproveErr('')
    setApproving(true)
    try {
      await api.post(`/api/v1/stock-take/${approveModal.id}/approve`, { approvedBy: currentUserName(user) })
      setApproveModal(null)
      load()
    } catch (err) {
      setApproveErr(err.response?.data?.message ?? 'Failed to approve stock take.')
    } finally {
      setApproving(false)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Stock Take" dismissKey="stores.pageInfo.stockTake.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            Physical counts reconciled against system stock. Approving a count is what adjusts
            the books to match what was physically counted.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Stock-Take Reconciliation"
          sub={loading ? 'Loading…' : `${totalCount} record${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ New Count</Btn>
            </div>
          }
        />

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Item', 'Location', 'Physical', 'System', 'Variance', 'Reason', 'Status', 'Action']}
              empty="No stock-take records yet — record a physical count to find variances against system stock."
              rows={stockTakes.map(st => [
                <strong style={{ fontSize: 12 }}>{st.itemCode}</strong>,
                st.locationName || '—',
                st.physicalCount,
                st.systemCount,
                <strong style={{ color: st.variance === 0 ? T.mgrey : st.variance > 0 ? T.green : T.red }}>{st.variance > 0 ? '+' : ''}{st.variance}</strong>,
                st.reasonCode,
                <Badge variant={st.status === 'Approved' ? 'green' : 'amber'}>{st.status}</Badge>,
                st.status === 'Pending'
                  ? <Btn variant="green" size="sm" onClick={() => openApprove(st)}>Approve</Btn>
                  : (st.approvedBy ? <span style={{ fontSize: 11, color: T.mgrey }}>by {st.approvedBy}</span> : '—'),
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title="New Physical Count" onClose={() => setModal(false)}>
          {formErr && <Alert type="error">{formErr}</Alert>}
          <SelectWithAdd label="Item" value={form.itemId} onChange={v => setForm(f => ({ ...f, itemId: v }))} required
            options={[{ value: '', label: 'Select item…' }, ...items.map(i => ({ value: i.id, label: `${i.itemCode} — ${i.description}` }))]}
            addTitle="Item"
            initialQuickAddForm={{ supplierId: '', categoryId: '', itemCode: '', description: '', uomId: '', minSellingPrice: '', minStockLevel: '', maxStockLevel: '', reorderQty: '' }}
            renderQuickAddForm={({ form: qForm, setForm: setQForm }) => (
              <>
                <Select label="Supplier" required value={qForm.supplierId} onChange={v => setQForm(f => ({ ...f, supplierId: v }))}
                  options={[{ value: '', label: 'Select supplier…' }, ...suppliers.map(s => ({ value: s.id, label: s.name }))]} />
                <SelectWithAdd label="Category" required value={qForm.categoryId} onChange={v => setQForm(f => ({ ...f, categoryId: v }))}
                  options={[{ value: '', label: 'Select category…' }, ...categories.map(c => ({ value: c.id, label: c.name }))]}
                  addTitle="Category"
                  initialQuickAddForm={{ code: '', name: '' }}
                  renderQuickAddForm={({ form: cForm, setForm: setCForm }) => (
                    <>
                      <Input label="Name" required value={cForm.name} onChange={v => setCForm(f => ({ ...f, name: v }))} />
                      <div>
                        <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
                          Code<span style={{ color: T.red }}> *</span>
                        </label>
                        <div style={{ display: 'flex', gap: 6 }}>
                          <input value={cForm.code} onChange={e => setCForm(f => ({ ...f, code: e.target.value }))}
                            placeholder="Type or generate…" required
                            style={{ flex: 1, minWidth: 0, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box' }} />
                          <Btn type="button" variant="ghost" size="sm" onClick={() => setCForm(f => ({ ...f, code: generateCodeFromName(f.name) }))}>Generate</Btn>
                        </div>
                        <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3, marginBottom: 14 }}>Type your own, or generate one from the name.</p>
                      </div>
                    </>
                  )}
                  onQuickAddSubmit={async (cForm) => {
                    if (!cForm.code.trim() || !cForm.name.trim()) throw new Error('Code and Name are required.')
                    const res = await api.post('/api/v1/categories', { code: cForm.code.trim(), name: cForm.name.trim() })
                    return res.data?.data
                  }}
                  onQuickAddCreated={(category) => setCategories(s => [...s, category])}
                  getOptionFromRecord={(category) => ({ value: category.id, label: category.name })}
                />
                <Input label="Item Code" required value={qForm.itemCode} onChange={v => setQForm(f => ({ ...f, itemCode: v }))} />
                <Input label="Description" value={qForm.description} onChange={v => setQForm(f => ({ ...f, description: v }))} />
                <SelectWithAdd label="Unit of Measure" required value={qForm.uomId} onChange={v => setQForm(f => ({ ...f, uomId: v }))}
                  options={[{ value: '', label: 'Select unit…' }, ...unitsOfMeasure.map(u => ({ value: u.id, label: u.name }))]}
                  addTitle="Unit"
                  initialQuickAddForm={{ name: '', description: '' }}
                  renderQuickAddForm={({ form: uForm, setForm: setUForm }) => (
                    <>
                      <Input label="Name" required value={uForm.name} onChange={v => setUForm(f => ({ ...f, name: v }))} placeholder="e.g. kg, pcs, ltr" />
                      <Input label="Description" value={uForm.description} onChange={v => setUForm(f => ({ ...f, description: v }))} placeholder="e.g. Kilograms" />
                    </>
                  )}
                  onQuickAddSubmit={async (uForm) => {
                    if (!uForm.name.trim()) throw new Error('Name is required.')
                    const res = await api.post('/api/v1/units-of-measure', { name: uForm.name.trim(), description: uForm.description.trim() || null })
                    return res.data?.data
                  }}
                  onQuickAddCreated={(unit) => setUnitsOfMeasure(s => [...s, unit])}
                  getOptionFromRecord={(unit) => ({ value: unit.id, label: unit.name })}
                />
                <div style={{ display: 'flex', gap: 12 }}>
                  <div style={{ flex: 1 }}><Input label="Min Selling Price" type="number" value={qForm.minSellingPrice} onChange={v => setQForm(f => ({ ...f, minSellingPrice: v }))} /></div>
                  <div style={{ flex: 1 }}><Input label="Reorder Qty" type="number" value={qForm.reorderQty} onChange={v => setQForm(f => ({ ...f, reorderQty: v }))} /></div>
                </div>
                <div style={{ display: 'flex', gap: 12 }}>
                  <div style={{ flex: 1 }}><Input label="Min Stock Level" type="number" value={qForm.minStockLevel} onChange={v => setQForm(f => ({ ...f, minStockLevel: v }))} /></div>
                  <div style={{ flex: 1 }}><Input label="Max Stock Level" type="number" value={qForm.maxStockLevel} onChange={v => setQForm(f => ({ ...f, maxStockLevel: v }))} /></div>
                </div>
              </>
            )}
            onQuickAddSubmit={async (qForm) => {
              if (!qForm.supplierId || !qForm.categoryId || !qForm.itemCode.trim() || !qForm.uomId) {
                throw new Error('Supplier, Category, Item Code and Unit of Measure are required.')
              }
              const res = await api.post('/api/v1/items', {
                supplierId: qForm.supplierId, categoryId: qForm.categoryId,
                itemCode: qForm.itemCode.trim(), description: qForm.description.trim(), uomId: qForm.uomId,
                minSellingPrice: Number(qForm.minSellingPrice) || 0,
                minStockLevel: Number(qForm.minStockLevel) || 0,
                maxStockLevel: Number(qForm.maxStockLevel) || 0,
                reorderQty: Number(qForm.reorderQty) || 0,
              })
              return res.data?.data
            }}
            onQuickAddCreated={(item) => setItems(s => [...s, item])}
            getOptionFromRecord={(item) => ({ value: item.id, label: `${item.itemCode} — ${item.description}` })}
          />

          <SelectWithAdd label="Location" value={form.locationId} onChange={v => setForm(f => ({ ...f, locationId: v }))} required
            options={[{ value: '', label: 'Select location…' }, ...locations.map(l => ({ value: l.id, label: l.name }))]}
            addTitle="Location"
            initialQuickAddForm={{ code: '', name: '', type: 'Warehouse' }}
            renderQuickAddForm={({ form: lForm, setForm: setLForm }) => (
              <>
                <Input label="Name" required value={lForm.name} onChange={v => setLForm(f => ({ ...f, name: v }))} />
                <div>
                  <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
                    Code<span style={{ color: T.red }}> *</span>
                  </label>
                  <div style={{ display: 'flex', gap: 6 }}>
                    <input value={lForm.code} onChange={e => setLForm(f => ({ ...f, code: e.target.value }))}
                      placeholder="Type or generate…" required
                      style={{ flex: 1, minWidth: 0, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box' }} />
                    <Btn type="button" variant="ghost" size="sm" onClick={() => setLForm(f => ({ ...f, code: generateCodeFromName(f.name) }))}>Generate</Btn>
                  </div>
                  <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3, marginBottom: 14 }}>Type your own, or generate one from the name.</p>
                </div>
                <Select label="Type" value={lForm.type} onChange={v => setLForm(f => ({ ...f, type: v }))}
                  options={['Warehouse', 'Site', 'Vehicle', 'Vendor'].map(t => ({ value: t, label: t }))} />
              </>
            )}
            onQuickAddSubmit={async (lForm) => {
              if (!lForm.code.trim() || !lForm.name.trim()) throw new Error('Code and Name are required.')
              const res = await api.post('/api/v1/locations', { code: lForm.code.trim(), name: lForm.name.trim(), type: lForm.type })
              return res.data?.data
            }}
            onQuickAddCreated={(location) => setLocations(s => [...s, location])}
            getOptionFromRecord={(location) => ({ value: location.id, label: location.name })}
          />

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <Input label="Physical Count" type="number" value={form.physicalCount} onChange={v => setForm(f => ({ ...f, physicalCount: v }))} required
                help={PHYSICAL_COUNT_HELP} helpKey="stores.physicalCountHelp.dismissed" />
            </div>
            <div style={{ flex: 1 }}>
              <Select label="Reason Code" value={form.reasonCode} onChange={v => setForm(f => ({ ...f, reasonCode: v }))} options={REASONS}
                help={REASON_CODE_HELP} helpKey="stores.reasonCodeHelp.dismissed" />
            </div>
          </div>

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : 'Record Count'}</Btn>
          </div>
        </Modal>
      )}

      {approveModal && (
        <Modal title="Approve Stock Take" onClose={() => setApproveModal(null)} width={420}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            This will adjust <strong>{approveModal.itemCode}</strong> at <strong>{approveModal.locationName}</strong>{' '}
            from {approveModal.systemCount} to <strong>{approveModal.physicalCount}</strong>.
          </p>
          {approveErr && <Alert type="error">{approveErr}</Alert>}
          <p style={{ fontSize: 12, color: T.mgrey, marginBottom: 14 }}>
            Approving as <strong style={{ color: T.dgrey }}>{currentUserName(user)}</strong>
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setApproveModal(null)}>Cancel</Btn>
            <Btn variant="green" disabled={approving} onClick={handleApprove}>{approving ? 'Approving…' : 'Confirm Approval'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
