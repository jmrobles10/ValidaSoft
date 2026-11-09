/** Textos es-CO de la cuenta bancaria designada del vínculo (se integran en `es-CO.ts`). */

export const cuentas = {
  titulo: 'Cuenta bancaria designada',
  errorCarga: 'No se pudo cargar la cuenta designada.',
  vacio: 'El tercero aún no ha designado una cuenta',
  vacioAyuda: 'La designa desde su portal; cuando lo haga, aparecerá aquí para que la aprueben.',
  secciones: { designada: 'Designada', porAprobar: 'Por aprobar', rechazada: 'Última rechazada' },
  campos: {
    banco: 'Banco',
    tipo: 'Tipo de cuenta',
    numero: 'Número',
    titular: 'Titular',
    identificacionTitular: 'Identificación del titular',
    moneda: 'Moneda',
  },
  tipos: { Ahorros: 'Ahorros', Corriente: 'Corriente' },
  numeroOculto: 'Solo quien aprueba cuentas ve el número completo.',
  aprobadaPor: 'Aprobada por {{nombres}} el {{fecha}}',
  aprobadaEl: 'Aprobada el {{fecha}}',
  propuestaEl: 'El tercero la propuso el {{fecha}}.',
  aprobaciones: '{{n}} de {{total}} aprobaciones',
  aprobacionesDe: '{{n}} de {{total}} aprobaciones: {{nombres}}',
  personaSinNombre: 'Persona sin nombre',
  cuatroOjos: 'La aprueban {{total}} personas distintas. Hasta entonces sigue vigente la cuenta designada anterior.',
  cuatroOjosSinAnterior: 'La aprueban {{total}} personas distintas. Hasta entonces el vínculo no tiene cuenta designada.',
  yaAprobaste: 'Ya la aprobaste; falta que la apruebe otra persona.',
  rechazadaResumen: '{{banco}} · {{tipo}} · {{numero}}',
  motivo: 'Motivo: {{motivo}}',
  aprobar: 'Aprobar',
  rechazar: 'Rechazar',
  aprobarTitulo: 'Aprobar la cuenta del tercero',
  aprobarUltima:
    'Coteja el número con la certificación bancaria. Con tu aprobación se completan las {{total}}: la cuenta queda designada y reemplaza a la anterior.',
  aprobarPrimera: 'Coteja el número con la certificación bancaria. Después de ti, falta que la apruebe otra persona para que quede designada.',
  aprobada: 'Aprobaste la cuenta; falta que la apruebe otra persona.',
  designada: 'La cuenta quedó designada.',
  rechazarTitulo: 'Rechazar la cuenta del tercero',
  rechazarAyuda: 'El tercero verá el motivo y podrá proponer otra cuenta. La designada anterior no cambia.',
  motivoCampo: 'Motivo para el tercero',
  motivoAyuda: 'Dile qué corregir: «El titular no coincide con la certificación bancaria».',
  faltaMotivo: 'Escribe el motivo.',
  rechazadaOk: 'Rechazaste la cuenta. El tercero verá tu motivo.',
  errores: {
    'aprobador-repetido': 'Ya aprobaste esta cuenta; falta que la apruebe otra persona.',
    'sin-cuenta-propuesta': 'Ya no hay una cuenta por aprobar: otra persona la resolvió o el tercero la cambió. Recargamos la tarjeta.',
    'motivo-requerido': 'Escribe el motivo para el tercero.',
    'usuario-delegado-requerido': 'Las cuentas las aprueba una persona; entra con tu usuario desde tu sistema.',
  },
} as const;

/**
 * Códigos nuevos de la cuenta designada para `errores.*`. `aprobador-repetido`, `motivo-requerido` y
 * `usuario-delegado-requerido` ya tienen texto global (expedientes y vínculos): la tarjeta usa los de `cuentas.errores`.
 */
export const erroresCuentas = {
  'sin-cuenta-propuesta': 'Ya no hay una cuenta por aprobar: otra persona la resolvió o el tercero la cambió. Recarga para ver la vigente.',
} as const;
