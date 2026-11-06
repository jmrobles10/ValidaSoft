import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { esCO, type Recursos } from './es-CO';

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'comun';
    resources: { comun: Recursos };
  }
}

void i18n.use(initReactI18next).init({
  lng: 'es-CO',
  fallbackLng: 'es-CO',
  supportedLngs: ['es-CO', 'en'],
  defaultNS: 'comun',
  ns: ['comun'],
  resources: { 'es-CO': { comun: esCO } },
  interpolation: { escapeValue: false },
  returnNull: false,
  showSupportNotice: false,
});

export default i18n;
