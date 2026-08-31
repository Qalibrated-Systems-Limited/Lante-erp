import { useState, useEffect, useCallback, useRef } from 'react'
import * as ops from '../../../services/operations.js'

// PR3b — a comment thread on a project, milestone or task. Replies nest one level; the API flattens
// anything deeper, so this never has to render an arbitrarily deep tree.
//
// Mentions are captured by an "@" picker rather than parsed out of the text on save: the picker knows
// which user it inserted, so the ids we send are the ones actually meant.

const fmtWhen = (d) => {
  if (!d) return ''
  const then = new Date(d)
  const mins = Math.round((Date.now() - then.getTime()) / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  if (mins < 1440) return `${Math.round(mins / 60)}h ago`
  return then.toLocaleDateString('en-KE', { day: '2-digit', month: 'short' })
}

const initials = (name) => (name || '?').split(' ').filter(Boolean).slice(0, 2).map(p => p[0]).join('').toUpperCase()

function Composer({ users, currentUserId, busy, placeholder, initialBody = '', initialMentions = [], onSubmit, onCancel }) {
  const [body, setBody] = useState(initialBody)
  const [mentioned, setMentioned] = useState(initialMentions)
  const [picking, setPicking] = useState(false)
  const [filter, setFilter] = useState('')
  const boxRef = useRef(null)

  const mentionable = (users ?? []).filter(u =>
    u.id !== currentUserId &&
    !mentioned.includes(u.id) &&
    (u.name ?? '').toLowerCase().includes(filter.toLowerCase()))

  const add = (u) => {
    setMentioned(prev => [...prev, u.id])
    // The name goes in the text for the reader; the id travels separately for the system.
    setBody(prev => `${prev}${prev && !prev.endsWith(' ') ? ' ' : ''}@${u.name} `)
    setPicking(false); setFilter('')
    boxRef.current?.focus()
  }

  const submit = (e) => {
    e.preventDefault()
    if (!body.trim()) return
    onSubmit({ body: body.trim(), mentionedUserIds: mentioned })
    setBody(''); setMentioned([])
  }

  return (
    <form onSubmit={submit} className="space-y-2">
      <textarea
          ref={boxRef}
          className="input"
          rows={2}
          value={body}
          placeholder={placeholder}
          onChange={e => setBody(e.target.value)}
      />
      {mentioned.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {mentioned.map(id => {
            const u = (users ?? []).find(x => x.id === id)
            return (
              <span key={id} className="inline-flex items-center gap-1 text-[11px] bg-amber-50 text-amber-800 border border-amber-200 rounded px-1.5 py-0.5">
                notifying {u?.name ?? id}
                <button type="button" onClick={() => setMentioned(prev => prev.filter(x => x !== id))}
                        className="text-amber-500 hover:text-amber-800" aria-label="Remove mention">×</button>
              </span>
            )
          })}
        </div>
      )}
      <div className="relative flex items-center gap-2">
        <button type="button" onClick={() => setPicking(p => !p)}
                className="text-xs font-semibold text-gray-500 hover:text-gray-800 border border-gray-200 rounded px-2 py-1">
          @ Mention
        </button>
        <button type="submit" disabled={busy || !body.trim()}
                className="text-xs font-semibold bg-amber-500 text-white px-3 py-1.5 rounded disabled:opacity-50">
          {onCancel ? 'Save' : 'Comment'}
        </button>
        {onCancel && <button type="button" onClick={onCancel} className="text-xs text-gray-500">Cancel</button>}

        {picking && (
          <div className="absolute z-20 bottom-9 left-0 w-64 bg-white border border-gray-200 rounded-lg shadow-lg p-2">
            <input autoFocus className="input mb-1" placeholder="Find someone…" value={filter}
                   onChange={e => setFilter(e.target.value)} />
            <div className="max-h-44 overflow-y-auto">
              {mentionable.length === 0
                ? <p className="text-xs text-gray-400 px-2 py-3">Nobody left to mention.</p>
                : mentionable.slice(0, 30).map(u => (
                    <button key={u.id} type="button" onClick={() => add(u)}
                            className="w-full text-left text-xs px-2 py-1.5 rounded hover:bg-amber-50">
                      {u.name}
                    </button>
                  ))}
            </div>
          </div>
        )}
      </div>
    </form>
  )
}

