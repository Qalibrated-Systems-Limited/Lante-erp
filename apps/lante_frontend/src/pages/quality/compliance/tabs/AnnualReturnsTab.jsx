import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { ANNUAL_RETURN_STATUSES, EMPTY_ANNUAL_RETURN, EMPTY_ANNUAL_RETURN_FILE, ragVariant, today } from '../constants.js'

const PAGE_SIZE = 20

export default function AnnualReturnsTab({ setMsg }) {
  const [annualReturns, setAnnualReturns] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [annualReturnForm, setAnnualReturnForm] = useState(EMPTY_ANNUAL_RETURN)
  const [annualReturnFileForm, setAnnualReturnFileForm] = useState(EMPTY_ANNUAL_RETURN_FILE)
  const [activeId, setActiveId] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_ANNUAL_RETURN)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/annual-returns', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setAnnualReturns(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitAnnualReturn() {
    if (!annualReturnForm.year || !annualReturnForm.dueDate) return
    setSaving(true)
    try {
      await api.post('/api/v1/annual-returns', {
        year: Number(annualReturnForm.year),
        dueDate: new Date(annualReturnForm.dueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Annual return record added.' })
      setAnnualReturnForm(EMPTY_ANNUAL_RETURN); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add annual return.' })
    } finally { setSaving(false) }
  }

  function openFileAnnualReturn(r) {
    setActiveId(r.id)
    setAnnualReturnFileForm(EMPTY_ANNUAL_RETURN_FILE)
    setModal('annualReturnFile')
  }

  async function submitFileAnnualReturn() {
    setSaving(true)
    try {
      await api.patch(`/api/v1/annual-returns/${activeId}/file`, {
        filedDate: new Date(annualReturnFileForm.filedDate || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Annual return marked as filed.' })
      setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to mark as filed.' })
    } finally { setSaving(false) }
  }

  function openView(r, startEditing = false) {
    setViewing(r)
    setEditing(startEditing)
    setEditForm({ year: r.year, dueDate: r.dueDate ? r.dueDate.slice(0, 10) : today() })
  }

  async function submitEdit() {
    if (!viewing || !editForm.year || !editForm.dueDate) return
    setSaving(true)
    try {
      await api.put(`/api/v1/annual-returns/${viewing.id}`, {
        year: Number(editForm.year),
        dueDate: new Date(editForm.dueDate).toISOString(),
      })
      setMsg({ type: 'success', text: 'Annual return record updated.' })
      setViewing(null); setEditing(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update annual return record.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Annual Returns Tracker">
        Tracks each year's Registrar of Companies annual return filing deadline and status,
        RAG-coded by how close the due date is. Click any row to see the full record and edit
        its year or due date.
        <br /><br />
        <strong>Mark Filed</strong> records when the return was actually submitted — it doesn't
        change the year or due date, only "Edit" does that.
      </HelpPanel>

      <SectionHeader title="Annual Returns Tracker" sub="Registrar of Companies — 2017-2025 backlog and ongoing; 60-day alert to MD and Company Secretary" action={<Btn onClick={() => setModal('annualReturn')}>+ Add Year</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Year', 'Due Date', 'Status', 'Filed Date', 'RAG', 'Actions']}
          empty="No annual return records yet."
          rows={annualReturns.map(r => [
            <span onClick={() => openView(r)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{r.year}</span>,
            fmt.date(r.dueDate),
            <Badge variant={r.status === 1 ? 'green' : r.status === 2 ? 'red' : 'amber'}>{ANNUAL_RETURN_STATUSES[r.status] ?? r.status}</Badge>,
            r.filedDate ? fmt.date(r.filedDate) : '—',
            <Badge variant={ragVariant(r.rag)}>{r.rag}</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(r)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(r, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {r.status !== 1 && <Btn size="sm" onClick={() => openFileAnnualReturn(r)}>Mark Filed</Btn>}
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'annualReturn' && (
        <Modal title="Add Annual Return Record" onClose={() => setModal(null)}>
          <Input label="Year" type="number" value={annualReturnForm.year} onChange={v => setAnnualReturnForm({ ...annualReturnForm, year: v })} required />
          <Input label="Due Date" type="date" value={annualReturnForm.dueDate} onChange={v => setAnnualReturnForm({ ...annualReturnForm, dueDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitAnnualReturn} disabled={saving || !annualReturnForm.year || !annualReturnForm.dueDate}>{saving ? 'Saving…' : 'Add Record'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'annualReturnFile' && (
        <Modal title="Mark Annual Return as Filed" onClose={() => setModal(null)}>
          <Input label="Filed Date" type="date" value={annualReturnFileForm.filedDate} onChange={v => setAnnualReturnFileForm({ ...annualReturnFileForm, filedDate: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitFileAnnualReturn} disabled={saving}>{saving ? 'Saving…' : 'Mark Filed'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Annual Return modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Annual Return' : 'Annual Return Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.year} Annual Return</span>
                  <Badge variant={viewing.status === 1 ? 'green' : viewing.status === 2 ? 'red' : 'amber'}>{ANNUAL_RETURN_STATUSES[viewing.status] ?? viewing.status}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Registrar of Companies</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Due Date" value={fmt.date(viewing.dueDate)} />
                <MiniStat label="Filed Date" value={viewing.filedDate ? fmt.date(viewing.filedDate) : '—'} />
                <MiniStat label="RAG" value={<Badge variant={ragVariant(viewing.rag)}>{viewing.rag}</Badge>} />
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Status changes via Mark Filed, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Year" type="number" value={editForm.year} onChange={v => setEditForm({ ...editForm, year: v })} required />
              <Input label="Due Date" type="date" value={editForm.dueDate} onChange={v => setEditForm({ ...editForm, dueDate: v })} required />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.year || !editForm.dueDate}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
