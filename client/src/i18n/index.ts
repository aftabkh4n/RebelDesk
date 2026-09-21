import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import en from './locales/en.json'
import de from './locales/de.json'
import fr from './locales/fr.json'
import nl from './locales/nl.json'
import ar from './locales/ar.json'

export const SUPPORTED_LANGUAGES = ['en', 'de', 'fr', 'nl', 'ar'] as const
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number]

export const RTL_LANGUAGES: SupportedLanguage[] = ['ar']

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      de: { translation: de },
      fr: { translation: fr },
      nl: { translation: nl },
      ar: { translation: ar },
    },
    fallbackLng: 'en',
    supportedLngs: SUPPORTED_LANGUAGES as unknown as string[],
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator', 'htmlTag'],
      caches: ['localStorage'],
      lookupLocalStorage: 'rebeldesk.lang',
    },
  })

function applyDirection(lng: string) {
  const dir = (RTL_LANGUAGES as string[]).includes(lng) ? 'rtl' : 'ltr'
  document.documentElement.dir = dir
  document.documentElement.lang = lng
}
applyDirection(i18n.language)
i18n.on('languageChanged', applyDirection)

export default i18n