function Comment({ c, users, currentUserId, busy, onReply, onEdit, onDelete, isReply }) {
  const [editing, setEditing] = useState(false)
  const [replying, setReplying] = useState(false)
  const mine = c.authorUserId === currentUserId

  return (
    <div className={isReply ? 'pl-8 border-l-2 border-gray-100' : ''}>
      <div className="flex gap-3 py-2.5">
        <div className="shrink-0 w-7 h-7 rounded-full bg-gray-100 text-gray-600 text-[10px] font-bold grid place-items-center">
          {initials(c.authorName)}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-xs font-semibold text-gray-900">{c.authorName || 'Someone'}</span>
            <span className="text-[11px] text-gray-400">{fmtWhen(c.createdAt)}</span>
            {c.editedAt && <span className="text-[11px] text-gray-400 italic">edited</span>}
          </div>

          {editing ? (
            <div className="mt-1.5">
              <Composer
                  users={users} currentUserId={currentUserId} busy={busy}
                  initialBody={c.body} initialMentions={c.mentionedUserIds ?? []}
                  placeholder="Edit your comment…"
                  onSubmit={(dto) => { onEdit(c.id, dto); setEditing(false) }}
                  onCancel={() => setEditing(false)}
              />
            </div>
          ) : (
            <p className={`text-sm mt-0.5 whitespace-pre-wrap ${c.isRedacted ? 'text-gray-400 italic' : 'text-gray-700'}`}>
              {c.body}
            </p>
          )}

          {!editing && !c.isRedacted && (
            <div className="flex gap-3 mt-1">
              {!isReply && (
                <button onClick={() => setReplying(r => !r)} className="text-[11px] font-semibold text-gray-500 hover:text-gray-800">
                  Reply
                </button>
              )}
              {mine && <button onClick={() => setEditing(true)} className="text-[11px] text-gray-500 hover:text-gray-800">Edit</button>}
              {mine && (
                <button onClick={() => window.confirm('Delete this comment?') && onDelete(c.id)}
                        className="text-[11px] text-red-500 hover:text-red-700">Delete</button>
              )}
            </div>
          )}

          {replying && (
            <div className="mt-2">
              <Composer
                  users={users} currentUserId={currentUserId} busy={busy}
                  placeholder="Write a reply…"
                  onSubmit={(dto) => { onReply(c.id, dto); setReplying(false) }}
                  onCancel={() => setReplying(false)}
              />
            </div>
          )}
        </div>
      </div>

      {(c.replies ?? []).map(r => (
        <Comment key={r.id} c={r} users={users} currentUserId={currentUserId} busy={busy}
                 onReply={onReply} onEdit={onEdit} onDelete={onDelete} isReply />
      ))}
    </div>
  )
}

export default function CommentThread({ projectId, targetType = 'Project', targetId, users, currentUserId, compact }) {
  const [thread, setThread] = useState([])
  const [err, setErr] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setThread(await ops.getProjectComments(projectId, targetType, targetId) ?? []) }
    catch { setErr('Could not load the discussion.') }
  }, [projectId, targetType, targetId])

  useEffect(() => { load() }, [load])

  const run = async (fn) => {
    setBusy(true); setErr('')
    try { await fn(); await load() }
    catch (e) { setErr(e?.response?.data?.message || 'Action failed.') }
    finally { setBusy(false) }
  }

  const post = (dto, parentCommentId) =>
    run(() => ops.addProjectComment(projectId, { ...dto, targetType, targetId, parentCommentId }))

  return (
    <div className="space-y-2">
      {!compact && <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Discussion</h3>}
      {err && <div className="bg-red-50 border border-red-200 text-red-700 text-xs rounded px-3 py-2">{err}</div>}

      <div className="border border-gray-200 rounded-lg px-3 py-2">
        <Composer users={users} currentUserId={currentUserId} busy={busy}
                  placeholder="Add a comment. Use @ to bring someone in." onSubmit={(dto) => post(dto, null)} />
      </div>

      {thread.length === 0
        ? <p className="text-xs text-gray-400 py-3">No comments yet.</p>
        : (
          <div className="divide-y divide-gray-100">
            {thread.map(c => (
              <Comment
                  key={c.id} c={c} users={users} currentUserId={currentUserId} busy={busy}
                  onReply={(parentId, dto) => post(dto, parentId)}
                  onEdit={(id, dto) => run(() => ops.updateComment(id, dto))}
                  onDelete={(id) => run(() => ops.deleteComment(id))}
              />
            ))}
          </div>
        )}
    </div>
  )
}
