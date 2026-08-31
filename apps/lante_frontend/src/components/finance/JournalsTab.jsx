import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import { listJournals, getChartOfAccounts, submitJournal, reviewJournal, approveJournal, reverseJournal } from '../../services/finance.js'
import JournalModal from './JournalModal.jsx'

const STATUS_VARIANT = { Draft: 'default', PendingReview: 'amber', PendingApproval: 'blue', Posted: 'green', Reversed: 'red' }
const WORKFLOW = { Draft: '1 of 3 · drafted', PendingReview: '2 of 3 · review', PendingApproval: '3 of 3 · approve', Posted: 'posted', Reversed: 'reversed' }

export default function JournalsTab({ notify }) {
  const [journals, setJournals] = useState([])
  const [accounts, setAccounts] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(false)
  const [busy, setBusy] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([listJournals(), getChartOfAccounts()])
      .then(([j, a]) => { setJournals(j ?? []); setAccounts(a ?? []) })
      .catch(() => notify?.('Failed to load journals.', 'error'))
      .finally(() => setLoading(false))
  }, [notify])

  useEffect(() => { load() }, [load])

  async function act(id, fn, label) {
    setBusy(id)
    try { await fn(id); notify?.(label); await load() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Action failed.', 'error') }
    finally { setBusy(null) }
  }

  const actionFor = (j) => {
    if (busy === j.id) return <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
    switch (j.status) {
      case 'Draft': return <Btn size="sm" onClick={() => act(j.id, submitJournal, 'Submitted for review.')}>Submit</Btn>
      case 'PendingReview': return <Btn size="sm" onClick={() => act(j.id, reviewJournal, 'Reviewed — awaiting approval.')}>Review</Btn>
      case 'PendingApproval': return <Btn size="sm" variant="green" onClick={() => act(j.id, approveJournal, 'Approved and posted.')}>Approve</Btn>
      case 'Posted': return <Btn size="sm" variant="ghost" onClick={() => act(j.id, reverseJournal, 'Reversed.')}>Reverse</Btn>
      default: return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
    }
  }

  return (
    <>
      <Alert type="info">
        <strong style={{ color: T.blue }}>FIN-002:</strong> journals require three distinct users — preparer → reviewer → approver. Approval posts the entry; posted journals can't be deleted, only reversed.
      </Alert>
      <SectionHeader title="Journal Entries" action={<Btn onClick={() => setModal(true)} disabled={loading}>+ New Journal</Btn>} />
      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Entry', 'Date', 'Description', 'Amount', 'Status', 'Workflow', 'Action']}
            empty="No journals yet."
            rows={journals.map(j => [
              <span style={{ fontFamily: T.mono, fontSize: 12 }}>{j.entryNo}</span>,
              fmt.date(j.entryDate),
              j.description,
              <strong>{fmt.money(j.totalDebit, j.currencyCode)}</strong>,
              <Badge variant={STATUS_VARIANT[j.status] || 'default'}>{j.status}</Badge>,
              <span style={{ fontSize: 12, color: T.mgrey }}>{WORKFLOW[j.status] ?? '—'}{j.sourceModule ? ` · via ${j.sourceModule}` : ''}</span>,
              actionFor(j),
            ])}
          />
        </Card>
      )}
      {modal && <JournalModal accounts={accounts} onClose={() => setModal(false)} onCreated={load} notify={notify} />}
    </>
  )
}
