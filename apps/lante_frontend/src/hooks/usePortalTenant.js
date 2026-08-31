import { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import api from '../api/axios'

// Brand shown on the public portal when the URL carries no company slug (the plain /portal/*
// and /staff paths). Deliberately the platform's own name — never a specific customer.
const DEFAULT_BRAND = 'Lante'

/**
 * Resolves the display brand for the public portal from the optional `:slug` route segment
 * (/portal/:slug/*, /staff/:slug). Works from any component rendered under those routes —
 * including the nested header/footer shells — since useParams() reads the matched route, not
 * the component tree. Falls back to DEFAULT_BRAND when there is no slug or the lookup fails,
 * so an unknown/inactive slug degrades to the platform brand instead of an error state.
 */
export default function usePortalTenant() {
  const { slug } = useParams()
  const [name, setName] = useState(DEFAULT_BRAND)
  const [logoUrl, setLogoUrl] = useState('')
  const [loading, setLoading] = useState(!!slug)

  useEffect(() => {
    if (!slug) {
      setName(DEFAULT_BRAND)
      setLogoUrl('')
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    api.get(`/api/v1/tenants/public/${encodeURIComponent(slug)}`)
      // user-service wraps every response in the ApiResponse envelope ({ success, data, ... })
      // and serializes properties camelCased, so the tenant sits at res.data.data.name.
      .then(res => {
        if (cancelled) return
        setName(res.data?.data?.name || DEFAULT_BRAND)
        setLogoUrl(res.data?.data?.logoUrl || '')
      })
      .catch(() => { if (!cancelled) { setName(DEFAULT_BRAND); setLogoUrl('') } })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, [slug])

  return { name, slug, logoUrl, loading }
}
