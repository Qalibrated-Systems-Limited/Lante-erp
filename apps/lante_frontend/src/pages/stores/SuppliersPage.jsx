import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Modal, Input, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import { exportToPdf, exportToExcel, SUPPLIER_COLUMNS } from '../../utils/export.js'
import { Download } from 'lucide-react'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const EXPORT_CAP = 100
const BLANK = { name: '', kraPin: '', rating: '', contactPerson: '', phone: '', email: '', address: '' }

export default function SuppliersPage() {
  const [suppliers, setSuppliers] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)

  const [modal, setModal] = useState(null)   // null | 'create' | { ...supplier }
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/suppliers', { params: { page, pageSize: PAGE_SIZE, search: search || undefined } })
      setSuppliers(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load suppliers.')
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
    const res = await api.get('/api/v1/suppliers', { params: { page: 1, pageSize: EXPORT_CAP, search: search || undefined } })
    const rows = res.data?.data?.items ?? []
    const total = res.data?.data?.totalCount ?? rows.length
    return { rows, capped: total > rows.length, total }
  }

  async function handleExportPdf() {
    setExporting(true)
    try {
      const { rows, capped, total } = await fetchExportRows()
      await exportToPdf({
        title: 'Suppliers',
        subtitle: capped ? `Showing first ${rows.length} of ${total} matching records` : `${total} matching record${total !== 1 ? 's' : ''}`,
        columns: SUPPLIER_COLUMNS, rows, filename: 'suppliers', theme: 'navy',
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
      exportToExcel({ title: 'Suppliers', columns: SUPPLIER_COLUMNS, rows, filename: 'suppliers' })
    } catch {
      setError('Failed to export Excel.')
    } finally {
      setExporting(false)
    }
  }

  function openCreate() {
    setForm(BLANK)
    setFormErr('')
    setModal('create')
  }

  function openEdit(s) {
    setForm({
      name: s.name, kraPin: s.kraPin ?? '', rating: s.rating ?? '',
      contactPerson: s.contactPerson ?? '', phone: s.phone ?? '', email: s.email ?? '', address: s.address ?? '',
    })
    setFormErr('')
    setModal(s)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.name.trim()) {
      setFormErr('Name is required.')
      return
    }
    setSaving(true)
    try {
      const payload = {
        name: form.name.trim(),
        kraPin: form.kraPin.trim() || null,
        rating: form.rating === '' ? null : Number(form.rating),
        contactPerson: form.contactPerson.trim() || null,
        phone: form.phone.trim() || null,
        email: form.email.trim() || null,
        address: form.address.trim() || null,
      }
      if (modal === 'create') {
        await api.post('/api/v1/suppliers', payload)
      } else {
        await api.put(`/api/v1/suppliers/${modal.id}`, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save supplier.')
    } finally {
      setSaving(false)
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/suppliers/${deleteTarget.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete supplier.')
    } finally {
      setDeleteTarget(null)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Suppliers" dismissKey="stores.pageInfo.suppliers.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            Suppliers are who you buy stock from. Linked to every GRN and to purchase price
            history — KRA PIN and rating are used for compliance and vendor scoring.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Suppliers"
          sub={loading ? 'Loading…' : `${totalCount} supplier${totalCount !== 1 ? 's' : ''}`}
          action={
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportExcel}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> Excel</span></Btn>
              <Btn variant="ghost" size="sm" disabled={exporting} onClick={handleExportPdf}><span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}><Download size={14} /> PDF</span></Btn>
              <Btn onClick={openCreate}>+ New Supplier</Btn>
            </div>
          }
        />

        <div style={{ marginBottom: 16, maxWidth: 360 }}>
          <Input placeholder="Search suppliers…" value={search} onChange={updateSearch} />
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Name', 'KRA PIN', 'Contact', 'Rating', 'Items', 'Action']}
              empty={search ? 'No results found.' : 'No suppliers yet — create one to get started.'}
              rows={suppliers.map(s => [
                <strong style={{ fontSize: 12 }}>{s.name}</strong>,
                s.kraPin || '—',
                s.contactPerson || s.phone || s.email || '—',
                s.rating ?? '—',
                s.itemCount,
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn variant="ghost" size="sm" onClick={() => openEdit(s)}>Edit</Btn>
                  <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(s)}>Delete</Btn>
                </div>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title={modal === 'create' ? 'New Supplier' : `Edit — ${modal.name}`} onClose={() => setModal(null)}>
          {formErr && <Alert type="error">{formErr}</Alert>}
          <Input label="Name" value={form.name} onChange={v => setForm(f => ({ ...f, name: v }))} required />
          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}><Input label="KRA PIN" value={form.kraPin} onChange={v => setForm(f => ({ ...f, kraPin: v }))} /></div>
            <div style={{ flex: 1 }}><Input label="Rating (0-5)" type="number" value={form.rating} onChange={v => setForm(f => ({ ...f, rating: v }))} /></div>
          </div>
          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}><Input label="Contact Person" value={form.contactPerson} onChange={v => setForm(f => ({ ...f, contactPerson: v }))} /></div>
            <div style={{ flex: 1 }}><Input label="Phone" value={form.phone} onChange={v => setForm(f => ({ ...f, phone: v }))} /></div>
          </div>
          <Input label="Email" type="email" value={form.email} onChange={v => setForm(f => ({ ...f, email: v }))} />
          <Input label="Address" value={form.address} onChange={v => setForm(f => ({ ...f, address: v }))} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : modal === 'create' ? 'Create Supplier' : 'Save Changes'}</Btn>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Delete Supplier" onClose={() => setDeleteTarget(null)} width={380}>
          <p style={{ fontSize: 13, color: T.dgrey }}>
            Are you sure you want to delete <strong>"{deleteTarget.name}"</strong>? This action cannot be undone.
          </p>
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Btn>
            <Btn variant="danger" onClick={confirmDelete}>Yes, Delete</Btn>
          </div>
        </Modal>
      )}
    </>
  )
}
