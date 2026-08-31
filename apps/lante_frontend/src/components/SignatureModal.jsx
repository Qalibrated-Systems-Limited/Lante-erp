import { useRef, useEffect, useState } from 'react'

function SignaturePad({ label, onCapture }) {
  const canvasRef = useRef(null)
  const drawing = useRef(false)
  const [hasStrokes, setHasStrokes] = useState(false)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    ctx.strokeStyle = '#1a1a1a'
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
  }, [])

  const pos = (e) => {
    const rect = canvasRef.current.getBoundingClientRect()
    const src = e.touches ? e.touches[0] : e
    return { x: src.clientX - rect.left, y: src.clientY - rect.top }
  }

  const start = (e) => {
    e.preventDefault()
    drawing.current = true
    const ctx = canvasRef.current.getContext('2d')
    const { x, y } = pos(e)
    ctx.beginPath(); ctx.moveTo(x, y)
  }

  const move = (e) => {
    e.preventDefault()
    if (!drawing.current) return
    const ctx = canvasRef.current.getContext('2d')
    const { x, y } = pos(e)
    ctx.lineTo(x, y); ctx.stroke()
    setHasStrokes(true)
    onCapture(canvasRef.current.toDataURL('image/png'))
  }

  const stop = (e) => {
    e.preventDefault()
    drawing.current = false
  }

  const clear = () => {
    const canvas = canvasRef.current
    const ctx = canvas.getContext('2d')
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    setHasStrokes(false)
    onCapture(null)
  }

  return (
    <div>
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">{label}</p>
      <div className="relative border-2 border-dashed border-gray-300 rounded-lg bg-gray-50 touch-none">
        <canvas
          ref={canvasRef}
          width={340} height={130}
          className="rounded-lg w-full"
          onMouseDown={start} onMouseMove={move} onMouseUp={stop} onMouseLeave={stop}
          onTouchStart={start} onTouchMove={move} onTouchEnd={stop}
        />
        {!hasStrokes && (
          <p className="absolute inset-0 flex items-center justify-center text-xs text-gray-400 pointer-events-none">
            Draw signature here
          </p>
        )}
      </div>
      {hasStrokes && (
        <button type="button" onClick={clear}
          className="mt-1 text-xs text-red-500 hover:text-red-700 underline">
          Clear
        </button>
      )}
    </div>
  )
}

export default function SignatureModal({ isOpen, onClose, onSubmit, loading }) {
  const today = new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'long', year: 'numeric' })

  const [customerName, setCustomerName] = useState('')
  const [customerSig, setCustomerSig] = useState(null)
  const [techName, setTechName] = useState('')
  const [techSig, setTechSig] = useState(null)

  const reset = () => {
    setCustomerName(''); setCustomerSig(null)
    setTechName(''); setTechSig(null)
  }

  const handleClose = () => { reset(); onClose() }

  const handleSubmit = () => {
    onSubmit({
      customerName,
      customerSignatureData: customerSig ?? '',
      technicianName: techName,
      technicianSignatureData: techSig ?? '',
    })
  }

  const canSubmit = customerName.trim() && customerSig && techName.trim() && techSig

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-6 pt-5 pb-3 border-b border-gray-100">
          <h2 className="text-base font-bold text-gray-800">Sign & Submit Service Report</h2>
          <button onClick={handleClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none">&times;</button>
        </div>

        <div className="px-6 py-5 space-y-6">
          <p className="text-xs text-gray-400">Date: <span className="font-medium text-gray-600">{today}</span></p>

          {/* Customer */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700">Customer Name</label>
            <input
              value={customerName}
              onChange={e => setCustomerName(e.target.value)}
              placeholder="Enter customer name"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
            <SignaturePad label="Customer Signature" onCapture={setCustomerSig} />
          </div>

          <hr className="border-gray-100" />

          {/* Technician */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700">Technician Name</label>
            <input
              value={techName}
              onChange={e => setTechName(e.target.value)}
              placeholder="Enter technician name"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-amber-400"
            />
            <SignaturePad label="Technician Signature" onCapture={setTechSig} />
          </div>
        </div>

        <div className="flex justify-end gap-3 px-6 pb-5">
          <button onClick={handleClose}
            className="px-4 py-2 text-sm font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={!canSubmit || loading}
            className="px-5 py-2 text-sm font-semibold text-black bg-amber-400 hover:bg-amber-500 disabled:opacity-40 disabled:cursor-not-allowed rounded-lg transition-colors">
            {loading ? 'Submitting…' : 'Submit'}
          </button>
        </div>
      </div>
    </div>
  )
}
