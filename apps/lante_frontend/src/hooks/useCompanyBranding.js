import { useState, useEffect } from 'react'
import api from '../api/axios.js'

// Fallback identity — the real accredited lab this deployment has always served, exactly as
// hardcoded (before this hook existed) in CertificatePdfService.cs's LabName/LabSuffix/LabLine1/
// LabLine2 consts and mirrored across the print pages that use this hook. Any tenant that saves
// its own company.legal_name / branding.company_display_name / company.address / company.phone /
// company.email overrides these immediately via the admin General Settings tab. A brand-new
// tenant with nothing configured still needs *something* to print — "Lante" would be wrong there
// too, since Lante is the software, not any tenant's real business name, so this default is the
// safest fallback: it preserves current behaviour exactly for the existing tenant.
export const DEFAULT_LEGAL_NAME = 'QALIBRATED SYSTEMS LIMITED'
export const DEFAULT_DISPLAY_NAME = 'QALIBRATED SYSTEMS LIMITED'
export const DEFAULT_ADDRESS = 'Calibration Laboratory · P.O. Box 47400–00100, Nairobi, Kenya'
export const DEFAULT_PHONE = '+254 714 999 996'
export const DEFAULT_EMAIL = 'info@qalibrated.co.ke'
// The prefix used in form/document reference codes (e.g. XX/QP/003/MRF) and certificate control
// IDs — tenant-editable so a new tenant isn't stuck with either the old "QSL" or a hardcoded "LT".
export const DEFAULT_DOC_PREFIX = 'LT'

// Module-level cache — every page that mounts this hook during the session shares one fetch
// instead of each print/certificate page re-requesting the same settings on its own mount.
let cache = null
let inflight = null

function parse(list) {
  const map = Object.fromEntries((list ?? []).map(s => [s.key, s.value]))
  const pick = (key, fallback) => {
    const v = map[key]
    return typeof v === 'string' && v.trim() ? v : fallback
  }
  return {
    legalName:   pick('company.legal_name', DEFAULT_LEGAL_NAME),
    displayName: pick('branding.company_display_name', DEFAULT_DISPLAY_NAME),
    address:     pick('company.address', DEFAULT_ADDRESS),
    phone:       pick('company.phone', DEFAULT_PHONE),
    email:       pick('company.email', DEFAULT_EMAIL),
    docPrefix:   pick('branding.doc_code_prefix', DEFAULT_DOC_PREFIX),
    // '' (not a static asset path) — callers fall back to whichever logo file they already
    // hardcode today, since different pages currently show different default images.
    logoUrl:     pick('branding.logo_url', ''),
  }
}

/**
 * Resolves the authenticated tenant's own company identity for document-generating pages —
 * calibration certificates, quotations, requisition forms, SOPs, service reports — replacing
 * what used to be a hardcoded "QALIBRATED SYSTEMS LIMITED" string scattered across each page.
 * Reads the same `/api/v1/system-settings` registry the admin's General Settings tab already
 * edits, so a tenant that fills in its own legal name/address/contact details there sees them
 * reflected on its documents with no further wiring.
 *
 * Unlike usePortalTenant (anonymous portal routes resolved by a `:slug` param), this hook is for
 * authenticated in-app pages: there is no slug, just the current session's tenant, so it calls
 * system-settings directly.
 */
export default function useCompanyBranding() {
  const [data, setData] = useState(cache ?? parse(null))
  const [loading, setLoading] = useState(!cache)

  useEffect(() => {
    if (cache) { setData(cache); setLoading(false); return }
    if (!inflight) {
      inflight = api.get('/api/v1/system-settings')
        .then(res => { cache = parse(res.data?.data); return cache })
        .catch(() => { cache = parse(null); return cache })
    }
    let cancelled = false
    inflight.then(result => { if (!cancelled) { setData(result); setLoading(false) } })
    return () => { cancelled = true }
  }, [])

  return { ...data, loading }
}
