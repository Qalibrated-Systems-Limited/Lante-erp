import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import api from '../../api/axios.js'
import useCompanyBranding from '../../hooks/useCompanyBranding.js'

function fmtDate(iso) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('en-US', { weekday: 'short', year: 'numeric', month: 'short', day: '2-digit' })
}

function fmt(n) {
  return Number(n ?? 0).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

export default function QuotationPrintPage() {
  const { id }     = useParams()
  const navigate   = useNavigate()
  const printRef   = useRef()
  const branding   = useCompanyBranding()

  const [sr,      setSr]      = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState(null)

  useEffect(() => {
    api.get(`/api/v1/service-requests/${id}`)
      .then(r => setSr(r.data?.data ?? r.data))
      .catch(() => setError('Failed to load quotation'))
      .finally(() => setLoading(false))
  }, [id])

  if (loading) return (
    <div className="min-h-screen flex items-center justify-center bg-gray-100">
      <p className="text-gray-500 text-sm">Loading…</p>
    </div>
  )

  if (error || !sr?.quotation) return (
    <div className="min-h-screen flex items-center justify-center bg-gray-100">
      <div className="text-center">
        <p className="text-sm text-red-500 mb-3">{error ?? 'No quotation found for this service request.'}</p>
        <button onClick={() => navigate(-1)} className="text-xs text-indigo-600 underline">Go back</button>
      </div>
    </div>
  )

  const q           = sr.quotation
  const lineItems   = q.lineItems ?? []
  const subtotal    = q.subtotal  ?? 0
  const vatRate     = q.vatRate   ?? 0.16
  const vatAmount   = q.vatAmount ?? 0
  const total       = q.totalAmount ?? 0
  const vatPct      = Math.round(vatRate * 100)
  const quoteDate   = fmtDate(q.createdAt ?? q.sentAt)
  const validUntil  = q.validUntil ? fmtDate(q.validUntil) : null

  const quoteNumber = q.quotationNumber ?? '—'
  const docRef      = `${branding.docPrefix}/QP/013- ${quoteNumber}`

  const customerName = sr.clientOrganization || sr.clientName
  const contactPerson = sr.clientOrganization ? sr.clientName : null
  const customerAddress = sr.clientAddress || sr.siteLocation || null
  const description = sr.description || null

  return (
    <div className="min-h-screen bg-gray-100 py-8 px-4">

      {/* Toolbar */}
      <div className="max-w-4xl mx-auto mb-4 flex items-center justify-between print:hidden">
        <button onClick={() => navigate(-1)} className="flex items-center gap-2 text-sm text-gray-600 hover:text-gray-900">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
          Back
        </button>
        <button onClick={() => window.print()}
          className="flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-lg transition-colors">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
          </svg>
          Print
        </button>
      </div>

      {/* Quotation document */}
      <div ref={printRef} className="max-w-4xl mx-auto bg-white shadow-lg print:shadow-none print:max-w-none font-sans text-gray-900 p-10 print:p-8">

        {/* ── Header: Logo | QUOTE title ── */}
        <div className="flex items-start justify-between mb-6">
          <img src={branding.logoUrl || "/qc-logo.png"} alt={branding.displayName} className="h-20 object-contain" />
          <div className="text-right">
            <p className="text-5xl font-bold text-gray-800 tracking-wide">QUOTE</p>
            <p className="text-base font-semibold text-gray-700 mt-1">{docRef}</p>
          </div>
        </div>

        {/* ── Company address ── */}
        <div className="mb-8 text-sm leading-6">
          <p className="font-bold">Inventing &amp; Making Happen</p>
          <p>P.O BOX 34463 - 00100</p>
          <p>NAIROBI  KENYA</p>
          <p>Kenya</p>
          <p>PIN P051889248C</p>
          <p>+254 714 999 996</p>
          <p>accounts@qalibrated.co.ke</p>
          <p>www.qalibrated.co.ke</p>
        </div>

        {/* ── Bill To + Quote Date ── */}
        <div className="flex items-start justify-between mb-6">
          <div className="text-sm leading-6">
            <p className="text-gray-500 mb-1">Bill To</p>
            <p className="font-bold">{customerName}</p>
            {contactPerson && <p>{contactPerson}</p>}
            {customerAddress && <p>{customerAddress}</p>}
          </div>
          <div className="text-sm text-right">
            <span className="text-gray-600">Quote Date :&nbsp;&nbsp;</span>
            <span>{quoteDate}</span>
          </div>
        </div>

        {/* ── Description ── */}
        {description && (
          <div className="mb-6 text-sm">
            <p className="text-gray-500 mb-1">Description :</p>
            <p>{description}</p>
          </div>
        )}

        {/* ── Line items table ── */}
        <table className="w-full text-sm mb-6 border-collapse">
          <thead>
            <tr className="bg-gray-700 text-white">
              <th className="px-3 py-2 text-left font-semibold w-8">#</th>
              <th className="px-3 py-2 text-left font-semibold">Description</th>
              <th className="px-3 py-2 text-right font-semibold w-20">Qty</th>
              <th className="px-3 py-2 text-right font-semibold w-28">Rate</th>
              <th className="px-3 py-2 text-right font-semibold w-28">Amount</th>
            </tr>
          </thead>
          <tbody>
            {lineItems.map((li, i) => (
              <tr key={i} className="border-b border-gray-200">
                <td className="px-3 py-2 text-gray-600">{i + 1}</td>
                <td className="px-3 py-2">{li.description}</td>
                <td className="px-3 py-2 text-right">{fmt(li.quantity)}</td>
                <td className="px-3 py-2 text-right">{fmt(li.unitPrice)}</td>
                <td className="px-3 py-2 text-right">{fmt(li.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {/* ── Totals ── */}
        <div className="flex justify-end mb-8">
          <table className="text-sm w-72">
            <tbody>
              <tr>
                <td className="py-1 pr-6 text-right text-gray-600">Sub Total</td>
                <td className="py-1 text-right font-medium">{fmt(subtotal)}</td>
              </tr>
              <tr>
                <td className="py-1 pr-6 text-right text-gray-600">General Rate ({vatPct}%)</td>
                <td className="py-1 text-right font-medium">{fmt(vatAmount)}</td>
              </tr>
              <tr className="border-t border-gray-300">
                <td className="py-2 pr-6 text-right font-bold">Total</td>
                <td className="py-2 text-right font-bold">KES{fmt(total)}</td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* ── Notes ── */}
        {q.notes && (
          <div className="mb-8 text-sm">
            <p className="font-semibold text-gray-700 mb-2">Notes</p>
            <p className="text-gray-600">{q.notes}</p>
          </div>
        )}

        {/* ── Terms & Conditions ── */}
        <div className="mb-10 text-sm">
          <p className="font-semibold text-gray-700 mb-2">Terms &amp; Conditions</p>
          <p className="text-gray-600 mb-1">Dear valued customer</p>
          <ol className="list-decimal list-inside space-y-1 text-gray-600">
            <li>Sale of goods and services are subject to the standard terms and conditions of sale</li>
            <li>All quoted prices are valid for {validUntil ? `until ${validUntil}` : '30 days'}</li>
            <li>Anything else noted to be defective as the work progresses shall be quoted for separately</li>
          </ol>
        </div>

        {/* ── Footer ── */}
        <div className="border-t border-gray-300 pt-4 text-center text-xs text-gray-500">
          <p>{[branding.legalName, branding.address, branding.phone].filter(Boolean).join(', ')}</p>
          {branding.email && <p>{branding.email}</p>}
          <p className="mt-2">{quoteNumber} - 1 of 1</p>
        </div>

      </div>

      <style>{`
        @media print {
          body { background: white; }
          .print\\:hidden { display: none !important; }
          .print\\:shadow-none { box-shadow: none !important; }
        }
      `}</style>
    </div>
  )
}
