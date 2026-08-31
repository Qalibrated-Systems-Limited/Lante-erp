import { describe, it, expect } from 'vitest';
import * as exportUtils from './export';
import { fmtKES, fmtDate, moneyNumFmt, MONEY_NUM_FMT } from './export';
import { fmt } from '../theme/tokens';

/**
 * Characterization tests for the Excel/PDF export helpers.
 *
 * As with `fmt` in src/theme/tokens.js, these pin what the code does TODAY so that any
 * change to exported figures is visible in a diff rather than discovered by a customer
 * reconciling a spreadsheet. Cases marked `BUG:` document defects, not intent — see #205.
 *
 * Context worth knowing: there are FOUR money formatters in this frontend and they do not
 * There used to be FOUR money formatters here that disagreed with each other. #205
 * consolidated them: `fmtKES` now delegates to `kes` in theme/tokens.js, as do the copies
 * in projectExports.js and assignmentExports.js. Screen and exports now match exactly.
 */

describe('export.js fmtKES — money in Excel exports', () => {
  // FIXED (#205): fmtKES now delegates to the single `kes` formatter in theme/tokens.js.
  // It previously used its own Intl.NumberFormat with maximumFractionDigits: 0, which
  // rounded every exported figure to whole shillings, labelled it "Ksh", separated symbol
  // and digits with U+00A0, and turned missing values into a real "Ksh 0".

  it('matches the on-screen formatter exactly', () => {
    expect(fmtKES(1234.56)).toBe(fmt.kes(1234.56));
    expect(fmtKES(0.5)).toBe(fmt.kes(0.5));
    expect(fmtKES(null)).toBe(fmt.kes(null));
    // The whole point of #205: a spreadsheet and the screen must not disagree.
  });

  it('uses the Kshs label with an ordinary space, not U+00A0', () => {
    expect(fmtKES(1000)).toBe('Kshs 1,000.00');
    expect(fmtKES(1000).charCodeAt(4)).toBe(32);
    expect(fmtKES(1000).includes('\u00A0')).toBe(false);
  });

  it('keeps cents instead of rounding to whole shillings', () => {
    expect(fmtKES(1234.56)).toBe('Kshs 1,234.56');   // was "Ksh 1,235"
    expect(fmtKES(1234.49)).toBe('Kshs 1,234.49');   // was "Ksh 1,234"
    expect(fmtKES(0.5)).toBe('Kshs 0.50');           // was "Ksh 1"
    expect(fmtKES(0.4)).toBe('Kshs 0.40');           // was "Ksh 0" — a real amount vanished
  });

  it('renders missing and unparseable values as Kshs 0.00', () => {
    expect(fmtKES(null)).toBe('Kshs 0.00');
    expect(fmtKES(undefined)).toBe('Kshs 0.00');
    expect(fmtKES('')).toBe('Kshs 0.00');
    expect(fmtKES('abc')).toBe('Kshs 0.00');
    expect(fmtKES(0)).toBe('Kshs 0.00');
  });

  it('handles negatives', () => {
    expect(fmtKES(-1234)).toBe('Kshs -1,234.00');
  });
});

describe('export.js moneyNumFmt — per-currency Excel cell format (#288)', () => {
  it('renders the base currency as Kshs, matching every existing export', () => {
    expect(moneyNumFmt()).toBe(MONEY_NUM_FMT);
    expect(moneyNumFmt('KES')).toBe(MONEY_NUM_FMT);
    expect(moneyNumFmt(null)).toBe(MONEY_NUM_FMT);
  });

  it('labels a foreign currency with its own code', () => {
    expect(moneyNumFmt('USD')).toBe('"USD" #,##0.00');
  });
});

describe('export.js fmtDate', () => {
  it('formats a date in medium style', () => {
    expect(fmtDate('2026-08-18')).toBe('18 Aug 2026');
  });

  it('renders missing values as an em dash', () => {
    expect(fmtDate(null)).toBe('—');
    expect(fmtDate(undefined)).toBe('—');
    expect(fmtDate('')).toBe('—');
  });

  // Consistent with fmt.date on screen — both surface the raw Invalid Date string.
  it('BUG: renders unparseable input as "Invalid Date"', () => {
    expect(fmtDate('not-a-date')).toBe('Invalid Date');
  });
});

describe('column definitions', () => {
  const columnSets = Object.entries(exportUtils).filter(([name]) => name.endsWith('_COLUMNS'));

  it('exports the expected number of column sets', () => {
    // Guards against a column set being accidentally removed or renamed, which would
    // break the page that imports it at runtime rather than at build time.
    expect(columnSets.length).toBeGreaterThanOrEqual(18);
  });

  it.each(columnSets)('%s is a non-empty array of well-formed columns', (_name, columns) => {
    expect(Array.isArray(columns)).toBe(true);
    expect(columns.length).toBeGreaterThan(0);

    for (const col of columns) {
      // exportToExcel does columns.map(c => c.header) and c.accessor(row), so a column
      // missing either throws mid-export, in front of the user, with no build-time warning.
      expect(typeof col.header).toBe('string');
      expect(col.header.length).toBeGreaterThan(0);
      expect(typeof col.accessor).toBe('function');
      if (col.width !== undefined) expect(typeof col.width).toBe('number');
    }
  });

  it.each(columnSets)('%s accessors tolerate an empty row without throwing', (_name, columns) => {
    // Real payloads have missing fields. exportToExcel passes whatever the API returned
    // straight into every accessor, so an accessor that assumes a nested object is
    // present fails the whole export rather than one cell.
    for (const col of columns) {
      expect(() => col.accessor({})).not.toThrow();
    }
  });

  it('has unique headers within each column set', () => {
    for (const [name, columns] of columnSets) {
      const headers = columns.map(c => c.header);
      expect(new Set(headers).size, `${name} has duplicate headers`).toBe(headers.length);
    }
  });
});
