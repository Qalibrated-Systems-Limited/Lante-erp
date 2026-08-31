import { useState, useEffect, useCallback } from 'react'
import StoresNav from './StoresNav.jsx'
import api from '../../api/axios.js'
import { T } from '../../theme/tokens.js'
import { Card, Btn, Alert, SectionHeader, DataTable, Modal, Input, Loading } from '../../components/ui.jsx'
import Pagination from '../../components/Pagination.jsx'
import Collapsible from '../../components/Collapsible.jsx'
import { generateCodeFromName } from '../../utils/codeGen.js'

const PAD = 'clamp(16px, 2.4vw, 26px)'
const PAGE_SIZE = 20
const BLANK = { code: '', name: '' }

export default function CategoriesPage() {
  const [categories, setCategories] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [modal, setModal] = useState(null)   // null | 'create' | { ...category }
  const [form, setForm] = useState(BLANK)
  const [saving, setSaving] = useState(false)
  const [formErr, setFormErr] = useState('')

  const [deleteTarget, setDeleteTarget] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/categories', { params: { page, pageSize: PAGE_SIZE, search: search || undefined } })
      setCategories(res.data?.data?.items ?? [])
      setTotalCount(res.data?.data?.totalCount ?? 0)
    } catch {
      setError('Failed to load categories.')
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

  function openEdit(c) {
    setForm({ code: c.code, name: c.name })
    setFormErr('')
    setModal(c)
  }

  async function handleSave() {
    setFormErr('')
    if (!form.code.trim() || !form.name.trim()) {
      setFormErr('Code and name are required.')
      return
    }
    setSaving(true)
    try {
      const payload = { code: form.code.trim(), name: form.name.trim() }
      if (modal === 'create') {
        await api.post('/api/v1/categories', payload)
      } else {
        await api.put(`/api/v1/categories/${modal.id}`, payload)
      }
      setModal(null)
      load()
    } catch (err) {
      setFormErr(err.response?.data?.message ?? 'Failed to save category.')
    } finally {
      setSaving(false)
    }
  }

  async function confirmDelete() {
    if (!deleteTarget) return
    try {
      await api.delete(`/api/v1/categories/${deleteTarget.id}`)
      load()
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete category.')
    } finally {
      setDeleteTarget(null)
    }
  }

  return (
    <>
      <div style={{ padding: PAD, width: '100%', boxSizing: 'border-box' }}>
        <StoresNav />

        <Collapsible title="About Categories" dismissKey="stores.pageInfo.categories.dismissed">
          <p style={{ fontSize: 13, color: T.dgrey, margin: 0, lineHeight: 1.5 }}>
            Categories group items for reporting and item-code generation. Deleting a category
            doesn't delete the items in it.
          </p>
        </Collapsible>

        {error && <Alert type="error">{error}</Alert>}

        <SectionHeader
          title="Categories"
          sub={loading ? 'Loading…' : `${totalCount} categor${totalCount !== 1 ? 'ies' : 'y'}`}
          action={<Btn onClick={openCreate}>+ New Category</Btn>}
        />

        <div style={{ marginBottom: 16, maxWidth: 360 }}>
          <Input placeholder="Search categories…" value={search} onChange={updateSearch} />
        </div>

        {loading ? <Loading /> : (
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <DataTable
              headers={['Code', 'Name', 'Items', 'Action']}
              empty={search ? 'No results found.' : 'No categories yet — create one to get started.'}
              rows={categories.map(c => [
                <span style={{ fontFamily: 'monospace', fontSize: 11, color: T.mgrey }}>{c.code}</span>,
                <strong style={{ fontSize: 12 }}>{c.name}</strong>,
                c.itemCount,
                <div style={{ display: 'flex', gap: 6 }}>
                  <Btn variant="ghost" size="sm" onClick={() => openEdit(c)}>Edit</Btn>
                  <Btn variant="danger" size="sm" onClick={() => setDeleteTarget(c)}>Delete</Btn>
                </div>,
              ])}
            />
            <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />
          </Card>
        )}
      </div>

      {modal && (
        <Modal title={modal === 'create' ? 'New Category' : `Edit — ${modal.name}`} onClose={() => setModal(null)} width={420}>
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
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn disabled={saving} onClick={handleSave}>{saving ? 'Saving…' : modal === 'create' ? 'Create Category' : 'Save Changes'}</Btn>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Delete Category" onClose={() => setDeleteTarget(null)} width={380}>
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
