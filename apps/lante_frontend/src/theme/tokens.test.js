import { describe, it, expect } from 'vitest';
import { fmt } from './tokens';

/**
 * Characterization tests for `fmt`.
 *
 * These pin what `fmt` does TODAY, not what it should do. `fmt.kes` alone has 249
 * call sites, `fmt.date` 141, `fmt.num` 38 — so nobody can safely change these
 * without a net that says exactly which rendered strings move.
 *
 * Cases that document a DEFECT rather than intended behaviour are marked `BUG:`.
 * When one is fixed, the assertion here should flip in the same commit — that's the
 * signal that a display change across many screens was deliberate.
 *
 * Locale note: `fmt.kes` pins 'en-KE'; `fmt.num` does not pin any locale, so its
 * output follows the host. Under Node 22 (full ICU) both resolve to comma
 * grouping, which is why these assertions hold here — see the `fmt.num` block.
 */

describe('fmt.kes — money, 249 call sites', () => {
  it('formats whole numbers with thousands separators and 2dp', () => {
    expect(fmt.kes(0)).toBe('Kshs 0.00');
    expect(fmt.kes(1234)).toBe('Kshs 1,234.00');
    expect(fmt.kes(1234567)).toBe('Kshs 1,234,567.00');
  });

  // Product decision (#205): every unparseable or absent value renders Kshs 0.00 — one
  // predictable rendering, no blanks or dashes in a money column. Known tradeoff: a
  // missing figure is indistinguishable from a nil balance.
  it('renders missing values as Kshs 0.00', () => {
    expect(fmt.kes(null)).toBe('Kshs 0.00');
    expect(fmt.kes(undefined)).toBe('Kshs 0.00');
  });

  it('renders a real zero the same way', () => {
    expect(fmt.kes(0)).toBe('Kshs 0.00');
  });

  it('handles negatives', () => {
    expect(fmt.kes(-1234)).toBe('Kshs -1,234.00');
    expect(fmt.kes(-1234.5)).toBe('Kshs -1,234.50');
  });

  // FIXED (#194): money is now pinned to exactly 2 decimal places, so a column
  // no longer renders 0, 1, 2 and 3 decimals depending on the value, and float
  // dust from a calculation rounds to cents instead of leaking a third decimal.
  it('pads and rounds to exactly 2 decimal places', () => {
    expect(fmt.kes(1234.5)).toBe('Kshs 1,234.50');
    expect(fmt.kes(1234.56)).toBe('Kshs 1,234.56');
    expect(fmt.kes(0.5)).toBe('Kshs 0.50');
  });

  it('rounds a third decimal to the nearest cent rather than showing it', () => {
    expect(fmt.kes(1234.567)).toBe('Kshs 1,234.57');
    expect(fmt.kes(1234.565)).toBe('Kshs 1,234.57');
    expect(fmt.kes(1234.564)).toBe('Kshs 1,234.56');
    expect(fmt.kes(0.005)).toBe('Kshs 0.01');
  });

  // FIXED (#205): unparseable input used to reach the user as the literal "Kshs NaN".
  it('renders unparseable input as Kshs 0.00 rather than "Kshs NaN"', () => {
    expect(fmt.kes('abc')).toBe('Kshs 0.00');
    expect(fmt.kes(NaN)).toBe('Kshs 0.00');
    expect(fmt.kes({})).toBe('Kshs 0.00');
    expect(fmt.kes(Infinity)).toBe('Kshs 0.00');
  });

  it('treats an empty string as zero', () => {
    expect(fmt.kes('')).toBe('Kshs 0.00');
  });

  it('accepts numeric strings, which is relied on by API payloads', () => {
    expect(fmt.kes('1234')).toBe('Kshs 1,234.00');
    expect(fmt.kes('1234.56')).toBe('Kshs 1,234.56');
  });

  it('coerces an empty array to zero, like every other unparseable value', () => {
    expect(fmt.kes([])).toBe('Kshs 0.00');
  });
});

