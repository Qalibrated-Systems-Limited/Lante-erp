import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { createServiceRequest } from '../../services/operations.js'

// Staff-side Service Request create — for a client who wants a service/calibration but won't use the
// anonymous portal. Mirrors the portal form fields; the instrument section adapts to the form type.

const FORM_TYPES = ['SRF', 'CRF_NAWI', 'CRF_MASS']
const FORM_LABEL = { SRF: 'Service Request (SRF)', CRF_NAWI: 'Calibration — NAWI (CRF-NAWI)', CRF_MASS: 'Calibration — Mass (CRF-MASS)' }

const emptyInstrument = () => ({
  manufacturer: '', model: '', serialNumber: '', tagNumber: '',
  description: '', remarks: '',
  nawiInstrumentType: '', nawiCapacity: '', nawiScaleInterval: '', nawiAccuracyClass: '',
  massNominalValue: '', massAccuracyClass: '',
})

export default function CreateServiceRequestPage() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const initialType = FORM_TYPES.includes(params.get('type')) ? params.get('type') : 'SRF'

  const [formType, setFormType]       = useState(initialType)
  const [serviceLocation, setServiceLocation] = useState('OnSite')
  const [clientName, setClientName]   = useState('')
  const [clientEmail, setClientEmail] = useState('')
  const [clientPhone, setClientPhone] = useState('')
  const [clientOrganization, setClientOrganization] = useState('')
  const [clientAddress, setClientAddress] = useState('')
  const [siteLocation, setSiteLocation] = useState('')
  const [description, setDescription] = useState('')
  const [specialInstructions, setSpecialInstructions] = useState('')
  const [instruments, setInstruments] = useState([emptyInstrument()])
  const [saving, setSaving]           = useState(false)
  const [error, setError]             = useState('')

  const isCalibration = formType === 'CRF_NAWI' || formType === 'CRF_MASS'

  const setInstrument = (i, patch) =>
    setInstruments(list => list.map((row, idx) => (idx === i ? { ...row, ...patch } : row)))
  const addInstrument = () => setInstruments(list => [...list, emptyInstrument()])
  const removeInstrument = (i) => setInstruments(list => list.filter((_, idx) => idx !== i))

  const submit = async () => {
    if (!clientName.trim())  return setError('Client name is required.')
    if (!clientEmail.trim()) return setError('Client email is required.')
    setError('')
    setSaving(true)
    try {
      // Only send non-empty instrument rows; keep a stable RowNumber for ordering.
      const rows = instruments
        .filter(r => Object.values(r).some(v => (v ?? '').toString().trim()))
        .map((r, idx) => ({ ...r, rowNumber: idx + 1 }))
      const created = await createServiceRequest({
        formType,
        serviceLocation,
        clientName, clientEmail, clientPhone, clientOrganization, clientAddress,
        siteLocation, description, specialInstructions,
        instruments: rows,
      })
      navigate(`/modules/operations/service-requests/${created.id}`)
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create service request.')
      setSaving(false)
    }
  }

  return (
    <>
      <main className="flex-1 max-w-4xl mx-auto w-full px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => navigate('/modules/operations/service-requests')}
          className="text-sm text-gray-500 hover:text-navy mb-4">← Back to Service Requests</button>

        <h1 className="text-2xl font-extrabold text-navy">New Service Request</h1>
        <p className="text-sm text-gray-500 mt-0.5 mb-6">Log a request on behalf of a client who is not using the portal.</p>

        {error && <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl px-5 py-3 text-sm mb-5">{error}</div>}

        {/* Request type + location */}
        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <h2 className="text-sm font-bold text-gray-700 mb-3">Request Type</h2>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Form type *">
              <select value={formType} onChange={e => setFormType(e.target.value)} className="input">
                {FORM_TYPES.map(t => <option key={t} value={t}>{FORM_LABEL[t]}</option>)}
              </select>
            </Field>
            <Field label="Service location *">
              <select value={serviceLocation} onChange={e => setServiceLocation(e.target.value)} className="input">
                <option value="OnSite">On-Site (technician travels to client)</option>
                <option value="InLab">In-Lab (instruments brought to lab)</option>
              </select>
            </Field>
          </div>
        </section>

        {/* Client details */}
        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <h2 className="text-sm font-bold text-gray-700 mb-3">Client Details</h2>
          <div className="grid sm:grid-cols-2 gap-4">
            <Field label="Client name *"><input value={clientName} onChange={e => setClientName(e.target.value)} className="input" placeholder="Contact person" /></Field>
            <Field label="Email *"><input type="email" value={clientEmail} onChange={e => setClientEmail(e.target.value)} className="input" placeholder="client@company.com" /></Field>
            <Field label="Phone"><input value={clientPhone} onChange={e => setClientPhone(e.target.value)} className="input" placeholder="+254…" /></Field>
            <Field label="Organization"><input value={clientOrganization} onChange={e => setClientOrganization(e.target.value)} className="input" placeholder="Company name" /></Field>
            <Field label="Address"><input value={clientAddress} onChange={e => setClientAddress(e.target.value)} className="input" /></Field>
            <Field label="Site location"><input value={siteLocation} onChange={e => setSiteLocation(e.target.value)} className="input" placeholder="Where the work happens" /></Field>
          </div>
        </section>

        {/* Description */}
        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <h2 className="text-sm font-bold text-gray-700 mb-3">Details</h2>
          <div className="grid gap-4">
            <Field label="Description"><textarea value={description} onChange={e => setDescription(e.target.value)} rows={2} className="input" placeholder="What does the client need?" /></Field>
            <Field label="Special instructions"><textarea value={specialInstructions} onChange={e => setSpecialInstructions(e.target.value)} rows={2} className="input" /></Field>
          </div>
        </section>

        {/* Instruments */}
        <section className="bg-white border border-gray-200 rounded-xl p-5 mb-5">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-sm font-bold text-gray-700">
              {isCalibration ? 'Instruments to Calibrate' : 'Equipment / Items'}
              <span className="text-gray-400 font-normal"> (optional)</span>
            </h2>
            <button onClick={addInstrument} className="text-sm font-semibold text-navy hover:underline">+ Add</button>
          </div>
          <div className="space-y-4">
            {instruments.map((row, i) => (
              <div key={i} className="border border-gray-200 rounded-lg p-4">
                <div className="flex items-center justify-between mb-3">
                  <span className="text-xs font-semibold text-gray-500">Item {i + 1}</span>
                  {instruments.length > 1 && (
                    <button onClick={() => removeInstrument(i)} className="text-xs text-red-600 hover:underline">Remove</button>
                  )}
                </div>
                <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  <Field label="Manufacturer"><input value={row.manufacturer} onChange={e => setInstrument(i, { manufacturer: e.target.value })} className="input" /></Field>
                  <Field label="Model"><input value={row.model} onChange={e => setInstrument(i, { model: e.target.value })} className="input" /></Field>
                  <Field label="Serial number"><input value={row.serialNumber} onChange={e => setInstrument(i, { serialNumber: e.target.value })} className="input" /></Field>
                  <Field label="Tag number"><input value={row.tagNumber} onChange={e => setInstrument(i, { tagNumber: e.target.value })} className="input" /></Field>

                  {formType === 'SRF' && (<>
                    <Field label="Description"><input value={row.description} onChange={e => setInstrument(i, { description: e.target.value })} className="input" /></Field>
                    <Field label="Remarks"><input value={row.remarks} onChange={e => setInstrument(i, { remarks: e.target.value })} className="input" /></Field>
                  </>)}

                  {formType === 'CRF_NAWI' && (<>
                    <Field label="Instrument type"><input value={row.nawiInstrumentType} onChange={e => setInstrument(i, { nawiInstrumentType: e.target.value })} className="input" placeholder="e.g. Platform scale" /></Field>
                    <Field label="Capacity"><input value={row.nawiCapacity} onChange={e => setInstrument(i, { nawiCapacity: e.target.value })} className="input" placeholder="e.g. 300 kg" /></Field>
                    <Field label="Scale interval (e)"><input value={row.nawiScaleInterval} onChange={e => setInstrument(i, { nawiScaleInterval: e.target.value })} className="input" placeholder="e.g. 50 g" /></Field>
                    <Field label="Accuracy class"><input value={row.nawiAccuracyClass} onChange={e => setInstrument(i, { nawiAccuracyClass: e.target.value })} className="input" placeholder="e.g. III" /></Field>
                  </>)}

                  {formType === 'CRF_MASS' && (<>
                    <Field label="Nominal value"><input value={row.massNominalValue} onChange={e => setInstrument(i, { massNominalValue: e.target.value })} className="input" placeholder="e.g. 1 kg" /></Field>
                    <Field label="Accuracy class"><input value={row.massAccuracyClass} onChange={e => setInstrument(i, { massAccuracyClass: e.target.value })} className="input" placeholder="e.g. F1" /></Field>
                  </>)}
                </div>
              </div>
            ))}
          </div>
        </section>

        <div className="flex justify-end gap-3">
          <button onClick={() => navigate('/modules/operations/service-requests')}
            className="px-5 py-2.5 text-sm font-semibold border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors">Cancel</button>
          <button onClick={submit} disabled={saving}
            className="px-5 py-2.5 text-sm font-bold bg-navy hover:bg-navy-dark text-white rounded-lg disabled:opacity-50 transition-colors">
            {saving ? 'Creating…' : 'Create Request'}
          </button>
        </div>
      </main>
    </>
  )
}

function Field({ label, children }) {
  return (
    <label className="block">
      <span className="block text-xs font-medium text-gray-500 mb-1">{label}</span>
      {children}
    </label>
  )
}
