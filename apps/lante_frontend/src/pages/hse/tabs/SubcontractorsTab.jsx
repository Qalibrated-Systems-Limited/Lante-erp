import { useState, useEffect } from 'react'
import api from '../../../api/axios.js'
import { T } from '../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Loading, MiniStat } from '../../../components/ui.jsx'
import { EMPTY_SUBCONTRACTOR, EMPTY_PREQUAL } from '../constants.js'

const empty = { data: { data: { items: [] } } }

export default function SubcontractorsTab({ setMsg, subcontractors, setSubcontractors, refreshDashboard }) {
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [modal, setModal] = useState(null)
  const [subForm, setSubForm] = useState(EMPTY_SUBCONTRACTOR)
  const [viewing, setViewing] = useState(null)
  const [editing, setEditing] = useState(false)
  const [prequalForm, setPrequalForm] = useState(EMPTY_PREQUAL)

  useEffect(() => { load() }, [])

  async function load() {
    setLoading(true)
    try {
      const res = await api.get('/api/v1/subcontractors').catch(() => empty)
      setSubcontractors(res.data?.data?.items ?? [])
    } finally {
      setLoading(false)
    }
  }

  async function submitSubcontractor() {
    if (!subForm.name || !subForm.tradeCategory) return
    setSaving(true)
    try {
      const res = await api.post('/api/v1/subcontractors', {
        name: subForm.name, tradeCategory: subForm.tradeCategory, notes: subForm.notes || null,
      })
      const id = res.data?.data?.id
      if (id) {
        await api.put(`/api/v1/subcontractors/${id}`, {
          name: subForm.name, tradeCategory: subForm.tradeCategory,
          safetyScore: Number(subForm.safetyScore) || 0, ramsSubmitted: subForm.ramsSubmitted,
          notes: subForm.notes || null,
        })
      }
      setMsg({ type: 'success', text: 'Subcontractor added to the ASR.' })
      setSubForm(EMPTY_SUBCONTRACTOR); setModal(null); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to add subcontractor.' })
    } finally { setSaving(false) }
  }

  function openView(s, startEditing = false) {
    setViewing(s)
    setEditing(startEditing)
    setPrequalForm({
      name: s.name ?? '', tradeCategory: s.tradeCategory ?? '',
      safetyScore: s.safetyScore ?? '', ramsSubmitted: !!s.ramsSubmitted,
      prequalified: !!s.prequalified, notes: s.notes ?? '',
    })
  }

  // Prequalified itself now only flips via the Subcontracts service's PQQ approval workflow
  // (SUB-002) — this modal only edits the HSE-relevant fields plus identity (name/trade), so it
  // PUTs the Subcontractor record rather than a dedicated prequalification route.
  async function submitPrequal() {
    if (!viewing || !prequalForm.name || !prequalForm.tradeCategory) return
    setSaving(true)
    try {
      await api.put(`/api/v1/subcontractors/${viewing.id}`, {
        name: prequalForm.name, tradeCategory: prequalForm.tradeCategory,
        safetyScore: Number(prequalForm.safetyScore) || 0,
        ramsSubmitted: prequalForm.ramsSubmitted,
        notes: prequalForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Subcontractor updated.' })
      setViewing(null); setEditing(false); load(); refreshDashboard()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update subcontractor.' })
    } finally { setSaving(false) }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About Subcontractor Prequalification">
        Tracks each subcontractor's RAMS submission status and safety record score, sourced from the
        Approved Subcontractor Register. Click a row to see the full record and to edit name, trade
        category, safety score, RAMS submission or notes. Prequalified status itself only changes via
        the Subcontracts module's PQQ approval workflow, not here.
      </HelpPanel>

      <SectionHeader title="Subcontractor HSE Prequalification" sub="RAMS submission status and safety record score — sourced from the Approved Subcontractor Register" action={<Btn onClick={() => setModal('sub')}>+ Add Subcontractor</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Subcontractor', 'Trade Category', 'Safety Score', 'RAMS Submitted', 'Prequalified', 'Actions']}
          empty="No subcontractors added yet."
          rows={subcontractors.map(s => [
            <span onClick={() => openView(s)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{s.name}</span>,
            s.tradeCategory || '—',
            <Badge variant={s.safetyScore >= 80 ? 'green' : s.safetyScore >= 50 ? 'amber' : 'red'}>{s.safetyScore}</Badge>,
            s.ramsSubmitted ? <Badge variant="green">Yes</Badge> : <Badge variant="red">No</Badge>,
            s.prequalified ? <Badge variant="green">Prequalified</Badge> : <Badge variant="amber">Pending</Badge>,
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openView(s)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openView(s, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>

      {/* ── Add Subcontractor modal ── */}
      {modal === 'sub' && (
        <Modal title="Add Subcontractor" onClose={() => setModal(null)}>
          <Input label="Name" value={subForm.name} onChange={v => setSubForm({ ...subForm, name: v })} required />
          <Input label="Trade Category" value={subForm.tradeCategory} onChange={v => setSubForm({ ...subForm, tradeCategory: v })} required placeholder="e.g. Electrical, Plumbing, Civil Works" />
          <Input label="Safety Score (0–100)" type="number" value={subForm.safetyScore} onChange={v => setSubForm({ ...subForm, safetyScore: v })} />
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 14 }}>
            <input type="checkbox" checked={subForm.ramsSubmitted} onChange={e => setSubForm({ ...subForm, ramsSubmitted: e.target.checked })} />
            RAMS submitted
          </label>
          <Input label="Notes" value={subForm.notes} onChange={v => setSubForm({ ...subForm, notes: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitSubcontractor} disabled={saving || !subForm.name || !subForm.tradeCategory}>{saving ? 'Saving…' : 'Add Subcontractor'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Subcontractor modal ── */}
      {viewing && (
        <Modal title={editing ? 'Edit Subcontractor' : 'Subcontractor Details'} onClose={() => { setViewing(null); setEditing(false) }} width={620}>
          {!editing ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewing.name}</span>
                  {viewing.prequalified ? <Badge variant="green">Prequalified</Badge> : <Badge variant="amber">Pending</Badge>}
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewing.tradeCategory || '—'}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Safety Score" value={<Badge variant={viewing.safetyScore >= 80 ? 'green' : viewing.safetyScore >= 50 ? 'amber' : 'red'}>{viewing.safetyScore}</Badge>} />
                <MiniStat label="RAMS Submitted" value={viewing.ramsSubmitted ? <Badge variant="green">Yes</Badge> : <Badge variant="red">No</Badge>} />
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Notes</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewing.notes || 'No notes.'}</div>
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Prequalified status is set via the Subcontracts PQQ workflow, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewing(null); setEditing(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditing(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Name" value={prequalForm.name} onChange={v => setPrequalForm({ ...prequalForm, name: v })} required />
              <Input label="Trade Category" value={prequalForm.tradeCategory} onChange={v => setPrequalForm({ ...prequalForm, tradeCategory: v })} required />
              <Input label="Safety Score (0–100)" type="number" value={prequalForm.safetyScore} onChange={v => setPrequalForm({ ...prequalForm, safetyScore: v })} />
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: T.dgrey, marginBottom: 10 }}>
                <input type="checkbox" checked={prequalForm.ramsSubmitted} onChange={e => setPrequalForm({ ...prequalForm, ramsSubmitted: e.target.checked })} />
                RAMS submitted
              </label>
              <Badge variant={prequalForm.prequalified ? 'green' : 'amber'}>{prequalForm.prequalified ? 'Prequalified' : 'Prequalification pending'}</Badge>
              <p style={{ fontSize: 11, color: T.mgrey, margin: '6px 0 14px' }}>Prequalified status is set by approving this subcontractor's PQQ in the Subcontracts module — it isn't edited directly here.</p>
              <Input label="Notes" value={prequalForm.notes} onChange={v => setPrequalForm({ ...prequalForm, notes: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditing(false)}>Cancel</Btn>
                <Btn onClick={submitPrequal} disabled={saving || !prequalForm.name || !prequalForm.tradeCategory}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
