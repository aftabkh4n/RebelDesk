import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { api, ApiError, setSession } from '../api/client'

export function Login() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [mode, setMode] = useState<'signin' | 'signup'>('signin')
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const auth = mode === 'signin'
        ? await api.login(email, password)
        : await api.register(email, displayName, password)
      setSession(auth.token, { userId: auth.userId, email: auth.email, displayName: auth.displayName })
      navigate('/devices')
    } catch (err) {
      if (err instanceof ApiError) {
        setError(t(`login.errors.${err.code === 'invalid-credentials' ? 'invalidCredentials'
          : err.code === 'email-in-use' ? 'emailInUse'
          : err.code === 'password-too-short' ? 'passwordTooShort'
          : 'generic'}`))
      } else {
        setError(t('login.errors.generic'))
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-container">
      <h1>{t('app.title')}</h1>
      <p className="tagline">{t('app.tagline')}</p>

      <form onSubmit={submit} className="auth-form">
        <h2>{mode === 'signin' ? t('login.title') : t('login.register')}</h2>

        <label>
          {t('login.email')}
          <input type="email" required value={email} onChange={e => setEmail(e.target.value)} autoComplete="email" />
        </label>

        {mode === 'signup' && (
          <label>
            {t('login.displayName')}
            <input required value={displayName} onChange={e => setDisplayName(e.target.value)} />
          </label>
        )}

        <label>
          {t('login.password')}
          <input type="password" required minLength={8} value={password}
                 onChange={e => setPassword(e.target.value)}
                 autoComplete={mode === 'signin' ? 'current-password' : 'new-password'} />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit" disabled={busy}>
          {mode === 'signin' ? t('login.submit') : t('login.register')}
        </button>

        <button type="button" className="link" onClick={() => setMode(mode === 'signin' ? 'signup' : 'signin')}>
          {mode === 'signin' ? t('login.createInstead') : t('login.signInInstead')}
        </button>
      </form>
    </div>
  )
}
