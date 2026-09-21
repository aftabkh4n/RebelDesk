import { useTranslation } from 'react-i18next'
import { SUPPORTED_LANGUAGES } from '../i18n'

export function LangSwitcher() {
  const { i18n, t } = useTranslation()
  return (
    <label className="lang-switcher">
      {t('nav.language')}
      <select value={i18n.language.split('-')[0]} onChange={(e) => i18n.changeLanguage(e.target.value)}>
        {SUPPORTED_LANGUAGES.map((l) => (
          <option key={l} value={l}>{t(`language.${l}`)}</option>
        ))}
      </select>
    </label>
  )
}
