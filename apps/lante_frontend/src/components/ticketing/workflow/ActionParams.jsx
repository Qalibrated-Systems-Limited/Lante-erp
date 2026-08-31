// Per-action parameter inputs for the workflow-rule builder (one input set per action type).

export function ActionParams({ action, index, setParam, tags }) {
  const p = action.parameters ?? {}

  // type 0 = AssignToUser
  if (action.type === 0) return (
    <input type="text" value={p.userId ?? ''} onChange={e => setParam(index, 'userId', e.target.value)}
      placeholder="User ID to assign to"
      className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white" />
  )

  // type 2 = ChangeStatus
  if (action.type === 2) return (
    <select value={p.status ?? ''} onChange={e => setParam(index, 'status', e.target.value)}
      className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white">
      <option value="">Select status…</option>
      {['New','Assigned','InProgress','Pending','Resolved','Closed'].map(s => <option key={s} value={s}>{s}</option>)}
    </select>
  )

  // type 3 = AddTag, type 4 = RemoveTag
  if (action.type === 3 || action.type === 4) return (
    <select value={p.tagId ?? ''} onChange={e => setParam(index, 'tagId', e.target.value)}
      className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white">
      <option value="">Select tag…</option>
      {tags.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
    </select>
  )

  // type 5 = SendNotification
  if (action.type === 5) return (
    <div className="space-y-1.5">
      <input type="text" value={p.recipientUserId ?? ''} onChange={e => setParam(index, 'recipientUserId', e.target.value)}
        placeholder="Recipient User ID"
        className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white" />
      <input type="text" value={p.message ?? ''} onChange={e => setParam(index, 'message', e.target.value)}
        placeholder="Message text"
        className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white" />
    </div>
  )

  // type 6 = AddComment
  if (action.type === 6) return (
    <div className="space-y-1.5">
      <textarea value={p.content ?? ''} onChange={e => setParam(index, 'content', e.target.value)}
        rows={2} placeholder="Comment content…"
        className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white resize-none" />
      <label className="flex items-center gap-1.5 cursor-pointer">
        <input type="checkbox" checked={p.isInternal === 'true'} onChange={e => setParam(index, 'isInternal', e.target.checked ? 'true' : 'false')}
          className="rounded border-gray-300 text-amber-500" />
        <span className="text-xs text-gray-500">Internal note</span>
      </label>
    </div>
  )

  // type 7 = EscalateTo
  if (action.type === 7) return (
    <div className="space-y-1.5">
      <input type="text" value={p.userId ?? ''} onChange={e => setParam(index, 'userId', e.target.value)}
        placeholder="Escalate to User ID"
        className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white" />
      <input type="text" value={p.reason ?? ''} onChange={e => setParam(index, 'reason', e.target.value)}
        placeholder="Reason (optional)"
        className="w-full px-2 py-1.5 border border-gray-200 rounded text-xs focus:outline-none focus:ring-1 focus:ring-amber-400 bg-white" />
    </div>
  )

  return null
}
