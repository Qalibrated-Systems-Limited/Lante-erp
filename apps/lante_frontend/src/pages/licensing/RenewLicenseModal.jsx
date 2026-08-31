import { useState } from 'react'
import { licensingApi } from '../../api/licensingApi.js'

export default function RenewLicenseModal({ license: lic, onClose, onRenewed }) {
  const currentExpiry = new Date(lic.expiresAt)
  const minDate = new Date(currentExpiry)
  minDate.setDate(minDate.getDate() + 1)

  const defaultNewExpiry = new Date(Math.max(minDate.getTime(), Date.now()))
  defaultNewExpiry.setFullYear(defaultNewExpiry.getFullYear() + 1)

  const [newExpiresAt, setNewExpiresAt] = useState(defaultNewExpiry.toISOString().split('T')[0])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e) => {
    e.preventDefault()
    setSubmitting(true)
    setError('')
    try {
      await licensingApi.renew(lic.id, new Date(newExpiresAt).toISOString())
      onRenewed()
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to renew license.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md">

        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div>
            <h2 className="text-lg font-bold text-zinc-950">Renew License</h2>
            <p className="text-xs text-gray-400 mt-0.5">
              for {lic.customerName || lic.customerId} · {lic.appId}
            </p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="flex items-center justify-between py-2 px-3 bg-gray-50 rounded-lg text-sm">
            <span className="text-gray-500">Current expiry</span>
            <span className="font-medium text-gray-800">{currentExpiry.toLocaleDateString()}</span>
          </div>

          <div>
            <label className="block text-xs font-semibold text-gray-600 mb-1">
              New Expiry Date <span className="text-red-400">*</span>
            </label>
            <input
              type="date"
              value={newExpiresAt}
              min={minDate.toISOString().split('T')[0]}
              onChange={e => setNewExpiresAt(e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
            <p className="text-xs text-gray-400 mt-1">
              The existing license token keeps working — no new key needed.
            </p>
          </div>

          {error && (
            <div className="flex items-center gap-2 bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-2.5 text-sm">
              <svg className="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
              {error}
            </div>
          )}

          <div className="flex gap-3 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 py-2.5 border border-gray-200 text-gray-600 rounded-lg text-sm font-semibold hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex-1 py-2.5 bg-amber-500 hover:bg-amber-600 text-white rounded-lg text-sm font-semibold disabled:opacity-50 transition-colors"
            >
              {submitting ? 'Renewing…' : 'Renew License'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
