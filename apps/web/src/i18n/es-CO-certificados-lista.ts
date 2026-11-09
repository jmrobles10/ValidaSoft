/** Textos es-CO de la lista de certificados de la empresa (CE-11); se integran en `es-CO.ts` como `certificados.lista`. */

export const certificadosLista = {
  titulo: 'Certificados',
  subtitulo: 'El certificado vigente de cada vínculo de {{empresa}}: su estado, hasta cuándo es válido y si bloquea la certificación.',
  columnas: {
    tercero: 'Tercero',
    tipo: 'Tipo de vínculo',
    folio: 'Folio',
    emision: 'Emisión',
    estado: 'Estado',
    vigenteHasta: 'Vigente hasta',
    emitido: 'Emitido el',
  },
  estados: {
    Vigente: 'Vigente',
    ConPendientes: 'Con pendientes',
    Suspendido: 'Suspendido',
    Vencido: 'Vencido',
    Revocado: 'Revocado',
    Bloqueante: 'Bloquean la certificación',
  },
  buscar: 'Buscar por tercero, identificación o folio',
  filtros: { estado: 'Estado', limpiar: 'Limpiar filtros' },
  abrir: 'Abrir el vínculo de {{tercero}} (certificado {{folio}})',
  vacio: '{{empresa}} aún no tiene certificados.',
  vacioAyuda: 'Se emite uno cuando se aprueba el expediente de un vínculo.',
  vacioFiltros: 'Ningún certificado coincide con los filtros.',
  errorLista: 'No se pudieron cargar los certificados.',
} as const;
