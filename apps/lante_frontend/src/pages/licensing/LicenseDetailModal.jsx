import { featureLabel } from '../../utils/featureCatalogue.js'

export default function LicenseDetailModal({ license: lic, onClose, onRevoke, onRenew }) {
  const statusColor = lic.revoked ? 'red' : lic.isExpired ? 'gray' : lic.daysUntilExpiry <= 30 ? 'orange' : 'green'
  const statusLabel = lic.revoked ? 'Revoked' : lic.isExpired ? 'Expired' : lic.daysUntilExpiry <= 30 ? 'Expiring Soon' : 'Active'

  const Row = ({ label, value }) => (
    <div className="flex items-start justify-between py-2.5 border-b border-gray-50">
      <span className="text-xs font-semibold text-gray-500 w-36 shrink-0">{label}</span>
      <span className="text-sm text-gray-800 text-right break-all">{value || '—'}</span>
    </div>
  )

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">

        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <h2 className="text-lg font-bold text-gray-900">License Detail</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl font-bold">×</button>
        </div>

        <div className="px-6 py-4 space-y-0">
          <Row label="Status" value={
            <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-bold
              ${statusColor === 'green'  ? 'bg-green-100 text-green-700' : ''}
              ${statusColor === 'orange' ? 'bg-orange-100 text-orange-700' : ''}
              ${statusColor === 'red'    ? 'bg-red-100 text-red-700' : ''}
              ${statusColor === 'gray'   ? 'bg-gray-100 text-gray-600' : ''}
            `}>{statusLabel}</span>
          } />
          <Row label="Customer ID"   value={lic.customerId} />
          <Row label="Customer Name" value={lic.customerName} />
          <Row label="Application"   value={lic.appId} />
          <Row label="Features" value={
            <div className="flex flex-wrap gap-1 justify-end">
              {(lic.features || []).map(f => (
                <span key={f} className="text-xs bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded font-medium">
                  {featureLabel(f)}
                </span>
              ))}
            </div>
          } />
          <Row label="Issued"        value={new Date(lic.issuedAt).toLocaleString()} />
          <Row label="Expires"       value={`${new Date(lic.expiresAt).toLocaleString()}${lic.isActive ? ` (${lic.daysUntilExpiry}d left)` : ''}`} />
          <Row label="Machine Bound" value={lic.machineId || 'No — floating license'} />
          <Row label="Last Check-in" value={lic.lastSeen ? new Date(lic.lastSeen).toLocaleString() : null} />
          <Row label="Last Machine"  value={lic.lastMachineId} />
          {lic.revoked && <Row label="Revoke Reason" value={lic.revokeReason} />}
          {lic.notes && <Row label="Notes" value={lic.notes} />}
        </div>

        {/* Token */}
        <div className="px-6 pb-2">
          <p className="text-xs font-semibold text-gray-500 mb-1">LICENSE TOKEN</p>
          <div className="bg-gray-50 border border-gray-200 rounded-lg p-3 relative">
            <textarea
              readOnly
              value={lic.token}
              rows={3}
              className="w-full font-mono text-xs text-gray-700 bg-transparent resize-none outline-none"
            />
            <button
              onClick={() => navigator.clipboard.writeText(lic.token).then(() => alert('Copied!'))}
              className="absolute top-2 right-2 text-xs text-indigo-600 hover:underline"
            >
              Copy
            </button>
          </div>
        </div>

        <div className="px-6 py-4 flex gap-3">
          <button onClick={onClose}
            className="flex-1 py-2.5 border border-gray-200 text-gray-600 rounded-lg text-sm font-medium hover:bg-gray-50">
            Close
          </button>
          {!lic.revoked && (
            <button onClick={onRenew}
              className="flex-1 py-2.5 bg-amber-500 text-white rounded-lg text-sm font-medium hover:bg-amber-600">
              Renew License
            </button>
          )}
          {lic.isActive && (
            <button onClick={onRevoke}
              className="flex-1 py-2.5 bg-red-600 text-white rounded-lg text-sm font-medium hover:bg-red-700">
              Revoke License
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
