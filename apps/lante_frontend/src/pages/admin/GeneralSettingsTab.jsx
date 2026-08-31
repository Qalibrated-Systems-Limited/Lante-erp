import { useState, useEffect } from 'react'
import api from '../../api/axios.js'
import { Card, Btn, Alert, Loading, SectionHeader, FileInput } from '../../components/ui.jsx'
import { T } from '../../theme/tokens.js'

// Generic per-tenant settings registry — mirrors the reference QSL admin's SETTINGS_REGISTRY,
// now backed by a real endpoint (api/v1/system-settings) in user-service instead of being
// hardcoded/absent. Each field maps to one settings key; adding a field here makes it editable
// immediately, no backend change needed since the store is a plain key/value table.
const SETTINGS_REGISTRY = [
  { category: 'company', label: 'Company Identity', help: 'Shown on invoices, certificates, payslips and PDF exports.', fields: [
    { key: 'company.legal_name', label: 'Legal Name', type: 'text', def: '' },
    { key: 'company.kra_pin', label: 'KRA PIN', type: 'text', def: '' },
    { key: 'company.address', label: 'Address', type: 'text', def: '' },
    { key: 'company.phone', label: 'Phone', type: 'text', def: '' },
    { key: 'company.email', label: 'Email', type: 'text', def: '' },
  ]},
  { category: 'branding', label: 'Branding & Theme', help: 'Logo, colours and name applied across the app.', fields: [
    { key: 'branding.company_display_name', label: 'Display Name', type: 'text', def: '' },
    { key: 'branding.logo_url', label: 'Company Logo', type: 'file', def: '' },
    { key: 'branding.doc_code_prefix', label: 'Document Code Prefix', type: 'text', def: 'LT',
      help: 'Used in form/document reference codes (e.g. XX/QP/003/MRF) and certificate control IDs.' },
    { key: 'branding.primary_color', label: 'Primary Colour', type: 'color', def: '#1B3A5C' },
    { key: 'branding.accent_color', label: 'Accent Colour', type: 'color', def: '#C8960C' },
  ]},
  { category: 'general', label: 'General', fields: [
    { key: 'general.default_currency', label: 'Default Currency', type: 'select', options: ['KES', 'USD', 'CNY'], def: 'KES' },
    { key: 'general.fiscal_year_start', label: 'Fiscal Year Start (MM-DD)', type: 'text', def: '01-01' },
  ]},
  { category: 'finance', label: 'Finance', fields: [
    { key: 'finance.vat_rate', label: 'VAT Rate', type: 'percent', def: '0.16' },
    { key: 'finance.imprest_retire_days', label: 'Imprest Retire Days', type: 'number', def: '14' },
    { key: 'finance.pay_limit_staff', label: 'Payment Limit — Staff (Kshs)', type: 'number', def: '5000' },
    { key: 'finance.pay_limit_dept_head', label: 'Payment Limit — Dept Head (Kshs)', type: 'number', def: '20000' },
    { key: 'finance.pay_limit_finance_mgr', label: 'Payment Limit — Finance Mgr (Kshs)', type: 'number', def: '100000' },
    { key: 'finance.pay_limit_cfo', label: 'Payment Limit — CFO (Kshs)', type: 'number', def: '500000' },
  ]},
  { category: 'msp', label: 'Minimum Selling Price Margins', help: 'Minimum margin by category used for the MSP floor.', fields: [
    { key: 'msp.margin_calibration', label: 'Calibration Equipment', type: 'percent', def: '0.25' },
    { key: 'msp.margin_construction', label: 'Construction Materials', type: 'percent', def: '0.15' },
    { key: 'msp.margin_spare_parts', label: 'Spare Parts', type: 'percent', def: '0.30' },
    { key: 'msp.margin_tools', label: 'Tools', type: 'percent', def: '0.20' },
    { key: 'msp.margin_safety', label: 'Safety Equipment', type: 'percent', def: '0.20' },
  ]},
  { category: 'alerts', label: 'Alert Windows (days before)', fields: [
    { key: 'alerts.cert_expiry_days', label: 'Certificate Expiry', type: 'number', def: '60' },
    { key: 'alerts.debtor_escalation_days', label: 'Debtor Escalation', type: 'number', def: '30' },
    { key: 'alerts.insurance_alert_days', label: 'Vehicle Insurance', type: 'number', def: '30' },
    { key: 'alerts.tender_alert_days', label: 'Tender Deadline', type: 'number', def: '14' },
  ]},
]

