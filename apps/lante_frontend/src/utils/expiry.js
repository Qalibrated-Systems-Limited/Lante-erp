/**
 * Reading an expiry date that may not be a date.
 *
 * `FieldVehicle.InsuranceExpiry` is a free-text `string?` on the server — unlike
 * `InspectionExpiryDate` and `Truck.InsuranceExpiryDate`, which are real `DateTime?` — with no
 * validation behind it. So whatever someone typed is what arrives.
 *
 * The previous check was `new Date(value) < new Date()`, which **fails open**: an unparseable
 * string produces an Invalid Date, every comparison against it is `false`, and the vehicle reads as
 * NOT expired. Measured in node against the values a Kenyan user would plausibly type:
 *
 *   "2020-01-31"  → expired    (ISO, works)
 *   "31/01/2020"  → NOT expired  ← six years out of date
 *   "31.01.2020"  → NOT expired
 *   "expired"     → NOT expired
 *
 * Insurance is the one field where failing open has a legal consequence: the screen said the
 * vehicle was fine. So an unreadable value is now its own state — `unknown` — rather than being
 * silently folded into "valid". Nobody can act on what they are not told.
 */

/** Days before expiry at which a document is "expiring" rather than merely valid. */
export const EXPIRY_WARNING_DAYS = 30

const MS_PER_DAY = 24 * 60 * 60 * 1000

/**
 * @param {string|Date|null|undefined} value the stored expiry, in whatever shape it arrived
 * @param {Date} [now] injected so the boundaries can be tested
 * @returns {{state: 'unset'|'unknown'|'expired'|'expiring'|'valid', date: Date|null, daysLeft: number|null}}
 */
export function expiryState(value, now = new Date()) {
  if (value === null || value === undefined || (typeof value === 'string' && value.trim() === '')) {
    // Genuinely not recorded. Distinct from unreadable: nothing was claimed, so nothing is wrong.
    return { state: 'unset', date: null, daysLeft: null }
  }

  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) {
    // Present but unreadable. The state the old check could not express, and the one that matters:
    // it means "we do not know whether this vehicle is insured", not "it is".
    return { state: 'unknown', date: null, daysLeft: null }
  }

  // Whole days, comparing calendar dates rather than instants — an expiry is a day, not a moment,
  // and a document expiring today has not expired yet.
  //
  // UTC getters throughout. The first version mixed local getters with Date.UTC construction, which
  // made the answer depend on the viewer's timezone: a `2026-08-19T23:59:59Z` expiry read as expired
  // in UTC and as expiring-today at +03:00. Self-consistent UTC matches how the server stores dates
  // and gives every viewer the same answer, which for a compliance flag matters more than matching
  // any one of their wall clocks.
  //
  // Caveat this leaves: a LOCALE-format string that happens to parse — "Jan 31 2020" parses as local
  // midnight — can land a day earlier in UTC east of Greenwich. That is a consequence of the server
  // storing InsuranceExpiry as free text rather than a date, and it is why fixing the field type is
  // the real answer; being one day out is a great deal better than reading as not-expired at all.
  const startOfDay = (d) => Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate())
  const daysLeft = Math.round((startOfDay(date) - startOfDay(now)) / MS_PER_DAY)

  if (daysLeft < 0) return { state: 'expired', date, daysLeft }
  if (daysLeft <= EXPIRY_WARNING_DAYS) return { state: 'expiring', date, daysLeft }
  return { state: 'valid', date, daysLeft }
}

/** True only when we positively know it has expired. */
export function isExpired(value, now = new Date()) {
  return expiryState(value, now).state === 'expired'
}

/**
 * True when the value needs a human to look at it — expired, expiring, or unreadable.
 *
 * `unknown` is included deliberately. An unreadable insurance date is not a cosmetic problem: it is
 * the case where the screen previously said everything was fine.
 */
export function needsAttention(value, now = new Date()) {
  return ['expired', 'expiring', 'unknown'].includes(expiryState(value, now).state)
}
