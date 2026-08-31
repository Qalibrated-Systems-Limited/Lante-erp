import { useState, useRef, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '../../api/axios.js'
import usePortalTenant from '../../hooks/usePortalTenant'

// ── Constants ─────────────────────────────────────────────────────────────────

const FORM_TYPES = {
  SRF: {
    label: 'Service Request Form',
    short: 'SRF',
    icon: '🔧',
    color: 'border-blue-400 bg-blue-50',
    badge: 'bg-blue-100 text-blue-700',
    description: 'Request technical service, maintenance, inspection or repair.',
  },
  CRF_NAWI: {
    label: 'Calibration — Weighing Instruments',
    short: 'CRF-NAWI',
    icon: '⚖️',
    color: 'border-amber-400 bg-amber-50',
    badge: 'bg-amber-100 text-amber-700',
    description: 'Calibration of balances, scales and non-automatic weighing instruments (NAWI).',
  },
  CRF_MASS: {
    label: 'Calibration — Mass Standards',
    short: 'CRF-MASS',
    icon: '📦',
    color: 'border-green-400 bg-green-50',
    badge: 'bg-green-100 text-green-700',
    description: 'Calibration of mass standards, reference weights and weight sets.',
  },
}

const SERVICE_TYPES = ['Inspection', 'Repair', 'Maintenance', 'Calibration', 'Installation', 'Other']
const NAWI_CLASSES  = ['Class I (Special)', 'Class II (High)', 'Class III (Medium)', 'Class IIII (Ordinary)']
const MASS_CLASSES  = ['E1', 'E2', 'F1', 'F2', 'M1', 'M2', 'M3']

const emptyInstrument = (row = 1) => ({
  rowNumber: row, description: '', manufacturer: '', model: '',
  serialNumber: '', tagNumber: '', range: '', rangeUnit: '', condition: '',
  remarks: '', lastCalibrationDate: '', certificateNumber: '',
  nawiInstrumentType: '', nawiCapacity: '', nawiScaleInterval: '', nawiAccuracyClass: '',
  massNominalValue: '', massAccuracyClass: '', serviceType: '',
})

// ── Inline SignaturePad ───────────────────────────────────────────────────────

function SignaturePad({ label, onCapture }) {
  const canvasRef = useRef(null)
  const drawing   = useRef(false)
  const [hasStrokes, setHasStrokes] = useState(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    ctx.strokeStyle = '#1a1a1a'; ctx.lineWidth = 2
    ctx.lineCap = 'round'; ctx.lineJoin = 'round'
  }, [])

  const pos = e => {
    const rect = canvasRef.current.getBoundingClientRect()
    const src  = e.touches ? e.touches[0] : e
    return { x: src.clientX - rect.left, y: src.clientY - rect.top }
  }
  const start = e => { e.preventDefault(); drawing.current = true; const ctx = canvasRef.current.getContext('2d'); const {x,y} = pos(e); ctx.beginPath(); ctx.moveTo(x,y) }
  const move  = e => { e.preventDefault(); if (!drawing.current) return; const ctx = canvasRef.current.getContext('2d'); const {x,y} = pos(e); ctx.lineTo(x,y); ctx.stroke(); setHasStrokes(true); onCapture(canvasRef.current.toDataURL('image/png')) }
  const stop  = e => { e.preventDefault(); drawing.current = false }
  const clear = () => { const c = canvasRef.current; c.getContext('2d').clearRect(0,0,c.width,c.height); setHasStrokes(false); onCapture(null) }

  return (
    <div>
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">{label}</p>
      <div className="relative border-2 border-dashed border-gray-300 rounded-lg bg-gray-50 touch-none">
        <canvas ref={canvasRef} width={480} height={140} className="rounded-lg w-full"
          onMouseDown={start} onMouseMove={move} onMouseUp={stop} onMouseLeave={stop}
          onTouchStart={start} onTouchMove={move} onTouchEnd={stop} />
        {!hasStrokes && (
          <p className="absolute inset-0 flex items-center justify-center text-xs text-gray-400 pointer-events-none">
            Draw your signature here
          </p>
        )}
      </div>
      {hasStrokes && <button type="button" onClick={clear} className="mt-1 text-xs text-red-500 hover:text-red-700 underline">Clear</button>}
    </div>
  )
}

