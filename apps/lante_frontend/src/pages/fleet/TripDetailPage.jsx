import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Truck, AlertTriangle } from 'lucide-react'
import SinglePhotoPicker from '../../components/SinglePhotoPicker.jsx'
import api from '../../api/axios.js'
import { useAuth } from '../../context/AuthContext.jsx'
import { INCIDENT_TYPES, SEVERITIES } from '../hse/constants.js'

const STATUS_LABEL = {
  Pending:    'PENDING',
  InProgress: 'IN PROGRESS',
  Completed:  'COMPLETED',
  Cancelled:  'CANCELLED',
}

const STATUS_BADGE_COLOR = {
  Pending:    'bg-blue-100 text-blue-700 border-blue-200',
  InProgress: 'bg-amber-100 text-amber-800 border-amber-200',
  Completed:  'bg-green-100 text-green-700 border-green-200',
  Cancelled:  'bg-gray-100 text-gray-600 border-gray-200',
}

const API_BASE = import.meta.env.VITE_API_URL || 'https://kmk.support.qalibrated.co.ke'

// Rewrites URLs that got baked with fleet-service's internal Docker hostname
// (only resolvable inside the container network) to the public gateway URL.
function fixUrl(url) {
  if (!url) return null
  return url.replace(/^https?:\/\/[^/]+:8080/, API_BASE)
}

function fmt(n) {
  return new Intl.NumberFormat('en-KE', { style: 'currency', currency: 'KES', maximumFractionDigits: 0 }).format(n ?? 0)
}

// Parses the M-Pesa "sent to <bank> account <name> <number> ... M-PESA Ref <ref>" confirmation
// SMS format used for driver bank deposits. Mirrors mpesa_sms_parser.dart in the mobile app.
function parseMpesaSms(text) {
  const re = /Ksh\s*([\d,]+\.\d{2})\s+sent to\s+(\w+)\s+account\s+(.+?)\s+(\d+)\s+has been received on\s+(\d{1,2}\/\d{1,2}\/\d{4})\s+at\s+(\d{1,2}:\d{2}\s*[AP]M).*?M-PESA\s*Ref\s+(\w+)/is
  const m = text.trim().match(re)
  if (!m) return null
  const [, amountStr, bankName, accountNameRaw, accountNumber, dateStr, timeStr, mpesaReference] = m
  const [d, mo, y] = dateStr.split('/').map(Number)
  const timeMatch = timeStr.trim().match(/(\d{1,2}):(\d{2})\s*([AP]M)/i)
  let transactionDate = null
  if (timeMatch) {
    let hh = Number(timeMatch[1]) % 12
    if (timeMatch[3].toUpperCase() === 'PM') hh += 12
    transactionDate = new Date(y, mo - 1, d, hh, Number(timeMatch[2]))
  }
  return {
    amount: parseFloat(amountStr.replace(/,/g, '')),
    bankName,
    accountName: accountNameRaw.trim(),
    accountNumber,
    transactionDate,
    mpesaReference,
  }
}

function timeAgo(dt) {
  if (!dt) return ''
  const diff = Date.now() - new Date(dt).getTime()
  const m = Math.floor(diff / 60000)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  return `${Math.floor(h / 24)}d ago`
}

