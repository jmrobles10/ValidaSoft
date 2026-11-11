/**
 * Ajustes al snapshot `v1.json` antes de generar el cliente con orval.
 *
 * El snapshot se guarda tal como lo sirve la API; estas correcciones compensan lo que el backend aún no declara
 * (ver "lo que falta" en apps/web/README.md):
 *  - operationId en español y estable (la API publica `POST_api_v1_vinculos`); las rutas `/public/v1` del tercero van
 *    todas a la etiqueta `publico`.
 *  - Respuesta real de los endpoints que devuelven `IResult` (Results.Created) y que la API documenta como `IResult`.
 *  - `TenantCreadoDto`, que no aparece en los esquemas.
 *  - Estados y roles como enum textual (la API los serializa como texto pero los publica como `string`).
 *  - Enteros publicados como `integer | string` (por `AllowReadingFromString`): la API siempre responde números.
 *
 * Si la API agrega o cambia un endpoint que no está en `NOMBRES`, la generación falla a propósito: así el cambio de
 * contrato se revisa antes de llegar a la UI.
 */

interface Esquema {
  type?: string | string[];
  pattern?: string;
  format?: string;
  enum?: string[];
  $ref?: string;
  properties?: Record<string, Esquema>;
  items?: Esquema;
  oneOf?: Esquema[];
  required?: string[];
  [clave: string]: unknown;
}

interface Respuesta {
  description?: string;
  content?: Record<string, { schema?: Esquema }>;
}

interface Operacion {
  operationId?: string;
  tags?: string[];
  summary?: string;
  description?: string;
  responses?: Record<string, Respuesta>;
  [clave: string]: unknown;
}

interface DocumentoOpenApi {
  paths: Record<string, Record<string, Operacion>>;
  components: { schemas: Record<string, Esquema> };
  [clave: string]: unknown;
}

