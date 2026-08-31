import { useState, useEffect, useCallback, useRef } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import SelectWithAdd from '../../components/SelectWithAdd.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'
import { Download } from 'lucide-react'
import { exportToPdf, exportToExcel, GRN_COLUMNS } from '../../utils/export.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const BLANK = { itemId: '', supplierId: '', locationId: '', qtyReceived: '', landedCost: '', notes: '', poNumber: '', poId: '', partialDelivery: false }
const PAGE_SIZE = 20
const EXPORT_CAP = 100

const STATUS_BADGE = { Passed: 'green', Failed: 'red', Pending: 'blue' }
const STATUS_HELP = 'Pending: awaiting inspection. Passed: goods checked and stock units created. Failed: goods rejected — no stock was added.'

export default function GrnPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const prefillHandled = useRef(false)

  const [grns, setGrns] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [items, setItems] = useState([])
  const [suppliers, setSuppliers] = useState([])
  const [categories, setCategories] = useState([])
  const [unitsOfMeasure, setUnitsOfMeasure] = useState([])
  const [locations, setLocations] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [exporting, setExporting] = useState(false)

  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState('')
  const [itemFilter, setItemFilter] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  const [modal, setModal] = useState(false)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  // Reference lists load once — not tied to GRN pagination/filters.
  useEffect(() => {
    Promise.all([
      api.get('/api/v1/items', { params: { pageSize: 100, isActive: true } }),
      api.get('/api/v1/suppliers', { params: { pageSize: 100, isActive: true } }),
      api.get('/api/v1/categories', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/locations', { params: { pageSize: 100, isActive: true } }),
      api.get('/api/v1/units-of-measure', { params: { pageSize: 200, isActive: true } }),
    ]).then(([itemsRes, suppliersRes, categoriesRes, locationsRes, unitsRes]) => {
      setItems(itemsRes.data?.data?.items ?? [])
      setSuppliers(suppliersRes.data?.data?.items ?? [])
      setCategories(categoriesRes.data?.data?.items ?? [])
      setLocations(locationsRes.data?.data?.items ?? [])
      setUnitsOfMeasure(unitsRes.data?.data?.items ?? [])
    }).catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/grn', {
        params: {
          page, pageSize: PAGE_SIZE,
          inspectionStatus: statusFilter || undefined,
          itemId: itemFilter || undefined,
          fromDate: fromDate || undefined,
          toDate: toDate || undefined,
        },
      })
      setGrns(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load GRNs.')
    } finally {
      setLoading(false)
    }
  }, [page, statusFilter, itemFilter, fromDate, toDate])

  useEffect(() => { load() }, [load])

  // "Add Stock Unit (via GRN)" entry point from ItemDetailPage/GrnDetailPage —
  // wait for reference lists so the prefilled form has real options.
  useEffect(() => {
    if (prefillHandled.current) return
    const prefillItemId = location.state?.prefillItemId
    if (!prefillItemId || items.length === 0 || suppliers.length === 0 || locations.length === 0) return
    prefillHandled.current = true
    setForm({
      ...BLANK,
      itemId: prefillItemId,
      supplierId: suppliers[0]?.id ?? '',
      locationId: location.state?.prefillLocationId ?? locations[0]?.id ?? '',
    })
    setFormErr('')
    setModal(true)
    navigate(location.pathname, { replace: true, state: null })
  }, [location.state, items, suppliers, locations, navigate, location.pathname])

  function openCreate() {
    setForm({ ...BLANK, itemId: items[0]?.id ?? '', supplierId: suppliers[0]?.id ?? '', locationId: locations[0]?.id ?? '' })
    setFormErr('')
    setModal(true)
  }

  function updateFilter(setter) {
    return (value) => { setter(value); setPage(1) }
  }

  async function handleSave() {
    setFormErr('')
    if (!form.itemId || !form.supplierId || !form.locationId || !form.qtyReceived || !form.landedCost) {
      setFormErr('Item, supplier, location, quantity and landed cost are required.')
      return
    }
    if (Number(form.qtyReceived) <= 0) {
      setFormErr('Qty Received must be greater than 0.')
      return
    }
    if (Number(form.landedCost) <= 0) {
      setFormErr('Landed Cost must be greater than 0.')
      return
    }
    setSaving(true)
    try {
      await api.post('/api/v1/grn', {
        itemId: form.itemId,
        supplierId: form.supplierId,
        locationId: form.locationId,
        qtyReceived: Number(form.qtyReceived),
        landedCost: Number(form.landedCost),
        notes: form.notes.trim() || null,
        // P5 — optional procurement LPO link; a passed GRN then closes the PO line.
        poId: form.poId.trim() || null,
        poNumber: form.poNumber.trim() || null,
        acceptedQty: form.poId.trim() ? Number(form.qtyReceived) : null,
        partialDelivery: form.partialDelivery,
      })
      setModal(false)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to create GRN.')
    } finally {
      setSaving(false)
    }
  }

  async function fetchExportRows() {
    const res = await api.get('/api/v1/grn', {
      params: {
        page: 1, pageSize: EXPORT_CAP,
        inspectionStatus: statusFilter || undefined,
        itemId: itemFilter || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
      },
    })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Goods Received Notes',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: GRN_COLUMNS, rows, filename: 'goods-received-notes', theme: 'navy',
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
      exportToExcel({ title: 'Goods Received Notes', columns: GRN_COLUMNS, rows, filename: 'goods-received-notes' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Goods Received Notes" dismissKey="stores.pageInfo.grn.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            GRNs record stock arriving from a supplier. Every GRN must be inspected before use —
            Pass creates trackable stock units, Fail rejects the batch with no stock added.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Goods Received Notes"
          sub={loading ? 'Loading…' : `${totalCount} GRN${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ New GRN</Btn>
            </div>
          }
        />

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div style={{ minWidth: 180 }}>
            <Select label="Status" value={statusFilter} onChange={updateFilter(setStatusFilter)} help={STATUS_HELP} helpKey="stores.grnStatusHelp.dismissed"
              options={[{ value: '', label: 'All statuses' }, { value: 'Pending', label: 'Pending' }, { value: 'Passed', label: 'Passed' }, { value: 'Failed', label: 'Failed' }]} />
          </div>
          <div style={{ minWidth: 200 }}>
            <Select label="Item" value={itemFilter} onChange={updateFilter(setItemFilter)}
              options={[{ value: '', label: 'All items' }, ...items.map(i => ({ value: i.id, label: `${i.itemCode} — ${i.description}` }))]} />
          </div>
          <div style={{ minWidth: 150 }}>
            <Input label="From" type="date" value={fromDate} onChange={updateFilter(setFromDate)} />
          </div>
          <div style={{ minWidth: 150 }}>
            <Input label="To" type="date" value={toDate} onChange={updateFilter(setToDate)} />
          </div>
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Item', 'Supplier', 'Qty · Cost', 'Variance', 'Status', '']}
              empty={statusFilter || itemFilter || fromDate || toDate ? 'No GRNs match these filters.' : 'No GRNs yet — create one when goods arrive from a supplier.'}
              rows={grns.map(grn => [
                <a onClick={() => navigate(`/modules/stores/grn/${grn.id}`)} style={{ cursor: 'pointer', color: T.navy, fontWeight: 700, fontSize: 12 }}>{grn.itemCode} — {grn.itemName}</a>,
                grn.supplierName,
                <span style={{ fontSize: 12 }}>{grn.qtyReceived} units · {fmt.kes(grn.landedCost)}</span>,
                grn.alertLevel && grn.alertLevel !== 'None' ? <Badge variant="amber">+{grn.variancePct?.toFixed(1)}%</Badge> : '—',
                <Badge variant={STATUS_BADGE[grn.inspectionStatus] ?? 'blue'}>{grn.inspectionStatus}</Badge>,
                <Btn variant="ghost" size="sm" onClick={() => navigate(`/modules/stores/items/${grn.itemId}`)}>View Item</Btn>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title="New Goods Received Note" onClose={() => setModal(false)}>
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
          <SelectWithAdd label="Supplier" value={form.supplierId} onChange={v => setForm(f => ({ ...f, supplierId: v }))} required
            options={[{ value: '', label: 'Select supplier…' }, ...suppliers.map(s => ({ value: s.id, label: s.name }))]}
            addTitle="Supplier"
            initialQuickAddForm={{ name: '', kraPin: '', rating: '', contactPerson: '', phone: '', email: '', address: '' }}
            renderQuickAddForm={({ form: qForm, setForm: setQForm }) => (
              <>
                <Input label="Name" required value={qForm.name} onChange={v => setQForm(f => ({ ...f, name: v }))} />
                <div style={{ display: 'flex', gap: 12 }}>
                  <div style={{ flex: 1 }}><Input label="KRA PIN" value={qForm.kraPin} onChange={v => setQForm(f => ({ ...f, kraPin: v }))} /></div>
                  <div style={{ flex: 1 }}><Input label="Rating (0-5)" type="number" value={qForm.rating} onChange={v => setQForm(f => ({ ...f, rating: v }))} /></div>
                </div>
                <div style={{ display: 'flex', gap: 12 }}>
                  <div style={{ flex: 1 }}><Input label="Contact Person" value={qForm.contactPerson} onChange={v => setQForm(f => ({ ...f, contactPerson: v }))} /></div>
                  <div style={{ flex: 1 }}><Input label="Phone" value={qForm.phone} onChange={v => setQForm(f => ({ ...f, phone: v }))} /></div>
                </div>
                <Input label="Email" type="email" value={qForm.email} onChange={v => setQForm(f => ({ ...f, email: v }))} />
                <Input label="Address" value={qForm.address} onChange={v => setQForm(f => ({ ...f, address: v }))} />
              </>
            )}
            onQuickAddSubmit={async (qForm) => {
              if (!qForm.name.trim()) throw new Error('Name is required.')
              const res = await api.post('/api/v1/suppliers', {
                name: qForm.name.trim(),
                kraPin: qForm.kraPin.trim() || null,
                rating: qForm.rating === '' ? null : Number(qForm.rating),
                contactPerson: qForm.contactPerson.trim() || null,
                phone: qForm.phone.trim() || null,
                email: qForm.email.trim() || null,
                address: qForm.address.trim() || null,
              })
              return res.data?.data
            }}
            onQuickAddCreated={(supplier) => setSuppliers(s => [...s, supplier])}
            getOptionFromRecord={(supplier) => ({ value: supplier.id, label: supplier.name })}
          />
          <SelectWithAdd label="Receiving Location" value={form.locationId} onChange={v => setForm(f => ({ ...f, locationId: v }))} required
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
            <div style={{ flex: 1 }}><Input label="Qty Received" type="number" value={form.qtyReceived} onChange={v => setForm(f => ({ ...f, qtyReceived: v }))} required /></div>
            <div style={{ flex: 1 }}>
              <Input label="Landed Cost (total)" type="number" value={form.landedCost} onChange={v => setForm(f => ({ ...f, landedCost: v }))} required
                note="Goods + freight + customs + transport, for the whole line." />
            </div>
          </div>
          <Input label="Notes" value={form.notes} onChange={v => setForm(f => ({ ...f, notes: v }))} />
          <div style={{ display: 'flex', gap: 10 }}>
            <div style={{ flex: 1 }}><Input label="LPO Number (optional)" value={form.poNumber} onChange={v => setForm(f => ({ ...f, poNumber: v }))} /></div>
            <div style={{ flex: 1 }}><Input label="LPO Id (procurement)" value={form.poId} onChange={v => setForm(f => ({ ...f, poId: v }))} note="Links the receipt to a procurement LPO — a passed GRN closes the PO line." /></div>
          </div>
          {form.poId.trim() && (
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, margin: '4px 0 8px' }}>
              <input type="checkbox" checked={form.partialDelivery} onChange={e => setForm(f => ({ ...f, partialDelivery: e.target.checked }))} />
              Partial delivery (LPO stays open for the balance)
            </label>
          )}
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : 'Create GRN'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
