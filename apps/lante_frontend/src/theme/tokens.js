// src/theme/tokens.js — QSL ERP design tokens.
//
// Single source of truth for the QaliCore → QSL rebrand (navy + gold).
// Ported from the QSL Next.js dashboard so every screen ported/built in this
// app shares one visual identity. Inline-style driven, matching the existing
// app's convention.
//
// `--brand` / `--accent` CSS vars (set on :root in index.css) allow per-tenant
// white-labelling: components that want to honour a tenant's custom colour use
// `T.brandVar` / `T.accentVar`; everything else uses the literal navy/gold.

export const T = {
  navy:    '#1B3A5C',
  navyD:   '#0D2238',
  navyL:   '#2E5F8A',
  gold:    '#C8960C',
  goldL:   '#E8B84D',
  white:   '#FFFFFF',
  offwt:   '#F0F4F8',
  lgrey:   '#E8ECF0',
  mgrey:   '#94A3B8',
  dgrey:   '#334155',
  green:   '#1E6B3C',
  greenL:  '#DCFCE7',
  red:     '#C00000',
  redL:    '#FEE2E2',
  amber:   '#B8600B',
  amberL:  '#FEF3C7',
  blue:    '#0070C0',
  blueL:   '#EFF6FF',
  purple:  '#6A0DAD',
  purpleL: '#F3E8FF',

  // White-label hooks — fall back to navy/gold when a tenant sets nothing.
  brandVar:  'var(--brand, #1B3A5C)',
  accentVar: 'var(--accent, #C8960C)',
};

export const FONT = {
  sans: "'Inter', 'Helvetica Neue', Arial, sans-serif",
  mono: "'JetBrains Mono', ui-monospace, SFMono-Regular, Menlo, monospace",
};

/**
 * The single money formatter for the whole frontend — screen, Excel and PDF.
 *
 * There were four of these and they disagreed on decimals, currency label, missing-value
 * handling and even whitespace, so one amount rendered three different ways depending on
 * where you looked (#205). The Excel copy rounded to whole shillings, so exports stopped
 * reconciling with the screen. Import this rather than writing another one.
 *
 * Rules:
 *  - Always `Kshs` and always exactly 2dp, so a column never mixes 0, 1 and 3 decimals
 *    and float dust rounds to cents instead of leaking a third decimal.
 *  - Anything that will not parse as a finite number — null, undefined, empty string,
 *    NaN, a non-numeric string — renders as `Kshs 0.00`. This is a deliberate product
 *    decision: one predictable rendering everywhere, no blanks or dashes in a money
 *    column. The known tradeoff, recorded in #205, is that "no figure recorded" and "the
 *    balance is nil" become indistinguishable. If that distinction is ever needed, the
 *    caller must carry it (e.g. render the cell empty before calling this).
 *  - An ordinary space separates label and digits. `Intl.NumberFormat` currency style
 *    uses U+00A0, which is invisible but breaks spreadsheet search and naive CSV parsing.
 */
export const kes = (n) => money(n);

/**
 * Currency-aware counterpart to `kes` (#288) — finance carries a real `currencyCode` on
 * invoices, bills, journals and imprest records (foreign-currency invoicing is a real,
 * validated backend feature, not a stub), but every finance screen rendered it through `kes`,
 * which has no currency parameter and always says "Kshs" — so a USD 1,000 invoice displayed
 * and exported as "Kshs 1,000.00", wrong by roughly two orders of magnitude.
 *
 * `ccy` is the record's ISO code (e.g. `'USD'`). Omitted, `null`, or the base currency `'KES'`
 * all render the existing `'Kshs'` label — preserving every current `kes` call site's output
 * exactly — anything else renders that code directly: `money(1000, 'USD')` → `"USD 1,000.00"`.
 * Same missing-value/rounding rules as `kes` otherwise; see its own doc comment above.
 */
export const money = (n, ccy) => {
  const label = (!ccy || ccy === 'KES') ? 'Kshs' : ccy;
  return `${label} ${(toFinite(n) ?? 0)
    .toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};

/**
 * `Number(n)` when n is genuinely a finite number, otherwise null.
 *
 * Exists because `Number(null)`, `Number('')` and `Number([])` are all `0` — a finite
 * value — so testing `Number.isFinite(Number(x))` alone silently accepts absent input and
 * reports a confident zero. That exact trap produced three separate bugs while this file
 * was being worked on, including one in `exportToExcel` that would have written a real
 * numeric 0 into a spreadsheet for a figure that was never recorded. Check for absence
 * first, always.
 */
function toFinite(n) {
  if (n === null || n === undefined || n === '') return null;
  const v = Number(n);
  return Number.isFinite(v) ? v : null;
}

// ── Formatting helpers (ported from the QSL dashboard `fmt`) ──────────────────
export const fmt = {
  kes,
  money,

  /**
   * Expects a fraction, not an already-scaled percentage: `pct(0.155)` is "15.5%", and
   * `pct(15)` renders "1500.0%". That contract is the caller's to honour — it is unguarded
   * because there is no way to tell 0.15 meaning 15% from 0.15 meaning 0.15%.
   */
  pct:  (n) => {
    const v = toFinite(n);
    return v === null ? '—' : `${(v * 100).toFixed(1)}%`;
  },

  /**
   * Renders an em dash for anything that is not a real date, rather than surfacing the
   * literal string "Invalid Date" to the user (#194).
   *
   * Epoch 0 deliberately reads as missing: in an ERP a timestamp of 1 Jan 1970 is an
   * uninitialised value, so "—" is more truthful than rendering it as a real date.
   */
  date: (d) => {
    if (d === null || d === undefined || d === '') return '—';
    const parsed = new Date(d);
    if (Number.isNaN(parsed.getTime()) || parsed.getTime() === 0) return '—';
    return parsed.toLocaleDateString('en-KE', { day: '2-digit', month: 'short', year: 'numeric' });
  },

  /**
   * Counts and quantities. Pinned to 'en-KE' to match `kes` — it previously called
   * `toLocaleString()` with no locale, so it followed the host and a de-DE browser showed
   * "1.234,5" from num beside "Kshs 1,234.56" from kes on the same screen (#194).
   *
   * Missing renders an em dash, unlike `kes` which renders Kshs 0.00. That difference is
   * deliberate: a count of zero is a meaningful fact and must not be confused with an
   * absent one, whereas money was standardised on a single predictable rendering.
   */
  num:  (n) => {
    const v = toFinite(n);
    return v === null ? '—' : v.toLocaleString('en-KE');
  },
};

export default T;