/** `MÉTODO /ruta` → nombre de la operación en el cliente. */
const NOMBRES: Record<string, string> = {
  'POST /api/v1/terceros': 'crearTercero',
  'GET /api/v1/terceros/{id}': 'obtenerTercero',
  'GET /api/v1/terceros/por-identificacion': 'buscarTerceroPorIdentificacion',
  'GET /api/v1/terceros/{id}/ficha': 'obtenerFichaTercero',
  'GET /api/v1/terceros/{id}/referencias': 'listarReferenciasTercero',
  'POST /api/v1/terceros/{id}/referencias': 'agregarReferenciaTercero',
  'GET /api/v1/terceros/por-referencia': 'buscarTerceroPorReferencia',
  'POST /api/v1/vinculos/{id}/referencias': 'agregarReferenciaVinculo',
  'PUT /api/v1/vinculos/{id}/contactos': 'actualizarContactosVinculo',
  'PUT /public/v1/vinculos/{id}/contactos': 'actualizarContactosDelTercero',
  'POST /api/v1/sesiones-tercero': 'crearSesionTercero',
  'GET /public/v1/sesion/actual': 'obtenerSesionActual',
  'POST /api/v1/vinculos': 'crearVinculo',
  'GET /api/v1/vinculos': 'listarVinculos',
  'GET /api/v1/vinculos/por-referencia': 'obtenerVinculoPorReferencia',
  'GET /api/v1/vinculos/{id}': 'obtenerVinculo',
  'PATCH /api/v1/vinculos/{id}/atributos': 'actualizarAtributosVinculo',
  'POST /api/v1/vinculos/{id}/invitaciones': 'invitarVinculo',
  'POST /api/v1/vinculos/{id}/bloquear': 'bloquearVinculo',
  'POST /api/v1/vinculos/{id}/desbloquear': 'desbloquearVinculo',
  'POST /api/v1/vinculos/{id}/desvincular': 'desvincularVinculo',
  'POST /api/v1/vinculos/{id}/reintegrar': 'reintegrarVinculo',
  'GET /api/v1/vinculos/{id}/reintegro': 'obtenerViabilidadDeReintegro',
  'POST /api/v1/vinculos/{id}/anular': 'anularVinculo',
  'GET /api/v1/vinculos/{id}/historial': 'obtenerHistorialVinculo',
  'POST /api/v1/plataforma/tenants': 'crearTenant',
  'GET /api/v1/plataforma/tenants': 'listarTenants',
  'GET /api/v1/plataforma/tenants/{id}': 'obtenerTenant',
  // Consola de Plataforma (PT-3…PT-6): grupos con sus empresas, sistemas y sesión del operador, secretos y prueba de la llave.
  'GET /api/v1/plataforma/grupos': 'listarGrupos',
  'GET /api/v1/plataforma/grupos/{id}': 'obtenerGrupo',
  'GET /api/v1/plataforma/sistemas': 'listarSistemasPlataforma',
  'PUT /api/v1/plataforma/sistemas/{codigo}': 'fijarSistemaPlataforma',
  'POST /api/v1/plataforma/sesiones': 'crearSesionPlataforma',
  'GET /api/v1/plataforma/secretos': 'obtenerEstadoSecretos',
  'POST /api/v1/plataforma/secretos/recifrar': 'recifrarSecretos',
  'POST /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia/probar-llave': 'probarLlaveIaEmpresa',
  'GET /api/v1/plataforma/textos-legales/aviso': 'obtenerAvisoPlataforma',
  'PUT /api/v1/plataforma/textos-legales/aviso': 'publicarAvisoPlataforma',
  'GET /api/v1/catalogos/paises': 'listarPaises',
  'GET /api/v1/catalogos/tipos-identificacion': 'listarTiposIdentificacion',
  'POST /api/v1/sistemas': 'crearSistema',
  'GET /api/v1/sistemas': 'listarSistemas',
  'GET /api/v1/sistemas/{id}': 'obtenerSistema',
  'PUT /api/v1/sistemas/{id}/clave-publica': 'rotarClavePublicaSistema',
  'PUT /api/v1/sistemas/{id}/origenes': 'actualizarOrigenesSistema',
  // Certificado verificable (H5).
  'GET /api/v1/vinculos/{id}/certificado': 'obtenerCertificadoVinculo',
  'GET /api/v1/certificados/{folio}': 'obtenerCertificado',
  'POST /api/v1/certificados/{folio}/descarga': 'descargarCertificado',
  'POST /api/v1/certificados/{folio}/revocar': 'revocarCertificado',
  'GET /public/v1/verificar/{folio}': 'verificarCertificado',
  'POST /public/v1/verificar/{folio}/descarga': 'descargarCertificadoVerificado',
  // Notificaciones a personas, webhooks y eventos públicos.
  'GET /api/v1/notificaciones': 'listarNotificaciones',
  'POST /api/v1/notificaciones/{id}/leida': 'marcarNotificacionLeida',
  'POST /api/v1/notificaciones/leidas': 'marcarNotificacionesLeidas',
  'GET /api/v1/notificaciones/stream': 'streamNotificaciones',
  'GET /api/v1/usuarios/me/preferencias-notificacion': 'obtenerPreferenciasNotificacion',
  'PUT /api/v1/usuarios/me/preferencias-notificacion': 'actualizarPreferenciasNotificacion',
  'GET /api/v1/webhooks': 'listarWebhooks',
  'POST /api/v1/webhooks': 'crearWebhook',
  'GET /api/v1/webhooks/{id}': 'obtenerWebhook',
  'PUT /api/v1/webhooks/{id}': 'actualizarWebhook',
  'POST /api/v1/webhooks/{id}/rotar-secreto': 'rotarSecretoWebhook',
  'POST /api/v1/webhooks/{id}/activar': 'activarWebhook',
  'POST /api/v1/webhooks/{id}/desactivar': 'desactivarWebhook',
  'GET /api/v1/webhooks/{id}/entregas': 'listarEntregasWebhook',
  'GET /api/v1/webhooks/{id}/entregas/{entregaId}': 'obtenerEntregaWebhook',
  'POST /api/v1/webhooks/{id}/entregas/{entregaId}/reenviar': 'reenviarEntregaWebhook',
  'GET /api/v1/eventos/tipos': 'listarTiposEvento',
  'GET /api/v1/eventos': 'listarEventos',
  'GET /api/v1/eventos/stream': 'streamEventos',
  'POST /api/v1/sistemas/{id}/desactivar': 'desactivarSistema',
  'POST /api/v1/usuarios': 'crearUsuario',
  'GET /api/v1/usuarios': 'listarUsuarios',
  'GET /api/v1/usuarios/{id}': 'obtenerUsuario',
  'PUT /api/v1/usuarios/{id}/roles': 'asignarRolesUsuario',
  'POST /api/v1/usuarios/{id}/desactivar': 'desactivarUsuario',
  // Ingreso directo sin contraseñas (ING-1…ING-7): código por correo y passkeys, sesiones propias, Mi cuenta y gestión por los administradores.
  'POST /api/v1/ingreso/codigo': 'solicitarCodigoIngreso',
  'POST /api/v1/ingreso/codigo/verificar': 'verificarCodigoIngreso',
  'POST /api/v1/ingreso/cuenta': 'elegirCuentaIngreso',
  'POST /api/v1/ingreso/passkey/opciones': 'obtenerOpcionesPasskeyIngreso',
  'POST /api/v1/ingreso/passkey/verificar': 'verificarPasskeyIngreso',
  'POST /api/v1/ingreso/passkey/registro': 'registrarPasskeyIngreso',
  'POST /api/v1/ingreso/sesion/renovar': 'renovarSesionIngreso',
  'POST /api/v1/ingreso/sesion/cerrar': 'cerrarSesionIngreso',
  'GET /api/v1/mi-cuenta': 'obtenerMiCuenta',
  'GET /api/v1/mi-cuenta/passkeys': 'listarMisPasskeys',
  'POST /api/v1/mi-cuenta/passkeys/opciones': 'obtenerOpcionesRegistroPasskey',
  'POST /api/v1/mi-cuenta/passkeys': 'registrarMiPasskey',
  'PUT /api/v1/mi-cuenta/passkeys/{id}': 'renombrarMiPasskey',
  'DELETE /api/v1/mi-cuenta/passkeys/{id}': 'eliminarMiPasskey',
  'GET /api/v1/mi-cuenta/sesiones': 'listarMisSesiones',
  'POST /api/v1/mi-cuenta/sesiones/{id}/cerrar': 'cerrarMiSesion',
  'GET /api/v1/usuarios/{id}/sesiones': 'listarSesionesUsuario',
  'POST /api/v1/usuarios/{id}/sesiones/{sesionId}/cerrar': 'cerrarSesionUsuario',
  'GET /api/v1/usuarios/{id}/passkeys': 'listarPasskeysUsuario',
  'POST /api/v1/usuarios/{id}/passkeys/{passkeyId}/revocar': 'revocarPasskeyUsuario',
  'POST /api/v1/usuarios/{id}/invitar': 'invitarUsuario',
  'GET /api/v1/configuracion/ingreso': 'obtenerConfiguracionIngreso',
  'PUT /api/v1/configuracion/ingreso': 'actualizarConfiguracionIngreso',
  'PUT /api/v1/plataforma/tenants/{id}/ingreso': 'actualizarIngresoGrupo',
  'POST /api/v1/plataforma/operadores': 'crearOperadorPlataforma',
  'GET /api/v1/plataforma/operadores': 'listarOperadoresPlataforma',
  'POST /api/v1/plataforma/operadores/{id}/desactivar': 'desactivarOperadorPlataforma',
  'GET /api/v1/plataforma/operadores/{id}/passkeys': 'listarPasskeysOperador',
  'POST /api/v1/plataforma/operadores/{id}/passkeys/{passkeyId}/revocar': 'revocarPasskeyOperador',
  'GET /api/v1/plataforma/operadores/{id}/sesiones': 'listarSesionesOperador',
  'POST /api/v1/plataforma/operadores/{id}/sesiones/{sesionId}/cerrar': 'cerrarSesionOperador',
  'POST /api/v1/sesiones-usuario': 'crearSesionUsuario',
  'POST /api/v1/empresas': 'crearEmpresa',
  'GET /api/v1/empresas': 'listarEmpresas',
  'GET /api/v1/empresas/{id}': 'obtenerEmpresa',
  'POST /api/v1/empresas/{id}/desactivar': 'desactivarEmpresa',
  'PUT /api/v1/empresas/{id}': 'actualizarEmpresa',
  'POST /api/v1/accesos-soporte': 'concederAccesoSoporte',
  'GET /api/v1/accesos-soporte': 'listarAccesosSoporte',
  'POST /api/v1/accesos-soporte/{id}/revocar': 'revocarAccesoSoporte',
  'GET /api/v1/plataforma/accesos-soporte': 'listarAccesosSoporteConcedidos',
  'GET /api/v1/empresas/{id}/textos-legales': 'obtenerTextosLegalesEmpresa',
  'PUT /api/v1/empresas/{id}/textos-legales/{tipo}': 'publicarTextoLegalEmpresa',
  'POST /api/v1/api-keys': 'crearApiKey',
  'GET /api/v1/api-keys': 'listarApiKeys',
  'DELETE /api/v1/api-keys/{id}': 'revocarApiKey',
  // Integración con SRM y Marco (H9, CN-1…CN-3).
  'POST /api/v1/api-keys/{id}/rotar': 'rotarApiKey',
  'GET /api/v1/consultas/certificacion': 'consultarCertificacion',
  // Sandbox del grupo y su buzón de correo de pruebas (SB-1…SB-6).
  'GET /api/v1/sandbox': 'obtenerSandbox',
  'POST /api/v1/sandbox': 'asegurarSandbox',
  'POST /api/v1/sandbox/reiniciar': 'reiniciarSandbox',
  'GET /api/v1/sandbox/correos': 'listarCorreosDePrueba',
  'GET /api/v1/sandbox/correos/{id}': 'obtenerCorreoDePrueba',
  'DELETE /api/v1/sandbox/correos': 'vaciarCorreosDePrueba',
  'POST /api/v1/tipos-vinculo': 'crearTipoVinculo',
  'GET /api/v1/tipos-vinculo': 'listarTiposVinculo',
  'GET /api/v1/tipos-vinculo/{id}': 'obtenerTipoVinculo',
  'PUT /api/v1/tipos-vinculo/{id}': 'actualizarTipoVinculo',
  'POST /api/v1/areas': 'crearArea',
  'GET /api/v1/areas': 'listarAreas',
  'GET /api/v1/areas/{id}': 'obtenerArea',
  'PUT /api/v1/areas/{id}': 'actualizarArea',
  'PUT /api/v1/areas/{id}/miembros': 'definirMiembrosArea',
  'GET /api/v1/motivos': 'listarMotivos',
  'POST /api/v1/motivos': 'crearMotivo',
  'PUT /api/v1/motivos/{id}': 'actualizarMotivo',
  'POST /api/v1/flujos': 'crearFlujo',
  'GET /api/v1/flujos': 'listarFlujos',
  'GET /api/v1/flujos/por-defecto': 'listarFlujosPorDefecto',
  'PUT /api/v1/flujos/por-defecto': 'definirFlujoPorDefecto',
  'GET /api/v1/flujos/{id}': 'obtenerFlujo',
  'PUT /api/v1/flujos/{id}': 'editarFlujo',
  'POST /api/v1/flujos/{id}/publicar': 'publicarFlujo',
  'GET /api/v1/flujos/{id}/versiones': 'listarVersionesFlujo',
  'GET /api/v1/flujos/{id}/versiones/{n}': 'obtenerVersionFlujo',
  'POST /api/v1/vinculos/{id}/expedientes': 'abrirExpediente',
  'GET /api/v1/expedientes': 'listarExpedientes',
  'GET /api/v1/expedientes/{id}': 'obtenerExpediente',
  'GET /api/v1/expedientes/{id}/historial': 'obtenerHistorialExpediente',
  'POST /api/v1/expedientes/{id}/areas/{areaId}/veredicto': 'emitirVeredicto',
  'POST /api/v1/expedientes/{id}/areas/{areaId}/aprobar-como-administrador': 'aprobarComoAdministrador',
  'POST /api/v1/expedientes/{id}/areas/{areaId}/anular-veredicto': 'anularVeredicto',
  'POST /api/v1/expedientes/{id}/rechazar': 'rechazarExpediente',
  'POST /api/v1/expedientes/{id}/reenviar': 'reenviarExpediente',
  'POST /api/v1/expedientes/{id}/anular': 'anularExpediente',
  // Desaprobar (DS-1, DS-2): reabrir una etapa aprobada, desaprobar un expediente aprobado y volver a la fase inicial.
  'POST /api/v1/expedientes/{id}/etapas/{orden}/reabrir': 'reabrirEtapaExpediente',
  'POST /api/v1/expedientes/{id}/desaprobar': 'desaprobarExpediente',
  'POST /api/v1/expedientes/{id}/fase-inicial': 'volverAFaseInicialExpediente',
  'GET /api/v1/bandeja': 'listarBandeja',
  'GET /api/v1/bandeja/aperturas-pendientes': 'listarAperturasPendientes',
  'GET /api/v1/revalidaciones': 'listarRevalidaciones',
  'GET /api/v1/vencimientos': 'listarVencimientos',
  'GET /api/v1/mis-pendientes': 'obtenerMisPendientes',
  'GET /api/v1/certificados': 'listarCertificados',
  'GET /api/v1/auditoria': 'listarAuditoria',
  'GET /api/v1/plantillas-notificacion': 'listarPlantillasNotificacion',
  'PUT /api/v1/plantillas-notificacion/{evento}/{canal}': 'actualizarPlantillaNotificacion',
  'POST /api/v1/plantillas-notificacion/{evento}/{canal}/vista-previa': 'previsualizarPlantillaNotificacion',
  // IA con OpenAI y cupo por empresa (ADR-0012): plan por Plataforma y uso de la empresa activa.
  'GET /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia': 'obtenerPlanIaEmpresa',
  'PUT /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia': 'actualizarPlanIaEmpresa',
  'GET /api/v1/ia/uso': 'obtenerUsoIa',
  'GET /api/v1/indicadores': 'obtenerIndicadores',
  'GET /api/v1/vencimientos/excel': 'exportarVencimientos',
  'POST /api/v1/vencimientos/recordar': 'recordarVencimientos',
  // Informes y exportaciones (IF-1…IF-6): JSON para la pantalla; Excel, PDF y ZIP como descarga binaria.
  'GET /api/v1/informes/estado-expedientes': 'obtenerInformeEstadoExpedientes',
  'GET /api/v1/informes/estado-expedientes/excel': 'exportarInformeEstadoExpedientes',
  'GET /api/v1/informes/estado-expedientes/pdf': 'exportarInformeEstadoExpedientesPdf',
  'GET /api/v1/informes/vinculos': 'obtenerInformeVinculos',
  'GET /api/v1/informes/vinculos/excel': 'exportarInformeVinculos',
  'GET /api/v1/auditoria/excel': 'exportarAuditoria',
  'GET /api/v1/expedientes/{id}/exportar': 'exportarExpediente',
  'POST /api/v1/revalidaciones/aprobar': 'aprobarRevalidaciones',
  'POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/areas/{areaId}/veredicto': 'emitirVeredictoRequisito',
  'POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/eximir': 'eximirRequisito',
  'GET /api/v1/usuarios/resumen': 'resumirUsuarios',
  'GET /api/v1/yo': 'obtenerYo',
  'GET /api/v1/catalogos/tipos-documento': 'listarTiposDocumentoCanonicos',
  'POST /api/v1/tipos-documento': 'crearTipoDocumento',
  'GET /api/v1/tipos-documento': 'listarTiposDocumento',
  'GET /api/v1/tipos-documento/{id}': 'obtenerTipoDocumento',
  'PUT /api/v1/tipos-documento/{id}': 'actualizarTipoDocumento',
  'PUT /api/v1/tipos-documento/{id}/plantilla': 'iniciarPlantillaTipoDocumento',
  'GET /api/v1/tipos-documento/{id}/plantilla': 'obtenerPlantillaTipoDocumento',
  'DELETE /api/v1/tipos-documento/{id}/plantilla': 'retirarPlantillaTipoDocumento',
  'POST /api/v1/tipos-documento/{id}/plantilla/{version}/confirmar': 'confirmarPlantillaTipoDocumento',
  'GET /api/v1/tipos-documento/{id}/plantilla/descarga': 'descargarPlantillaTipoDocumento',
  'GET /api/v1/vinculos/{id}/documentos': 'listarDocumentosVinculo',
  'GET /api/v1/vinculos/{id}/documentos/{documentoId}': 'obtenerDocumentoVinculo',
  'POST /api/v1/vinculos/{id}/documentos/{documentoId}/versiones/{numero}/descarga': 'descargarDocumentoVinculo',
  // Rutas del tercero (`apps/public`): sesión del tercero en Bearer, sin empresa activa.
  'POST /public/v1/enlaces/{token}/canjear': 'canjearEnlace',
  'POST /public/v1/enlaces/{token}/otp': 'verificarOtpEnlace',
  'GET /public/v1/vinculos': 'listarVinculosPublicos',
  'GET /public/v1/embed/origenes-permitidos': 'listarOrigenesEmbebido',
  'GET /public/v1/invitaciones/{vinculoId}': 'obtenerInvitacionPublica',
  'POST /public/v1/invitaciones/{vinculoId}/aceptar': 'aceptarInvitacion',
  'POST /public/v1/autorregistro': 'autorregistrarTercero',
  'POST /public/v1/acceso': 'solicitarAcceso',
  'POST /public/v1/sesion/renovar': 'renovarSesionTercero',
  'POST /public/v1/sesion/cerrar': 'cerrarSesionTercero',
  'GET /public/v1/expedientes/{id}': 'obtenerExpedientePublico',
  'POST /public/v1/expedientes/{id}/reenviar': 'reenviarExpedientePublico',
  'POST /public/v1/expedientes/{id}/requisitos/{requisitoId}/ratificar': 'ratificarRequisito',
  'GET /public/v1/boveda/cuentas-bancarias': 'listarCuentasBancarias',
  'POST /public/v1/boveda/cuentas-bancarias': 'registrarCuentaBancaria',
  'POST /public/v1/boveda/cuentas-bancarias/{id}/retirar': 'retirarCuentaBancaria',
  'POST /public/v1/vinculos/{id}/cuenta-designada': 'designarCuenta',
  // Reinicio tras un rechazo (DS-4): el tercero vuelve a postularse.
  'POST /public/v1/vinculos/{id}/reintentar': 'reintentarPostulacion',
  'GET /api/v1/vinculos/{id}/cuenta-designada': 'obtenerCuentaDesignada',
  'GET /api/v1/vinculos/{id}/ficha': 'obtenerFichaDelVinculo',
  'POST /api/v1/vinculos/{id}/cuenta-designada/aprobar': 'aprobarCuentaDesignada',
  'POST /api/v1/vinculos/{id}/cuenta-designada/rechazar': 'rechazarCuentaDesignada',
  'GET /public/v1/boveda': 'obtenerBoveda',
  'GET /public/v1/boveda/documentos/{documentoId}': 'obtenerDocumentoBoveda',
  'POST /public/v1/boveda/documentos': 'iniciarCargaDocumento',
  'POST /public/v1/boveda/formularios': 'enviarFormularioBoveda',
  'GET /public/v1/expedientes/{id}/requisitos/{requisitoId}/plantilla': 'descargarPlantillaRequisito',
  'POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/confirmar': 'confirmarCargaDocumento',
  'POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/descarga': 'descargarDocumentoBoveda',
  'POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retirar': 'retirarVersionDocumento',
  'GET /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retiro/impacto': 'obtenerImpactoDeRetiro',
  // Ficha maestra del tercero (F1, docs/14): catálogos de plataforma, secciones del wizard y recálculo de completitud.
  'POST /api/v1/plataforma/catalogos/{catalogo}/importar': 'importarCatalogo',
  'GET /api/v1/catalogos/{catalogo}': 'buscarEnCatalogo',
  'POST /api/v1/plataforma/fichas/recalcular': 'recalcularFichas',
  'GET /public/v1/catalogos/{catalogo}': 'buscarEnCatalogoPublico',
  'GET /public/v1/ficha': 'obtenerFicha',
  'GET /public/v1/ficha/{seccion}': 'obtenerSeccionFicha',
  'PUT /public/v1/ficha/{seccion}': 'guardarSeccionFicha',
  'POST /public/v1/ficha/{seccion}/evaluacion': 'evaluarSeccionFicha',
  // Actualizar desde el documento (F5, FM-14).
  'GET /public/v1/ficha/propuestas': 'listarPropuestasFicha',
  'GET /public/v1/ficha/{seccion}/propuestas': 'obtenerPropuestasSeccion',
  // Importador de la ficha desde el Portal y sincronizador de coexistencia (F4, docs/08 §6.6).
  'POST /api/v1/plataforma/importacion/{origen}': 'importarTerceros',
  'GET /api/v1/plataforma/importacion/{origen}/{id}': 'obtenerImportacion',
  'GET /api/v1/plataforma/importacion/{origen}/{id}/rechazos': 'descargarRechazosImportacion',
  'POST /api/v1/plataforma/importacion/{origen}/{id}/reanudar': 'reanudarImportacion',
  'POST /api/v1/plataforma/importacion/{origen}/terceros/{clave}/sincronizar': 'sincronizarTerceroDeOrigen',
  // Verificaciones externas y cumplimiento (H7, VE-5…VE-16).
  'POST /api/v1/expedientes/{id}/verificaciones/{tipo}/consultar': 'consultarVerificacion',
  'POST /api/v1/expedientes/{id}/verificaciones/{tipo}/coincidencias/{coincidenciaId}/resolver': 'resolverCoincidencia',
  'POST /api/v1/expedientes/{id}/verificaciones/estudio-cumplimiento': 'registrarEstudioCumplimiento',
  'POST /api/v1/expedientes/{id}/verificaciones/{tipo}/evidencia/descarga': 'descargarEvidenciaVerificacion',
  'GET /api/v1/cumplimiento/bandeja': 'listarBandejaCumplimiento',
  'GET /api/v1/vinculos/{id}/monitoreo-listas': 'obtenerMonitoreoListas',
  'POST /api/v1/vinculos/{id}/monitoreo-listas/coincidencias/{coincidenciaId}/resolver': 'resolverCoincidenciaMonitoreo',
  'POST /api/v1/vinculos/{id}/monitoreo-listas/evidencia/descarga': 'descargarEvidenciaMonitoreo',
  'GET /api/v1/configuracion/listas-restrictivas': 'obtenerListasRestrictivas',
  'PUT /api/v1/configuracion/listas-restrictivas': 'actualizarListasRestrictivas',
  'GET /api/v1/informes/cumplimiento': 'obtenerInformeCumplimiento',
  'GET /api/v1/informes/cumplimiento/excel': 'exportarInformeCumplimiento',
  // Cumplimiento completo (CU-15…CU-21): matriz de riesgo, archivos del oficial, consulta de listas por fuera, rechazo directo e informe SAGRILAFT.
  'GET /api/v1/configuracion/matriz-riesgo': 'obtenerMatrizDeRiesgo',
  'PUT /api/v1/configuracion/matriz-riesgo': 'actualizarMatrizDeRiesgo',
  'POST /api/v1/expedientes/{id}/cumplimiento/matriz/calcular': 'calcularMatrizDeRiesgo',
  'PUT /api/v1/expedientes/{id}/cumplimiento/adjuntos': 'iniciarAdjuntoDeCumplimiento',
  'POST /api/v1/expedientes/{id}/cumplimiento/adjuntos/{cargaId}/confirmar': 'confirmarAdjuntoDeCumplimiento',
  'POST /api/v1/expedientes/{id}/cumplimiento/adjuntos/{adjuntoId}/descarga': 'descargarAdjuntoDeCumplimiento',
  'GET /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/personas': 'listarPersonasDeConsultaDeListas',
  'POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual': 'registrarConsultaManualDeListas',
  'POST /api/v1/expedientes/{id}/cumplimiento/rechazar': 'rechazarPorCumplimiento',
  'GET /api/v1/informes/sagrilaft': 'obtenerInformeSagrilaft',
  'GET /api/v1/informes/sagrilaft/excel': 'exportarInformeSagrilaft',
  'GET /api/v1/informes/sagrilaft/terceros/{terceroId}/personas': 'obtenerPersonasDelTerceroSagrilaft',
  // Revisión automática con IA (Manual, Asistido, Automático) y bandeja de excepciones (13 y 14).
  'GET /api/v1/configuracion/revision-automatica': 'obtenerRevisionAutomatica',
  'PUT /api/v1/configuracion/revision-automatica': 'actualizarRevisionAutomatica',
  'GET /api/v1/revision-automatica/excepciones': 'listarExcepcionesDeRevision',
  'POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/revision-automatica/resolver': 'resolverRevisionAutomatica',
  // Plazo de cargue del expediente inicial: reapertura del caducado y job de Plataforma (P-4-2, LT-1…LT-4).
  'POST /api/v1/expedientes/{id}/reabrir': 'reabrirExpediente',
  'POST /api/v1/plataforma/plazos-de-cargue/revisar': 'revisarPlazosDeCargue',
  // Campana del tercero y recordatorio de observaciones pendientes (NT-1…NT-3).
  'GET /public/v1/notificaciones': 'listarNotificacionesTercero',
  'GET /public/v1/notificaciones/stream': 'streamNotificacionesTercero',
  'POST /public/v1/notificaciones/{id}/leida': 'marcarNotificacionTerceroLeida',
  'POST /public/v1/notificaciones/leidas': 'marcarNotificacionesTerceroLeidas',
  'GET /public/v1/preferencias-notificacion': 'obtenerPreferenciasNotificacionTercero',
  'PUT /public/v1/preferencias-notificacion': 'actualizarPreferenciasNotificacionTercero',
  'POST /api/v1/expedientes/{id}/recordar-observaciones': 'recordarObservaciones',
  'POST /api/v1/expedientes/recordar-observaciones': 'recordarObservacionesEnLote',
  'GET /api/v1/configuracion/recordatorio-observaciones': 'obtenerRecordatorioAutomatico',
  'PUT /api/v1/configuracion/recordatorio-observaciones': 'actualizarRecordatorioAutomatico',
  'POST /api/v1/plataforma/recordatorios-observaciones/revisar': 'revisarRecordatoriosDeObservaciones',
  'POST /api/v1/plataforma/vigencias-de-vinculos/revisar': 'revisarVigenciasDeVinculos',
  'POST /api/v1/plataforma/sla/revisar': 'revisarSlaDeRevision',
  // Invitación masiva por lote (H8, LT-5…LT-10).
  'GET /api/v1/invitaciones/lotes/plantilla': 'descargarPlantillaLote',
  'POST /api/v1/invitaciones/lotes/vista-previa': 'previsualizarLote',
  'POST /api/v1/invitaciones/lotes': 'crearLote',
  'GET /api/v1/invitaciones/lotes': 'listarLotes',
  'GET /api/v1/invitaciones/lotes/{id}': 'obtenerLote',
  'GET /api/v1/invitaciones/lotes/{id}/filas': 'listarFilasLote',
  'POST /api/v1/invitaciones/lotes/{id}/pausar': 'pausarLote',
  'POST /api/v1/invitaciones/lotes/{id}/reanudar': 'reanudarLote',
  'POST /api/v1/invitaciones/lotes/{id}/cancelar': 'cancelarLote',
  'POST /api/v1/invitaciones/lotes/{id}/reintentar': 'reintentarLote',
  'GET /api/v1/invitaciones/lotes/{id}/informe': 'descargarInformeLote',
  // Operación de la revisión (OP-1…OP-19): campañas de renovación, delegación, SLA y ayudas por empresa.
  'POST /api/v1/campanias/vista-previa': 'previsualizarCampania',
  'POST /api/v1/campanias': 'crearCampania',
  'GET /api/v1/campanias': 'listarCampanias',
  'GET /api/v1/campanias/{id}': 'obtenerCampania',
  'GET /api/v1/campanias/{id}/filas': 'listarFilasCampania',
  'POST /api/v1/campanias/{id}/pausar': 'pausarCampania',
  'POST /api/v1/campanias/{id}/reanudar': 'reanudarCampania',
  'POST /api/v1/campanias/{id}/cancelar': 'cancelarCampania',
  'POST /api/v1/campanias/{id}/reintentar': 'reintentarCampania',
  'GET /api/v1/campanias/{id}/informe': 'descargarInformeCampania',
  'GET /api/v1/delegaciones/candidatos': 'listarCandidatosDeDelegacion',
  'GET /api/v1/delegaciones': 'listarDelegaciones',
  'POST /api/v1/delegaciones': 'crearDelegacion',
  'POST /api/v1/delegaciones/{id}/revocar': 'revocarDelegacion',
  'GET /api/v1/configuracion/sla': 'obtenerConfiguracionSla',
  'PUT /api/v1/configuracion/sla': 'actualizarConfiguracionSla',
  'GET /api/v1/informes/sla': 'obtenerInformeSla',
  'GET /api/v1/informes/sla/excel': 'descargarInformeSlaExcel',
  'GET /api/v1/ayudas-de-documentos': 'listarAyudasDeDocumentos',
  'PUT /api/v1/ayudas-de-documentos/{tipoId}': 'actualizarAyudaDeDocumento',
  // Habeas data (Ley 1581): solicitudes del titular desde su portal, las que atiende la empresa y las que atiende la plataforma (OD-1…OD-8).
  'POST /public/v1/solicitudes-habeas': 'crearSolicitudHabeas',
  'GET /public/v1/solicitudes-habeas': 'listarSolicitudesHabeas',
  'GET /public/v1/solicitudes-habeas/{id}': 'obtenerSolicitudHabeas',
  'GET /public/v1/solicitudes-habeas/{id}/constancia': 'obtenerConstanciaHabeas',
  'GET /api/v1/solicitudes-habeas': 'listarSolicitudesHabeasDeLaEmpresa',
  'GET /api/v1/solicitudes-habeas/resumen': 'resumirSolicitudesHabeasDeLaEmpresa',
  'GET /api/v1/solicitudes-habeas/{id}': 'obtenerSolicitudHabeasDeLaEmpresa',
  'POST /api/v1/solicitudes-habeas/{id}/resolver': 'resolverSolicitudHabeasDeLaEmpresa',
  'GET /api/v1/plataforma/solicitudes-habeas': 'listarSolicitudesHabeasDePlataforma',
  'GET /api/v1/plataforma/solicitudes-habeas/resumen': 'resumirSolicitudesHabeasDePlataforma',
  'GET /api/v1/plataforma/solicitudes-habeas/{id}': 'obtenerSolicitudHabeasDePlataforma',
  'GET /api/v1/plataforma/solicitudes-habeas/{id}/plan': 'obtenerPlanDeSupresion',
  'POST /api/v1/plataforma/solicitudes-habeas/{id}/resolver': 'resolverSolicitudHabeasDePlataforma',
  'POST /api/v1/plataforma/habeas/revisar-plazos': 'revisarPlazosDeHabeas',
  // Exportación firmada de la bóveda del tercero (OD-9, OD-10).
  'POST /public/v1/boveda/exportar': 'exportarBoveda',
  'GET /public/v1/exportaciones': 'listarExportacionesBoveda',
  'GET /public/v1/exportaciones/{id}': 'obtenerExportacionBoveda',
  'POST /public/v1/exportaciones/{id}/descarga': 'descargarExportacionBoveda',
  // Plan y límites por grupo (OD-14…OD-17): Plataforma los fija; el grupo ve su plan y su uso.
  'GET /api/v1/plataforma/tenants/{tenantId}/plan': 'obtenerPlanDeUnGrupo',
  'PUT /api/v1/plataforma/tenants/{tenantId}/plan': 'actualizarPlanDeUnGrupo',
  'POST /api/v1/plataforma/planes/revisar': 'revisarPlanesDeGrupos',
  'GET /api/v1/tenant/plan': 'obtenerPlanDelGrupo',
  'GET /api/v1/tenant/uso': 'obtenerUsoDelGrupo',
  // Dominio de correo propio por empresa (OD-18…OD-22).
  'GET /api/v1/empresas/{id}/dominio-correo': 'obtenerDominioDeCorreo',
  'PUT /api/v1/empresas/{id}/dominio-correo': 'fijarDominioDeCorreo',
  'POST /api/v1/empresas/{id}/dominio-correo/verificar': 'verificarDominioDeCorreo',
  'DELETE /api/v1/empresas/{id}/dominio-correo': 'quitarDominioDeCorreo',
};

