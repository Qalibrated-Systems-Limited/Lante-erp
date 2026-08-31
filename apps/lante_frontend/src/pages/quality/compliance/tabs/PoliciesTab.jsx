import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, FileInput, Loading, DocumentPreview, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { EMPTY_POLICY } from '../constants.js'

const PAGE_SIZE = 20

export default function PoliciesTab({ setMsg }) {
  const [policies, setPolicies] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [policyForm, setPolicyForm] = useState(EMPTY_POLICY)
  const [uploading, setUploading] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_POLICY)
  const [previewUrl, setPreviewUrl] = useState(null)

  async function uploadComplianceFile(folder, file) {
    if (!file) return null
    setUploading(folder)
    try {
      const form = new FormData()
      form.append('folder', folder)
      form.append('file', file)
      const res = await api.post('/api/v1/compliance-uploads', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      return res.data?.data?.url ?? null
    } catch {
      setMsg({ type: 'error', text: 'File upload failed.' })
      return null
    } finally {
      setUploading(null)
    }
  }

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-policies', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setPolicies(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitPolicy() {
    if (!policyForm.title || !policyForm.version) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-policies', policyForm)
      setMsg({ type: 'success', text: 'Policy published.' })
      setPolicyForm(EMPTY_POLICY); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to publish policy.' })
    } finally { setSaving(false) }
  }

  async function acknowledgePolicy(id) {
    try {
      await api.post(`/api/v1/compliance-policies/${id}/acknowledge`)
      setMsg({ type: 'success', text: 'Policy acknowledged.' })
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to acknowledge policy.' })
    }
  }

  function openView(p, startEditing = false) {
    setViewing(p)
    setEditing(startEditing)
    setEditForm({ title: p.title, version: p.version, fileUrl: p.fileUrl ?? '' })
  }

  async function submitEdit() {
    if (!viewing || !editForm.title || !editForm.version) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-policies/${viewing.id}`, {
        title: editForm.title, version: editForm.version, fileUrl: editForm.fileUrl || null,
      })
      setMsg({ type: 'success', text: 'Policy updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update policy.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Policy Acknowledgement Tracker">
        Staff digitally acknowledge every published company policy. This register tracks each
        policy version, its document, and how many staff have signed off. Click any row to see
        the full record, including the uploaded document, and to edit its details.
      </HelpPanel>

      <SectionHeader title="Policy Acknowledgement Tracker" sub="Every staff member digitally signs all company policies" action={<Btn onClick={() => setModal('policy')}>+ Publish Policy</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Title', 'Version', 'Published', 'Acknowledged', 'Actions']}
          empty="No policies published yet."
          rows={policies.map(p => [
            <span onClick={() => openView(p)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{p.title}</span>,
            <Badge variant="blue">{p.version}</Badge>,
            fmt.date(p.publishedAt),
            <Badge variant="navy">{p.acknowledgedCount}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(p)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(p, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => acknowledgePolicy(p.id)}>I Acknowledge</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'policy' && (
        <Modal title="Publish Policy" onClose={() => setModal(null)}>
          <Input label="Title" value={policyForm.title} onChange={v => setPolicyForm({ ...policyForm, title: v })} required />
          <Input label="Version" value={policyForm.version} onChange={v => setPolicyForm({ ...policyForm, version: v })} required placeholder="e.g. v2.1" />
          <FileInput label="Policy Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
            uploading={uploading === 'policies'} fileUrl={policyForm.fileUrl}
            onFileSelected={async file => { const url = await uploadComplianceFile('policies', file); if (url) setPolicyForm(f => ({ ...f, fileUrl: url })) }} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitPolicy} disabled={saving || !!uploading || !policyForm.title || !policyForm.version}>{saving ? 'Publishing…' : 'Publish'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Policy modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Policy' : 'Policy Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.title}</span>
                  <Badge variant="blue">{viewing.version}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Published {fmt.date(viewing.publishedAt)}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Acknowledged Count" value={<Badge variant="navy">{viewing.acknowledgedCount}</Badge>} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Policy Document</div>
                {viewing.fileUrl
                  ? <Btn size="sm" variant="ghost" onClick={() => setPreviewUrl(viewing.fileUrl)}>👁 View document</Btn>
                  : <span style={{ fontSize: 13, color: T.mgrey }}>No document uploaded.</span>}
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Title" value={editForm.title} onChange={v => setEditForm({ ...editForm, title: v })} required />
              <Input label="Version" value={editForm.version} onChange={v => setEditForm({ ...editForm, version: v })} required placeholder="e.g. v2.1" />
              <FileInput label="Policy Document" accept=".pdf,.doc,.docx,.jpg,.jpeg,.png,.webp"
                uploading={uploading === 'policies'} fileUrl={editForm.fileUrl}
                onFileSelected={async file => { const url = await uploadComplianceFile('policies', file); if (url) setEditForm(f => ({ ...f, fileUrl: url })) }} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !!uploading || !editForm.title || !editForm.version}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── Document preview modal ── */}
      {previewUrl && (
        <Modal title="Policy Document" onClose={() => setPreviewUrl(null)} width={800}>
          <DocumentPreview url={previewUrl} />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 16, marginTop: 12 }}>
            <a href={previewUrl} download style={{ fontSize: 12, color: T.blue }}>⬇ Download</a>
            <a href={previewUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12, color: T.blue }}>Open in new tab ↗</a>
          </div>
        </Modal>
      )}
    </>
  )
}
