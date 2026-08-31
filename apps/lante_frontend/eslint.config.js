import js from '@eslint/js'
import globals from 'globals'
import react from 'eslint-plugin-react'
import reactHooks from 'eslint-plugin-react-hooks'

/**
 * Lint config deliberately scoped to CORRECTNESS, not style.
 *
 * Added after `/portal/survey` shipped as a blank page for every visitor: SurveyShell read
 * `logoUrl`, which was destructured in the parent component and never passed down, so the
 * page threw `ReferenceError: logoUrl is not defined` on render. `no-undef` catches that in
 * milliseconds. Nothing in the toolchain did — Vite/esbuild does not do scope analysis for
 * undeclared identifiers, it assumes they are globals, so the build was clean and the bundle
 * crashed at runtime. See #292.
 *
 * Every rule enabled below can produce a blank page, a stale render, or a silent no-op. No
 * formatting rules, no import ordering, no opinions about quotes: a lint run that reports
 * hundreds of cosmetic findings is a lint run people stop reading, and this one has to stay
 * worth reading for the next `logoUrl`.
 */
export default [
  { ignores: ['dist/**', 'node_modules/**', 'playwright-report/**', 'test-results/**'] },

  // ── Application code ──
  {
    files: ['src/**/*.{js,jsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'module',
      globals: { ...globals.browser },
      parserOptions: { ecmaFeatures: { jsx: true } },
    },
    plugins: { react, 'react-hooks': reactHooks },
    settings: { react: { version: 'detect' } },
    rules: {
      ...js.configs.recommended.rules,

      // The rule this config exists for.
      'no-undef': 'error',

      // JSX counts as a use — without this, every imported component reads as unused.
      'react/jsx-uses-vars': 'error',
      'react/jsx-uses-react': 'off',   // React 17+ automatic runtime
      'react/react-in-jsx-scope': 'off',

      // Hook misuse produces stale or missing renders rather than an error, which is worse.
      'react-hooks/rules-of-hooks': 'error',
      // Warn, not error: a wrong dep array is a real bug class, but the existing code has not
      // been audited against it and turning it red would bury `no-undef` in noise.
      'react-hooks/exhaustive-deps': 'warn',

      // Unused variables are a warning: usually dead code, occasionally a genuine missed wire-up.
      // Args are ignored because event handlers legitimately omit trailing params.
      'no-unused-vars': ['warn', { args: 'none', ignoreRestSiblings: true }],

      // Silent-failure patterns worth erroring on.
      'no-dupe-keys': 'error',          // second key silently wins
      'no-dupe-class-members': 'error',
      'no-unreachable': 'error',
      'no-cond-assign': 'error',
      'no-self-assign': 'error',
      'no-constant-condition': ['error', { checkLoops: false }],
      'no-empty': ['error', { allowEmptyCatch: true }],   // `catch {}` is used deliberately here
    },
  },

  // ── Tests: node + test globals, and console is expected ──
  {
    files: ['e2e/**/*.js', 'e2e-live/**/*.js', 'src/**/*.test.js', '*.config.js'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'module',
      globals: { ...globals.node, ...globals.browser },
    },
    rules: { ...js.configs.recommended.rules, 'no-undef': 'error' },
  },
]
