import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, Alert, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { WB_STATUSES, EMPTY_WB, EMPTY_WB_UPDATE } from '../constants.js'

const wbStatusVariant = s => s === 'Closed' || s === 'Resolved' ? 'green' : s === 'UnderInvestigation' ? 'amber' : 'blue'

const EMPTY_WB_DETAILS = { anonymous: false, summary: '' }
const PAGE_SIZE = 20

export default function WhistleblowerTab({ setMsg }) {
  const [whistleblowerCases, setWhistleblowerCases] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [wbForm, setWbForm] = useState(EMPTY_WB)
  const [wbUpdateForm, setWbUpdateForm] = useState(EMPTY_WB_UPDATE)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_WB_DETAILS)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-whistleblower-cases', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setWhistleblowerCases(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitWhistleblowerCase() {
    if (!wbForm.summary) return
    setSaving(true)
    try {
      const res = await api.post('/api/v1/compliance-whistleblower-cases', wbForm)
      setMsg({ type: 'success', text: `Case ${res.data?.data?.refNo ?? ''} submitted.` })
      setWbForm(EMPTY_WB); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to submit case.' })
    } finally { setSaving(false) }
  }

  function openWbUpdate(c) {
    setActiveId(c.id)
    setWbUpdateForm({ status: WB_STATUSES[c.status] ?? 'New', outcome: c.outcome ?? '' })
    setModal('wbUpdate')
  }

  async function submitWbUpdate() {
    setSaving(true)
    try {
      await api.patch(`/api/v1/compliance-whistleblower-cases/${activeId}`, {
        status: WB_STATUSES.indexOf(wbUpdateForm.status), outcome: wbUpdateForm.outcome || null,
      })
      setMsg({ type: 'success', text: 'Case updated.' })
      setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update case.' })
    } finally { setSaving(false) }
  }

  function openView(c, startEditing = false) {
    setViewing(c)
    setEditing(startEditing)
    setEditForm({ anonymous: c.anonymous, summary: c.summary })
  }

  // Separate from submitWbUpdate/PATCH above — this saves only the editable case
  // details (anonymous flag + summary) via PUT. Status/outcome stay controlled by
  // the "Update" action and its PATCH call, untouched here.
  async function submitEditDetails() {
    if (!viewing || !editForm.summary) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-whistleblower-cases/${viewing.id}`, {
        anonymous: editForm.anonymous, summary: editForm.summary,
      })
      setMsg({ type: 'success', text: 'Case updated.' })
      setEditing(false)
      setViewing(v => ({ ...v, anonymous: editForm.anonymous, summary: editForm.summary }))
      load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update case.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Whistleblower Case Tracker">
        This is the restricted whistleblower case tracker (COMP-003) — visible only to holders of
        the whistleblower permission. Cases can be submitted anonymously, and each one is assigned
        a unique reference number on submission. Click any case below to see its full details.
        Status and outcome move forward through the case's own "Update" action — the edit option
        in the details view only changes the anonymity flag and summary text.
      </HelpPanel>
      <SectionHeader title="Whistleblower Case Tracker" action={<Btn onClick={() => setModal('wb')}>+ Submit Case</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Ref No', 'Anonymous', 'Summary', 'Status', 'Outcome', 'Actions']}
          empty="No whistleblower cases."
          rows={whistleblowerCases.map(c => [
            <strong style={{ fontFamily: 'monospace', fontSize: 12 }}>{c.refNo}</strong>,
            c.anonymous ? <Badge variant="purple">Anonymous</Badge> : <Badge variant="default">Named</Badge>,
            c.summary,
            <Badge variant={wbStatusVariant(WB_STATUSES[c.status] ?? c.status)}>{WB_STATUSES[c.status] ?? c.status}</Badge>,
            c.outcome ?? '—',
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(c)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(c, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              <Btn size="sm" onClick={() => openWbUpdate(c)}>Update</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'wb' && (
        <Modal title="Submit Whistleblower Case" onClose={() => setModal(null)}>
          <Alert type="warning">Restricted access — only compliance officers and management can view submitted cases.</Alert>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
            <input type="checkbox" checked={wbForm.anonymous} onChange={e => setWbForm({ ...wbForm, anonymous: e.target.checked })} />
            Submit anonymously
          </label>
          <Input label="Summary" value={wbForm.summary} onChange={v => setWbForm({ ...wbForm, summary: v })} required placeholder="Describe the concern" />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitWhistleblowerCase} disabled={saving || !wbForm.summary}>{saving ? 'Submitting…' : 'Submit Case'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'wbUpdate' && (
        <Modal title="Update Case" onClose={() => setModal(null)}>
          <Select label="Status" value={wbUpdateForm.status} onChange={v => setWbUpdateForm({ ...wbUpdateForm, status: v })} options={WB_STATUSES.map(s => ({ value: s, label: s }))} />
          <Input label="Outcome" value={wbUpdateForm.outcome} onChange={v => setWbUpdateForm({ ...wbUpdateForm, outcome: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitWbUpdate} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Case modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Case' : 'Case Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy, fontFamily: 'monospace' }}>{viewing.refNo}</span>
                  <Badge variant={wbStatusVariant(WB_STATUSES[viewing.status] ?? viewing.status)}>{WB_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.anonymous ? 'Anonymous submission' : 'Named submission'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Outcome" value={viewing.outcome ?? '—'} />
                <MiniStat label="Created" value={fmt.date(viewing.createdAt)} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Summary</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.summary}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status and outcome change via Update, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
                <input type="checkbox" checked={editForm.anonymous} onChange={e => setEditForm({ ...editForm, anonymous: e.target.checked })} />
                Anonymous
              </label>
              <Input label="Summary" value={editForm.summary} onChange={v => setEditForm({ ...editForm, summary: v })} required placeholder="Describe the concern" />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEditDetails} disabled={saving || !editForm.summary}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
