import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import SelectWithAdd from '../../components/SelectWithAdd.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'
import { Download } from 'lucide-react'
import { exportToPdf, exportToExcel, SOLD_ITEM_COLUMNS } from '../../utils/export.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const EXPORT_CAP = 100
const BLANK = { itemId: '', stockUnitId: '', clientId: '', qty: '1', salePrice: '', invoiceNo: '' }

export default function SoldItemsPage() {
  const navigate = useNavigate()
  const [soldItems, setSoldItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [items, setItems] = useState([])
  const [suppliers, setSuppliers] = useState([])
  const [categories, setCategories] = useState([])
  const [unitsOfMeasure, setUnitsOfMeasure] = useState([])
  const [availableUnits, setAvailableUnits] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const [modal, setModal] = useState(false)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/items', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/suppliers', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/categories', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/units-of-measure', { params: { pageSize: 200, isActive: true } }),
    ]).then(([itemsRes, suppliersRes, categoriesRes, unitsRes]) => {
      setItems(itemsRes.data?.data?.items ?? [])
      setSuppliers(suppliersRes.data?.data?.items ?? [])
      setCategories(categoriesRes.data?.data?.items ?? [])
      setUnitsOfMeasure(unitsRes.data?.data?.items ?? [])
    }).catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/sold-items', { params: { page, pageSize: PAGE_SIZE } })
      setSoldItems(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load sold items.')
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function fetchExportRows() {
    const res = await api.get('/api/v1/sold-items', { params: { page: 1, pageSize: EXPORT_CAP } })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Sold Items',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: SOLD_ITEM_COLUMNS, rows, filename: 'sold-items', theme: 'navy',
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
      exportToExcel({ title: 'Sold Items', columns: SOLD_ITEM_COLUMNS, rows, filename: 'sold-items' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  useEffect(() => {
    if (!form.itemId) { setAvailableUnits([]); return }
    api.get('/api/v1/stock-units', { params: { itemId: form.itemId, status: 'InStock', pageSize: 100 } })
      .then(res => setAvailableUnits(res.data?.data?.items ?? []))
      .catch(() => setAvailableUnits([]))
  }, [form.itemId])

  function openCreate() {
    setForm(BLANK)
    setFormErr('')
    setModal(true)
  }

  const selectedUnit = availableUnits.find(u => u.id === form.stockUnitId)

  async function handleSave() {
    setFormErr('')
    if (!form.itemId || !form.stockUnitId || !form.clientId.trim() || !form.qty || !form.salePrice || !form.invoiceNo.trim()) {
      setFormErr('All fields are required.')
      return
    }
    if (Number(form.qty) <= 0) {
      setFormErr('Qty must be greater than 0.')
      return
    }
    if (Number(form.salePrice) <= 0) {
      setFormErr('Sale Price must be greater than 0.')
      return
    }
    if (selectedUnit && Number(form.qty) > selectedUnit.qty) {
      setFormErr(`Only ${selectedUnit.qty} available in this lot.`)
      return
    }
    setSaving(true)
    try {
      await api.post('/api/v1/sold-items', {
        itemId: form.itemId,
        stockUnitId: form.stockUnitId,
        clientId: form.clientId.trim(),
        qty: Number(form.qty),
        salePrice: Number(form.salePrice),
        invoiceNo: form.invoiceNo.trim(),
      })
      setModal(false)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to record sale.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Sold Items" dismissKey="stores.pageInfo.soldItems.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            A record of stock sold to a client, matched against the specific stock unit/lot it came
            from so margin is calculated against its real cost.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Sold Items"
          sub={loading ? 'Loading…' : `${totalCount} sale${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ Record Sale</Btn>
            </div>
          }
        />

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Item', 'Invoice', 'Qty', 'Sale Price', 'Cost', 'Margin', 'Date']}
              empty="No sales recorded yet — record a sale when a stock unit is sold to a client."
              rows={soldItems.map(s => [
                <strong style={{ fontSize: 12 }}>{s.itemCode}</strong>,
                s.invoiceNo,
                s.qty,
                fmt.kes(s.salePrice),
                fmt.kes(s.costAtSale),
                <strong style={{ color: s.grossMargin >= 0 ? T.green : T.red }}>{fmt.kes(s.grossMargin)}</strong>,
                fmt.date(s.soldOn),
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title="Record Sale" onClose={() => setModal(false)}>
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

          <Select label="Stock Unit" value={form.stockUnitId} onChange={v => setForm(f => ({ ...f, stockUnitId: v }))} required
            options={[{ value: '', label: 'Select stock unit…' }, ...availableUnits.map(u => ({ value: u.id, label: `${u.serialNo || 'Bulk lot'} — ${u.qty} available` }))]} />
          {form.itemId && availableUnits.length === 0 && (
            <p style={{ fontSize: 11, color: T.red, marginTop: -8, marginBottom: 14 }}>
              No in-stock units available for this item —{' '}
              <a onClick={() => navigate('/modules/stores/grn', { state: { prefillItemId: form.itemId } })}
                style={{ color: T.navy, fontWeight: 600, cursor: 'pointer', textDecoration: 'underline' }}>
                receive stock via a GRN
              </a> first.
            </p>
          )}

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <Input label="Qty" type="number" value={form.qty} onChange={v => setForm(f => ({ ...f, qty: v }))} required
                note={selectedUnit ? `${selectedUnit.qty} available in this lot` : undefined} />
            </div>
            <div style={{ flex: 1 }}><Input label="Client ID" value={form.clientId} onChange={v => setForm(f => ({ ...f, clientId: v }))} required /></div>
          </div>

          <Input label="Sale Price (total for this quantity)" type="number" value={form.salePrice} onChange={v => setForm(f => ({ ...f, salePrice: v }))} required />

          <Input label="Invoice No" value={form.invoiceNo} onChange={v => setForm(f => ({ ...f, invoiceNo: v }))} required />

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(false)}>Cancel</Btn>
            <Btn disabled={saving || (form.itemId && availableUnits.length === 0)} onClick={handleSave}>{saving ? 'Saving…' : 'Record Sale'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
