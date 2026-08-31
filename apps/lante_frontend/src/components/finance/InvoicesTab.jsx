import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import { listInvoices, getCustomers, getTaxCategories, issueInvoice, cancelInvoice } from '../../services/finance.js'
import InvoiceModal from './InvoiceModal.jsx'
import CancelDocumentModal from './CancelDocumentModal.jsx'
import { useAuth } from '../../context/AuthContext.jsx'
import { cancellability } from '../../utils/documentActions.js'

// No Overdue: the API cannot emit it (#343). Aging is computed from the due date, not stored on
// the invoice, so a red "Overdue" badge here was a state the backend had no way to produce.
const STATUS_VARIANT = { Draft: 'default', Issued: 'blue', PartPaid: 'amber', Paid: 'green', Cancelled: 'default' }
const ETIMS_VARIANT = { Accepted: 'green', Pending: 'amber', Rejected: 'red', NotSubmitted: 'default' }

export default function InvoicesTab({ notify }) {
  const [invoices, setInvoices] = useState([])
  const [customers, setCustomers] = useState([])
  const [taxCats, setTaxCats] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(false)
  const [busy, setBusy] = useState(null)
  const [cancelling, setCancelling] = useState(null)
  const { hasPermission } = useAuth()
  const canApprove = hasPermission('finance.approve')

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([listInvoices(), getCustomers(), getTaxCategories()])
      .then(([inv, c, t]) => { setInvoices(inv ?? []); setCustomers(c ?? []); setTaxCats(t ?? []) })
      .catch(() => notify?.('Failed to load invoices.', 'error'))
      .finally(() => setLoading(false))
  }, [notify])
  useEffect(() => { load() }, [load])

  async function issue(id) {
    setBusy(id)
    try { await issueInvoice(id); notify?.('Invoice issued, posted to the ledger & submitted to eTIMS.'); await load() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed to issue.', 'error') }
    finally { setBusy(null) }
  }

  async function confirmCancel(reason) {
    const id = cancelling.id
    setCancelling(null)
    setBusy(id)
    try { await cancelInvoice(id, reason); notify?.('Invoice cancelled; its ledger posting was reversed.'); await load() }
    // The API's own message is more useful than anything generic — it names the amount received and
    // what to do instead ("reverse or refund the receipt first, or raise a credit note").
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed to cancel.', 'error') }
    finally { setBusy(null) }
  }

  const action = (i) => {
    if (busy === i.id) return <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
    const cancel = cancellability(i, { canApprove })
    return (
      <span style={{ display: 'inline-flex', gap: 6 }}>
        {i.status === 'Draft' && <Btn size="sm" onClick={() => issue(i.id)}>Issue</Btn>}
        {cancel.allowed && (
          <Btn size="sm" variant="ghost" onClick={() => setCancelling(i)}>Cancel</Btn>
        )}
        {i.status !== 'Draft' && !cancel.allowed && <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>}
      </span>
    )
  }

  return (
    <>
      <Alert type="info">Invoices post to the general ledger and submit to KRA eTIMS on issue. This is the same register as Tax → Tax Invoices.</Alert>
      <SectionHeader title="Invoices" action={<Btn onClick={() => setModal(true)} disabled={loading}>+ Create Invoice</Btn>} />
      {loading ? <Loading /> : (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <DataTable
            headers={['Invoice No', 'Client', 'Date', 'Total', 'Balance', 'Status', 'eTIMS', 'Action']}
            empty="No invoices yet."
            rows={invoices.map(i => [
              <span style={{ fontFamily: T.mono, fontSize: 12 }}>{i.invoiceNo}</span>,
              i.customerName,
              fmt.date(i.invoiceDate),
              <strong>{fmt.money(i.total, i.currencyCode)}</strong>,
              <span style={{ color: i.balance > 0 ? T.dgrey : T.green }}>{fmt.money(i.balance, i.currencyCode)}</span>,
              // The reason rides on the badge rather than taking a column: it only exists on
              // cancelled rows, and an empty column on every other invoice costs more than it gives.
              <span title={i.status === 'Cancelled' && i.cancellationReason ? `Cancelled: ${i.cancellationReason}` : undefined}>
                <Badge variant={STATUS_VARIANT[i.status] || 'default'}>{i.status}</Badge>
              </span>,
              i.etimsReference
                ? <Badge variant={ETIMS_VARIANT[i.etimsStatus] || 'default'}>{i.etimsStatus}</Badge>
                : <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>,
              action(i),
            ])}
          />
        </Card>
      )}
      {cancelling && (
        <CancelDocumentModal
          doc={{ id: cancelling.id, number: cancelling.invoiceNo }}
          label="invoice"
          posted={cancelling.status !== 'Draft'}
          onClose={() => setCancelling(null)}
          onConfirm={confirmCancel}
        />
      )}
      {modal && <InvoiceModal customers={customers} taxCategories={taxCats} onClose={() => setModal(false)} onCreated={load} notify={notify} />}
    </>
  )
}
