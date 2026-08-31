import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { RELATIONSHIP_TYPES, EMPTY_RELATED_PARTY, today } from '../constants.js'

const PAGE_SIZE = 20

export default function RelatedPartyTab({ setMsg, refreshDashboard }) {
  const [relatedParty, setRelatedParty] = useState([])
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [rpForm, setRpForm] = useState(EMPTY_RELATED_PARTY)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [editForm, setEditForm] = useState(EMPTY_RELATED_PARTY)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/compliance-related-party', { params: { page, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } }))
      const data = res.data?.data || {}
      setRelatedParty(data.items ?? [])
      setTotalCount(data.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [page])

  useEffect(() => { load() }, [load])

  async function submitRelatedParty() {
    if (!rpForm.partyName || !rpForm.amount) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-related-party', {
        partyName: rpForm.partyName, relationshipType: RELATIONSHIP_TYPES.indexOf(rpForm.relationshipType),
        transactionDate: new Date(rpForm.transactionDate || Date.now()).toISOString(),
        amount: Number(rpForm.amount) || 0, description: rpForm.description || null,
      })
      setMsg({ type: 'success', text: 'Related party transaction flagged.' })
      setRpForm(EMPTY_RELATED_PARTY); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to log transaction.' })
    } finally { setSaving(false) }
  }

  async function markReported(id) {
    try {
      await api.patch(`/api/v1/compliance-related-party/${id}/report`)
      load(); refreshDashboard()
    } catch {
      setMsg({ type: 'error', text: 'Failed to mark as reported.' })
    }
  }

  function openView(r, startEditing = false) {
    setViewing(r)
    setEditing(startEditing)
    setEditForm({
      partyName: r.partyName ?? '',
      relationshipType: RELATIONSHIP_TYPES[r.relationshipType] ?? r.relationshipType,
      transactionDate: r.transactionDate ? r.transactionDate.slice(0, 10) : today(),
      amount: r.amount ?? '',
      description: r.description ?? '',
    })
  }

  async function submitEdit() {
    if (!viewing || !editForm.partyName || !editForm.amount) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-related-party/${viewing.id}`, {
        partyName: editForm.partyName, relationshipType: RELATIONSHIP_TYPES.indexOf(editForm.relationshipType),
        transactionDate: new Date(editForm.transactionDate || Date.now()).toISOString(),
        amount: Number(editForm.amount) || 0, description: editForm.description || null,
      })
      setMsg({ type: 'success', text: 'Related party transaction updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update transaction.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About the Related Party Transaction Log">
        Any transaction involving a shareholder, director, or affiliate is logged here and
        automatically flagged for disclosure — flagging isn't editable and reflects the
        relationship itself, not the amount. Click any row to see the full record and edit its
        details. "Mark Reported" records when the transaction has been included in the statutory
        filing.
      </HelpPanel>

      <SectionHeader title="Related Party Transaction Log" sub="Transactions with shareholders, directors, or affiliates — flagged and reported" action={<Btn onClick={() => setModal('rp')}>+ Log Transaction</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Party', 'Relationship', 'Date', 'Amount', 'Reported', 'Actions']}
          empty="No related party transactions logged yet."
          rows={relatedParty.map(r => [
            <span onClick={() => openView(r)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{r.partyName}</span>,
            <Badge variant="navy">{RELATIONSHIP_TYPES[r.relationshipType] ?? r.relationshipType}</Badge>,
            fmt.date(r.transactionDate),
            fmt.kes(r.amount),
            r.reported ? <Badge variant="green">Reported</Badge> : <Badge variant="amber">Pending</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(r)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(r, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {!r.reported && <Btn size="sm" onClick={() => markReported(r.id)}>Mark Reported</Btn>}
            </div>,
          ])}
        />
      </Card>
      <Pagination page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPageChange={setPage} />

      {modal === 'rp' && (
        <Modal title="Log Related Party Transaction" onClose={() => setModal(null)}>
          <Input label="Party Name" value={rpForm.partyName} onChange={v => setRpForm({ ...rpForm, partyName: v })} required />
          <Select label="Relationship" value={rpForm.relationshipType} onChange={v => setRpForm({ ...rpForm, relationshipType: v })} options={RELATIONSHIP_TYPES.map(r => ({ value: r, label: r }))} />
          <Input label="Transaction Date" type="date" value={rpForm.transactionDate} onChange={v => setRpForm({ ...rpForm, transactionDate: v })} />
          <Input label="Amount (Kshs)" type="number" value={rpForm.amount} onChange={v => setRpForm({ ...rpForm, amount: v })} required />
          <Input label="Description" value={rpForm.description} onChange={v => setRpForm({ ...rpForm, description: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitRelatedParty} disabled={saving || !rpForm.partyName || !rpForm.amount}>{saving ? 'Saving…' : 'Log Transaction'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Related Party modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Related Party Transaction' : 'Related Party Transaction Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.partyName}</span>
                  <Badge variant="navy">{RELATIONSHIP_TYPES[viewing.relationshipType] ?? viewing.relationshipType}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{fmt.date(viewing.transactionDate)}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Amount" value={fmt.kes(viewing.amount)} />
                <MiniStat label="Flagged" value={viewing.flagged ? <Badge variant="red">Flagged</Badge> : <Badge variant="green">OK</Badge>} />
                <MiniStat label="Reported" value={viewing.reported ? <Badge variant="green">Reported</Badge> : <Badge variant="amber">Pending</Badge>} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Description</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.description || 'No description.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Flagged is server-computed from the relationship; Reported changes via Mark Reported, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Party Name" value={editForm.partyName} onChange={v => setEditForm({ ...editForm, partyName: v })} required />
              <Select label="Relationship" value={editForm.relationshipType} onChange={v => setEditForm({ ...editForm, relationshipType: v })} options={RELATIONSHIP_TYPES.map(r => ({ value: r, label: r }))} />
              <Input label="Transaction Date" type="date" value={editForm.transactionDate} onChange={v => setEditForm({ ...editForm, transactionDate: v })} />
              <Input label="Amount (Kshs)" type="number" value={editForm.amount} onChange={v => setEditForm({ ...editForm, amount: v })} required />
              <Input label="Description" value={editForm.description} onChange={v => setEditForm({ ...editForm, description: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitEdit} disabled={saving || !editForm.partyName || !editForm.amount}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
