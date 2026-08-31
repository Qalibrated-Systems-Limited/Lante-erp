import { useState } from 'react'
import { licensingApi } from '../../api/licensingApi.js'
import { FEATURE_CATALOGUE } from '../../utils/featureCatalogue.js'

// features prop is { value, label, group }[] loaded from backend metadata.
// Falls back to local catalogue if the API hasn't responded yet.
export default function IssueLicenseModal({ appIds, features, onClose, onIssued }) {
  const resolvedFeatures = features?.length ? features : FEATURE_CATALOGUE
  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)

  // Derive groups for rendering
  const groups = resolvedFeatures.reduce((acc, f) => {
    if (!acc[f.group]) acc[f.group] = []
    acc[f.group].push(f)
    return acc
  }, {})

  const [form, setForm] = useState({
    customerId:   '',
    customerName: '',
    appId:        appIds[0],
    features:     [],
    expiresAt:    tomorrow.toISOString().split('T')[0],
    machineId:    '',
    notes:        '',
  })

  const [submitting, setSubmitting] = useState(false)
  const [error, setError]           = useState('')
  const [issued, setIssued]         = useState(null)
  const [copied, setCopied]         = useState(false)

  const setField = (key, val) => setForm(f => ({ ...f, [key]: val }))

  const toggleFeature = (f) => {
    setForm(prev => ({
      ...prev,
      features: prev.features.includes(f)
        ? prev.features.filter(x => x !== f)
        : [...prev.features, f],
    }))
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (!form.customerId.trim()) return setError('Customer ID is required.')
    if (!form.features.length)   return setError('Select at least one feature.')
    setSubmitting(true)
    setError('')
    try {
      const res = await licensingApi.issue({
        ...form,
        machineId: form.machineId.trim() || null,
        expiresAt: new Date(form.expiresAt).toISOString(),
      })
      setIssued(res.data?.data)
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to issue license.')
    } finally {
      setSubmitting(false)
    }
  }

  const handleCopy = () => {
    navigator.clipboard.writeText(issued.token).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    })
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg max-h-[90vh] overflow-y-auto">

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div>
            <h2 className="text-lg font-bold text-zinc-950">Issue New License</h2>
            <p className="text-xs text-gray-400 mt-0.5">Fill in the details below to generate a signed JWT license</p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {issued ? (
          /* Success state */
          <div className="p-6 space-y-4">
            <div className="flex items-center gap-3 p-4 bg-green-50 border border-green-200 rounded-xl">
              <div className="w-9 h-9 rounded-full bg-green-100 flex items-center justify-center flex-shrink-0">
                <svg className="w-5 h-5 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              </div>
              <div>
                <p className="text-sm font-semibold text-green-800">License issued successfully</p>
                <p className="text-xs text-green-600">for {issued.customerName || issued.customerId} · {issued.appId}</p>
              </div>
            </div>

            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">License Token</p>
              <p className="text-xs text-gray-500 mb-2">Copy and send this token to the customer — they paste it in the app to activate.</p>
              <div className="bg-gray-50 border border-gray-200 rounded-xl p-3 relative">
                <textarea
                  readOnly
                  value={issued.token}
                  rows={4}
                  className="w-full font-mono text-xs text-gray-700 bg-transparent resize-none outline-none"
                />
              </div>
            </div>

            <div className="flex gap-3">
              <button
                onClick={handleCopy}
                className={`flex-1 py-2.5 rounded-lg text-sm font-semibold transition-colors ${
                  copied
                    ? 'bg-green-100 text-green-700'
                    : 'bg-zinc-950 hover:bg-zinc-950 text-white'
                }`}
              >
                {copied ? '✓ Copied!' : 'Copy Token'}
              </button>
              <button
                onClick={onIssued}
                className="flex-1 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg text-sm font-semibold transition-colors"
              >
                Done
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="p-6 space-y-4">

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Customer ID <span className="text-red-400">*</span></label>
                <input
                  value={form.customerId}
                  onChange={e => setField('customerId', e.target.value)}
                  placeholder="KTDA-001"
                  className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Customer Name</label>
                <input
                  value={form.customerName}
                  onChange={e => setField('customerName', e.target.value)}
                  placeholder="KTDA Farmers"
                  className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-transparent"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1">Application <span className="text-red-400">*</span></label>
              <select
                value={form.appId}
                onChange={e => setField('appId', e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              >
                {appIds.map(id => <option key={id} value={id}>{id}</option>)}
              </select>
            </div>

            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="block text-xs font-semibold text-gray-600">Features <span className="text-red-400">*</span></label>
                <span className="text-xs text-gray-400">{form.features.length} selected</span>
              </div>
              <div className="space-y-3 max-h-52 overflow-y-auto pr-1">
                {Object.entries(groups).map(([group, items]) => (
                  <div key={group}>
                    <p className="text-[10px] font-bold text-gray-400 uppercase tracking-wider mb-1.5">{group}</p>
                    <div className="flex flex-wrap gap-1.5">
                      {items.map(f => (
                        <label
                          key={f.value}
                          className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg border cursor-pointer text-xs transition-colors ${
                            form.features.includes(f.value)
                              ? 'border-amber-400 bg-amber-50 text-amber-800 font-semibold'
                              : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                          }`}
                        >
                          <input
                            type="checkbox"
                            checked={form.features.includes(f.value)}
                            onChange={() => toggleFeature(f.value)}
                            className="hidden"
                          />
                          {f.label}
                        </label>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1">Expires At <span className="text-red-400">*</span></label>
              <input
                type="date"
                value={form.expiresAt}
                min={tomorrow.toISOString().split('T')[0]}
                onChange={e => setField('expiresAt', e.target.value)}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1">
                Machine ID{' '}
                <span className="text-gray-400 font-normal">(optional — locks to one device)</span>
              </label>
              <input
                value={form.machineId}
                onChange={e => setField('machineId', e.target.value)}
                placeholder="Leave blank for floating license"
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-amber-400"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-600 mb-1">Notes</label>
              <textarea
                value={form.notes}
                onChange={e => setField('notes', e.target.value)}
                rows={2}
                placeholder="Internal notes (not included in token)"
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-400"
              />
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
                {submitting ? 'Issuing…' : 'Issue License'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
