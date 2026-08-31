import { useState, useEffect, useCallback } from 'react'
import api from '../../../../api/axios.js'
import { T, fmt } from '../../../../theme/tokens.js'
import { Card, Btn, Badge, HelpPanel, SectionHeader, DataTable, Modal, Input, Select, Loading, MiniStat } from '../../../../components/ui.jsx'
import Pagination from '../../../../components/Pagination.jsx'
import { PARTY_RELATIONSHIPS, PARTY_RELATIONSHIP_LABELS, EMPTY_ICM_PARTY, EMPTY_ICSA, EMPTY_ICM_TXN, today } from '../constants.js'

const PAGE_SIZE = 20

function ageVariant(days) {
  if (days >= 45) return 'red'
  if (days >= 30) return 'amber'
  return 'green'
}

export default function IntercompanyTab({ setMsg }) {
  const [parties, setParties] = useState([])
  const [agreements, setAgreements] = useState([])
  const [txns, setTxns] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null)
  const [saving, setSaving] = useState(false)
  const [partyForm, setPartyForm] = useState(EMPTY_ICM_PARTY)
  const [icsaForm, setIcsaForm] = useState(EMPTY_ICSA)
  const [txnForm, setTxnForm] = useState(EMPTY_ICM_TXN)

  // Independent pagination per sub-register
  const [partiesPage, setPartiesPage] = useState(1)
  const [partiesTotalCount, setPartiesTotalCount] = useState(0)
  const [agreementsPage, setAgreementsPage] = useState(1)
  const [agreementsTotalCount, setAgreementsTotalCount] = useState(0)
  const [txnsPage, setTxnsPage] = useState(1)
  const [txnsTotalCount, setTxnsTotalCount] = useState(0)

  // ── Related Party view/edit ──
  const [viewingParty, setViewingParty] = useState(null)
  const [editingParty, setEditingParty] = useState(false)
  const [partyEditForm, setPartyEditForm] = useState(EMPTY_ICM_PARTY)

  // ── ICSA view/edit ──
  const [viewingIcsa, setViewingIcsa] = useState(null)
  const [editingIcsa, setEditingIcsa] = useState(false)
  const [icsaEditForm, setIcsaEditForm] = useState(EMPTY_ICSA)

  // ── Transaction view/edit ──
  const [viewingTxn, setViewingTxn] = useState(null)
  const [editingTxn, setEditingTxn] = useState(false)
  const [txnEditForm, setTxnEditForm] = useState(EMPTY_ICM_TXN)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [pRes, aRes, tRes] = await Promise.all([
        api.get('/api/v1/compliance-icm-related-parties', { params: { page: partiesPage, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } })),
        api.get('/api/v1/compliance-icm-agreements', { params: { page: agreementsPage, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } })),
        api.get('/api/v1/compliance-icm-transactions', { params: { page: txnsPage, pageSize: PAGE_SIZE } }).catch(() => ({ data: { data: {} } })),
      ])
      const pData = pRes.data?.data || {}
      const aData = aRes.data?.data || {}
      const tData = tRes.data?.data || {}
      setParties(pData.items ?? [])
      setPartiesTotalCount(pData.totalCount ?? 0)
      setAgreements(aData.items ?? [])
      setAgreementsTotalCount(aData.totalCount ?? 0)
      setTxns(tData.items ?? [])
      setTxnsTotalCount(tData.totalCount ?? 0)
    } finally {
      setLoading(false)
    }
  }, [partiesPage, agreementsPage, txnsPage])

  useEffect(() => { load() }, [load])

  const partyName = id => parties.find(p => p.id === id)?.companyName ?? id

  async function submitParty() {
    if (!partyForm.companyName || !partyForm.regNo) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-icm-related-parties', {
        companyName: partyForm.companyName, regNo: partyForm.regNo,
        relationship: PARTY_RELATIONSHIPS.indexOf(partyForm.relationship),
        notes: partyForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Related party registered.' })
      setPartyForm(EMPTY_ICM_PARTY); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to register related party.' })
    } finally { setSaving(false) }
  }

  function openViewParty(p, startEditing = false) {
    setViewingParty(p)
    setEditingParty(startEditing)
    setPartyEditForm({
      companyName: p.companyName ?? '',
      regNo: p.regNo ?? '',
      relationship: PARTY_RELATIONSHIPS[p.relationship] ?? p.relationship,
      notes: p.notes ?? '',
    })
  }

  async function submitPartyEdit() {
    if (!viewingParty || !partyEditForm.companyName || !partyEditForm.regNo) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-icm-related-parties/${viewingParty.id}`, {
        companyName: partyEditForm.companyName, regNo: partyEditForm.regNo,
        relationship: PARTY_RELATIONSHIPS.indexOf(partyEditForm.relationship),
        notes: partyEditForm.notes || null,
      })
      setMsg({ type: 'success', text: 'Related party updated.' })
      setViewingParty(null); setEditingParty(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update related party.' })
    } finally { setSaving(false) }
  }

  async function submitIcsa() {
    if (!icsaForm.relatedPartyId || !icsaForm.scope || !icsaForm.rechargeRate) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-icm-agreements', {
        relatedPartyId: icsaForm.relatedPartyId, scope: icsaForm.scope,
        rechargeRate: Number(icsaForm.rechargeRate) || 0,
        startDate: new Date(icsaForm.startDate || Date.now()).toISOString(),
        endDate: icsaForm.endDate ? new Date(icsaForm.endDate).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'Inter-company services agreement created.' })
      setIcsaForm(EMPTY_ICSA); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to create agreement.' })
    } finally { setSaving(false) }
  }

  function openViewIcsa(a, startEditing = false) {
    setViewingIcsa(a)
    setEditingIcsa(startEditing)
    setIcsaEditForm({
      relatedPartyId: a.relatedPartyId ?? '',
      scope: a.scope ?? '',
      rechargeRate: a.rechargeRate ?? '',
      startDate: a.startDate ? a.startDate.slice(0, 10) : today(),
      endDate: a.endDate ? a.endDate.slice(0, 10) : '',
    })
  }

  async function submitIcsaEdit() {
    if (!viewingIcsa || !icsaEditForm.relatedPartyId || !icsaEditForm.scope || !icsaEditForm.rechargeRate) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-icm-agreements/${viewingIcsa.id}`, {
        relatedPartyId: icsaEditForm.relatedPartyId, scope: icsaEditForm.scope,
        rechargeRate: Number(icsaEditForm.rechargeRate) || 0,
        startDate: new Date(icsaEditForm.startDate || Date.now()).toISOString(),
        endDate: icsaEditForm.endDate ? new Date(icsaEditForm.endDate).toISOString() : null,
      })
      setMsg({ type: 'success', text: 'Inter-company services agreement updated.' })
      setViewingIcsa(null); setEditingIcsa(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update agreement.' })
    } finally { setSaving(false) }
  }

  async function submitTxn() {
    if (!txnForm.icsaId || !txnForm.relatedPartyId || !txnForm.amount || !txnForm.qslLedgerRef || !txnForm.sisterLedgerRef) return
    setSaving(true)
    try {
      await api.post('/api/v1/compliance-icm-transactions', {
        icsaId: txnForm.icsaId, relatedPartyId: txnForm.relatedPartyId, year: Number(txnForm.year) || new Date().getFullYear(),
        amount: Number(txnForm.amount) || 0, qslLedgerRef: txnForm.qslLedgerRef, sisterLedgerRef: txnForm.sisterLedgerRef,
      })
      setMsg({ type: 'success', text: 'Intercompany transaction posted.' })
      setTxnForm(EMPTY_ICM_TXN); setModal(null); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to post transaction.' })
    } finally { setSaving(false) }
  }

  function openViewTxn(t, startEditing = false) {
    setViewingTxn(t)
    setEditingTxn(startEditing)
    setTxnEditForm({
      icsaId: t.icsaId ?? '',
      relatedPartyId: t.relatedPartyId ?? '',
      year: t.year ?? new Date().getFullYear(),
      amount: t.amount ?? '',
      qslLedgerRef: t.qslLedgerRef ?? '',
      sisterLedgerRef: t.sisterLedgerRef ?? '',
      postedAt: t.postedAt ? t.postedAt.slice(0, 10) : today(),
    })
  }

  async function submitTxnEdit() {
    if (!viewingTxn || !txnEditForm.icsaId || !txnEditForm.relatedPartyId || !txnEditForm.amount || !txnEditForm.qslLedgerRef || !txnEditForm.sisterLedgerRef) return
    setSaving(true)
    try {
      await api.put(`/api/v1/compliance-icm-transactions/${viewingTxn.id}`, {
        icsaId: txnEditForm.icsaId, relatedPartyId: txnEditForm.relatedPartyId, year: Number(txnEditForm.year) || new Date().getFullYear(),
        amount: Number(txnEditForm.amount) || 0, qslLedgerRef: txnEditForm.qslLedgerRef, sisterLedgerRef: txnEditForm.sisterLedgerRef,
        postedAt: new Date(txnEditForm.postedAt || Date.now()).toISOString(),
      })
      setMsg({ type: 'success', text: 'Intercompany transaction updated.' })
      setViewingTxn(null); setEditingTxn(false); load()
    } catch (e) {
      setMsg({ type: 'error', text: e.response?.data?.message ?? 'Failed to update transaction.' })
    } finally { setSaving(false) }
  }

  async function reconcile(id) {
    try {
      await api.patch(`/api/v1/compliance-icm-transactions/${id}/reconcile`)
      load()
    } catch {
      setMsg({ type: 'error', text: 'Failed to mark as reconciled.' })
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <HelpPanel title="About Inter-Company Management">
        <strong>Related Parties</strong> are the sister companies and affiliates that agreements and
        transactions below are posted against. An <strong>Inter-Company Services Agreement (ICSA)</strong>
        defines the recharge scope and rate governing a related party — its "Active" status is
        server-computed from the End Date, not editable directly. An <strong>Inter-Company Transaction</strong>
        is a dual-ledger recharge posting made under an ICSA; unreconciled entries are flagged amber at
        30 days and red at 45 days. Click any row across the three registers to view full details and
        edit them. Use <strong>Mark Reconciled</strong> to clear an unreconciled transaction — reconciliation
        is a one-way workflow action, not part of the edit form.
      </HelpPanel>

      <SectionHeader title="Related Parties" sub="Sister companies and affiliates that inter-company agreements are posted against" action={<Btn onClick={() => setModal('party')}>+ Register Related Party</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 18 }}>
        <DataTable
          headers={['Company', 'Reg No.', 'Relationship', 'Notes', 'Actions']}
          empty="No related parties registered yet."
          rows={parties.map(p => [
            <span onClick={() => openViewParty(p)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{p.companyName}</span>,
            p.regNo,
            <Badge variant="navy">{PARTY_RELATIONSHIP_LABELS[PARTY_RELATIONSHIPS[p.relationship]] ?? p.relationship}</Badge>,
            p.notes ?? '—',
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openViewParty(p)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openViewParty(p, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={partiesPage} pageSize={PAGE_SIZE} totalCount={partiesTotalCount} onPageChange={setPartiesPage} />

      <SectionHeader title="Inter-Company Services Agreements" sub="Recharge scope and rate governing recurring intercompany recharges" action={<Btn onClick={() => setModal('icsa')}>+ New Agreement</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden', marginBottom: 18 }}>
        <DataTable
          headers={['Related Party', 'Scope', 'Recharge Rate', 'Start', 'End', 'Status', 'Actions']}
          empty="No inter-company services agreements yet."
          rows={agreements.map(a => [
            <span onClick={() => openViewIcsa(a)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{a.relatedPartyName ?? partyName(a.relatedPartyId)}</span>,
            a.scope,
            fmt.kes(a.rechargeRate),
            fmt.date(a.startDate),
            a.endDate ? fmt.date(a.endDate) : '—',
            a.isActive ? <Badge variant="green">Active</Badge> : <Badge variant="default">Expired</Badge>,
            <div style={{ display: 'flex', gap: 4 }}>
              <Btn variant="ghost" size="sm" onClick={() => openViewIcsa(a)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openViewIcsa(a, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
            </div>,
          ])}
        />
      </Card>
      <Pagination page={agreementsPage} pageSize={PAGE_SIZE} totalCount={agreementsTotalCount} onPageChange={setAgreementsPage} />

      <SectionHeader title="Inter-Company Transactions" sub="Dual-ledger recharges — unreconciled entries are flagged at 30 days, critical at 45 days" action={<Btn onClick={() => setModal('txn')}>+ Post Transaction</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable
          headers={['Related Party', 'Year', 'Amount', 'QSL Ref', 'Sister Ref', 'Posted', 'Age / Status', 'Actions']}
          empty="No inter-company transactions posted yet."
          rows={txns.map(t => [
            <span onClick={() => openViewTxn(t)} style={{ color: T.blue, textDecoration: 'underline', cursor: 'pointer', fontWeight: 600 }}>{t.relatedPartyName ?? partyName(t.relatedPartyId)}</span>,
            t.year,
            fmt.kes(t.amount),
            t.qslLedgerRef,
            t.sisterLedgerRef,
            fmt.date(t.postedAt),
            t.reconciledAt
              ? <Badge variant="green">Reconciled</Badge>
              : <Badge variant={ageVariant(t.ageDays)}>{t.ageDays}d unreconciled</Badge>,
            <div style={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
              <Btn variant="ghost" size="sm" onClick={() => openViewTxn(t)} style={{ padding: '3px 10px', fontSize: 11 }}>View</Btn>
              <Btn variant="ghost" size="sm" onClick={() => openViewTxn(t, true)} style={{ padding: '3px 10px', fontSize: 11 }}>Edit</Btn>
              {!t.reconciledAt && <Btn size="sm" onClick={() => reconcile(t.id)}>Mark Reconciled</Btn>}
            </div>,
          ])}
        />
      </Card>
      <Pagination page={txnsPage} pageSize={PAGE_SIZE} totalCount={txnsTotalCount} onPageChange={setTxnsPage} />

      {modal === 'party' && (
        <Modal title="Register Related Party" onClose={() => setModal(null)}>
          <Input label="Company Name" value={partyForm.companyName} onChange={v => setPartyForm({ ...partyForm, companyName: v })} required />
          <Input label="Registration No." value={partyForm.regNo} onChange={v => setPartyForm({ ...partyForm, regNo: v })} required />
          <Select label="Relationship" value={partyForm.relationship} onChange={v => setPartyForm({ ...partyForm, relationship: v })} options={PARTY_RELATIONSHIPS.map(r => ({ value: r, label: PARTY_RELATIONSHIP_LABELS[r] }))} />
          <Input label="Notes" value={partyForm.notes} onChange={v => setPartyForm({ ...partyForm, notes: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitParty} disabled={saving || !partyForm.companyName || !partyForm.regNo}>{saving ? 'Saving…' : 'Register'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'icsa' && (
        <Modal title="New Inter-Company Services Agreement" onClose={() => setModal(null)}>
          <Select label="Related Party" value={icsaForm.relatedPartyId} onChange={v => setIcsaForm({ ...icsaForm, relatedPartyId: v })} options={[{ value: '', label: '— Select —' }, ...parties.map(p => ({ value: p.id, label: p.companyName }))]} />
          <Input label="Scope" value={icsaForm.scope} onChange={v => setIcsaForm({ ...icsaForm, scope: v })} required placeholder="e.g. Shared IT services, management fees" />
          <Input label="Recharge Rate (Kshs)" type="number" value={icsaForm.rechargeRate} onChange={v => setIcsaForm({ ...icsaForm, rechargeRate: v })} required />
          <Input label="Start Date" type="date" value={icsaForm.startDate} onChange={v => setIcsaForm({ ...icsaForm, startDate: v })} />
          <Input label="End Date (optional)" type="date" value={icsaForm.endDate} onChange={v => setIcsaForm({ ...icsaForm, endDate: v })} />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitIcsa} disabled={saving || !icsaForm.relatedPartyId || !icsaForm.scope || !icsaForm.rechargeRate}>{saving ? 'Saving…' : 'Create Agreement'}</Btn>
          </div>
        </Modal>
      )}

      {modal === 'txn' && (
        <Modal title="Post Inter-Company Transaction" onClose={() => setModal(null)}>
          <Select label="Agreement" value={txnForm.icsaId} onChange={v => {
            const a = agreements.find(x => x.id === v)
            setTxnForm({ ...txnForm, icsaId: v, relatedPartyId: a?.relatedPartyId ?? txnForm.relatedPartyId })
          }} options={[{ value: '', label: '— Select —' }, ...agreements.map(a => ({ value: a.id, label: `${a.relatedPartyName ?? partyName(a.relatedPartyId)} — ${a.scope}` }))]} />
          <Input label="Year" type="number" value={txnForm.year} onChange={v => setTxnForm({ ...txnForm, year: v })} required />
          <Input label="Amount (Kshs)" type="number" value={txnForm.amount} onChange={v => setTxnForm({ ...txnForm, amount: v })} required />
          <Input label="QSL Ledger Ref" value={txnForm.qslLedgerRef} onChange={v => setTxnForm({ ...txnForm, qslLedgerRef: v })} required />
          <Input label="Sister Company Ledger Ref" value={txnForm.sisterLedgerRef} onChange={v => setTxnForm({ ...txnForm, sisterLedgerRef: v })} required />
          <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
            <Btn variant="ghost" onClick={() => setModal(null)}>Cancel</Btn>
            <Btn onClick={submitTxn} disabled={saving || !txnForm.icsaId || !txnForm.amount || !txnForm.qslLedgerRef || !txnForm.sisterLedgerRef}>{saving ? 'Saving…' : 'Post Transaction'}</Btn>
          </div>
        </Modal>
      )}

      {/* ── View / Edit Related Party modal ── */}
      {viewingParty && (
        <Modal title={editingParty ? 'Edit Related Party' : 'Related Party Details'} onClose={() => { setViewingParty(null); setEditingParty(false) }} width={620}>
          {!editingParty ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewingParty.companyName}</span>
                  <Badge variant="navy">{PARTY_RELATIONSHIP_LABELS[PARTY_RELATIONSHIPS[viewingParty.relationship]] ?? viewingParty.relationship}</Badge>
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Reg No. {viewingParty.regNo}</div>
              </div>

              <div style={{ borderTop: `1px solid ${T.lgrey}`, paddingTop: 14, marginBottom: 14 }}>
                <div style={{ fontSize: 11, fontWeight: 600, color: T.mgrey, textTransform: 'uppercase', letterSpacing: .5, marginBottom: 8 }}>Notes</div>
                <div style={{ fontSize: 13, color: T.dgrey }}>{viewingParty.notes || 'No notes.'}</div>
              </div>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewingParty(null); setEditingParty(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditingParty(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Input label="Company Name" value={partyEditForm.companyName} onChange={v => setPartyEditForm({ ...partyEditForm, companyName: v })} required />
              <Input label="Registration No." value={partyEditForm.regNo} onChange={v => setPartyEditForm({ ...partyEditForm, regNo: v })} required />
              <Select label="Relationship" value={partyEditForm.relationship} onChange={v => setPartyEditForm({ ...partyEditForm, relationship: v })} options={PARTY_RELATIONSHIPS.map(r => ({ value: r, label: PARTY_RELATIONSHIP_LABELS[r] }))} />
              <Input label="Notes" value={partyEditForm.notes} onChange={v => setPartyEditForm({ ...partyEditForm, notes: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditingParty(false)}>Cancel</Btn>
                <Btn onClick={submitPartyEdit} disabled={saving || !partyEditForm.companyName || !partyEditForm.regNo}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── View / Edit ICSA modal ── */}
      {viewingIcsa && (
        <Modal title={editingIcsa ? 'Edit Inter-Company Services Agreement' : 'Inter-Company Services Agreement Details'} onClose={() => { setViewingIcsa(null); setEditingIcsa(false) }} width={620}>
          {!editingIcsa ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewingIcsa.relatedPartyName ?? partyName(viewingIcsa.relatedPartyId)}</span>
                  {viewingIcsa.isActive ? <Badge variant="green">Active</Badge> : <Badge variant="default">Expired</Badge>}
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>{viewingIcsa.scope}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Recharge Rate" value={fmt.kes(viewingIcsa.rechargeRate)} />
                <MiniStat label="Start Date" value={fmt.date(viewingIcsa.startDate)} />
                <MiniStat label="End Date" value={viewingIcsa.endDate ? fmt.date(viewingIcsa.endDate) : '—'} />
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Active/Expired status is server-computed from End Date, not editable.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewingIcsa(null); setEditingIcsa(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditingIcsa(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Related Party" value={icsaEditForm.relatedPartyId} onChange={v => setIcsaEditForm({ ...icsaEditForm, relatedPartyId: v })} options={[{ value: '', label: '— Select —' }, ...parties.map(p => ({ value: p.id, label: p.companyName }))]} />
              <Input label="Scope" value={icsaEditForm.scope} onChange={v => setIcsaEditForm({ ...icsaEditForm, scope: v })} required />
              <Input label="Recharge Rate (Kshs)" type="number" value={icsaEditForm.rechargeRate} onChange={v => setIcsaEditForm({ ...icsaEditForm, rechargeRate: v })} required />
              <Input label="Start Date" type="date" value={icsaEditForm.startDate} onChange={v => setIcsaEditForm({ ...icsaEditForm, startDate: v })} />
              <Input label="End Date (optional)" type="date" value={icsaEditForm.endDate} onChange={v => setIcsaEditForm({ ...icsaEditForm, endDate: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditingIcsa(false)}>Cancel</Btn>
                <Btn onClick={submitIcsaEdit} disabled={saving || !icsaEditForm.relatedPartyId || !icsaEditForm.scope || !icsaEditForm.rechargeRate}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}

      {/* ── View / Edit Transaction modal ── */}
      {viewingTxn && (
        <Modal title={editingTxn ? 'Edit Inter-Company Transaction' : 'Inter-Company Transaction Details'} onClose={() => { setViewingTxn(null); setEditingTxn(false) }} width={620}>
          {!editingTxn ? (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 18, fontWeight: 700, color: T.navy }}>{viewingTxn.relatedPartyName ?? partyName(viewingTxn.relatedPartyId)}</span>
                  {viewingTxn.reconciledAt ? <Badge variant="green">Reconciled</Badge> : <Badge variant={ageVariant(viewingTxn.ageDays)}>{viewingTxn.ageDays}d unreconciled</Badge>}
                </div>
                <div style={{ fontSize: 13, color: T.mgrey, marginTop: 2 }}>Year {viewingTxn.year}</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(110px, 1fr))', gap: 10, marginBottom: 18 }}>
                <MiniStat label="Amount" value={fmt.kes(viewingTxn.amount)} />
                <MiniStat label="QSL Ledger Ref" value={viewingTxn.qslLedgerRef} />
                <MiniStat label="Sister Ledger Ref" value={viewingTxn.sisterLedgerRef} />
                <MiniStat label="Posted At" value={fmt.date(viewingTxn.postedAt)} />
              </div>

              <p style={{ fontSize: 11, color: T.mgrey, margin: '4px 0 0' }}>Reconciliation status changes via Mark Reconciled, not here.</p>

              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 18 }}>
                <Btn variant="ghost" onClick={() => { setViewingTxn(null); setEditingTxn(false) }}>Close</Btn>
                <Btn variant="gold" onClick={() => setEditingTxn(true)}>Edit</Btn>
              </div>
            </>
          ) : (
            <>
              <Select label="Agreement" value={txnEditForm.icsaId} onChange={v => {
                const a = agreements.find(x => x.id === v)
                setTxnEditForm({ ...txnEditForm, icsaId: v, relatedPartyId: a?.relatedPartyId ?? txnEditForm.relatedPartyId })
              }} options={[{ value: '', label: '— Select —' }, ...agreements.map(a => ({ value: a.id, label: `${a.relatedPartyName ?? partyName(a.relatedPartyId)} — ${a.scope}` }))]} />
              <Select label="Related Party" value={txnEditForm.relatedPartyId} onChange={v => setTxnEditForm({ ...txnEditForm, relatedPartyId: v })} options={[{ value: '', label: '— Select —' }, ...parties.map(p => ({ value: p.id, label: p.companyName }))]} />
              <Input label="Year" type="number" value={txnEditForm.year} onChange={v => setTxnEditForm({ ...txnEditForm, year: v })} required />
              <Input label="Amount (Kshs)" type="number" value={txnEditForm.amount} onChange={v => setTxnEditForm({ ...txnEditForm, amount: v })} required />
              <Input label="QSL Ledger Ref" value={txnEditForm.qslLedgerRef} onChange={v => setTxnEditForm({ ...txnEditForm, qslLedgerRef: v })} required />
              <Input label="Sister Company Ledger Ref" value={txnEditForm.sisterLedgerRef} onChange={v => setTxnEditForm({ ...txnEditForm, sisterLedgerRef: v })} required />
              <Input label="Posted At" type="date" value={txnEditForm.postedAt} onChange={v => setTxnEditForm({ ...txnEditForm, postedAt: v })} />
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
                <Btn variant="ghost" onClick={() => setEditingTxn(false)}>Cancel</Btn>
                <Btn onClick={submitTxnEdit} disabled={saving || !txnEditForm.icsaId || !txnEditForm.relatedPartyId || !txnEditForm.amount || !txnEditForm.qslLedgerRef || !txnEditForm.sisterLedgerRef}>{saving ? 'Saving…' : 'Save'}</Btn>
              </div>
            </>
          )}
        </Modal>
      )}
    </>
  )
}