/** Prefijos del contrato que el cliente genera (el resto, como los health checks, no es parte del contrato). */
const PREFIJOS = ['/api/v1/', '/public/v1/'];

/** Respuesta real (código → esquema) de los endpoints publicados como `IResult`; `null` = sin cuerpo. */
const RESPUESTAS_REALES: Record<string, Record<string, string | null>> = {
  // 201 al crear; 200 si la clave externa ya identifica a ese tercero (idempotente, IO-2).
  crearTercero: { '200': 'TerceroDto', '201': 'TerceroDto' },
  // 201 al crear; 200 cuando otro sistema integrado se suma al vínculo existente (I-0).
  crearVinculo: { '200': 'VinculoDto', '201': 'VinculoDto' },
  invitarVinculo: { '201': 'EnlaceInvitacionDto' },
  crearTenant: { '201': 'TenantCreadoDto' },
  crearSistema: { '201': 'SistemaIntegradoDto' },
  crearWebhook: { '201': 'WebhookDto' },
  reenviarEntregaWebhook: { '202': 'EntregaWebhookDto' },
  crearUsuario: { '201': 'UsuarioDto' },
  crearOperadorPlataforma: { '201': 'OperadorDePlataformaDto' },
  cerrarSesionIngreso: { '204': null },
  eliminarMiPasskey: { '204': null },
  cerrarMiSesion: { '204': null },
  cerrarSesionUsuario: { '204': null },
  cerrarSesionOperador: { '204': null },
  crearEmpresa: { '201': 'EmpresaDto' },
  crearApiKey: { '201': 'ApiKeyDto' },
  revocarApiKey: { '204': null },
  vaciarCorreosDePrueba: { '204': null },
  // La nueva llave con su `valor` (una sola vez); la anterior sigue valiendo hasta la fecha pedida (CN-3).
  rotarApiKey: { '201': 'ApiKeyDto' },
  // 201 al crear el sandbox; 200 si ya existía (SB-6).
  asegurarSandbox: { '200': 'SandboxDto', '201': 'SandboxDto' },
  // 304 sin cuerpo con `If-None-Match` igual al ETag (CN-1).
  consultarCertificacion: { '200': 'ConsultaCertificacionDto', '304': null },
  crearTipoVinculo: { '201': 'TipoVinculoDto' },
  crearArea: { '201': 'AreaDto' },
  crearMotivo: { '201': 'MotivoTipificadoDto' },
  crearFlujo: { '201': 'FlujoDto' },
  abrirExpediente: { '201': 'ExpedienteDto' },
  crearTipoDocumento: { '201': 'TipoDocumentoTenantDto' },
  // 200 con la sesión; 202 cuando el tipo de vínculo exige OTP (el código llegó al correo).
  canjearEnlace: { '200': 'SesionTerceroDto', '202': 'OtpRequeridoDto' },
  autorregistrarTercero: { '202': 'SolicitudRecibidaDto' },
  // Siempre 202 con el mismo cuerpo, exista o no el correo.
  solicitarAcceso: { '202': 'SolicitudRecibidaDto' },
  cerrarSesionTercero: { '204': null },
  // 201 con la URL prefirmada; 200 `duplicado` sin `carga` (el archivo ya es una versión del documento).
  iniciarCargaDocumento: { '200': 'CargaPrefirmadaDto', '201': 'CargaPrefirmadaDto' },
  // 202: la corrida queda encolada; su estado se consulta con obtenerImportacionPortal.
  importarPortal: { '202': 'CorridaImportacionPortalDto' },
  // 202: el lote queda en proceso; su avance se consulta con obtenerLote.
  crearLote: { '202': 'LoteDto' },
  // 202: la campaña queda en proceso; su avance se consulta con obtenerCampania.
  crearCampania: { '202': 'CampaniaDto' },
  // 201 con la solicitud y su radicado; 202: la exportación queda encolada y su estado se consulta con obtenerExportacionBoveda.
  crearSolicitudHabeas: { '201': 'SolicitudHabeasDto' },
  exportarBoveda: { '202': 'ExportacionBovedaDto' },
  reanudarImportacionPortal: { '202': 'CorridaImportacionPortalDto' },
};