// ── OTP Input ─────────────────────────────────────────────────────────────────

function OtpInput({ value, onChange }) {
  // One ref holding the six inputs, not six useRef calls from inside Array.from: calling a
  // hook in a callback violates the rules of hooks. It happens to work here because
  // Array.from invokes the callback exactly six times in the same order on every render, so
  // hook order is stable by luck — it stops being stable the moment the count is derived
  // from anything, and React makes no guarantee about it either way.
  const refs = useRef([])

  const handleKey = (i, e) => {
    if (e.key === 'Backspace') {
      if (value[i]) { const next = value.split(''); next[i] = ''; onChange(next.join('')) }
      else if (i > 0) refs.current[i - 1]?.focus()
    }
  }

  const handleChange = (i, e) => {
    const char = e.target.value.replace(/\D/g, '').slice(-1)
    if (!char) return
    const next = value.split('')
    next[i] = char
    onChange(next.join(''))
    if (i < 5) refs.current[i + 1]?.focus()
  }

  const handlePaste = e => {
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 6)
    if (pasted.length === 6) { onChange(pasted); refs.current[5]?.focus() }
    e.preventDefault()
  }

  return (
    <div className="flex gap-2 justify-center">
      {Array.from({ length: 6 }).map((_, i) => (
        <input
          key={i}
          ref={el => { refs.current[i] = el }}
          type="text"
          inputMode="numeric"
          maxLength={1}
          value={value[i] || ''}
          onChange={e => handleChange(i, e)}
          onKeyDown={e => handleKey(i, e)}
          onPaste={handlePaste}
          className="w-11 h-14 text-center text-xl font-bold border-2 border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-amber-400 focus:border-amber-400 bg-white transition"
        />
      ))}
    </div>
  )
}

// ── Shared field components ───────────────────────────────────────────────────

const Label = ({ children, required }) => (
  <label className="block text-sm font-semibold text-gray-700 mb-1">
    {children}{required && <span className="text-red-500 ml-0.5">*</span>}
  </label>
)

const Input = ({ ...props }) => (
  <input {...props} className={`w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 ${props.className ?? ''}`} />
)

const Select = ({ children, ...props }) => (
  <select {...props} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 bg-white">
    {children}
  </select>
)

const Textarea = ({ ...props }) => (
  <textarea {...props} rows={3} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400 resize-none" />
)

// ── Instrument Row ─────────────────────────────────────────────────────────────

