import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import SelectWithAdd from '../../components/SelectWithAdd.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'
import { Download } from 'lucide-react'
import { exportToPdf, exportToExcel, STORE_ISSUE_COLUMNS } from '../../utils/export.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const BLANK = { itemId: '', stockUnitId: '', locationId: '', qtyIssued: '', costCenter: '', issueType: 'Internal', issuedTo: '' }
const PAGE_SIZE = 20
const EXPORT_CAP = 100
const STOCK_UNIT_HELP = 'Choosing a specific lot here is optional. Leave it as "Bulk / no specific unit" and pick a location instead — a store issue works either way, regardless of whether the related GRN has been inspected yet.'
const ISSUE_TYPE_HELP = '"Internal" is stock consumed within the business (e.g. maintenance, kitchen use). "Sale" is stock leaving as part of a sale to a customer.'

export default function StoreIssuesPage() {
  const navigate = useNavigate()

  const [issues, setIssues] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [items, setItems] = useState([])
  const [suppliers, setSuppliers] = useState([])
  const [categories, setCategories] = useState([])
  const [unitsOfMeasure, setUnitsOfMeasure] = useState([])
  const [locations, setLocations] = useState([])
  const [availableUnits, setAvailableUnits] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [exporting, setExporting] = useState(false)

  const [page, setPage] = useState(1)
  const [itemFilter, setItemFilter] = useState('')
  const [costCenterFilter, setCostCenterFilter] = useState('')
  const [issueTypeFilter, setIssueTypeFilter] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')

  const [modal, setModal] = useState(false)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/items', { params: { pageSize: 100, isActive: true } }),
      api.get('/api/v1/locations', { params: { pageSize: 100, isActive: true } }),
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

  const filterParams = useCallback(() => ({
    itemId: itemFilter || undefined,
    costCenter: costCenterFilter.trim() || undefined,
    issueType: issueTypeFilter || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  }), [itemFilter, costCenterFilter, issueTypeFilter, fromDate, toDate])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/store-issues', { params: { page, pageSize: PAGE_SIZE, ...filterParams() } })
      setIssues(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load store issue notes.')
    } finally {
      setLoading(false)
    }
  }, [page, filterParams])

  useEffect(() => { load() }, [load])

  useEffect(() => {
    if (!form.itemId) { setAvailableUnits([]); return }
    api.get('/api/v1/stock-units', { params: { itemId: form.itemId, status: 'InStock', pageSize: 100 } })
      .then(res => setAvailableUnits(res.data?.data?.items ?? []))
      .catch(() => setAvailableUnits([]))
  }, [form.itemId])

  function updateFilter(setter) {
    return (value) => { setter(value); setPage(1) }
  }

  function openCreate() {
    setForm({ ...BLANK, itemId: items[0]?.id ?? '', locationId: locations[0]?.id ?? '' })
    setFormErr('')
    setModal(true)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.itemId || !form.qtyIssued || !form.costCenter.trim() || !form.issuedTo.trim()) {
      setFormErr('Item, quantity, cost center and issued-to are required.')
      return
    }
    if (Number(form.qtyIssued) <= 0) {
      setFormErr('Qty Issued must be greater than 0.')
      return
    }
    if (!form.stockUnitId && !form.locationId) {
      setFormErr('A location is required for a bulk issue with no specific stock unit.')
      return
    }
    const selectedUnit = availableUnits.find(u => u.id === form.stockUnitId)
    if (selectedUnit && Number(form.qtyIssued) > selectedUnit.qty) {
      setFormErr(`Only ${selectedUnit.qty} available in this lot.`)
      return
    }
    setSaving(true)
    try {
      await api.post('/api/v1/store-issues', {
        itemId: form.itemId,
        stockUnitId: form.stockUnitId || null,
        locationId: form.stockUnitId ? null : form.locationId,
        qtyIssued: Number(form.qtyIssued),
        costCenter: form.costCenter.trim(),
        issueType: form.issueType,
        issuedTo: form.issuedTo.trim(),
      })
      setModal(false)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to create store issue note.')
    } finally {
      setSaving(false)
    }
  }

  async function fetchExportRows() {
    const res = await api.get('/api/v1/store-issues', { params: { page: 1, pageSize: EXPORT_CAP, ...filterParams() } })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Store Issue Notes',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: STORE_ISSUE_COLUMNS, rows, filename: 'store-issue-notes', theme: 'navy',
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
      exportToExcel({ title: 'Store Issue Notes', columns: STORE_ISSUE_COLUMNS, rows, filename: 'store-issue-notes' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  const hasFilters = itemFilter || costCenterFilter || issueTypeFilter || fromDate || toDate

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Store Issue Notes" dismissKey="stores.pageInfo.storeIssues.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            Store Issue Notes record stock leaving for internal use or a sale. This works
            independently of GRN inspection status — issue a specific lot, or just a location for a bulk issue.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Store Issue Notes"
          sub={loading ? 'Loading…' : `${totalCount} issue${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ New Issue</Btn>
            </div>
          }
        />

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div style={{ minWidth: 200 }}>
            <Select label="Item" value={itemFilter} onChange={updateFilter(setItemFilter)}
              options={[{ value: '', label: 'All items' }, ...items.map(i => ({ value: i.id, label: `${i.itemCode} — ${i.description}` }))]} />
          </div>
          <div style={{ minWidth: 160 }}>
            <Input label="Cost Center" value={costCenterFilter} onChange={updateFilter(setCostCenterFilter)} placeholder="Exact match…" />
          </div>
          <div style={{ minWidth: 150 }}>
            <Select label="Issue Type" value={issueTypeFilter} onChange={updateFilter(setIssueTypeFilter)}
              options={[{ value: '', label: 'All types' }, { value: 'Internal', label: 'Internal' }, { value: 'Sale', label: 'Sale' }]} />
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
              headers={['Item', 'Location', 'Qty', 'Cost Center', 'Type', 'Issued To', 'Date', '']}
              empty={hasFilters ? 'No store issues match these filters.' : 'No store issues yet — record stock leaving the store for internal use or sale.'}
              rows={issues.map(i => [
                <strong style={{ fontSize: 12 }}>{i.itemCode}</strong>,
                i.locationName || '—',
                i.qtyIssued,
                i.costCenter,
                <Badge variant={i.issueType === 'Sale' ? 'green' : 'blue'}>{i.issueType}</Badge>,
                i.issuedTo,
                fmt.date(i.issuedOn),
                <Btn variant="ghost" size="sm" onClick={() => navigate(`/modules/stores/items/${i.itemId}`)}>View Item</Btn>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title="New Store Issue Note" onClose={() => setModal(false)}>
          {formErr && <Alert type="error">{formErr}</Alert>}
          <SelectWithAdd label="Item" value={form.itemId} onChange={v => setForm(f => ({ ...f, itemId: v, stockUnitId: '' }))} required
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

          <Select label="Stock Unit (optional)" value={form.stockUnitId} onChange={v => setForm(f => ({ ...f, stockUnitId: v }))}
            help={STOCK_UNIT_HELP} helpKey="stores.stockUnitOptionalHelp.dismissed"
            options={[{ value: '', label: 'Bulk / no specific unit' }, ...availableUnits.map(u => ({ value: u.id, label: `${u.serialNo || 'Bulk lot'} — ${u.qty} available` }))]} />

          {!form.stockUnitId && (
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
          )}

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}><Input label="Qty Issued" type="number" value={form.qtyIssued} onChange={v => setForm(f => ({ ...f, qtyIssued: v }))} required /></div>
            <div style={{ flex: 1 }}>
              <Select label="Issue Type" value={form.issueType} onChange={v => setForm(f => ({ ...f, issueType: v }))}
                help={ISSUE_TYPE_HELP} helpKey="stores.issueTypeHelp.dismissed"
                options={[{ value: 'Internal', label: 'Internal' }, { value: 'Sale', label: 'Sale' }]} />
            </div>
          </div>

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}><Input label="Cost Center" value={form.costCenter} onChange={v => setForm(f => ({ ...f, costCenter: v }))} required /></div>
            <div style={{ flex: 1 }}><Input label="Issued To" value={form.issuedTo} onChange={v => setForm(f => ({ ...f, issuedTo: v }))} required /></div>
          </div>

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : 'Create Issue'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
