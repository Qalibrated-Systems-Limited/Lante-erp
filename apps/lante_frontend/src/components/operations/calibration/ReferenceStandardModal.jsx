import { useState } from 'react'
import { Modal, Input, Select, Btn, Alert } from '../../ui.jsx'

// O11.5 — create / edit a reference standard. Presentational; onSave(dto, id) does the API work.
export default function ReferenceStandardModal({ standard, onClose, onSave }) {
  const editing = !!standard?.id
  const [f, setF] = useState({
    assetId:            standard?.assetId ?? '',
    description:        standard?.description ?? '',
    nominalValue:       standard?.nominalValue ?? '',
    accuracyClass:      standard?.accuracyClass ?? '',
    traceabilityCertNo: standard?.traceabilityCertNo ?? '',
    issuingBody:        standard?.issuingBody ?? '',
    certUncertainty:    standard?.certUncertainty ?? '',
    coverageFactor:     standard?.coverageFactor ?? 2,
    calibrationDate:    standard?.calibrationDate?.slice(0, 10) ?? '',
    nextDueDate:        standard?.nextDueDate?.slice(0, 10) ?? '',
    status:             standard?.status ?? 'Active',
  })
  const [saving, setSaving] = useState(false)
  const [error, setError]   = useState('')

  const set = (k) => (v) => setF(prev => ({ ...prev, [k]: v }))

  const submit = async () => {
    if (!f.assetId.trim() || !f.description.trim()) { setError('Asset ID and description are required.'); return }
    setSaving(true); setError('')
    try {
      const dto = {
        ...f,
        certUncertainty: f.certUncertainty === '' ? null : Number(f.certUncertainty),
        coverageFactor:  Number(f.coverageFactor) || 2,
        calibrationDate: f.calibrationDate || null,
        nextDueDate:     f.nextDueDate || null,
      }
      if (!editing) delete dto.status
      await onSave(dto, standard?.id)
      onClose()
    } catch {
      setError('Could not save the reference standard.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={editing ? `Edit ${standard.assetId}` : 'New Reference Standard'} onClose={onClose} width={620}>
      {error && <Alert type="error">{error}</Alert>}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
        <Input label="Asset ID"        value={f.assetId}      onChange={set('assetId')} required />
        <Input label="Accuracy class"  value={f.accuracyClass} onChange={set('accuracyClass')} placeholder="e.g. OIML F1" />
        <div style={{ gridColumn: '1 / -1' }}>
          <Input label="Description"   value={f.description}   onChange={set('description')} required />
        </div>
        <Input label="Nominal value"   value={f.nominalValue}  onChange={set('nominalValue')} placeholder="e.g. 1 kg" />
        <Input label="Issuing body"    value={f.issuingBody}   onChange={set('issuingBody')} placeholder="KEBS / accredited lab" />
        <Input label="Traceability certificate #" value={f.traceabilityCertNo} onChange={set('traceabilityCertNo')} />
        <Input label="Cert uncertainty (U)" type="number" value={f.certUncertainty} onChange={set('certUncertainty')} />
        <Input label="Coverage factor (k)"  type="number" value={f.coverageFactor}  onChange={set('coverageFactor')} />
        <div />
        <Input label="Last calibration" type="date" value={f.calibrationDate} onChange={set('calibrationDate')} />
        <Input label="Next due date"    type="date" value={f.nextDueDate}     onChange={set('nextDueDate')} note="Drives the 60/30-day expiry alerts." />
        {editing && (
          <Select label="Status" value={f.status} onChange={set('status')}
            options={[{ value: 'Active', label: 'Active' }, { value: 'Retired', label: 'Retired' }]} />
        )}
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
        <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
        <Btn variant="primary" onClick={submit} disabled={saving}>{saving ? 'Saving…' : editing ? 'Save changes' : 'Create'}</Btn>
      </div>
    </Modal>
  )
}