/** Enums textuales del dominio (docs/03 §3.1, docs/07 §3.1). */
const ENUMS: Record<string, string[]> = {
  EstadoCorridaPortal: ['Encolada', 'EnCurso', 'Completada', 'Fallida'],
  EstadoLote: ['Procesando', 'Pausado', 'Completado', 'CompletadoConErrores', 'Cancelado'],
  EstadoFilaLote: ['Pendiente', 'Exitosa', 'Fallida', 'Duplicada', 'Invalida', 'Cancelada'],
  EstadoCampania: ['Procesando', 'Pausada', 'Completada', 'CompletadaConErrores', 'Cancelada'],
  EstadoFilaCampania: ['Pendiente', 'Exitosa', 'Fallida', 'Omitida', 'Cancelada'],
  EstadoDeDelegacion: ['Programada', 'Vigente', 'Vencida', 'Revocada'],
  EstadoDeSla: ['EnTiempo', 'PorVencer', 'Vencido'],
  OrigenDeAceptacion: ['Expediente', 'Importacion'],
  EstadoVinculo: ['Invitado', 'EnValidacion', 'Activo', 'PorVencer', 'Vencido', 'Bloqueado', 'Desvinculado', 'Anulado'],
  OrigenVinculo: ['Invitacion', 'Autorregistro', 'Migracion', 'Api'],
  Rol: ['Administrador', 'Revisor', 'OficialCumplimiento', 'Auditor'],
  EstadoExpediente: ['Abierto', 'EnRevision', 'Observado', 'Aprobado', 'Rechazado', 'Anulado', 'Caducado'],
  TipoExpediente: ['Inicial', 'Renovacion', 'Actualizacion'],
  EstadoEtapa: ['Esperando', 'Activa', 'Aprobada'],
  EstadoAreaExpediente: ['Esperando', 'Pendiente', 'Aprobada', 'Observada'],
  Veredicto: ['Aprobado', 'Observado'],
  OrigenVeredicto: ['Humano', 'Sistema', 'Administrador'],
  OrigenFlujo: ['Explicito', 'Regla', 'PorDefecto'],
  PoliticaDevolucion: ['Inmediata', 'AlCerrarArea'],
  EstadoFlujoVersion: ['Borrador', 'Publicada', 'Retirada'],
  CategoriaMotivo: ['Documento', 'Formulario', 'Contacto', 'Otro'],
  AplicaMotivo: ['Observacion', 'Rechazo', 'Ambos'],
  EstadoVersion: ['Recibida', 'EnAnalisis', 'Actual', 'Superada', 'Devuelta', 'Retirada'],
  AmbitoDocumento: ['Global', 'PorVinculo'],
  ClaseDocumento: ['Archivo', 'Formulario'],
  TipoTextoLegal: ['AvisoPlataforma', 'AutorizacionEmpresa', 'TerminosEmpresa'],
  EstadoRequisito: ['Pendiente', 'Cargado', 'EnRevision', 'Observado', 'Aprobado', 'Eximido'],
  EstadoAreaRequisito: ['Esperando', 'Pendiente', 'Aprobado', 'Observado', 'Eximido'],
  // Ficha maestra por empresa (F2): qué pide un requisito y cómo cambió un campo entre la versión aceptada y la nueva.
  TipoRequisito: ['Documento', 'Seccion'],
  TipoCambio: ['Agregado', 'Quitado', 'Modificado'],
  // Verificaciones externas (H7).
  TipoVerificacion: ['ListasRestrictivas', 'EstudioDeCumplimiento'],
  EstadoVerificacion: ['Pendiente', 'CoincidenciaPorAnalizar', 'Satisfecha', 'Fallida', 'Expirada'],
  ResultadoVerificacion: ['SinCoincidencias', 'Coincidencias', 'Aprobado', 'Rechazado'],
  DecisionCoincidencia: ['Descartada', 'Confirmada'],
  DebidaDiligencia: ['Simple', 'Ordinaria', 'Ampliada'],
  OrigenDelFactor: ['Manual', 'Automatico'],
  PropositoDeAdjunto: ['Estudio', 'ConsultaManual'],
  ConceptoCumplimiento: ['Favorable', 'Desfavorable'],
  ModoBusquedaListas: ['PorNombre', 'PorDocumento'],
  OrigenFilaCumplimiento: ['Expediente', 'Monitoreo'],
};

