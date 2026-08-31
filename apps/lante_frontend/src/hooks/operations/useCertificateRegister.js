import { useState, useEffect, useCallback } from 'react'
import { useAuth } from '../../context/AuthContext.jsx'
import * as opsApi from '../../services/operations.js'

// O6.1 — data for the issued-certificate register: the paged list, the audit counts, and the
// withdraw action. Filters are held here and passed to both endpoints so the counts always
// describe exactly the same set of certificates as the table beneath them.
export function useCertificateRegister() {
  const { hasPermission } = useAuth()
  const canWithdraw = hasPermission('calibration.sign')

  const [items, setItems]     = useState([])
  const [summary, setSummary] = useState(null)
  const [total, setTotal]     = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState('')

  const [page, setPage]       = useState(1)
  const [filters, setFilters] = useState({ q: '', validity: '', sheetType: '', issuedFrom: '', issuedTo: '' })

  const pageSize = 50

  // Only send filters that are actually set — an empty string would otherwise be matched literally.
  const activeFilters = useCallback(() => {
    const p = {}
    for (const [k, v] of Object.entries(filters)) if (v) p[k] = v
    return p
  }, [filters])

  const load = useCallback(async () => {
    setLoading(true); setError('')
    const f = activeFilters()
    try {
      const [list, sum] = await Promise.all([
        opsApi.listCertificates({ ...f, page, pageSize }),
        opsApi.getCertificateSummary(f),
      ])
      setItems(list?.items ?? [])
      setTotal(list?.totalCount ?? 0)
      setSummary(sum ?? null)
    } catch {
      setError('Failed to load the certificate register.')
    } finally {
      setLoading(false)
    }
  }, [activeFilters, page])

  useEffect(() => { load() }, [load])

  // Any filter change invalidates the current page number — page 7 of the old result set is
  // meaningless against the new one.
  const updateFilter = (key, value) => {
    setPage(1)
    setFilters(f => ({ ...f, [key]: value }))
  }

  const resetFilters = () => {
    setPage(1)
    setFilters({ q: '', validity: '', sheetType: '', issuedFrom: '', issuedTo: '' })
  }

  const withdraw = async (id, reason) => {
    await opsApi.withdrawCertificate(id, { reason })
    await load()
  }

  // The PDF endpoint needs an Authorization header, so it can't be an <iframe src>. Fetch it as a
  // blob and hand back an object URL — the caller must revoke it when the viewer closes, or every
  // preview leaks a copy of the document for the life of the tab.
  const loadPdfUrl = async (id) => URL.createObjectURL(await opsApi.getRegisterCertificatePdf(id))

  return {
    items, summary, total, loading, error,
    page, setPage, pageSize, totalPages: Math.max(1, Math.ceil(total / pageSize)),
    filters, updateFilter, resetFilters,
    canWithdraw, withdraw, loadPdfUrl, reload: load,
  }
}

// Maps a certificate's derived validity to a { variant, label } badge descriptor.
export function validityBadge(cert) {
  const d = cert?.daysToDue
  switch (cert?.validity) {
    case 'Withdrawn': return { variant: 'default', label: 'Withdrawn' }
    case 'Expired':   return { variant: 'red',     label: d != null ? `Expired ${Math.abs(d)}d ago` : 'Expired' }
    case 'Expiring':  return { variant: 'amber',   label: d != null ? `Due in ${d}d` : 'Expiring' }
    case 'Valid':     return { variant: 'green',   label: d != null ? `Valid · ${d}d` : 'Valid' }
    default:          return { variant: 'default', label: 'No due date' }
  }
}

// Which recall tiers have gone out, for the audit trail column.
export function recallTrail(cert) {
  const sent = []
  if (cert?.recall60SentAt) sent.push('60d')
  if (cert?.recall30SentAt) sent.push('30d')
  if (cert?.recall7SentAt)  sent.push('7d')
  return sent
}
