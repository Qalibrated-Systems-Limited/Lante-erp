// src/components/Pagination.jsx — controlled Prev/Next pager for server-paginated lists.
import { T } from '../theme/tokens.js';
import { Btn } from './ui.jsx';

export default function Pagination({ page, pageSize, totalCount, onPageChange }) {
  const totalPages = Math.max(1, Math.ceil((totalCount || 0) / pageSize));
  const from = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount || 0);

  return (
    <div style={{
      display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between',
      padding: '12px 16px', borderTop: `1px solid ${T.lgrey}`, fontSize: 12, color: T.mgrey,
    }}>
      <span>{totalCount ? `Showing ${from}–${to} of ${totalCount}` : 'No records'}</span>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        <Btn variant="ghost" size="sm" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>← Prev</Btn>
        <span>Page {page} of {totalPages}</span>
        <Btn variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Next →</Btn>
      </div>
    </div>
  );
}
