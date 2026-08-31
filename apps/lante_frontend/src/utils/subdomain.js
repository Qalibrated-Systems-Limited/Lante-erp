/**
 * subdomain.js — tenant/subdomain helpers.
 *
 * A tenant's slug doubles as its subdomain: <slug>.qalibrated.co.ke
 * The platform/super-admin portal lives on the reserved `support` subdomain.
 *
 * Phase 1: these are groundwork utilities. Runtime host-based routing that
 * consumes getSubdomain() lands in Phase 3.
 */

export const BASE_DOMAIN = import.meta.env.VITE_BASE_DOMAIN || 'qalibrated.co.ke'

// Hostnames reserved for platform / infra — must mirror TenantSlug.Reserved on the backend.
export const RESERVED_SUBDOMAINS = new Set([
  'www', 'support', 'api', 'app', 'admin', 'mail', 'platform',
  'static', 'assets', 'cdn', 'help', 'docs', 'status', 'dashboard', 'portal',
])

const SLUG_FORMAT = /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/

/** True if a slug is a well-formed subdomain (2–50 chars, lowercase, single hyphens, starts with a letter). */
export function isValidSlug(slug) {
  return typeof slug === 'string' && slug.length >= 2 && slug.length <= 50 && SLUG_FORMAT.test(slug)
}

/** True if a slug collides with a reserved platform/infra hostname. */
export function isReservedSlug(slug) {
  return typeof slug === 'string' && RESERVED_SUBDOMAINS.has(slug.trim().toLowerCase())
}

/**
 * Extracts the tenant subdomain from the current host.
 * Returns the label (e.g. "qsl"), or null on the apex/marketing host or in local dev.
 */
export function getSubdomain(hostname = window.location.hostname) {
  if (!hostname || hostname === 'localhost' || /^[0-9.]+$/.test(hostname)) return null
  const suffix = '.' + BASE_DOMAIN
  if (hostname === BASE_DOMAIN || !hostname.endsWith(suffix)) return null
  const label = hostname.slice(0, -suffix.length).split('.')[0]
  return label ? label.toLowerCase() : null
}

/** True when the current host is the platform/super-admin portal. */
export function isPlatformHost(hostname = window.location.hostname) {
  return getSubdomain(hostname) === 'support'
}
