import { useRef, useState, useEffect } from 'react'
import { Modal, Input, Btn, Alert } from '../../ui.jsx'

// O11.4 — capture one mandatory handover signature (role fixed by the caller). Name is required; the
// drawn signature is optional. Compact inline drawpad (mirrors components/SignatureModal's canvas).
export default function HandoverSignatureModal({ role, roleLabel, existing, onClose, onSave }) {
  const canvasRef = useRef(null)
  const drawing = useRef(false)
  const [name, setName]   = useState(existing?.signatoryName ?? '')
  const [busy, setBusy]   = useState(false)
  const [error, setError] = useState('')
  const [dirty, setDirty] = useState(false)

  useEffect(() => {
    const c = canvasRef.current
    if (!c) return
    const ctx = c.getContext('2d')
    ctx.strokeStyle = '#1a1a1a'; ctx.lineWidth = 2; ctx.lineCap = 'round'; ctx.lineJoin = 'round'
  }, [])

  const pos = (e) => {
    const r = canvasRef.current.getBoundingClientRect()
    const s = e.touches ? e.touches[0] : e
    return { x: s.clientX - r.left, y: s.clientY - r.top }
  }
  const start = (e) => { e.preventDefault(); drawing.current = true; const ctx = canvasRef.current.getContext('2d'); const { x, y } = pos(e); ctx.beginPath(); ctx.moveTo(x, y) }
  const move  = (e) => { if (!drawing.current) return; e.preventDefault(); const ctx = canvasRef.current.getContext('2d'); const { x, y } = pos(e); ctx.lineTo(x, y); ctx.stroke(); setDirty(true) }
  const stop  = () => { drawing.current = false }
  const clear = () => { const c = canvasRef.current; c.getContext('2d').clearRect(0, 0, c.width, c.height); setDirty(false) }

  const submit = async () => {
    if (!name.trim()) { setError('Signatory name is required.'); return }
    setBusy(true); setError('')
    try {
      await onSave({ role, signatoryName: name.trim(), signatureData: dirty ? canvasRef.current.toDataURL('image/png') : null })
      onClose()
    } catch (e) {
      setError(e?.response?.data?.message || 'Could not save the signature.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title={`Sign — ${roleLabel}`} onClose={onClose} width={460}>
      {error && <Alert type="error">{error}</Alert>}
      <Input label="Signatory name" value={name} onChange={setName} required />
      <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#5b6b7c', marginBottom: 5 }}>Signature (optional)</label>
      <canvas ref={canvasRef} width={400} height={130}
        onMouseDown={start} onMouseMove={move} onMouseUp={stop} onMouseLeave={stop}
        onTouchStart={start} onTouchMove={move} onTouchEnd={stop}
        style={{ width: '100%', height: 130, border: '1.5px dashed #cdd6df', borderRadius: 8, touchAction: 'none', background: '#fff' }} />
      <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 10 }}>
        <Btn variant="ghost" size="sm" onClick={clear}>Clear</Btn>
        <div style={{ display: 'flex', gap: 10 }}>
          <Btn variant="ghost" onClick={onClose}>Cancel</Btn>
          <Btn variant="primary" onClick={submit} disabled={busy}>{busy ? 'Saving…' : 'Sign'}</Btn>
        </div>
      </div>
    </Modal>
  )
}