export default function GeneralSettingsTab() {
  const [settings, setSettings] = useState([])
  const [draft, setDraft] = useState({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [msg, setMsg] = useState(null)
  const [uploadingLogo, setUploadingLogo] = useState(false)

  useEffect(() => { load() }, [])

  async function load() {
    setLoading(true)
    setError('')
    try {
      const res = await api.get('/api/v1/system-settings')
      setSettings(res.data?.data ?? [])
    } catch {
      setError('Failed to load settings.')
    } finally {
      setLoading(false)
    }
  }

  const settingsMap = Object.fromEntries(settings.map(s => [s.key, s.value]))

  const dispVal = (f) => {
    if (draft[f.key] !== undefined) return draft[f.key]
    let v = settingsMap[f.key] ?? f.def
    if (f.type === 'percent') { const n = parseFloat(v); return Number.isFinite(n) ? String(+(n * 100).toFixed(4)) : v }
    return v ?? ''
  }
  const setVal = (key, value) => setDraft(d => ({ ...d, [key]: value }))
  const toStored = (f, display) => {
    if (f.type === 'percent') { const n = parseFloat(display); return String(Number.isFinite(n) ? n / 100 : 0) }
    return String(display)
  }

  async function uploadLogo(file) {
    if (!file) return
    setUploadingLogo(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const res = await api.post('/api/v1/system-settings/logo', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      const url = res.data?.data?.url
      if (url) setVal('branding.logo_url', url)
    } catch (err) {
      setMsg({ type: 'error', text: err.response?.data?.message ?? 'Logo upload failed.' })
    } finally {
      setUploadingLogo(false)
    }
  }

  async function saveGroup(group) {
    const dirty = group.fields.filter(f => draft[f.key] !== undefined)
    if (!dirty.length) { setMsg({ type: 'info', text: 'No changes in this section.' }); return }
    try {
      for (const f of dirty) {
        const value = toStored(f, draft[f.key])
        await api.put('/api/v1/system-settings', { key: f.key, value })
      }
      setMsg({ type: 'success', text: `${group.label}: ${dirty.length} setting(s) saved.` })
      setDraft(d => { const n = { ...d }; group.fields.forEach(f => delete n[f.key]); return n })
      load()
    } catch (err) {
      setMsg({ type: 'error', text: err.response?.data?.message ?? 'Failed to save.' })
    }
  }

  if (loading) return <Loading />

  return (
    <>
      <Alert type="info">Adjust system-wide behaviour without a deploy — VAT rate, payment limits, MSP margins, alert timings and branding are read live by the relevant modules.</Alert>
      {error && <Alert type="error">{error}</Alert>}
      {msg && <Alert type={msg.type}>{msg.text}</Alert>}

      {SETTINGS_REGISTRY.map(group => {
        const dirty = group.fields.some(f => draft[f.key] !== undefined)
        return (
          <Card key={group.category} style={{ marginBottom: 14 }}>
            <SectionHeader title={group.label} sub={group.help} action={
              <Btn size="sm" disabled={!dirty} onClick={() => saveGroup(group)}>{dirty ? 'Save Changes' : 'Saved'}</Btn>
            } />
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 18px' }}>
              {group.fields.map(f => (
                <div key={f.key}>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: T.dgrey, marginBottom: 4 }}>
                    {f.label}{f.type === 'percent' && <span style={{ color: T.mgrey, fontWeight: 400 }}> (%)</span>}
                  </label>
                  {f.type === 'file' ? (
                    <FileInput accept="image/png,image/jpeg,image/svg+xml" uploading={uploadingLogo}
                      fileUrl={dispVal(f)} onFileSelected={uploadLogo} note="PNG, JPEG or SVG, up to 2MB." />
                  ) : f.type === 'color' ? (
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                      <input type="color" value={dispVal(f)} onChange={e => setVal(f.key, e.target.value)}
                        style={{ width: 42, height: 32, padding: 0, border: `1px solid ${T.lgrey}`, borderRadius: 5, cursor: 'pointer' }} />
                      <input value={dispVal(f)} onChange={e => setVal(f.key, e.target.value)}
                        style={{ flex: 1, padding: '7px 10px', border: `1px solid ${T.lgrey}`, borderRadius: 6, fontSize: 13, fontFamily: 'monospace' }} />
                    </div>
                  ) : f.type === 'select' ? (
                    <select value={dispVal(f)} onChange={e => setVal(f.key, e.target.value)}
                      style={{ width: '100%', padding: '8px 10px', border: `1px solid ${T.lgrey}`, borderRadius: 6, fontSize: 13, background: T.white }}>
                      {f.options.map(o => <option key={o} value={o}>{o}</option>)}
                    </select>
                  ) : (
                    <input type={f.type === 'number' || f.type === 'percent' ? 'number' : 'text'} step="any"
                      value={dispVal(f)} onChange={e => setVal(f.key, e.target.value)}
                      style={{ width: '100%', padding: '8px 10px', border: `1px solid ${T.lgrey}`, borderRadius: 6, fontSize: 13 }} />
                  )}
                </div>
              ))}
            </div>
          </Card>
        )
      })}
    </>
  )
}
