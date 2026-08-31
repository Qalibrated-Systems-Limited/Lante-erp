import { useEffect, useState, useCallback } from 'react'
import { T, fmt } from '../../theme/tokens.js'
import { Card, Kpi, KPI_GRID, Btn, Badge, Alert, SectionHeader, DataTable, Loading } from '../ui.jsx'
import {
  listSupplierInvoices, listVouchers, getSuppliers, getTaxCategories,
  approveSupplierInvoice, createVoucher, approveVoucher, payVoucher, cancelSupplierInvoice,
} from '../../services/finance.js'
import SupplierInvoiceModal from './SupplierInvoiceModal.jsx'
import CancelDocumentModal from './CancelDocumentModal.jsx'
import { useAuth } from '../../context/AuthContext.jsx'
import { cancellability } from '../../utils/documentActions.js'

const BILL_VARIANT = { Received: 'amber', Approved: 'blue', PartPaid: 'amber', Paid: 'green', Cancelled: 'default' }
const MATCH_VARIANT = { Matched: 'green', Pending: 'amber', Exception: 'red', NotRequired: 'default' }
const VOUCHER_VARIANT = { Draft: 'default', PendingApproval: 'amber', Approved: 'blue', Paid: 'green', Rejected: 'red' }

export default function PaymentsTab({ notify }) {
  const [bills, setBills] = useState([])
  const [vouchers, setVouchers] = useState([])
  const [suppliers, setSuppliers] = useState([])
  const [taxCats, setTaxCats] = useState([])
  const [loading, setLoading] = useState(true)
  const [modal, setModal] = useState(null)  // 'bill' | 'adhoc'
  const [cancelling, setCancelling] = useState(null)
  const { hasPermission } = useAuth()
  const canApprove = hasPermission('finance.approve')
  const [busy, setBusy] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    return Promise.all([listSupplierInvoices(), listVouchers(), getSuppliers(), getTaxCategories()])
      .then(([b, v, s, t]) => { setBills(b ?? []); setVouchers(v ?? []); setSuppliers(s ?? []); setTaxCats(t ?? []) })
      .catch(() => notify?.('Failed to load payments.', 'error'))
      .finally(() => setLoading(false))
  }, [notify])
  useEffect(() => { load() }, [load])

  async function act(id, fn, label) {
    setBusy(id)
    try { await fn(id); notify?.(label); await load() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Action failed.', 'error') }
    finally { setBusy(null) }
  }
  async function raiseVoucher(billId) {
    setBusy(billId)
    try { await createVoucher({ supplierInvoiceId: billId }); notify?.('Voucher raised — routed for approval by amount.'); await load() }
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed to raise voucher.', 'error') }
    finally { setBusy(null) }
  }

  const exceptions = bills.filter(b => b.matchStatus === 'Exception').length
  const paid = vouchers.filter(v => v.status === 'Paid').length

  async function confirmCancel(reason) {
    const id = cancelling.id
    setCancelling(null)
    setBusy(id)
    try { await cancelSupplierInvoice(id, reason); notify?.('Supplier invoice cancelled; its ledger posting was reversed.'); await load() }
    // The API names the amount already paid and points at the voucher reversal, which is more use
    // than any message this component could invent.
    catch (e) { notify?.(e.response?.data?.message ?? 'Failed to cancel.', 'error') }
    finally { setBusy(null) }
  }

  const billAction = (b) => {
    if (busy === b.id) return <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
    const cancel = cancellability(b, { canApprove })
    const primary = b.status === 'Received'
      ? <Btn size="sm" onClick={() => act(b.id, approveSupplierInvoice, 'Approved & posted to payables.')}>Approve</Btn>
      : b.status === 'Approved' && b.balance > 0
        ? <Btn size="sm" variant="gold" onClick={() => raiseVoucher(b.id)}>Raise Voucher</Btn>
        : null
    if (!primary && !cancel.allowed) return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
    return (
      <span style={{ display: 'inline-flex', gap: 6 }}>
        {primary}
        {cancel.allowed && <Btn size="sm" variant="ghost" onClick={() => setCancelling(b)}>Cancel</Btn>}
      </span>
    )
  }
  const voucherAction = (v) => {
    if (busy === v.id) return <span style={{ color: T.mgrey, fontSize: 12 }}>…</span>
    if (v.status === 'PendingApproval') return <Btn size="sm" onClick={() => act(v.id, approveVoucher, 'Voucher approved.')}>Approve</Btn>
    if (v.status === 'Approved') return <Btn size="sm" variant="green" onClick={() => act(v.id, payVoucher, 'Voucher paid & posted.')}>Pay</Btn>
    return <span style={{ color: T.mgrey, fontSize: 12 }}>—</span>
  }

  if (loading) return <Loading />

  return (
    <>
      <Alert type="info"><strong>Accounts Payable:</strong> supplier invoices post to payables + input VAT on approval; a voucher's amount sets the required approval authority (Staff → Dept Head → FM → CFO → MD); paying a voucher posts Dr Payables / Cr Bank.</Alert>
      <div style={{ ...KPI_GRID, marginBottom: 18 }}>
        <Kpi label="Supplier Invoices" value={bills.length} icon="🧾" />
        <Kpi label="Match Exceptions" value={exceptions} icon="⚠️" variant={exceptions ? 'amber' : undefined} />
        <Kpi label="Vouchers" value={vouchers.length} icon="💳" />
        <Kpi label="Paid" value={paid} icon="✅" variant="green" />
      </div>

      <SectionHeader title="Supplier Invoices" action={<Btn onClick={() => setModal('bill')}>+ New Supplier Invoice</Btn>} />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Ref', 'Supplier', 'Total', 'Balance', 'Match', 'Status', 'Action']}
          empty="No supplier invoices yet."
          rows={bills.map(b => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{b.internalNo}</span>,
            b.supplierName,
            <strong>{fmt.money(b.total, b.currencyCode)}</strong>,
            <span style={{ color: b.balance > 0 ? T.dgrey : T.green }}>{fmt.money(b.balance, b.currencyCode)}</span>,
            <Badge variant={MATCH_VARIANT[b.matchStatus] || 'default'}>{b.matchStatus}</Badge>,
            <span title={b.status === 'Cancelled' && b.cancellationReason ? `Cancelled: ${b.cancellationReason}` : undefined}>
              <Badge variant={BILL_VARIANT[b.status] || 'default'}>{b.status}</Badge>
            </span>,
            billAction(b),
          ])} />
      </Card>

      <div style={{ height: 22 }} />
      <SectionHeader title="Payment Vouchers" sub="Approval enforced by amount (payment authority matrix)." />
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        <DataTable headers={['Voucher', 'Payee', 'Amount', 'Requires', 'Status', 'Action']}
          empty="No vouchers yet."
          rows={vouchers.map(v => [
            <span style={{ fontFamily: T.mono, fontSize: 12 }}>{v.voucherNo}</span>,
            v.payee,
            <strong>{fmt.money(v.amount, v.currencyCode)}</strong>,
            <Badge variant="navy">{v.requiredAuthority}</Badge>,
            <Badge variant={VOUCHER_VARIANT[v.status] || 'default'}>{v.status}</Badge>,
            voucherAction(v),
          ])} />
      </Card>

      {cancelling && (
        <CancelDocumentModal
          doc={{ id: cancelling.id, number: cancelling.internalNo }}
          label="bill"
          posted={cancelling.status !== 'Received'}
          onClose={() => setCancelling(null)}
          onConfirm={confirmCancel}
        />
      )}
      {modal === 'bill' && (
        <SupplierInvoiceModal suppliers={suppliers} taxCategories={taxCats}
          onClose={() => setModal(null)} onCreated={load} notify={notify} />
      )}
    </>
  )
}