/** `Esquema.propiedad` → enum. */
const PROPIEDADES_ENUM: Record<string, string> = {
  'VinculoDto.estado': 'EstadoVinculo',
  'VerificacionDto.tipo': 'TipoVerificacion',
  'VerificacionDto.estado': 'EstadoVerificacion',
  'VerificacionRequeridaDto.tipo': 'TipoVerificacion',
  'VerificacionPendienteDto.tipo': 'TipoVerificacion',
  'VerificacionPendienteDto.estado': 'EstadoVerificacion',
  'ResolverCoincidenciaDto.decision': 'DecisionCoincidencia',
  'EstudioCumplimientoDto.debidaDiligencia': 'DebidaDiligencia',
  'EstudioCumplimientoDto.concepto': 'ConceptoCumplimiento',
  'RegistrarEstudioCumplimientoDto.debidaDiligencia': 'DebidaDiligencia',
  'RegistrarEstudioCumplimientoDto.concepto': 'ConceptoCumplimiento',
  'FilaCumplimientoDto.origen': 'OrigenFilaCumplimiento',
  'ResultadoDeMatrizDto.debidaDiligencia': 'DebidaDiligencia',
  'NivelDeRiesgoDto.debidaDiligencia': 'DebidaDiligencia',
  'FactorDeRiesgoDto.origen': 'OrigenDelFactor',
  'DesgloseDeMatrizDto.origen': 'OrigenDelFactor',
  'AdjuntoDeCumplimientoDto.proposito': 'PropositoDeAdjunto',
  'IniciarAdjuntoDeCumplimientoDto.proposito': 'PropositoDeAdjunto',
  'ConfirmarAdjuntoDeCumplimientoDto.proposito': 'PropositoDeAdjunto',
  'VinculoDto.origen': 'OrigenVinculo',
  'FilaEstadoExpedienteDto.estado': 'EstadoExpediente',
  'FilaEstadoExpedienteDto.tipo': 'TipoExpediente',
  'TiempoDeAreaDto.estado': 'EstadoAreaExpediente',
  'FilaInformeVinculoDto.estado': 'EstadoVinculo',
  'ExpedienteDelInformeDto.estado': 'EstadoExpediente',
  'ExpedienteDelInformeDto.tipo': 'TipoExpediente',
  'RolAsignadoDto.rol': 'Rol',
  'ExpedienteDto.estado': 'EstadoExpediente',
  'LoteDto.estado': 'EstadoLote',
  'FilaLoteDto.estado': 'EstadoFilaLote',
  'CampaniaDto.estado': 'EstadoCampania',
  'FilaCampaniaDto.estado': 'EstadoFilaCampania',
  'DelegacionDto.estado': 'EstadoDeDelegacion',
  'FilaBandejaDto.sla': 'EstadoDeSla',
  'ExpedienteDto.tipo': 'TipoExpediente',
  'ExpedienteDto.politicaDevolucion': 'PoliticaDevolucion',
  'EtapaExpedienteDto.estado': 'EstadoEtapa',
  'AreaExpedienteDto.estado': 'EstadoAreaExpediente',
  'VeredictoEmitidoDto.veredicto': 'Veredicto',
  'VeredictoEmitidoDto.origen': 'OrigenVeredicto',
  'VeredictoDto.veredicto': 'Veredicto',
  'FlujoDeExpedienteDto.origen': 'OrigenFlujo',
  'FilaBandejaDto.estadoArea': 'EstadoAreaExpediente',
  'FilaBandejaDto.estadoExpediente': 'EstadoExpediente',
  'FlujoVersionDto.estado': 'EstadoFlujoVersion',
  'FlujoVersionDto.politicaDevolucion': 'PoliticaDevolucion',
  'MotivoTipificadoDto.categoria': 'CategoriaMotivo',
  'MotivoTipificadoDto.aplicaA': 'AplicaMotivo',
  'CrearMotivoDto.categoria': 'CategoriaMotivo',
  'CrearMotivoDto.aplicaA': 'AplicaMotivo',
  'ActualizarMotivoDto.categoria': 'CategoriaMotivo',
  'ActualizarMotivoDto.aplicaA': 'AplicaMotivo',
  'VersionDocumentoDto.estado': 'EstadoVersion',
  'VersionDocumentoDto.clase': 'ClaseDocumento',
  'DocumentoBovedaDto.ambito': 'AmbitoDocumento',
  'DocumentoBovedaDto.clase': 'ClaseDocumento',
  'DocumentoDto.ambito': 'AmbitoDocumento',
  'DocumentoDto.clase': 'ClaseDocumento',
  'DocumentoVistaEmpresaDto.ambito': 'AmbitoDocumento',
  'ExpedientePublicoDto.estado': 'EstadoExpediente',
  'VinculoPublicoDto.estado': 'EstadoVinculo',
  'VinculoDeSesionDto.estado': 'EstadoVinculo',
  'TextoLegalPublicoDto.tipo': 'TipoTextoLegal',
  'ConsentimientoDto.tipo': 'TipoTextoLegal',
  'AceptacionDto.tipo': 'TipoTextoLegal',
  'RequisitoExpedienteDto.estado': 'EstadoRequisito',
  'RequisitoPublicoDto.estado': 'EstadoRequisito',
  'AreaDeRequisitoDto.estado': 'EstadoAreaRequisito',
  'TipoDocumentoRequisitoDto.ambito': 'AmbitoDocumento',
  'RequisitoExpedienteDto.tipoRequisito': 'TipoRequisito',
  'RequisitoPublicoDto.tipoRequisito': 'TipoRequisito',
  'DocumentoPorDecidirDto.tipoRequisito': 'TipoRequisito',
  'CambioDeCampoDto.tipoCambio': 'TipoCambio',
  'RevalidacionDeSeccionDto.estado': 'EstadoRequisito',
  'AceptacionDeSeccionDto.origen': 'OrigenDeAceptacion',
  'SeccionAceptadaDto.origen': 'OrigenDeAceptacion',
  'CorridaImportacionPortalDto.estado': 'EstadoCorridaPortal',
};

