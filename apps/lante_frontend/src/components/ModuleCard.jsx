import { useNavigate } from 'react-router-dom'

export default function ModuleCard({ module }) {
  const navigate = useNavigate()
  const { title, description, icon, active, route } = module

  function handleClick() {
    if (active && route) navigate(route)
  }

  return (
    <div
      onClick={handleClick}
      className={`relative card p-6 flex flex-col gap-3 transition-all duration-200
        ${active
          ? 'hover:shadow-md hover:-translate-y-0.5 cursor-pointer hover:border-zinc-300'
          : 'opacity-70 cursor-not-allowed'
        }`}
    >
      {/* Lock overlay for inactive modules */}
      {!active && (
        <div className="absolute inset-0 rounded-xl flex items-end justify-end p-3 pointer-events-none">
          <span className="flex items-center gap-1 bg-gray-100 text-gray-500 text-xs font-semibold px-2 py-1 rounded-full border border-gray-200">
            <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd"
                d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z"
                clipRule="evenodd"
              />
            </svg>
            Coming soon
          </span>
        </div>
      )}

      {/* Icon */}
      <div className={`w-12 h-12 rounded-xl flex items-center justify-center text-2xl
        ${active ? 'bg-zinc-50' : 'bg-gray-50'}`}
      >
        {icon}
      </div>

      {/* Text */}
      <div>
        <h3 className="font-semibold text-gray-900 text-base">{title}</h3>
        <p className="text-sm text-gray-500 mt-0.5 leading-snug">{description}</p>
      </div>

      {/* Active indicator */}
      {active && (
        <div className="flex items-center gap-1.5 mt-auto">
          <span className="w-1.5 h-1.5 rounded-full bg-green-500"></span>
          <span className="text-xs text-green-600 font-medium">Active</span>
        </div>
      )}
    </div>
  )
}
