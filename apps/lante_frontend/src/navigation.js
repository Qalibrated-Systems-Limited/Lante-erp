// Lets modules outside the React tree (e.g. the axios response interceptor) trigger
// client-side navigation instead of a hard `window.location` reload. Registered once
// by <NavigationListener/> in App.jsx via useNavigate().
let navigator = null

export function setNavigator(fn) {
  navigator = fn
}

export function navigateTo(path, options) {
  if (navigator) {
    navigator(path, options)
  } else {
    window.location.href = path
  }
}
