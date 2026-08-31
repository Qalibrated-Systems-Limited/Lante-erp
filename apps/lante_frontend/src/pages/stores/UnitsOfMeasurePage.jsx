import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Modal, Input, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const BLANK = { name: '', description: '' }

export default function UnitsOfMeasurePage() {
  const [units, setUnits] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [modal, setModal] = useState(null)   // null | 'create' | { ...unit }
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/units-of-measure', { params: { page, pageSize: PAGE_SIZE, search: search || undefined } })
      setUnits(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load units of measure.')
    } finally {
      setLoading(false)
    }
  }, [page, search])

  useEffect(() => { load() }, [load])

  function updateSearch(value) {
    setSearch(value)
    setPage(1)
  }

  function openCreate() {
    setForm(BLANK)
    setFormErr('')
    setModal('create')
  }

  function openEdit(u) {
    setForm({ name: u.name, description: u.description ?? '' })
    setFormErr('')
    setModal(u)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.name.trim()) {
      setFormErr('Name is required.')
      return
    }
    setSaving(true)
    try {
      const payload = { name: form.name.trim(), description: form.description.trim() || null }
      if (modal === 'create') {
        await api.post('/api/v1/units-of-measure', payload)
      } else {
        await api.put(`/api/v1/units-of-measure/${modal.id}`, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save unit of measure.')
    } finally {
      setSaving(false)
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/units-of-measure/${deleteTarget.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete unit of measure.')
    } finally {
      setDeleteTarget(null)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Units of Measure" dismissKey="stores.pageInfo.unitsOfMeasure.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            The unit each item is stocked/sold in (e.g. kg, pcs, ltr). Managed here so two items
            can't end up with "kg" and "Kg" as different values. Deleting a unit doesn't delete
            the items using it.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Units of Measure"
          sub={loading ? 'Loading…' : `${totalCount} unit${totalCount !== 1 ? 's' : ''}`}
          action={<Btn onClick={openCreate}>+ New Unit</Btn>}
        />

        <div style={{ marginBottom: 16, maxWidth: 360 }}>
          <Input placeholder="Search units…" value={search} onChange={updateSearch} />
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Name', 'Description', 'Items', 'Action']}
              empty={search ? 'No results found.' : 'No units of measure yet — create one to get started.'}
              rows={units.map(u => [
                <strong style={{ fontSize: 12 }}>{u.name}</strong>,
                u.description || '—',
                u.itemCount,
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn variant="ghost" size="sm" onClick={() => openEdit(u)}>Edit</Btn>
                  <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(u)}>Delete</Btn>
                </div>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title={modal === 'create' ? 'New Unit of Measure' : `Edit — ${modal.name}`} onClose={() => setModal(null)} width={420}>
          {formErr && <Alert type="error">{formErr}</Alert>}
          <Input label="Name" value={form.name} onChange={v => setForm(f => ({ ...f, name: v }))} required placeholder="e.g. kg, pcs, ltr" />
          <Input label="Description" value={form.description} onChange={v => setForm(f => ({ ...f, description: v }))} placeholder="e.g. Kilograms" />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : modal === 'create' ? 'Create Unit' : 'Save Changes'}</Btn>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Delete Unit of Measure" onClose={() => setDeleteTarget(null)} width={380}>
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
