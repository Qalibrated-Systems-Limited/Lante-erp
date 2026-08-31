import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import SelectWithAdd from '../../components/SelectWithAdd.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'
import ImagePicker from '../../components/ImagePicker.jsx'
import { exportToPdf, exportToExcel, ITEM_COLUMNS } from '../../utils/export.js'
import { Download } from 'lucide-react'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const EXPORT_CAP = 100
const MIN_STOCK_HELP = 'When Qty on Hand drops to or below this, the item is flagged as low stock on the dashboard.'
const MAX_STOCK_HELP = 'The upper stocking target for this item — a guide for how much to order, not a hard limit.'
const REORDER_QTY_HELP = 'How much to order when this item hits its Min Stock Level.'
const BLANK = {
  supplierId: '', itemCode: '', description: '', categoryId: '', uomId: '', barcode: '',
  minSellingPrice: '', minStockLevel: '', maxStockLevel: '', reorderQty: '',
}

async function uploadImages(files, itemId) {
  for (const file of files) {
    const fd = new FormData()
    fd.append('image', file)
    await api.post(`/api/v1/item-photos/item/${itemId}`, fd).catch(() => {})
  }
}


export default function ItemMasterPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [suppliers, setSuppliers] = useState([])
  const [categories, setCategories] = useState([])
  const [unitsOfMeasure, setUnitsOfMeasure] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const [modal, setModal] = useState(null)
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)
  const [deleting, setDeleting] = useState(false)

  const [pendingImages, setPendingImages] = useState([])
  const [existingImages, setExistingImages] = useState([])
  const [imagesLoading, setImagesLoading] = useState(false)

  useEffect(() => {
    Promise.all([
      api.get('/api/v1/suppliers', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/categories', { params: { pageSize: 200, isActive: true } }),
      api.get('/api/v1/units-of-measure', { params: { pageSize: 200, isActive: true } }),
    ]).then(([suppliersRes, categoriesRes, unitsRes]) => {
      setSuppliers(suppliersRes.data?.data?.items ?? [])
      setCategories(categoriesRes.data?.data?.items ?? [])
      setUnitsOfMeasure(unitsRes.data?.data?.items ?? [])
    }).catch(() => {})
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/items', { params: { page, pageSize: PAGE_SIZE, search: search || undefined } })
      setItems(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load items.')
    } finally {
      setLoading(false)
    }
  }, [page, search])

  useEffect(() => { load() }, [load])

  function updateSearch(value) {
    setSearch(value)
    setPage(1)
  }

  async function fetchExportRows() {
    const res = await api.get('/api/v1/items', { params: { page: 1, pageSize: EXPORT_CAP, search: search || undefined } })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Item Master',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: ITEM_COLUMNS, rows, filename: 'item-master', theme: 'navy',
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
      exportToExcel({ title: 'Item Master', columns: ITEM_COLUMNS, rows, filename: 'item-master' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  function openCreate() {
    setForm({ ...BLANK, supplierId: suppliers[0]?.id ?? '', categoryId: categories[0]?.id ?? '' })
    setFormErr('')
    setPendingImages([])
    setExistingImages([])
    setModal('create')
  }

  async function openEdit(item) {
    setForm({
      supplierId: item.supplierId, itemCode: item.itemCode, description: item.description,
      categoryId: item.categoryId, uomId: item.uomId, barcode: item.barcode ?? '', minSellingPrice: item.minSellingPrice,
      minStockLevel: item.minStockLevel, maxStockLevel: item.maxStockLevel, reorderQty: item.reorderQty,
    })
    setFormErr('')
    setPendingImages([])
    setModal(item)
    setImagesLoading(true)
    try {
      const res = await api.get(`/api/v1/item-photos/item/${item.id}`)
      setExistingImages(res.data?.data ?? [])
    } catch {
      setExistingImages([])
    } finally {
      setImagesLoading(false)
    }
  }

  function generateItemCode() {
    const category = categories.find(c => c.id === form.categoryId)
    const prefix = (category?.code || 'ITM').toUpperCase()
    const suffix = String(Math.floor(1000 + Math.random() * 9000))
    setForm(f => ({ ...f, itemCode: `${prefix}-${suffix}` }))
  }

  function generateBarcode() {
    // Code128-safe: uppercase alnum + dashes, derived from item code plus a random suffix
    // so two items sharing a code prefix (e.g. before Item Code is finalized) don't collide.
    const base = (form.itemCode || 'ITEM').toUpperCase().replace(/[^A-Z0-9-]/g, '')
    const suffix = String(Math.floor(100000 + Math.random() * 900000))
    setForm(f => ({ ...f, barcode: `${base}-${suffix}` }))
  }

  async function handleSave() {
    setFormErr('')
    if (!form.supplierId || !form.itemCode.trim() || !form.categoryId || !form.uomId) {
      setFormErr('Supplier, item code, category and Unit of Measure are required.')
      return
    }
    if (form.minStockLevel !== '' && Number(form.minStockLevel) < 0) {
      setFormErr('Min Stock Level cannot be negative.')
      return
    }
    if (form.maxStockLevel !== '' && Number(form.maxStockLevel) < 0) {
      setFormErr('Max Stock Level cannot be negative.')
      return
    }
    if (form.reorderQty !== '' && Number(form.reorderQty) < 0) {
      setFormErr('Reorder Qty cannot be negative.')
      return
    }
    if (form.minSellingPrice !== '' && Number(form.minSellingPrice) < 0) {
      setFormErr('Min Selling Price cannot be negative.')
      return
    }
    setSaving(true)
    try {
      const payload = {
        supplierId: form.supplierId,
        itemCode: form.itemCode.trim(),
        description: form.description.trim(),
        categoryId: form.categoryId,
        uomId: form.uomId,
        barcode: form.barcode.trim() || null,
        minSellingPrice: Number(form.minSellingPrice) || 0,
        minStockLevel: Number(form.minStockLevel) || 0,
        maxStockLevel: Number(form.maxStockLevel) || 0,
        reorderQty: Number(form.reorderQty) || 0,
      }
      let itemId = modal !== 'create' ? modal.id : null
      if (modal === 'create') {
        const res = await api.post('/api/v1/items', payload)
        itemId = res.data?.data?.id
      } else {
        await api.put(`/api/v1/items/${modal.id}`, payload)
      }
      if (pendingImages.length && itemId) {
        await uploadImages(pendingImages, itemId)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save item.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteImage(imageId) {
    try {
      await api.delete(`/api/v1/item-photos/${imageId}`)
      setExistingImages(imgs => imgs.filter(i => i.id !== imageId))
    } catch {}
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await api.delete(`/api/v1/items/${deleteTarget.id}`)
      setDeleteTarget(null)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete item.')
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About the Item Master" dismissKey="stores.pageInfo.items.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            The Item Master is your product catalogue — pricing, stock levels and reorder thresholds.
            Qty on Hand and Avg Cost update automatically from GRNs and issues; edit here only for
            catalogue details like description or reorder rules.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Item Master"
          sub={loading ? 'Loading…' : `${totalCount} item${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ New Item</Btn>
            </div>
          }
        />

        <div style={{ marginBottom: 16, maxWidth: 360 }}>
          <Input placeholder="Search items…" value={search} onChange={updateSearch} />
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Code', 'Description', 'Category', 'Qty on Hand', 'Avg Cost', 'Action']}
              empty={search ? 'No results found.' : 'No items yet — create one to get started.'}
              rows={items.map(item => [
                <a onClick={() => navigate(`/modules/stores/items/${item.id}`)} style={{ fontWeight: 700, fontSize: 12, color: T.navy, cursor: 'pointer' }}>{item.itemCode}</a>,
                <span style={{ fontSize: 12 }}>{item.description}</span>,
                <Badge variant="navy">{item.categoryName}</Badge>,
                <span>
                  <strong style={{ color: item.isLowStock ? T.red : T.dgrey }}>{item.qtyOnHand} {item.uom}</strong>
                  {item.isLowStock && <span style={{ marginLeft: 8 }}><Badge variant="red">Low</Badge></span>}
                </span>,
                item.avgWeightedCost?.toFixed(2),
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn variant="ghost" size="sm" onClick={() => navigate(`/modules/stores/items/${item.id}`)}>View</Btn>
                  <Btn variant="ghost" size="sm" onClick={() => openEdit(item)}>Edit</Btn>
                  <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(item)}>Delete</Btn>
                </div>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title={modal === 'create' ? 'New Item' : `Edit — ${modal.itemCode}`} onClose={() => setModal(null)} width={600}>
          {formErr && <Alert type="error">{formErr}</Alert>}
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

          <SelectWithAdd label="Category" value={form.categoryId} onChange={v => setForm(f => ({ ...f, categoryId: v }))} required
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

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
                Item Code<span style={{ color: T.red }}> *</span>
              </label>
              <div style={{ display: 'flex', gap: 6 }}>
                <input value={form.itemCode} onChange={e => setForm(f => ({ ...f, itemCode: e.target.value }))}
                  placeholder="Type or generate…" required
                  style={{ flex: 1, minWidth: 0, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box' }} />
                <Btn type="button" variant="ghost" size="sm" onClick={generateItemCode}>Generate</Btn>
              </div>
              <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3 }}>Type your own, or generate one from the category code.</p>
            </div>
          </div>

          <Input label="Description" value={form.description} onChange={v => setForm(f => ({ ...f, description: v }))} />

          <div>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>Barcode</label>
            <div style={{ display: 'flex', gap: 6 }}>
              <input value={form.barcode} onChange={e => setForm(f => ({ ...f, barcode: e.target.value }))}
                placeholder="Scan with a barcode reader, or generate…"
                style={{ flex: 1, minWidth: 0, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box' }} />
              <Btn type="button" variant="ghost" size="sm" onClick={generateBarcode}>Generate</Btn>
            </div>
            <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3, marginBottom: 14 }}>
              Click into the field and scan a physical barcode, or generate a system code.
            </p>
          </div>

          <div style={{ marginBottom: 14 }}>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 8 }}>Photos</label>
            {imagesLoading ? (
              <p style={{ fontSize: 11, color: T.mgrey }}>Loading photos…</p>
            ) : existingImages.length > 0 ? (
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 10 }}>
                {existingImages.map(img => (
                  <div key={img.id} style={{ position: 'relative' }}>
                    <a href={img.photoUrl} target="_blank" rel="noreferrer">
                      <img src={img.photoUrl} alt={img.caption ?? 'Item photo'}
                        style={{ width: 72, height: 72, objectFit: 'cover', borderRadius: 8, border: `1px solid ${T.lgrey}`, display: 'block' }}
                        onError={e => { e.target.style.display = 'none' }} />
                    </a>
                    <button type="button" onClick={() => handleDeleteImage(img.id)}
                      style={{ position: 'absolute', top: -6, right: -6, width: 18, height: 18, borderRadius: '50%', background: T.red, color: T.white, border: 'none', fontSize: 11, lineHeight: 1, cursor: 'pointer' }}>×</button>
                  </div>
                ))}
              </div>
            ) : modal !== 'create' ? (
              <p style={{ fontSize: 11, color: T.mgrey, marginBottom: 10 }}>No photos uploaded yet.</p>
            ) : null}
            <ImagePicker files={pendingImages} onChange={setPendingImages} />
            {pendingImages.length > 0 && (
              <p style={{ fontSize: 11, color: T.mgrey, marginTop: 4 }}>
                {pendingImages.length} new photo{pendingImages.length !== 1 ? 's' : ''} will be uploaded on save.
              </p>
            )}
          </div>

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <SelectWithAdd label="Unit of Measure" value={form.uomId} onChange={v => setForm(f => ({ ...f, uomId: v }))} required
                options={[{ value: '', label: 'Select…' }, ...unitsOfMeasure.map(u => ({ value: u.id, label: u.name }))]}
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
            </div>
            <div style={{ flex: 1 }}><Input label="Min Selling Price" type="number" value={form.minSellingPrice} onChange={v => setForm(f => ({ ...f, minSellingPrice: v }))} /></div>
          </div>

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <Input label="Min Stock Level" type="number" value={form.minStockLevel} onChange={v => setForm(f => ({ ...f, minStockLevel: v }))}
                help={MIN_STOCK_HELP} helpKey="stores.minStockHelp.dismissed" />
            </div>
            <div style={{ flex: 1 }}>
              <Input label="Max Stock Level" type="number" value={form.maxStockLevel} onChange={v => setForm(f => ({ ...f, maxStockLevel: v }))}
                help={MAX_STOCK_HELP} helpKey="stores.maxStockHelp.dismissed" />
            </div>
            <div style={{ flex: 1 }}>
              <Input label="Reorder Qty" type="number" value={form.reorderQty} onChange={v => setForm(f => ({ ...f, reorderQty: v }))}
                help={REORDER_QTY_HELP} helpKey="stores.reorderQtyHelp.dismissed" />
            </div>
          </div>

          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : modal === 'create' ? 'Create Item' : 'Save Changes'}</Btn>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Delete Item" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete <strong>"{deleteTarget.itemCode}"</strong>? It will be hidden from the catalogue but its transaction history is preserved.
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Btn>
            <Btn variant="danger" disabled={deleting} onClick={confirmDelete}>{deleting ? 'Deleting…' : 'Yes, Delete'}</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
