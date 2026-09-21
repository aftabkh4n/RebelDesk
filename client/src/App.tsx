import React, { useEffect, useState } from 'react'
import { BrowserRouter, Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Login } from './pages/Login'
import { Devices } from './pages/Devices'
import { Session } from './pages/Session'
import { LangSwitcher } from './components/LangSwitcher'
import { clearSession, getToken, getUser } from './api/client'
import './App.css'

function RequireAuth({ children }: { children: React.ReactElement }) {
  return getToken() ? children : <Navigate to="/login" replace />
}

function Nav() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const user = getUser()
  function signOut() { clearSession(); navigate('/login', { replace: true }) }
  return (
    <nav className="app-nav">
      <Link to="/devices" className="brand">{t('app.title')}</Link>
      <div className="spacer" />
      <LangSwitcher />
      {user && (
        <>
          <span className="user">{user.displayName}</span>
          <button onClick={signOut}>{t('nav.logout')}</button>
        </>
      )}
    </nav>
  )
}

export default function App() {
  const [, force] = useState(0)
  useEffect(() => {
    const onStorage = () => force(x => x + 1)
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [])

  return (
    <BrowserRouter>
      <Nav />
      <main className="app-main">
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/devices" element={<RequireAuth><Devices /></RequireAuth>} />
          <Route path="/session/:deviceId" element={<RequireAuth><Session /></RequireAuth>} />
          <Route path="*" element={<Navigate to={getToken() ? '/devices' : '/login'} replace />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
