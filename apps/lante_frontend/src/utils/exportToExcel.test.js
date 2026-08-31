import { describe, it, expect, vi, beforeEach } from 'vitest';

// Capture the workbook instead of writing a file, so we can inspect the cells that
// actually land in the .xlsx rather than trusting the code path by reading it.
const written = [];
vi.mock('xlsx', async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    writeFile: vi.fn((wb, filename) => { written.push({ wb, filename }); }),
  };
});

const { exportToExcel, MONEY_NUM_FMT } = await import('./export');
const XLSX = await import('xlsx');

const sheetOf = () => {
  const { wb } = written[written.length - 1];
  return wb.Sheets[wb.SheetNames[0]];
};
const cell = (col, row) => sheetOf()[XLSX.utils.encode_cell({ c: col, r: row })];

beforeEach(() => { written.length = 0; });

describe('exportToExcel — numeric money columns (#205)', () => {
  const columns = [
    { header: 'Item',   accessor: r => r.name,                       width: 20 },
    { header: 'Amount', accessor: r => `Kshs ${r.amount?.toFixed(2)}`, raw: r => r.amount, width: 18 },
  ];

  it('writes money as a real number, not a formatted string', () => {
    exportToExcel({ title: 'T', columns, rows: [{ name: 'Widget', amount: 1234.56 }] });

    const c = cell(1, 1);
    expect(c.t).toBe('n');          // numeric cell type, not 's' for string
    expect(c.v).toBe(1234.56);      // the exact value, cents intact
  });

  it('applies the Kshs display format so it still reads as money', () => {
    exportToExcel({ title: 'T', columns, rows: [{ name: 'Widget', amount: 1234.56 }] });
    expect(cell(1, 1).z).toBe(MONEY_NUM_FMT);
    expect(MONEY_NUM_FMT).toBe('"Kshs" #,##0.00');
  });

  it('keeps a summable column: every row is numeric', () => {
    // This is the point of the change — a recipient can sum, chart or pivot the column.
    const rows = [
      { name: 'A', amount: 10.25 },
      { name: 'B', amount: 20.50 },
      { name: 'C', amount: 0.25 },
    ];
    exportToExcel({ title: 'T', columns, rows });

    const values = rows.map((_, i) => cell(1, i + 1));
    expect(values.every(c => c.t === 'n')).toBe(true);
    expect(values.reduce((s, c) => s + c.v, 0)).toBeCloseTo(31.0, 10);
  });

  it('does not round to whole shillings', () => {
    // The defect this replaced: maximumFractionDigits: 0 turned 0.4 into "Ksh 0".
    exportToExcel({ title: 'T', columns, rows: [{ name: 'A', amount: 0.4 }] });
    expect(cell(1, 1).v).toBe(0.4);
  });

  it('falls back to the display string when raw is missing, rather than inventing a 0', () => {
    exportToExcel({ title: 'T', columns: [
      { header: 'Amount', accessor: () => 'Kshs 0.00', raw: () => null },
    ], rows: [{}] });

    const c = cell(0, 1);
    expect(c.t).toBe('s');
    expect(c.v).toBe('Kshs 0.00');
    expect(c.z).toBeUndefined();   // no number format on a text cell
  });

  it('leaves columns without a raw accessor as text', () => {
    exportToExcel({ title: 'T', columns, rows: [{ name: 'Widget', amount: 1 }] });
    const c = cell(0, 1);
    expect(c.t).toBe('s');
    expect(c.v).toBe('Widget');
  });

  it('writes headers and respects column widths', () => {
    exportToExcel({ title: 'T', columns, rows: [{ name: 'A', amount: 1 }] });
    expect(cell(0, 0).v).toBe('Item');
    expect(cell(1, 0).v).toBe('Amount');
    expect(sheetOf()['!cols']).toEqual([{ wch: 20 }, { wch: 18 }]);
  });

  it('honours a per-column numFmt override', () => {
    exportToExcel({
      title: 'T',
      columns: [{ header: 'Qty', accessor: r => String(r.q), raw: r => r.q, numFmt: '#,##0' }],
      rows: [{ q: 5 }],
    });
    expect(cell(0, 1).z).toBe('#,##0');
  });

  it('uses the provided filename and sheet name', () => {
    exportToExcel({ title: 'Report', columns, rows: [{ name: 'A', amount: 1 }],
                    filename: 'lante-report', sheetName: 'Sheet One' });
    const { wb, filename } = written[written.length - 1];
    expect(filename).toBe('lante-report.xlsx');
    expect(wb.SheetNames[0]).toBe('Sheet One');
  });
});