const METODOS = ['get', 'post', 'put', 'patch', 'delete'];

export function transformarOpenApi(entrada: unknown): DocumentoOpenApi {
  const documento = structuredClone(entrada) as DocumentoOpenApi;
  const esquemas = documento.components.schemas;

  for (const [nombre, valores] of Object.entries(ENUMS)) {
    esquemas[nombre] = { type: 'string', enum: valores };
  }

  // Los datos de cada evento del historial son siempre un objeto JSON (el cuerpo del evento de dominio).
  const historial = esquemas.EventoHistorialDto?.properties;
  if (historial) historial.datos = { type: 'object', additionalProperties: true };
  // `JsonElement` (contenido canónico del certificado, datos de un evento público): siempre un objeto JSON.
  esquemas.JsonElement = { type: 'object', additionalProperties: true };

  // Con el alta completa (PT-5) trae además la empresa, el administrador, el sistema y su llave; si no, llegan nulos.
  const opcional = (esquema: string): Esquema => ({ oneOf: [{ type: 'null' }, { $ref: `#/components/schemas/${esquema}` }] });
  esquemas.TenantCreadoDto = {
    type: 'object',
    required: ['tenant', 'apiKeyAdministrador', 'empresa', 'administrador', 'sistema', 'apiKeySistema'],
    properties: {
      tenant: { $ref: '#/components/schemas/TenantDto' },
      apiKeyAdministrador: { $ref: '#/components/schemas/ApiKeyDto' },
      empresa: opcional('EmpresaDto'),
      administrador: opcional('UsuarioDto'),
      sistema: opcional('SistemaIntegradoDto'),
      apiKeySistema: opcional('ApiKeyDto'),
    },
  };

  // Respuestas `IResult` cuyo tipo no llega a los esquemas.
  esquemas.OtpRequeridoDto = {
    type: 'object',
    required: ['requiereOtp', 'expira'],
    properties: { requiereOtp: { type: 'boolean' }, expira: { type: 'string', format: 'date-time' } },
  };
  esquemas.SolicitudRecibidaDto = { type: 'object', required: ['mensaje'], properties: { mensaje: { type: 'string' } } };
  esquemas.InstruccionCargaDto = {
    type: 'object',
    required: ['metodo', 'url', 'cabeceras', 'expira'],
    properties: {
      metodo: { type: 'string' },
      url: { type: 'string' },
      cabeceras: { type: 'object', additionalProperties: { type: 'string' } },
      expira: { type: 'string', format: 'date-time' },
    },
  };
  esquemas.CargaPrefirmadaDto = {
    type: 'object',
    required: ['documentoId', 'version', 'duplicado', 'carga'],
    properties: {
      documentoId: { type: 'string', format: 'uuid' },
      version: { type: 'integer', format: 'int32' },
      duplicado: { type: 'boolean' },
      carga: { oneOf: [{ type: 'null' }, { $ref: '#/components/schemas/InstruccionCargaDto' }] },
    },
  };

  for (const [nombre, esquema] of Object.entries(esquemas)) {
    normalizarEnteros(esquema);
    for (const [propiedad, valor] of Object.entries(esquema.properties ?? {})) {
      const enumeracion = PROPIEDADES_ENUM[`${nombre}.${propiedad}`];
      if (enumeracion && esquema.properties) {
        esquema.properties[propiedad] = { $ref: `#/components/schemas/${enumeracion}` };
      } else {
        normalizarEnteros(valor);
      }
    }
  }

  const rutas: DocumentoOpenApi['paths'] = {};
  for (const [ruta, operaciones] of Object.entries(documento.paths)) {
    if (!PREFIJOS.some((prefijo) => ruta.startsWith(prefijo))) continue;

    for (const metodo of METODOS) {
      const operacion = operaciones[metodo];
      if (!operacion) continue;

      const clave = `${metodo.toUpperCase()} ${ruta}`;
      const nombre = NOMBRES[clave];
      if (!nombre) {
        throw new Error(
          `El contrato trae ${clave}, que el cliente no conoce. Agrégalo en packages/api-client/openapi/transformar.ts.`,
        );
      }

      operacion.operationId = nombre;
      // Todo lo del tercero en un módulo propio (`publico`), que es lo único que importa `apps/public`.
      operacion.tags = [ruta.startsWith('/public/') ? 'publico' : etiqueta(operacion.tags?.[0])];
      delete operacion.summary;
      delete operacion.description;

      const reales = RESPUESTAS_REALES[nombre];
      if (reales) {
        operacion.responses = Object.fromEntries(
          Object.entries(reales).map(([codigo, esquema]) => [
            codigo,
            esquema
              ? {
                  description: 'OK',
                  content: { 'application/json': { schema: { $ref: `#/components/schemas/${esquema}` } } },
                }
              : { description: 'Sin contenido' },
          ]),
        );
      }

      for (const parametro of (operacion.parameters as { schema?: Esquema }[] | undefined) ?? []) {
        if (parametro.schema) normalizarEnteros(parametro.schema);
      }
    }
    rutas[ruta] = operaciones;
  }
  documento.paths = rutas;

  const faltantes = Object.keys(NOMBRES).filter((clave) => {
    const [metodo = '', ruta = ''] = clave.split(' ');
    return !documento.paths[ruta]?.[metodo.toLowerCase()];
  });
  if (faltantes.length > 0) {
    throw new Error(`El contrato ya no trae: ${faltantes.join(', ')}. Actualiza transformar.ts y la UI.`);
  }

  return documento;
}

/** `Validasoft.Aplicacion.Vinculos.TercerosEndpoints` → `terceros`. */
function etiqueta(original: string | undefined): string {
  const ultimo = (original ?? 'general').split('.').pop() ?? 'general';
  return ultimo.replace(/Endpoints$/, '').replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}

function normalizarEnteros(esquema: Esquema): void {
  if (Array.isArray(esquema.type) && esquema.type.includes('integer') && esquema.type.includes('string')) {
    esquema.type = esquema.type.filter((t) => t !== 'string');
    if (esquema.type.length === 1) esquema.type = esquema.type[0];
    delete esquema.pattern;
  }
  if (esquema.items) normalizarEnteros(esquema.items);
}
