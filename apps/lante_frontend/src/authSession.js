// Lets modules outside the React tree (the axios response interceptor) clear AuthContext's
// in-memory session state instead of only localStorage. Without this, a 401 on any ordinary API
// call (session expired mid-use, revoked elsewhere, ...) clears localStorage and navigates to
// /login, but `token`/`user` in AuthContext stay stale in memory — isAuthenticated never flips to
// false, so anything gated on it (route guards, and any context that clears its own state on a
// login/logout transition) never notices the session actually ended. Registered once by
// AuthProvider in AuthContext.jsx.
let clearer = null

export function setAuthClearer(fn) {
  clearer = fn
}

export function clearAuthSession() {
  if (clearer) clearer()
}