describe('fmt.money — currency-aware money (#288)', () => {
  it('renders the base currency as Kshs, same as fmt.kes, when no currency is given', () => {
    expect(fmt.money(1234)).toBe(fmt.kes(1234));
    expect(fmt.money(1234.56)).toBe('Kshs 1,234.56');
  });

  it('renders the base currency as Kshs even when the record explicitly says KES', () => {
    expect(fmt.money(1234.56, 'KES')).toBe('Kshs 1,234.56');
  });

  it('renders a foreign currency using its own code, not Kshs', () => {
    expect(fmt.money(1000, 'USD')).toBe('USD 1,000.00');
    expect(fmt.money(1000, 'CNY')).toBe('CNY 1,000.00');
  });

  it('shares every other rule with fmt.kes: missing/unparseable input, rounding, negatives', () => {
    expect(fmt.money(null, 'USD')).toBe('USD 0.00');
    expect(fmt.money(undefined, 'USD')).toBe('USD 0.00');
    expect(fmt.money('abc', 'USD')).toBe('USD 0.00');
    expect(fmt.money(-1234.5, 'USD')).toBe('USD -1,234.50');
    expect(fmt.money(1234.567, 'USD')).toBe('USD 1,234.57');
  });

  it('treats an empty currency code the same as no currency at all', () => {
    expect(fmt.money(1234.56, '')).toBe('Kshs 1,234.56');
    expect(fmt.money(1234.56, null)).toBe('Kshs 1,234.56');
  });
});

describe('fmt.num — plain numbers, 38 call sites', () => {
  it('groups thousands', () => {
    expect(fmt.num(1234567)).toBe('1,234,567');
  });

  it('renders null and undefined as an em dash', () => {
    expect(fmt.num(null)).toBe('—');
    expect(fmt.num(undefined)).toBe('—');
  });

  // FIXED (#194): pinned to 'en-KE' like fmt.kes. It previously took no locale, so it
  // followed the host — a de-DE browser rendered "1.234,5" from num beside
  // "Kshs 1,234.56" from kes, on the same screen.
  it('is locale-pinned to en-KE, matching fmt.kes', () => {
    expect(fmt.num(1234.5)).toBe('1,234.5');
    expect(fmt.num(1234567)).toBe('1,234,567');
  });

  // FIXED (#194): used to render the literal string "NaN".
  it('renders unparseable input as an em dash, not "NaN"', () => {
    expect(fmt.num(NaN)).toBe('—');
    expect(fmt.num('abc')).toBe('—');
    expect(fmt.num({})).toBe('—');
    expect(fmt.num(Infinity)).toBe('—');
  });

  it('treats an empty string as missing rather than zero', () => {
    expect(fmt.num('')).toBe('—');
  });

  // Deliberately unlike fmt.kes, which renders Kshs 0.00 for missing: a count of zero is a
  // meaningful fact and must not be confused with an absent one.
  it('still renders a real zero', () => {
    expect(fmt.num(0)).toBe('0');
  });
});

describe('fmt.date — 141 call sites', () => {
  it('formats an ISO date as DD Mon YYYY', () => {
    expect(fmt.date('2026-08-18')).toBe('18 Aug 2026');
    expect(fmt.date('2026-01-01')).toBe('01 Jan 2026');
  });

  it('renders null, undefined and empty string as an em dash', () => {
    expect(fmt.date(null)).toBe('—');
    expect(fmt.date(undefined)).toBe('—');
    expect(fmt.date('')).toBe('—');
  });

  it('accepts a Date instance', () => {
    expect(fmt.date(new Date('2026-08-18T00:00:00Z'))).toBe('18 Aug 2026');
  });

  // FIXED (#194): used to surface the literal string "Invalid Date".
  it('renders unparseable input as an em dash, not "Invalid Date"', () => {
    expect(fmt.date('not-a-date')).toBe('—');
    expect(fmt.date('2026-13-45')).toBe('—');
    expect(fmt.date({})).toBe('—');
  });

  // Intended, not a defect: in an ERP a timestamp of 1 Jan 1970 is an uninitialised value,
  // so an em dash is more truthful than rendering the epoch as though it were real.
  it('treats epoch 0 as missing', () => {
    expect(fmt.date(0)).toBe('—');
    expect(fmt.date(new Date(0))).toBe('—');
  });
});

describe('fmt.pct — 1 call site', () => {
  it('multiplies by 100 and fixes to 1 decimal, i.e. expects a fraction', () => {
    expect(fmt.pct(0.155)).toBe('15.5%');
    expect(fmt.pct(0)).toBe('0.0%');
    expect(fmt.pct(1)).toBe('100.0%');
  });

  it('renders null and undefined as an em dash', () => {
    expect(fmt.pct(null)).toBe('—');
    expect(fmt.pct(undefined)).toBe('—');
  });

  // BUG: the fraction-vs-percent contract is undocumented and unguarded, so
  // passing an already-scaled percentage silently renders 100x too large.
  it('BUG: an already-scaled percentage renders 100x too large', () => {
    expect(fmt.pct(15)).toBe('1500.0%');
  });

  // FIXED (#194): used to render "NaN%".
  it('renders unparseable input as an em dash', () => {
    expect(fmt.pct(NaN)).toBe('—');
    expect(fmt.pct('abc')).toBe('—');
  });
});