export default function TripDetailPage() {
  const { id }   = useParams()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const canViewMileage = hasPermission('fleet.viewMileage')

  const [trip, setTrip]             = useState(null)
  const [loading, setLoading]       = useState(true)
  const [error, setError]           = useState('')
  const [working, setWorking]       = useState('')

  // Expenses
  const [expenses, setExpenses]           = useState([])
  const [loadingExpenses, setLoadingExpenses] = useState(false)
  const [showExpenseForm, setShowExpenseForm] = useState(false)
  const [expenseForm, setExpenseForm]     = useState({ description: '', amount: '' })
  const [receiptPhoto, setReceiptPhoto]   = useState(null)
  const [savingExpense, setSavingExpense] = useState(false)
  const [expenseError, setExpenseError]  = useState('')

  // Bank Deposits (driver banking / M-Pesa)
  const [deposits, setDeposits]           = useState([])
  const [loadingDeposits, setLoadingDeposits] = useState(false)
  const [showDepositForm, setShowDepositForm] = useState(false)
  const [depositSms, setDepositSms]       = useState('')
  const [depositScreenshot, setDepositScreenshot] = useState(null)
  const [savingDeposit, setSavingDeposit] = useState(false)
  const [depositError, setDepositError]   = useState('')
  const [expandedDepositId, setExpandedDepositId] = useState(null)

  const [driverName, setDriverName]       = useState(null)
  const [truckLabel, setTruckLabel]       = useState(null)
  const [showDeleteModal, setShowDeleteModal] = useState(false)

  // Complete Trip
  const [showCompleteModal, setShowCompleteModal] = useState(false)
  const [completeForm, setCompleteForm] = useState({ endMileage: '', odometerEndPhoto: null })
  const [completeError, setCompleteError] = useState('')
  const [completing, setCompleting] = useState(false)

  // Report Incident — links to the HSE Incidents module (POST /api/v1/hse-incidents directly;
  // no HSE code is touched, this only calls the same endpoint its own UI uses).
  const [showIncidentModal, setShowIncidentModal] = useState(false)
  const [incidentForm, setIncidentForm] = useState({ type: INCIDENT_TYPES[0], severity: SEVERITIES[0], description: '' })
  const [incidentError, setIncidentError] = useState('')
  const [savingIncident, setSavingIncident] = useState(false)

  useEffect(() => {
    api.get(`/api/v1/trips/${id}`)
      .then(res => setTrip(res.data?.data))
      .catch(() => setError('Trip not found.'))
      .finally(() => setLoading(false))
  }, [id])

  useEffect(() => {
    if (!id) return
    setLoadingExpenses(true)
    api.get('/api/v1/expenses', { params: { tripId: id } })
      .then(res => setExpenses(res.data?.data ?? []))
      .catch(() => {})
      .finally(() => setLoadingExpenses(false))
  }, [id])

  useEffect(() => {
    if (!id) return
    setLoadingDeposits(true)
    api.get('/api/v1/tripdeposits', { params: { tripId: id } })
      .then(res => setDeposits(res.data?.data ?? []))
      .catch(() => {})
      .finally(() => setLoadingDeposits(false))
  }, [id])

  // Resolve driver name and truck label from IDs once trip loads
  useEffect(() => {
    if (!trip) return

    if (trip.driverId) {
      api.get(`/api/v1/DriverProfiles/driver/${trip.driverId}/current`)
        .then(res => {
          const p = res.data?.data
          setDriverName(p?.fullName ?? null)
        })
        .catch(() => {})
    }

    if (trip.truckId) {
      api.get(`/api/v1/trucks/${trip.truckId}`)
        .then(res => {
          const t = res.data?.data
          if (t) setTruckLabel(`${t.licensePlate} — ${t.model}`)
        })
        .catch(() => {})
    }
  }, [trip])

  async function action(type) {
    setWorking(type)
    setError('')
    try {
      const res = await api.post(`/api/v1/trips/${id}/${type}`)
      setTrip(res.data?.data)
    } catch (err) {
      setError(err.response?.data?.message ?? `Failed to ${type} trip.`)
    } finally {
      setWorking('')
    }
  }

  function openCompleteModal() {
    setCompleteForm({ endMileage: '', odometerEndPhoto: null })
    setCompleteError('')
    setShowCompleteModal(true)
  }

  async function handleComplete(e) {
    e.preventDefault()
    setCompleteError('')
    const endMileage = parseFloat(completeForm.endMileage)
    if (completeForm.endMileage === '' || Number.isNaN(endMileage)) {
      setCompleteError('End mileage is required.')
      return
    }
    if (trip.startMileage != null && endMileage < Number(trip.startMileage)) {
      setCompleteError(`End mileage can't be less than the start mileage (${Number(trip.startMileage).toLocaleString()} km).`)
      return
    }
    if (trip.startMileage != null && endMileage - Number(trip.startMileage) > 2000) {
      setCompleteError(`End mileage is ${(endMileage - Number(trip.startMileage)).toLocaleString()} km above the start mileage — that's more than the 2,000 km sanity limit per entry. Double-check the value.`)
      return
    }
    if (!completeForm.odometerEndPhoto) {
      setCompleteError('An odometer photo is required.')
      return
    }
    setCompleting(true)
    try {
      const fd = new FormData()
      fd.append('EndMileage', completeForm.endMileage)
      if (completeForm.odometerEndPhoto) fd.append('odometerEndPhoto', completeForm.odometerEndPhoto)
      const res = await api.post(`/api/v1/trips/${id}/complete`, fd)
      setTrip(res.data?.data)
      setShowCompleteModal(false)
    } catch (err) {
      setCompleteError(err.response?.data?.message ?? 'Failed to complete trip.')
    } finally {
      setCompleting(false)
    }
  }

  function openIncidentModal() {
    setIncidentError('')
    setIncidentForm({
      type: INCIDENT_TYPES[0],
      severity: SEVERITIES[0],
      description: `Truck ${truckLabel ?? trip.truckId}, Trip ${trip.startLocation} → ${trip.endLocation}: `,
    })
    setShowIncidentModal(true)
  }

  async function handleReportIncident(e) {
    e.preventDefault()
    setIncidentError('')
    if (!incidentForm.description.trim()) {
      setIncidentError('Description is required.')
      return
    }
    setSavingIncident(true)
    try {
      await api.post('/api/v1/hse-incidents', {
        siteId: trip.startLocation || 'Fleet',
        siteName: trip.startLocation || 'Fleet',
        type: INCIDENT_TYPES.indexOf(incidentForm.type),
        severity: SEVERITIES.indexOf(incidentForm.severity),
        occurredAt: new Date().toISOString(),
        description: incidentForm.description.trim(),
        isEnvironmental: false,
      })
      setShowIncidentModal(false)
    } catch (err) {
      setIncidentError(err.response?.data?.message ?? 'Failed to report incident.')
    } finally {
      setSavingIncident(false)
    }
  }

  async function handleDelete() {
    setShowDeleteModal(false)
    setWorking('delete')
    setError('')
    try {
      await api.delete(`/api/v1/trips/${id}`)
      navigate('/modules/fleet/trips')
    } catch (err) {
      setError(err.response?.data?.message ?? 'Failed to delete trip.')
      setWorking('')
    }
  }

  async function addExpense(e) {
    e.preventDefault()
    setExpenseError('')
    if (!expenseForm.description || !expenseForm.amount) {
      setExpenseError('Description and amount are required.')
      return
    }
    setSavingExpense(true)
    try {
      const fd = new FormData()
      fd.append('TripId', id)
      fd.append('Description', expenseForm.description)
      fd.append('Amount', expenseForm.amount)
      if (receiptPhoto) fd.append('image', receiptPhoto)
      const res = await api.post('/api/v1/expenses', fd)
      setExpenses(prev => [res.data?.data, ...prev])
      setExpenseForm({ description: '', amount: '' })
      setReceiptPhoto(null)
      setShowExpenseForm(false)
      // Recalculate trip cost
      const updated = await api.post(`/api/v1/trips/${id}/recalculate-cost`)
      setTrip(updated.data?.data)
    } catch (err) {
      setExpenseError(err.response?.data?.message ?? 'Failed to add expense.')
    } finally {
      setSavingExpense(false)
    }
  }

  async function addDeposit(e) {
    e.preventDefault()
    setDepositError('')
    const parsed = parseMpesaSms(depositSms)
    if (!depositSms.trim()) {
      setDepositError('Paste the M-Pesa message first.')
      return
    }
    if (!parsed?.mpesaReference) {
      setDepositError('Could not find an M-Pesa Ref in this message.')
      return
    }
    if (!parsed.amount) {
      setDepositError('Could not detect the amount — check the message text.')
      return
    }
    setSavingDeposit(true)
    try {
      const fd = new FormData()
      fd.append('TripId', id)
      fd.append('Amount', parsed.amount)
      if (parsed.bankName) fd.append('BankName', parsed.bankName)
      if (parsed.accountName) fd.append('AccountName', parsed.accountName)
      if (parsed.accountNumber) fd.append('AccountNumber', parsed.accountNumber)
      fd.append('TransactionDate', (parsed.transactionDate ?? new Date()).toISOString())
      fd.append('MpesaReference', parsed.mpesaReference)
      fd.append('RawSmsText', depositSms.trim())
      if (depositScreenshot) fd.append('screenshot', depositScreenshot)
      const res = await api.post('/api/v1/tripdeposits', fd)
      setDeposits(prev => [res.data?.data, ...prev])
      setDepositSms('')
      setDepositScreenshot(null)
      setShowDepositForm(false)
    } catch (err) {
      setDepositError(err.response?.data?.message ?? 'Failed to record banking.')
    } finally {
      setSavingDeposit(false)
    }
  }

  if (loading) {
    return (
      <>
        <div className="max-w-2xl mx-auto px-4 sm:px-6 py-5 space-y-4">
          <div className="h-24 rounded-2xl bg-gray-200 animate-pulse" />
          <div className="h-32 rounded-2xl bg-gray-100 animate-pulse" />
        </div>
      </>
    )
  }

  if (!trip) {
    return (
      <>
        <div className="max-w-2xl mx-auto px-4 py-6">
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-4 text-sm">{error || 'Not found.'}</div>
        </div>
      </>
    )
  }

  const badgeCls  = STATUS_BADGE_COLOR[trip.status] ?? STATUS_BADGE_COLOR.Pending
  const canCancel = trip.status === 'Pending' || trip.status === 'InProgress'

  return (
    <>
      <main className="w-full px-4 sm:px-6 py-6 space-y-4">

        {/* Header bar */}
        <div className="rounded-2xl bg-gradient-to-br from-navy to-navy-dark px-5 py-4 text-white flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button
              onClick={() => navigate('/modules/fleet/trips')}
              className="p-1.5 rounded-lg bg-white/20 hover:bg-white/30 transition-colors flex-shrink-0"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
              </svg>
            </button>
            <div>
              <h1 className="text-base font-extrabold leading-tight text-white flex items-center gap-1.5">
                <Truck size={16} /> {trip.startLocation} → {trip.endLocation}
              </h1>
              <div className="flex items-center gap-3 mt-0.5 text-white/70 text-xs">
                <span>{timeAgo(trip.createdAt) || new Date(trip.date).toLocaleDateString()}</span>
                {trip.tripTypeName && <span className="px-2 py-0.5 rounded-full bg-white/20 font-medium">{trip.tripTypeName}</span>}
              </div>
            </div>
          </div>
          <span className={`px-3 py-1 rounded-full text-xs font-extrabold tracking-widest border flex-shrink-0 ${badgeCls}`}>
            {STATUS_LABEL[trip.status] ?? trip.status}
          </span>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-2xl px-5 py-3 text-sm">{error}</div>
        )}

        {/* Two-column layout */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">

          {/* Left column — info cards */}
          <div className="space-y-4">

            {/* Trip details */}
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Trip Details</p>
              <div className="space-y-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-gray-400">Date</span>
                  <span className="font-medium text-gray-800">{new Date(trip.date).toLocaleDateString()}</span>
                </div>
                <div className="flex justify-between gap-2">
                  <span className="text-gray-400 flex-shrink-0">Driver</span>
                  <span className="font-medium text-gray-800 text-right">{driverName ?? <span className="font-mono text-xs text-gray-400">{trip.driverId?.slice(0,8)}…</span>}</span>
                </div>
                <div className="flex justify-between gap-2">
                  <span className="text-gray-400 flex-shrink-0">Truck</span>
                  <span className="font-medium text-gray-800 text-right">{truckLabel ?? <span className="font-mono text-xs text-gray-400">{trip.truckId?.slice(0,8)}…</span>}</span>
                </div>
              </div>
            </div>

            {/* Mileage */}
            {(trip.startMileage || trip.endMileage || trip.totalMileage != null) && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
                <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Mileage</p>
                <div className="space-y-2 text-sm">
                  {canViewMileage && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">Start</span>
                      <span className="font-semibold text-gray-800">{trip.startMileage ?? '—'} km</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-gray-400">End</span>
                    <span className="font-semibold text-gray-800">{trip.endMileage ?? '—'} km</span>
                  </div>
                  {trip.totalMileage != null && (
                    <div className="flex justify-between pt-2 border-t border-gray-100">
                      <span className="text-gray-500 font-medium">Total</span>
                      <span className="font-extrabold text-zinc-900">{trip.totalMileage} km</span>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Financials */}
            {(trip.totalCost > 0 || trip.revenue > 0 || trip.profit !== 0) && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
                <p className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Financials</p>
                <div className="space-y-2 text-sm">
                  {trip.revenue > 0 && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">Revenue</span>
                      <span className="font-semibold text-green-700">{fmt(trip.revenue)}</span>
                    </div>
                  )}
                  {trip.totalCost > 0 && (
                    <div className="flex justify-between">
                      <span className="text-gray-400">Cost</span>
                      <span className="font-semibold text-gray-800">{fmt(trip.totalCost)}</span>
                    </div>
                  )}
                  {(trip.revenue > 0 || trip.profit !== 0) && (
                    <div className="flex justify-between pt-2 border-t border-gray-100">
                      <span className="text-gray-500 font-medium">Profit</span>
                      <span className={`font-extrabold ${trip.profit >= 0 ? 'text-green-700' : 'text-red-600'}`}>
                        {fmt(trip.profit ?? (trip.revenue - trip.totalCost))}
                      </span>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* Linked ticket */}
            {trip.linkedTicketId && (
              <div className="bg-blue-50 border border-blue-200 rounded-2xl px-4 py-3">
                <p className="text-xs text-blue-500 font-semibold">Linked Ticket</p>
                <p className="text-xs font-mono text-blue-800 mt-0.5 break-all">{trip.linkedTicketId}</p>
              </div>
            )}

            {/* Actions */}
            <div className="space-y-2">
              {trip.status === 'Pending' && (
                <button
                  onClick={() => action('start')}
                  disabled={!!working}
                  className="w-full py-2.5 bg-amber-500 hover:bg-amber-600 text-white text-sm font-semibold rounded-2xl transition-colors disabled:opacity-50"
                >
                  {working === 'start' ? <Spinner /> : 'Start Trip'}
                </button>
              )}
              {trip.status === 'InProgress' && (
                <button
                  onClick={openCompleteModal}
                  disabled={!!working}
                  className="w-full py-2.5 bg-green-600 hover:bg-green-700 text-white text-sm font-semibold rounded-2xl transition-colors disabled:opacity-50"
                >
                  Complete Trip
                </button>
              )}
              {canCancel && (
                <button
                  onClick={() => { if (window.confirm('Cancel this trip?')) action('cancel') }}
                  disabled={!!working}
                  className="w-full py-2.5 border-2 border-red-300 text-red-600 hover:bg-red-50 text-sm font-semibold rounded-2xl transition-colors disabled:opacity-50"
                >
                  {working === 'cancel' ? <Spinner /> : 'Cancel Trip'}
                </button>
              )}
              {trip.status === 'Completed' && (
                <button
                  onClick={openIncidentModal}
                  className="w-full py-2.5 border-2 border-amber-300 text-amber-700 hover:bg-amber-50 text-sm font-semibold rounded-2xl transition-colors inline-flex items-center justify-center gap-1.5"
                >
                  <AlertTriangle size={16} /> Report Incident
                </button>
              )}
              <button
                onClick={() => setShowDeleteModal(true)}
                disabled={!!working}
                className="w-full py-2.5 border border-gray-200 text-gray-400 hover:border-red-200 hover:text-red-500 hover:bg-red-50 text-sm font-medium rounded-2xl transition-colors disabled:opacity-50"
              >
                {working === 'delete' ? <Spinner /> : 'Delete Trip'}
              </button>
            </div>
          </div>

          {/* Right column — expenses & photos */}
          <div className="lg:col-span-2 space-y-4">

            {/* Expenses */}
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-base font-bold text-gray-900">
                  Expenses {!loadingExpenses && `(${expenses.length})`}
                </h3>
                {(trip.status === 'InProgress' || trip.status === 'Pending') && (
                  <button
                    onClick={() => { setShowExpenseForm(v => !v); setExpenseError('') }}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-navy hover:bg-navy-dark text-white text-xs font-bold rounded-lg transition-colors"
                  >
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Add Expense
                  </button>
                )}
              </div>

              {showExpenseForm && (
                <form onSubmit={addExpense} className="bg-gray-50 rounded-xl p-4 mb-4 space-y-3 border border-gray-100">
                  <h4 className="text-sm font-semibold text-gray-700">New Expense</h4>
                  {expenseError && (
                    <div className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{expenseError}</div>
                  )}
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Description *</label>
                      <input
                        type="text"
                        value={expenseForm.description}
                        onChange={e => setExpenseForm(f => ({ ...f, description: e.target.value }))}
                        placeholder="e.g. Fuel refill, Toll fee"
                        className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                        required
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-gray-500 mb-1">Amount (KES) *</label>
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        value={expenseForm.amount}
                        onChange={e => setExpenseForm(f => ({ ...f, amount: e.target.value }))}
                        placeholder="e.g. 1500"
                        className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                        required
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Receipt <span className="text-gray-400 font-normal">(optional)</span></label>
                    <SinglePhotoPicker file={receiptPhoto} onChange={setReceiptPhoto} />
                  </div>
                  <div className="flex gap-2">
                    <button type="submit" disabled={savingExpense}
                      className="flex-1 py-2.5 bg-navy hover:bg-navy-dark text-white text-sm font-bold rounded-lg disabled:opacity-50 transition-colors">
                      {savingExpense ? 'Saving…' : 'Add Expense'}
                    </button>
                    <button type="button" onClick={() => { setShowExpenseForm(false); setExpenseError(''); setReceiptPhoto(null) }}
                      className="px-4 py-2.5 border border-gray-200 text-gray-600 rounded-lg hover:bg-gray-50 text-sm">
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {loadingExpenses ? (
                <div className="space-y-2">
                  {[1,2].map(i => <div key={i} className="h-10 bg-gray-100 rounded-lg animate-pulse" />)}
                </div>
              ) : expenses.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-6">No expenses recorded for this trip.</p>
              ) : (
                <div className="space-y-2">
                  {expenses.map(exp => (
                    <div key={exp.id} className="bg-gray-50 rounded-xl border border-gray-100 overflow-hidden">
                      <div className="flex items-center justify-between px-3 py-2.5">
                        <div>
                          <p className="text-sm font-medium text-gray-800">{exp.description}</p>
                          <p className="text-xs text-gray-400">{new Date(exp.createdAt).toLocaleDateString()}</p>
                        </div>
                        <div className="flex items-center gap-3">
                          {exp.receiptPhotoUrl && (
                            <a href={exp.receiptPhotoUrl} target="_blank" rel="noreferrer"
                              className="inline-flex items-center gap-1 text-xs text-navy hover:text-navy-dark font-semibold">
                              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                              </svg>
                              Receipt
                            </a>
                          )}
                          <span className="text-sm font-bold text-gray-900">{fmt(exp.amount)}</span>
                        </div>
                      </div>
                      {exp.receiptPhotoUrl && (
                        <a href={exp.receiptPhotoUrl} target="_blank" rel="noreferrer" className="block">
                          <img src={exp.receiptPhotoUrl} alt="Receipt" className="w-full max-h-40 object-cover border-t border-gray-100 hover:opacity-90 transition-opacity" />
                        </a>
                      )}
                    </div>
                  ))}
                  <div className="flex items-center justify-between px-3 py-2 border-t border-gray-200 mt-1">
                    <span className="text-sm font-semibold text-gray-600">Total Expenses</span>
                    <span className="text-base font-extrabold text-gray-900">
                      {fmt(expenses.reduce((s, e) => s + Number(e.amount), 0))}
                    </span>
                  </div>
                </div>
              )}
            </div>

            {/* Bank Deposits */}
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-base font-bold text-gray-900">
                  Bank Deposits {!loadingDeposits && `(${deposits.length})`}
                </h3>
                {trip.status === 'Completed' && (
                  <button
                    onClick={() => { setShowDepositForm(v => !v); setDepositError('') }}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-green-600 hover:bg-green-700 text-white text-xs font-bold rounded-lg transition-colors"
                  >
                    <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                    </svg>
                    Record Banking
                  </button>
                )}
              </div>

              {showDepositForm && (
                <form onSubmit={addDeposit} className="bg-gray-50 rounded-xl p-4 mb-4 space-y-3 border border-gray-100">
                  <h4 className="text-sm font-semibold text-gray-700">Record Banking</h4>
                  {depositError && (
                    <div className="text-xs text-red-600 bg-red-50 rounded-lg px-3 py-2">{depositError}</div>
                  )}
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">M-Pesa message *</label>
                    <textarea
                      value={depositSms}
                      onChange={e => setDepositSms(e.target.value)}
                      rows={4}
                      placeholder="Ksh 23600.00 sent to KCB account ... M-PESA Ref ..."
                      className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
                      required
                    />
                  </div>
                  {(() => {
                    const parsed = parseMpesaSms(depositSms)
                    if (!parsed) return null
                    return (
                      <div className="bg-white rounded-lg border border-gray-200 px-3 py-2 text-xs space-y-1">
                        <div className="flex justify-between"><span className="text-gray-400">Amount</span><span className="font-semibold text-gray-800">{parsed.amount ? fmt(parsed.amount) : '—'}</span></div>
                        {parsed.bankName && <div className="flex justify-between"><span className="text-gray-400">Bank</span><span className="font-semibold text-gray-800">{parsed.bankName}</span></div>}
                        {parsed.mpesaReference && <div className="flex justify-between"><span className="text-gray-400">M-Pesa Ref</span><span className="font-semibold text-gray-800">{parsed.mpesaReference}</span></div>}
                      </div>
                    )
                  })()}
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Screenshot <span className="text-gray-400 font-normal">(optional)</span></label>
                    <SinglePhotoPicker file={depositScreenshot} onChange={setDepositScreenshot} />
                  </div>
                  <div className="flex gap-2">
                    <button type="submit" disabled={savingDeposit}
                      className="flex-1 py-2.5 bg-green-600 hover:bg-green-700 text-white text-sm font-bold rounded-lg disabled:opacity-50 transition-colors">
                      {savingDeposit ? 'Saving…' : 'Record Banking'}
                    </button>
                    <button type="button" onClick={() => { setShowDepositForm(false); setDepositError(''); setDepositScreenshot(null) }}
                      className="px-4 py-2.5 border border-gray-200 text-gray-600 rounded-lg hover:bg-gray-50 text-sm">
                      Cancel
                    </button>
                  </div>
                </form>
              )}

              {loadingDeposits ? (
                <div className="space-y-2">
                  {[1,2].map(i => <div key={i} className="h-10 bg-gray-100 rounded-lg animate-pulse" />)}
                </div>
              ) : deposits.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-6">No bank deposits recorded for this trip.</p>
              ) : (
                <div className="space-y-2">
                  {deposits.map(dep => (
                    <div key={dep.id} className="bg-gray-50 rounded-xl border border-gray-100 overflow-hidden">
                      <button
                        onClick={() => setExpandedDepositId(id => id === dep.id ? null : dep.id)}
                        className="w-full px-3 py-2.5 flex items-center justify-between text-left hover:bg-gray-100 transition-colors"
                      >
                        <div>
                          <p className="text-sm font-medium text-gray-800">{dep.bankName ? `Banked to ${dep.bankName}` : 'Bank Deposit'}</p>
                          <p className="text-xs text-gray-400">Ref: {dep.mpesaReference} · {new Date(dep.transactionDate ?? dep.createdAt).toLocaleDateString()}</p>
                        </div>
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-bold text-green-700">{fmt(dep.amount)}</span>
                          {dep.rawSmsText && (
                            <svg className={`w-4 h-4 text-gray-400 transition-transform ${expandedDepositId === dep.id ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                              <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
                            </svg>
                          )}
                        </div>
                      </button>
                      {expandedDepositId === dep.id && dep.rawSmsText && (
                        <div className="px-3 pb-3 pt-0.5 border-t border-gray-200">
                          <p className="text-xs text-gray-500 whitespace-pre-wrap leading-relaxed mt-2">{dep.rawSmsText}</p>
                        </div>
                      )}
                    </div>
                  ))}
                  <div className="flex items-center justify-between px-3 py-2 border-t border-gray-200 mt-1">
                    <span className="text-sm font-semibold text-gray-600">Total Banked</span>
                    <span className="text-base font-extrabold text-green-700">
                      {fmt(deposits.reduce((s, d) => s + Number(d.amount), 0))}
                    </span>
                  </div>
                </div>
              )}
            </div>

            {/* Odometer Photos */}
            {(fixUrl(trip.odometerStartPhotoUrl) || fixUrl(trip.odometerEndPhotoUrl)) && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-4">Odometer</h3>
                <div className="grid grid-cols-2 gap-3">
                  {fixUrl(trip.odometerStartPhotoUrl) && (
                    <a href={fixUrl(trip.odometerStartPhotoUrl)} target="_blank" rel="noreferrer" className="group block rounded-xl overflow-hidden border border-gray-200 hover:border-gold transition-colors">
                      <img src={fixUrl(trip.odometerStartPhotoUrl)} alt="Odometer Start" className="w-full aspect-video object-cover" />
                      <p className="text-xs text-center text-gray-500 group-hover:text-gold py-1.5 font-medium">Start{canViewMileage ? ` — ${trip.startMileage ?? '—'} km` : ''}</p>
                    </a>
                  )}
                  {fixUrl(trip.odometerEndPhotoUrl) && (
                    <a href={fixUrl(trip.odometerEndPhotoUrl)} target="_blank" rel="noreferrer" className="group block rounded-xl overflow-hidden border border-gray-200 hover:border-gold transition-colors">
                      <img src={fixUrl(trip.odometerEndPhotoUrl)} alt="Odometer End" className="w-full aspect-video object-cover" />
                      <p className="text-xs text-center text-gray-500 group-hover:text-gold py-1.5 font-medium">End — {trip.endMileage ?? '—'} km</p>
                    </a>
                  )}
                </div>
              </div>
            )}

            {/* Material Photo */}
            {fixUrl(trip.materialPhotoUrl) && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-4">Material Photo</h3>
                <a href={fixUrl(trip.materialPhotoUrl)} target="_blank" rel="noreferrer" className="group block rounded-xl overflow-hidden border border-gray-200 hover:border-gold transition-colors">
                  <img src={fixUrl(trip.materialPhotoUrl)} alt="Material" className="w-full max-h-64 object-cover" />
                  <p className="text-xs text-center text-gray-500 group-hover:text-gold py-1.5 font-medium">Tap to view full size</p>
                </a>
              </div>
            )}

            {/* Trip Photos */}
            {trip.tripPhotos?.length > 0 && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
                <h3 className="text-sm font-semibold text-gray-500 uppercase tracking-wider mb-4">Trip Photos ({trip.tripPhotos.length})</h3>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                  {trip.tripPhotos.map((photo, i) => {
                    const url = fixUrl(photo.url ?? photo.photoUrl ?? photo)
                    return url ? (
                      <a key={i} href={url} target="_blank" rel="noreferrer" className="group block rounded-xl overflow-hidden border border-gray-200 hover:border-gold transition-colors">
                        <img src={url} alt={`Trip photo ${i + 1}`} className="w-full aspect-square object-cover" />
                      </a>
                    ) : null
                  })}
                </div>
              </div>
            )}

          </div>
        </div>
      </main>

      {/* Complete Trip modal */}
      {showCompleteModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div>
              <h3 className="text-base font-bold text-gray-900">Complete Trip</h3>
              <p className="text-xs text-gray-500 mt-0.5">Enter the ending odometer reading to close out this trip.</p>
            </div>
            <form onSubmit={handleComplete} className="space-y-3">
              {completeError && (
                <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">{completeError}</div>
              )}
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">End Mileage (km) *</label>
                <input
                  type="number"
                  step="0.1"
                  min={trip.startMileage ?? 0}
                  value={completeForm.endMileage}
                  onChange={e => setCompleteForm(f => ({ ...f, endMileage: e.target.value }))}
                  placeholder="e.g. 45890"
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                  required
                />
                {canViewMileage && trip.startMileage != null && (
                  <p className="text-xs text-gray-400 mt-1">Start mileage was {Number(trip.startMileage).toLocaleString()} km</p>
                )}
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Odometer Photo <span className="text-red-500">*</span></label>
                <SinglePhotoPicker
                  file={completeForm.odometerEndPhoto}
                  onChange={file => setCompleteForm(f => ({ ...f, odometerEndPhoto: file }))}
                />
              </div>
              <div className="flex gap-3 pt-1">
                <button
                  type="button"
                  onClick={() => setShowCompleteModal(false)}
                  className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={completing || !completeForm.endMileage || !completeForm.odometerEndPhoto}
                  className="flex-1 py-2.5 bg-green-600 hover:bg-green-700 text-white text-sm font-bold rounded-xl transition-colors disabled:opacity-60"
                >
                  {completing ? 'Completing…' : 'Complete Trip'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Report Incident modal */}
      {showIncidentModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div>
              <h3 className="text-base font-bold text-gray-900">Report Incident</h3>
              <p className="text-xs text-gray-500 mt-0.5">Recorded in the HSE Incident Register, linked to this trip.</p>
            </div>
            <form onSubmit={handleReportIncident} className="space-y-3">
              {incidentError && (
                <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-lg px-3 py-2">{incidentError}</div>
              )}
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Type</label>
                <select
                  value={incidentForm.type}
                  onChange={e => setIncidentForm(f => ({ ...f, type: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                >
                  {INCIDENT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Severity</label>
                <select
                  value={incidentForm.severity}
                  onChange={e => setIncidentForm(f => ({ ...f, severity: e.target.value }))}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold"
                >
                  {SEVERITIES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-600 mb-1">Description *</label>
                <textarea
                  value={incidentForm.description}
                  onChange={e => setIncidentForm(f => ({ ...f, description: e.target.value }))}
                  rows={3}
                  className="w-full px-3 py-2.5 border border-gray-200 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gold resize-none"
                  required
                />
              </div>
              <div className="flex gap-3 pt-1">
                <button
                  type="button"
                  onClick={() => setShowIncidentModal(false)}
                  className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={savingIncident || !incidentForm.description.trim()}
                  className="flex-1 py-2.5 bg-amber-600 hover:bg-amber-700 text-white text-sm font-bold rounded-xl transition-colors disabled:opacity-60"
                >
                  {savingIncident ? 'Submitting…' : 'Submit Report'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete confirmation modal */}
      {showDeleteModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-6 space-y-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center flex-shrink-0">
                <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </div>
              <div>
                <h3 className="text-base font-bold text-gray-900">Delete Trip</h3>
                <p className="text-xs text-gray-500 mt-0.5">This action cannot be undone.</p>
              </div>
            </div>
            <p className="text-sm text-gray-600">
              Are you sure you want to permanently delete the trip{' '}
              <span className="font-semibold text-gray-900">{trip.startLocation} → {trip.endLocation}</span>?
              All expenses and photos will be lost.
            </p>
            <div className="flex gap-3 pt-1">
              <button
                onClick={() => setShowDeleteModal(false)}
                className="flex-1 py-2.5 border border-gray-200 text-gray-600 hover:bg-gray-50 text-sm font-semibold rounded-xl transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleDelete}
                className="flex-1 py-2.5 bg-red-600 hover:bg-red-700 text-white text-sm font-bold rounded-xl transition-colors"
              >
                Yes, Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}

function FinRow({ label, value, color, bold }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-gray-600">{label}</span>
      <span className={`text-sm ${bold ? 'font-extrabold text-base' : 'font-semibold'} ${color}`}>{value}</span>
    </div>
  )
}


function Spinner() {
  return (
    <svg className="w-5 h-5 animate-spin mx-auto" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z"/>
    </svg>
  )
}
