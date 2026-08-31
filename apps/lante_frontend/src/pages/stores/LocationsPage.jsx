import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Modal, Input, Select, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const BLANK = { code: '', name: '', type: 'Warehouse' }
const TYPES = ['Warehouse', 'Site', 'Vehicle', 'Vendor']
const TYPE_BADGE = { Warehouse: 'blue', Site: 'amber', Vehicle: 'purple', Vendor: 'default' }

export default function LocationsPage() {
  const [locations, setLocations] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [modal, setModal] = useState(null)   // null | 'create' | { ...location }
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/locations', { params: { page, pageSize: PAGE_SIZE, search: search || undefined } })
      setLocations(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load locations.')
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

  function openEdit(l) {
    setForm({ code: l.code, name: l.name, type: l.type })
    setFormErr('')
    setModal(l)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.code.trim() || !form.name.trim()) {
      setFormErr('Code and name are required.')
      return
    }
    setSaving(true)
    try {
      const payload = { code: form.code.trim(), name: form.name.trim(), type: form.type }
      if (modal === 'create') {
        await api.post('/api/v1/locations', payload)
      } else {
        await api.put(`/api/v1/locations/${modal.id}`, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save location.')
    } finally {
      setSaving(false)
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/locations/${deleteTarget.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete location.')
    } finally {
      setDeleteTarget(null)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Locations" dismissKey="stores.pageInfo.locations.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            Locations are the physical places stock can sit — warehouses, sites, vehicles or
            vendor-held stock. Used everywhere stock is received, issued, transferred, or counted.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Locations"
          sub={loading ? 'Loading…' : `${totalCount} location${totalCount !== 1 ? 's' : ''}`}
          action={<Btn onClick={openCreate}>+ New Location</Btn>}
        />

        <div style={{ marginBottom: 16, maxWidth: 360 }}>
          <Input placeholder="Search locations…" value={search} onChange={updateSearch} />
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Code', 'Name', 'Type', 'Status', 'Action']}
              empty={search ? 'No results found.' : 'No locations yet — create one to get started.'}
              rows={locations.map(l => [
                <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{l.code}</span>,
                <strong style={{ fontSize: 12 }}>{l.name}</strong>,
                <Badge variant={TYPE_BADGE[l.type] ?? 'default'}>{l.type}</Badge>,
                l.isActive ? <Badge variant="green">Active</Badge> : <Badge variant="default">Inactive</Badge>,
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn variant="ghost" size="sm" onClick={() => openEdit(l)}>Edit</Btn>
                  <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(l)}>Delete</Btn>
                </div>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title={modal === 'create' ? 'New Location' : `Edit — ${modal.name}`} onClose={() => setModal(null)} width={420}>
          {formErr && <Alert type="error">{formErr}</Alert>}
          <Input label="Name" value={form.name} onChange={v => setForm(f => ({ ...f, name: v }))} required />
          <div>
            <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 5 }}>
              Code<span style={{ color: T.red }}> *</span>
            </label>
            <div style={{ display: 'flex', gap: 6 }}>
              <input value={form.code} onChange={e => setForm(f => ({ ...f, code: e.target.value }))}
                placeholder="Type or generate…" required
                style={{ flex: 1, minWidth: 0, padding: '9px 12px', border: `1.5px solid ${T.lgrey}`, borderRadius: 7, fontSize: 13, color: T.dgrey, outline: 'none', boxSizing: 'border-box' }} />
              <Btn type="button" variant="ghost" size="sm" onClick={() => setForm(f => ({ ...f, code: generateCodeFromName(f.name) }))}>Generate</Btn>
            </div>
            <p style={{ fontSize: 11, color: T.mgrey, marginTop: 3, marginBottom: 14 }}>Type your own, or generate one from the name.</p>
          </div>
          <Select label="Type" value={form.type} onChange={v => setForm(f => ({ ...f, type: v }))} options={TYPES} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : modal === 'create' ? 'Create Location' : 'Save Changes'}</Btn>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Delete Location" onClose={() => setDeleteTarget(null)} width={380}>
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