function InstrumentRow({ index, formType, data, onChange, onRemove, canRemove }) {
  const upd = (field, val) => onChange(index, { ...data, [field]: val })

  return (
    <div className="border border-gray-200 rounded-xl p-4 bg-gray-50 space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-bold text-amber-700 uppercase tracking-wide">
          Instrument {index + 1}
        </span>
        {canRemove && (
          <button type="button" onClick={() => onRemove(index)}
            className="text-xs text-red-500 hover:text-red-700 font-medium">
            Remove
          </button>
        )}
      </div>

      {/* Common fields */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div>
          <Label>Description</Label>
          <Input placeholder="e.g. Analytical balance" value={data.description} onChange={e => upd('description', e.target.value)} />
        </div>
        <div>
          <Label>Manufacturer</Label>
          <Input placeholder="e.g. Mettler Toledo" value={data.manufacturer} onChange={e => upd('manufacturer', e.target.value)} />
        </div>
        <div>
          <Label>Model / Type</Label>
          <Input placeholder="e.g. XS205" value={data.model} onChange={e => upd('model', e.target.value)} />
        </div>
        <div>
          <Label>Serial Number</Label>
          <Input placeholder="Serial no." value={data.serialNumber} onChange={e => upd('serialNumber', e.target.value)} />
        </div>
        <div>
          <Label>Tag / Asset ID</Label>
          <Input placeholder="Asset tag or sticker ID" value={data.tagNumber} onChange={e => upd('tagNumber', e.target.value)} />
        </div>

        {/* SRF-specific */}
        {formType === 'SRF' && (
          <div>
            <Label>Service Type</Label>
            <Select value={data.serviceType} onChange={e => upd('serviceType', e.target.value)}>
              <option value="">Select…</option>
              {SERVICE_TYPES.map(s => <option key={s} value={s}>{s}</option>)}
            </Select>
          </div>
        )}

        {/* CRF-NAWI-specific */}
        {formType === 'CRF_NAWI' && (<>
          <div>
            <Label>Instrument Type</Label>
            <Input placeholder="e.g. Weighing scale, Balance" value={data.nawiInstrumentType} onChange={e => upd('nawiInstrumentType', e.target.value)} />
          </div>
          <div>
            <Label>Max Capacity</Label>
            <Input placeholder="e.g. 220 g" value={data.nawiCapacity} onChange={e => upd('nawiCapacity', e.target.value)} />
          </div>
          <div>
            <Label>Scale Interval (e)</Label>
            <Input placeholder="e.g. 0.1 mg" value={data.nawiScaleInterval} onChange={e => upd('nawiScaleInterval', e.target.value)} />
          </div>
          <div>
            <Label>Accuracy Class</Label>
            <Select value={data.nawiAccuracyClass} onChange={e => upd('nawiAccuracyClass', e.target.value)}>
              <option value="">Select…</option>
              {NAWI_CLASSES.map(c => <option key={c} value={c}>{c}</option>)}
            </Select>
          </div>
        </>)}

        {/* CRF-MASS-specific */}
        {formType === 'CRF_MASS' && (<>
          <div>
            <Label>Nominal Value</Label>
            <Input placeholder="e.g. 1 kg, 200 g" value={data.massNominalValue} onChange={e => upd('massNominalValue', e.target.value)} />
          </div>
          <div>
            <Label>Accuracy Class</Label>
            <Select value={data.massAccuracyClass} onChange={e => upd('massAccuracyClass', e.target.value)}>
              <option value="">Select…</option>
              {MASS_CLASSES.map(c => <option key={c} value={c}>{c}</option>)}
            </Select>
          </div>
        </>)}

        {/* Calibration history (NAWI + MASS) */}
        {(formType === 'CRF_NAWI' || formType === 'CRF_MASS') && (<>
          <div>
            <Label>Last Calibration Date</Label>
            <Input type="date" value={data.lastCalibrationDate} onChange={e => upd('lastCalibrationDate', e.target.value)} />
          </div>
          <div>
            <Label>Certificate No.</Label>
            <Input placeholder="Existing cert. number" value={data.certificateNumber} onChange={e => upd('certificateNumber', e.target.value)} />
          </div>
        </>)}

        <div>
          <Label>Condition</Label>
          <Input placeholder="e.g. Good, Damaged, Missing parts" value={data.condition} onChange={e => upd('condition', e.target.value)} />
        </div>
      </div>

      <div>
        <Label>Remarks</Label>
        <Input placeholder="Any additional notes" value={data.remarks} onChange={e => upd('remarks', e.target.value)} />
      </div>
    </div>
  )
}

// ── Step indicator ─────────────────────────────────────────────────────────────

const STEPS = ['Form Type', 'Your Details', 'Instruments', 'Verify Email', 'Sign', 'Done']

function StepBar({ current }) {
  return (
    <div className="flex items-center gap-0 mb-8">
      {STEPS.map((s, i) => (
        <div key={s} className="flex items-center flex-1 last:flex-none">
          <div className={`flex flex-col items-center ${i < STEPS.length - 1 ? 'flex-1' : ''}`}>
            <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold border-2 transition-all
              ${i < current  ? 'bg-amber-400 border-amber-400 text-white'
              : i === current ? 'bg-white border-amber-400 text-amber-600'
              : 'bg-white border-gray-200 text-gray-400'}`}>
              {i < current ? '✓' : i + 1}
            </div>
            <span className={`mt-1 text-[10px] font-medium hidden sm:block ${i === current ? 'text-amber-600' : 'text-gray-400'}`}>
              {s}
            </span>
          </div>
          {i < STEPS.length - 1 && (
            <div className={`h-0.5 flex-1 mx-1 transition-all ${i < current ? 'bg-amber-400' : 'bg-gray-200'}`} />
          )}
        </div>
      ))}
    </div>
  )
}

// ── Portal layout wrapper ──────────────────────────────────────────────────────

function PortalLayout({ children, brandName }) {
  const navigate = useNavigate()
  const { logoUrl } = usePortalTenant()
  const brand = brandName || 'Lante'
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <header className="bg-white border-b border-gray-200 sticky top-0 z-10">
        <div className="max-w-3xl mx-auto px-4 sm:px-6 py-4 flex items-center justify-between">
          <button onClick={() => navigate('/portal')} className="flex items-center gap-3">
            <img src={logoUrl || "/qc-logo.png"} alt={brand} className="w-9 h-9 object-contain" />
            <div className="text-left">
              <p className="font-extrabold text-zinc-950 text-sm leading-none">{brand}</p>
              <p className="text-xs text-gray-400">Service Request</p>
            </div>
          </button>
          <button onClick={() => navigate('/portal/track')}
            className="text-sm font-medium text-amber-600 hover:text-amber-800 transition-colors">
            Track a submission →
          </button>
        </div>
      </header>
      <main className="flex-1 max-w-3xl mx-auto w-full px-4 sm:px-6 py-8">
        {children}
      </main>
      <footer className="border-t border-gray-200 bg-white py-4 px-6 text-center">
        <p className="text-xs text-gray-400">
          {brand} &copy; {new Date().getFullYear()} &mdash;{' '}
          <a href="mailto:info@qalibrated.co.ke" className="hover:text-amber-600">info@qalibrated.co.ke</a>
        </p>
      </footer>
    </div>
  )
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function ServiceRequestFormPage() {
  const navigate     = useNavigate()
  const [params]     = useSearchParams()
  const { slug, name: brandName } = usePortalTenant()
  const slugParams   = slug ? { slug } : undefined
  const defaultType  = params.get('type')?.toUpperCase()

  // wizard state
  const [step, setStep]         = useState(defaultType ? 1 : 0)
  const [formType, setFormType] = useState(defaultType || '')
  const [error, setError]       = useState('')
  const [loading, setLoading]   = useState(false)

  // step 1 — client info
  const defaultLocation = (t) => (t === 'CRF_NAWI' || t === 'CRF_MASS') ? 'InLab' : 'OnSite'
  const [client, setClient] = useState({
    clientName: '', clientEmail: '', clientPhone: '',
    clientOrganization: '', clientAddress: '',
    siteLocation: '', description: '', specialInstructions: '',
    serviceLocation: defaultLocation(defaultType || ''),
    latitude: null, longitude: null,
  })

  // step 2 — instruments
  const [instruments, setInstruments] = useState([emptyInstrument(1), emptyInstrument(2), emptyInstrument(3)])

  // step 3 — OTP
  const [pendingId, setPendingId]   = useState('')
  const [otp, setOtp]               = useState('')
  const [resendMsg, setResendMsg]   = useState('')
  const [resendCooldown, setResendCooldown] = useState(0)

  // step 4 — signature
  const [sigData, setSigData]       = useState(null)
  const [sigLoading, setSigLoading] = useState(false)

  // step 5 — success
  const [result, setResult] = useState(null)  // { referenceNumber, ticketReference }

  useEffect(() => {
    if (resendCooldown > 0) {
      const t = setTimeout(() => setResendCooldown(c => c - 1), 1000)
      return () => clearTimeout(t)
    }
  }, [resendCooldown])

  // ── GPS capture ────────────────────────────────────────────────────────────

  const captureGps = () => {
    if (!navigator.geolocation) return
    navigator.geolocation.getCurrentPosition(
      pos => setClient(c => ({ ...c, latitude: pos.coords.latitude, longitude: pos.coords.longitude })),
      () => {},
      { enableHighAccuracy: true, timeout: 8000, maximumAge: 0 }
    )
  }

  // ── Instrument handlers ────────────────────────────────────────────────────

  const updateInstrument = (i, data) => setInstruments(prev => prev.map((row, idx) => idx === i ? data : row))
  const addInstrument    = () => setInstruments(prev => [...prev, emptyInstrument(prev.length + 1)])
  const removeInstrument = i => setInstruments(prev => prev.filter((_, idx) => idx !== i).map((r, idx) => ({ ...r, rowNumber: idx + 1 })))

  // ── Step 0 → 1: select form type ──────────────────────────────────────────

  const selectType = type => { setFormType(type); setStep(1) }

  // ── Step 1 → 2: validate client info, capture GPS ─────────────────────────

  const goToInstruments = () => {
    if (!client.clientName.trim())  return setError('Your name is required.')
    if (!client.clientEmail.trim()) return setError('Your email address is required.')
    if (!/\S+@\S+\.\S+/.test(client.clientEmail)) return setError('Please enter a valid email address.')
    setError(''); captureGps(); setStep(2)
  }

  // ── Step 2 → 3: submit form, trigger OTP ──────────────────────────────────

  const submitForm = async () => {
    const filled = instruments.filter(i => i.description?.trim() || i.serialNumber?.trim() || i.manufacturer?.trim())
    if (filled.length === 0) return setError('Please fill in at least one instrument.')
    setError(''); setLoading(true)
    try {
      const { data } = await api.post('/api/v1/portal/service-request/initiate', {
        formType,
        ...client,
        instruments: instruments.map((i, idx) => ({
          ...i,
          rowNumber: idx + 1,
          lastCalibrationDate: i.lastCalibrationDate || null,
        })),
      }, { params: slugParams })
      setPendingId(data.data.pendingId)
      setStep(3)
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to submit form. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  // ── Step 3: verify OTP ─────────────────────────────────────────────────────

  const verifyOtp = async () => {
    if (otp.length < 6) return setError('Please enter the full 6-digit code.')
    setError(''); setLoading(true)
    try {
      const { data } = await api.post('/api/v1/portal/service-request/verify', { pendingId, otpCode: otp }, { params: slugParams })
      setResult(data.data)
      setStep(4)  // go to signature step
    } catch (err) {
      setError(err.response?.data?.message || 'Incorrect code. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  const resendOtp = async () => {
    if (resendCooldown > 0) return
    setLoading(true); setResendMsg('')
    try {
      await api.post('/api/v1/portal/service-request/resend-otp', { pendingId }, { params: slugParams })
      setResendMsg('A new code has been sent to your email.')
      setResendCooldown(60)
      setOtp('')
    } catch (err) {
      setResendMsg(err.response?.data?.message || 'Could not resend. Please restart the form.')
    } finally {
      setLoading(false)
    }
  }

  // ── Step 4: submit signature (optional, skip allowed) ─────────────────────

  const submitSignature = async (skip = false) => {
    if (!skip && !sigData) return setError('Please draw your signature or click Skip.')
    setSigLoading(true); setError('')
    try {
      if (!skip && sigData && result?.referenceNumber) {
        await api.post('/api/v1/portal/service-request/signature', {
          referenceNumber: result.referenceNumber,
          signatureData: sigData,
        }, { params: slugParams })
      }
      setStep(5)
    } catch {
      // non-fatal — still advance
      setStep(5)
    } finally {
      setSigLoading(false)
    }
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <PortalLayout brandName={brandName}>
      {step < 5 && <StepBar current={step} />}

      {/* ── STEP 0: Form type selection ─────────────────────────────────── */}
      {step === 0 && (
        <div>
          <h1 className="text-2xl font-extrabold text-zinc-950 mb-2">Request a Service or Calibration</h1>
          <p className="text-gray-500 text-sm mb-8 leading-relaxed">
            Select the type of request you would like to submit. You will be guided through the form step by step.
          </p>
          <div className="space-y-4">
            {Object.entries(FORM_TYPES).map(([key, ft]) => (
              <button key={key} onClick={() => selectType(key)}
                className={`w-full text-left bg-white rounded-2xl border-2 p-5 transition-all duration-150 group hover:shadow-md ${ft.color}`}>
                <div className="flex items-center gap-4">
                  <div className="text-3xl shrink-0">{ft.icon}</div>
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-bold text-gray-900">{ft.label}</span>
                      <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${ft.badge}`}>{ft.short}</span>
                    </div>
                    <p className="text-sm text-gray-500">{ft.description}</p>
                  </div>
                  <svg className="w-5 h-5 text-gray-300 group-hover:text-gray-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                  </svg>
                </div>
              </button>
            ))}
          </div>
          <div className="mt-10 bg-zinc-950 rounded-2xl p-5 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div>
              <p className="font-bold text-white text-sm">Already submitted a request?</p>
              <p className="text-xs text-zinc-300 mt-0.5">Use your reference number to check the status.</p>
            </div>
            <button onClick={() => navigate('/portal/track')}
              className="shrink-0 px-5 py-2.5 bg-amber-500 hover:bg-amber-400 text-white text-sm font-bold rounded-xl transition-colors">
              Track Request →
            </button>
          </div>
        </div>
      )}

      {/* ── STEP 1: Client info ──────────────────────────────────────────── */}
      {step === 1 && (
        <div>
          <div className="flex items-center gap-3 mb-6">
            <span className="text-2xl">{FORM_TYPES[formType]?.icon}</span>
            <div>
              <h2 className="text-xl font-extrabold text-zinc-950">{FORM_TYPES[formType]?.label}</h2>
              <p className="text-sm text-gray-500">Step 1 of 4 — Your contact information</p>
            </div>
          </div>

          <div className="bg-white rounded-2xl border border-gray-200 p-6 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="sm:col-span-2">
                <Label required>Full Name</Label>
                <Input placeholder="Your name" value={client.clientName}
                  onChange={e => setClient(c => ({ ...c, clientName: e.target.value }))} />
              </div>
              <div>
                <Label required>Email Address</Label>
                <Input type="email" placeholder="your@email.com" value={client.clientEmail}
                  onChange={e => setClient(c => ({ ...c, clientEmail: e.target.value }))} />
              </div>
              <div>
                <Label>Phone Number</Label>
                <Input type="tel" placeholder="+254 7XX XXX XXX" value={client.clientPhone}
                  onChange={e => setClient(c => ({ ...c, clientPhone: e.target.value }))} />
              </div>
              <div>
                <Label>Organisation / Company</Label>
                <Input placeholder="Company name" value={client.clientOrganization}
                  onChange={e => setClient(c => ({ ...c, clientOrganization: e.target.value }))} />
              </div>
              <div>
                <Label>Address</Label>
                <Input placeholder="Physical or postal address" value={client.clientAddress}
                  onChange={e => setClient(c => ({ ...c, clientAddress: e.target.value }))} />
              </div>
              {/* Service location type */}
              <div className="sm:col-span-2">
                <Label required>Service Location</Label>
                <div className="flex gap-3 mt-1">
                  {[
                    { value: 'OnSite',  label: 'On-Site Visit',    desc: 'Our technician travels to your location', icon: '🚗' },
                    { value: 'InLab',   label: 'In-Lab Service',   desc: 'Instruments brought to our laboratory',   icon: '🔬' },
                  ].map(opt => (
                    <button
                      key={opt.value}
                      type="button"
                      onClick={() => setClient(c => ({ ...c, serviceLocation: opt.value }))}
                      className={`flex-1 text-left p-3 rounded-xl border-2 transition-all ${
                        client.serviceLocation === opt.value
                          ? 'border-amber-400 bg-amber-50'
                          : 'border-gray-200 bg-white hover:border-gray-300'
                      }`}
                    >
                      <p className="text-lg mb-0.5">{opt.icon}</p>
                      <p className="text-sm font-bold text-gray-900">{opt.label}</p>
                      <p className="text-xs text-gray-500 leading-relaxed">{opt.desc}</p>
                    </button>
                  ))}
                </div>
              </div>

              <div className="sm:col-span-2">
                <Label>{client.serviceLocation === 'InLab' ? 'Collection / Return Address' : 'Site / Location'}</Label>
                <div className="flex gap-2">
                  <Input
                    placeholder={client.serviceLocation === 'InLab'
                      ? 'Where should we collect / return instruments?'
                      : 'Where are the instruments located?'}
                    value={client.siteLocation}
                    onChange={e => setClient(c => ({ ...c, siteLocation: e.target.value }))} />
                  {client.serviceLocation === 'OnSite' && (
                    <button type="button" onClick={captureGps}
                      title="Capture GPS"
                      className="shrink-0 px-3 py-2 border border-gray-300 rounded-lg text-sm text-gray-600 hover:bg-gray-50 transition-colors">
                      {client.latitude ? '📍' : '🧭'}
                    </button>
                  )}
                </div>
                {client.latitude && client.serviceLocation === 'OnSite' && (
                  <p className="text-xs text-green-600 mt-1 font-medium">
                    GPS: {client.latitude.toFixed(5)}, {client.longitude.toFixed(5)}
                  </p>
                )}
              </div>
              <div className="sm:col-span-2">
                <Label>Description / Nature of Request</Label>
                <Textarea placeholder="Briefly describe what you need and any known issues"
                  value={client.description}
                  onChange={e => setClient(c => ({ ...c, description: e.target.value }))} />
              </div>
              <div className="sm:col-span-2">
                <Label>Special Instructions</Label>
                <Input placeholder="Access requirements, preferred dates, etc." value={client.specialInstructions}
                  onChange={e => setClient(c => ({ ...c, specialInstructions: e.target.value }))} />
              </div>
            </div>
          </div>

          {error && <p className="mt-4 text-sm text-red-600 font-medium">{error}</p>}

          <div className="flex gap-3 mt-6">
            <button onClick={() => { setStep(0); setError('') }}
              className="px-5 py-2.5 text-sm font-medium text-gray-600 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-colors">
              Back
            </button>
            <button onClick={goToInstruments}
              className="flex-1 py-2.5 text-sm font-bold text-black bg-amber-400 hover:bg-amber-500 rounded-xl transition-colors">
              Continue — Add Instruments →
            </button>
          </div>
        </div>
      )}

      {/* ── STEP 2: Instruments ──────────────────────────────────────────── */}
      {step === 2 && (
        <div>
          <div className="flex items-center gap-3 mb-2">
            <span className="text-2xl">{FORM_TYPES[formType]?.icon}</span>
            <div>
              <h2 className="text-xl font-extrabold text-zinc-950">Instrument Details</h2>
              <p className="text-sm text-gray-500">Step 2 of 4 — List all instruments for {FORM_TYPES[formType]?.short}</p>
            </div>
          </div>
          <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-6">
            Start with 3 rows. Click <strong>Add Instrument</strong> to include more. Fill in as much detail as possible — it helps us prepare an accurate quotation.
          </p>

          <div className="space-y-4">
            {instruments.map((inst, i) => (
              <InstrumentRow key={i} index={i} formType={formType} data={inst}
                onChange={updateInstrument} onRemove={removeInstrument} canRemove={instruments.length > 1} />
            ))}
          </div>

          <button onClick={addInstrument}
            className="mt-4 w-full py-2.5 border-2 border-dashed border-amber-300 rounded-xl text-sm font-semibold text-amber-600 hover:bg-amber-50 transition-colors">
            + Add Instrument
          </button>

          {error && <p className="mt-4 text-sm text-red-600 font-medium">{error}</p>}

          <div className="flex gap-3 mt-6">
            <button onClick={() => { setStep(1); setError('') }}
              className="px-5 py-2.5 text-sm font-medium text-gray-600 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-colors">
              Back
            </button>
            <button onClick={submitForm} disabled={loading}
              className="flex-1 py-2.5 text-sm font-bold text-black bg-amber-400 hover:bg-amber-500 disabled:opacity-50 rounded-xl transition-colors">
              {loading ? 'Sending…' : 'Submit & Verify Email →'}
            </button>
          </div>
        </div>
      )}

      {/* ── STEP 3: OTP ──────────────────────────────────────────────────── */}
      {step === 3 && (
        <div className="max-w-md mx-auto text-center">
          <div className="text-5xl mb-4">📧</div>
          <h2 className="text-xl font-extrabold text-zinc-950 mb-2">Verify your email</h2>
          <p className="text-sm text-gray-500 mb-2 leading-relaxed">
            We sent a 6-digit verification code to{' '}
            <strong className="text-gray-800">{client.clientEmail}</strong>.
            Enter it below to confirm your request.
          </p>
          <p className="text-xs text-gray-400 mb-8">The code expires in 10 minutes.</p>

          <OtpInput value={otp} onChange={setOtp} />

          {error && <p className="mt-4 text-sm text-red-600 font-medium">{error}</p>}
          {resendMsg && <p className="mt-4 text-sm text-green-600 font-medium">{resendMsg}</p>}

          <button onClick={verifyOtp} disabled={loading || otp.length < 6}
            className="mt-6 w-full py-3 text-sm font-bold text-black bg-amber-400 hover:bg-amber-500 disabled:opacity-50 rounded-xl transition-colors">
            {loading ? 'Verifying…' : 'Confirm Code →'}
          </button>

          <button onClick={resendOtp} disabled={loading || resendCooldown > 0}
            className="mt-3 text-sm text-gray-500 hover:text-amber-600 disabled:opacity-50 transition-colors">
            {resendCooldown > 0 ? `Resend in ${resendCooldown}s` : "Didn't receive the code? Resend →"}
          </button>
        </div>
      )}

      {/* ── STEP 4: Signature ─────────────────────────────────────────────── */}
      {step === 4 && (
        <div className="max-w-lg mx-auto">
          <div className="text-center mb-6">
            <div className="text-4xl mb-3">✍️</div>
            <h2 className="text-xl font-extrabold text-zinc-950 mb-1">Sign your request</h2>
            <p className="text-sm text-gray-500">
              Draw your signature below to authorise this request. You may skip this step if preferred.
            </p>
            <p className="text-xs text-gray-400 mt-1">
              Date: <strong>{new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })}</strong>
            </p>
          </div>

          <div className="bg-white border border-gray-200 rounded-2xl p-5 space-y-3">
            <div>
              <p className="text-sm font-semibold text-gray-700 mb-0.5">{client.clientName}</p>
              <p className="text-xs text-gray-400">{client.clientOrganization || client.clientEmail}</p>
            </div>
            <SignaturePad label="Client Signature" onCapture={setSigData} />
          </div>

          {error && <p className="mt-4 text-sm text-red-600 font-medium text-center">{error}</p>}

          <div className="flex gap-3 mt-6">
            <button onClick={() => submitSignature(true)} disabled={sigLoading}
              className="px-5 py-2.5 text-sm font-medium text-gray-600 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl transition-colors">
              Skip
            </button>
            <button onClick={() => submitSignature(false)} disabled={sigLoading || !sigData}
              className="flex-1 py-2.5 text-sm font-bold text-black bg-amber-400 hover:bg-amber-500 disabled:opacity-50 rounded-xl transition-colors">
              {sigLoading ? 'Saving…' : 'Submit Signature →'}
            </button>
          </div>
        </div>
      )}

      {/* ── STEP 5: Success ───────────────────────────────────────────────── */}
      {step === 5 && result && (
        <div className="max-w-lg mx-auto text-center">
          <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-5">
            <svg className="w-8 h-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
            </svg>
          </div>
          <h2 className="text-2xl font-extrabold text-zinc-950 mb-2">Request Submitted</h2>
          <p className="text-gray-500 text-sm mb-8 leading-relaxed">
            Your {FORM_TYPES[formType]?.label} has been received. Our team will review it and send you a quotation by email.
          </p>

          <div className="bg-amber-50 border border-amber-200 rounded-2xl p-6 mb-6 text-left space-y-4">
            <div>
              <p className="text-xs font-semibold text-amber-700 uppercase tracking-wide">Service Request Reference</p>
              <p className="text-2xl font-black text-zinc-950 font-mono tracking-widest mt-1">
                {result.referenceNumber}
              </p>
            </div>
            <div className="border-t border-amber-200 pt-4 grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-xs text-gray-500">Ticket Reference</p>
                <p className="font-bold text-gray-800 font-mono">{result.ticketReference}</p>
              </div>
              <div>
                <p className="text-xs text-gray-500">Form Type</p>
                <p className="font-bold text-gray-800">{FORM_TYPES[formType]?.short}</p>
              </div>
              <div className="col-span-2">
                <p className="text-xs text-gray-500">Confirmation sent to</p>
                <p className="font-medium text-gray-800">{client.clientEmail}</p>
              </div>
            </div>
          </div>

          <div className="bg-zinc-950 rounded-2xl p-5 text-left mb-6">
            <p className="text-sm font-bold text-white mb-2">What happens next?</p>
            <ol className="text-xs text-zinc-300 space-y-1.5 list-decimal list-inside leading-relaxed">
              <li>Our technical team will review your request</li>
              <li>We will prepare and send you a quotation by email</li>
              <li>Upon acceptance, we will schedule the service visit</li>
              <li>A calibration certificate will be issued on completion</li>
            </ol>
          </div>

          <div className="flex gap-3">
            <button onClick={() => navigate('/portal/track')}
              className="flex-1 py-2.5 text-sm font-semibold text-amber-700 border-2 border-amber-300 hover:bg-amber-50 rounded-xl transition-colors">
              Track this Request
            </button>
            <button onClick={() => navigate('/portal')}
              className="flex-1 py-2.5 text-sm font-bold text-black bg-amber-400 hover:bg-amber-500 rounded-xl transition-colors">
              Back to Portal
            </button>
          </div>
        </div>
      )}
    </PortalLayout>
  )
}
