import { useState } from 'react'
import { Card, DataTable, Btn, Badge, Loading, Alert } from '../../ui.jsx'
import { useVariationOrders } from '../../../hooks/operations/useVariationOrders.js'
import VariationOrderModal from './VariationOrderModal.jsx'

const STATUS_VARIANT = {
  Draft: 'default', PendingMdApproval: 'amber', PendingClientApproval: 'blue', Approved: 'green', Rejected: 'red',
}
const STATUS_LABEL = {
  Draft: 'Draft', PendingMdApproval: 'MD approval', PendingClientApproval: 'Client approval', Approved: 'Approved', Rejected: 'Rejected',
}
const money = (n) => (Number(n) || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

// O11.3 — variation-orders tab for the project detail page (list + Draft→MD→client workflow).
export default function VariationOrdersTab({ projectId }) {
  const { items, loading, error, canWrite, canApproveMd, save, remove, submit, approveMd, approveClient } = useVariationOrders(projectId)
  const [modal, setModal] = useState(null)   // null | {} (new) | vo (edit)

  const guard = (p) => p.catch(() => {})
  const onDelete = (vo) => { if (window.confirm(`Delete ${vo.number}?`)) guard(remove(vo.id)) }
  const onRejectMd = (vo) => { const c = window.prompt('Reason for rejection:'); if (c != null) guard(approveMd(vo.id, false, c)) }
  const onClient = (vo) => { const name = window.prompt('Client representative who approved:'); if (name) guard(approveClient(vo.id, name)) }

  const headers = ['Number', 'Title', 'Status', 'Total', 'Billable', 'Actions']
  const rows = items.map(vo => [
    <span style={{ fontWeight: 600 }}>{vo.number}</span>,
    vo.title,
    <Badge variant={STATUS_VARIANT[vo.status] ?? 'default'}>{STATUS_LABEL[vo.status] ?? vo.status}</Badge>,
    money(vo.totalAmount),
    vo.isBillable ? <Badge variant="blue">Billable</Badge> : <Badge variant="default">Internal</Badge>,
    <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
      {vo.status === 'Draft' && canWrite && <>
        <Btn size="sm" variant="outline" onClick={() => setModal(vo)}>Edit</Btn>
        <Btn size="sm" variant="primary" onClick={() => guard(submit(vo.id))}>Submit</Btn>
        <Btn size="sm" variant="danger" onClick={() => onDelete(vo)}>Del</Btn>
      </>}
      {vo.status === 'PendingMdApproval' && canApproveMd && <>
        <Btn size="sm" variant="green" onClick={() => guard(approveMd(vo.id, true))}>Approve (MD)</Btn>
        <Btn size="sm" variant="danger" onClick={() => onRejectMd(vo)}>Reject</Btn>
      </>}
      {vo.status === 'PendingClientApproval' && canWrite &&
        <Btn size="sm" variant="green" onClick={() => onClient(vo)}>Record Client Approval</Btn>}
      {vo.status === 'Approved' && <Badge variant="green">Applied</Badge>}
    </div>,
  ])

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
        <p style={{ fontSize: 13, color: '#5b6b7c', margin: 0 }}>Approved variations grow the contract value and planned budget.</p>
        {canWrite && <Btn variant="primary" onClick={() => setModal({})}>+ New Variation</Btn>}
      </div>
      {error && <Alert type="error">{error}</Alert>}
      <Card style={{ padding: 0 }}>
        {loading ? <Loading /> : <DataTable headers={headers} rows={rows} empty="No variation orders on this project." />}
      </Card>

      {modal && (
        <VariationOrderModal
          vo={modal.id ? modal : null}
          onClose={() => setModal(null)}
          onSave={save}
        />
      )}
    </div>
  )
}
