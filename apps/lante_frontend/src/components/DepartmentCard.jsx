export default function DepartmentCard({ department }) {
  const { name, description, userCount } = department

  // Derive initials from department name for the avatar
  const initials = name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()

  // Cycle through a set of accent colours based on name length
  const colours = [
    'bg-blue-100 text-blue-700',
    'bg-purple-100 text-purple-700',
    'bg-emerald-100 text-emerald-700',
    'bg-orange-100 text-orange-700',
    'bg-pink-100 text-pink-700',
    'bg-cyan-100 text-cyan-700',
  ]
  const colour = colours[name.length % colours.length]

  return (
    <div className="card flex-shrink-0 w-56 p-5 flex flex-col gap-4 hover:shadow-md hover:-translate-y-0.5 transition-all duration-200">
      {/* Avatar + name */}
      <div className="flex items-center gap-3">
        <div className={`w-10 h-10 rounded-lg flex items-center justify-center text-sm font-bold flex-shrink-0 ${colour}`}>
          {initials}
        </div>
        <div className="min-w-0">
          <h4 className="font-semibold text-gray-900 text-sm truncate">{name}</h4>
          {description && (
            <p className="text-xs text-gray-400 truncate mt-0.5">{description}</p>
          )}
        </div>
      </div>

      {/* User count badge */}
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-1.5 text-xs text-gray-500">
          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
              d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"
            />
          </svg>
          {userCount ?? 0} {userCount === 1 ? 'user' : 'users'}
        </span>

        <button className="text-xs font-semibold text-zinc-950 hover:text-zinc-950 hover:underline transition-colors">
          View →
        </button>
      </div>
    </div>
  )
}
