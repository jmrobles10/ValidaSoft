import { randomUUID } from 'node:crypto';
import { asercion, CLAVE_PLATAFORMA, clavesDeSistema, ErrorDeApi, llamar, nitAleatorio, type PersonaDelSistema } from './api';

/** Origen del sistema integrado de prueba que aloja las pantallas embebidas (lo sirve `page.route`, ver embebido.ts). */
export const ORIGEN_SISTEMA = 'http://localhost:5199';
export const CODIGO_SISTEMA = 'E2E';

/** Tipo de documento propio de Sur con fecha de expedición: vence 30 días después, avisa a 10 días, 5 de gracia. */
export const SST = { codigo: 'CERT_SST', nombre: 'Certificado de seguridad y salud en el trabajo', dias: 30, aviso: 10, gracia: 5 } as const;

const SCOPES = [
  'empresas:administrar', 'api-keys:administrar', 'sistemas:administrar', 'usuarios:administrar', 'configuracion:leer',
  'configuracion:escribir', 'terceros:leer', 'terceros:escribir', 'vinculos:leer', 'vinculos:escribir', 'vinculos:bloquear', 'cuentas:aprobar',
  'auditoria:leer', 'auditoria:exportar', 'flujos:leer', 'flujos:escribir', 'expedientes:leer', 'expedientes:revisar', 'expedientes:administrar',
  'expedientes:exportar', 'documentos:leer', 'documentos:descargar', 'certificados:leer', 'eventos:leer', 'webhooks:administrar',
  'verificaciones:escribir', 'cumplimiento:decidir', 'sesiones-tercero:crear',
];

export interface Persona extends PersonaDelSistema {
  id: string;
}

export interface Area {
  id: string;
  codigo: string;
  nombre: string;
}

export interface Empresa {
  id: string;
  nombre: string;
  prefijo: string;
  areas: Record<string, Area>;
}

/**
 * Un grupo empresarial (tenant) nuevo por prueba, con dos empresas que deciden por separado y el sistema integrado E2E:
 *
 * - **Constructora Norte**: PROVEEDOR con Compras y Cumplimiento en paralelo (Cumplimiento puede rechazar); pide RUT
 *   (lo revisan las dos) y Cámara de comercio (solo Cumplimiento). CLIENTE con solo Compras; pide RUT y certificación
 *   bancaria (un documento que no pide como proveedor).
 * - **Constructora Sur**: PROVEEDOR con un área, Revisión (puede rechazar); pide RUT y el certificado propio {@link SST}.
 *
 * Personas: Ana administra el grupo (no es de ningún área); Rubén (Compras de Norte); Olga, oficial de cumplimiento
 * (Cumplimiento de Norte); Sofía (Revisión de Sur).
 */
export interface Escenario {
  sufijo: string;
  tenantId: string;
  admin: string;
  llave: string;
  norte: Empresa;
  sur: Empresa;
  ana: Persona;
  ruben: Persona;
  olga: Persona;
  sofia: Persona;
  /** Aserción de identidad delegada de esa persona (una por petición). */
  delegado: (persona: PersonaDelSistema) => string;
}

export interface OpcionesEscenario {
  /** Frescura del RUT en este grupo (días desde la carga); sin ella, la del catálogo. */
  diasRut?: number;
}

export async function crearEscenario(opciones: OpcionesEscenario = {}): Promise<Escenario> {
  const sufijo = `${Date.now().toString(36)}${randomUUID().slice(0, 4)}`;
  const creado = await llamar<{ tenant: { id: string }; apiKeyAdministrador: { valor: string } }>('POST', '/api/v1/plataforma/tenants', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { slug: `e2e-${sufijo}`, nombre: `Grupo E2E ${sufijo}`, region: 'co', zonaHoraria: 'America/Bogota' },
  });
  const admin = creado.apiKeyAdministrador.valor;

  const crearEmpresa = async (nombre: string, letra: string): Promise<Empresa> => {
    const prefijo = `${letra}${sufijo.slice(-5).toUpperCase()}`;
    const empresa = await llamar<{ id: string; nombre: string }>('POST', '/api/v1/empresas', {
      llave: admin,
      cuerpo: { identificacion: nitAleatorio(), nombre, prefijoFolio: prefijo, logoUrl: null, replyTo: null },
    });
    for (const [tipo, contenido] of [
      ['autorizacion', `Autorizo a ${nombre} a tratar mis datos personales conforme a la Ley 1581 de 2012. (Texto de prueba.)`],
      ['terminos', `Términos y condiciones para terceros de ${nombre}. (Texto de prueba.)`],
    ] as const) {
      await llamar('PUT', `/api/v1/empresas/${empresa.id}/textos-legales/${tipo}`, { llave: admin, cuerpo: { contenido } });
    }
    return { id: empresa.id, nombre: empresa.nombre, prefijo, areas: {} };
  };
  const norte = await crearEmpresa('Constructora Norte S.A.S.', 'N');
  const sur = await crearEmpresa('Constructora Sur S.A.S.', 'S');

  const claves = clavesDeSistema();
  const sistema = await llamar<{ id: string }>('POST', '/api/v1/sistemas', {
    llave: admin,
    cuerpo: { codigo: CODIGO_SISTEMA, nombre: 'Sistema integrado E2E', clavePublicaPem: claves.publica, origenesPermitidos: [ORIGEN_SISTEMA] },
  });
  const llave = await llamar<{ valor: string }>('POST', '/api/v1/api-keys', {
    llave: admin,
    cuerpo: { nombre: 'E2E embebido', scopes: SCOPES, empresas: null, expiraEn: null, sandbox: false, sistemaId: sistema.id },
  });

  const crearPersona = async (sub: string, nombre: string, roles: { rol: string; empresaId: string | null }[]): Promise<Persona> => {
    const correo = `${sub}.${sufijo}@e2e.validasoft.co`;
    const usuario = await llamar<{ id: string }>('POST', '/api/v1/usuarios', {
      llave: admin,
      cuerpo: { nombre, correo, identificacion: null, roles, identidades: [{ sistemaId: sistema.id, idExterno: sub }] },
    });
    return { id: usuario.id, sub, nombre, correo };
  };
  const ana = await crearPersona('ana', 'Ana Administradora', [{ rol: 'Administrador', empresaId: null }]);
  const ruben = await crearPersona('ruben', 'Rubén Revisor', [{ rol: 'Revisor', empresaId: norte.id }]);
  const olga = await crearPersona('olga', 'Olga Oficial', [{ rol: 'OficialCumplimiento', empresaId: norte.id }]);
  const sofia = await crearPersona('sofia', 'Sofía Revisora', [{ rol: 'Revisor', empresaId: sur.id }]);

  // Cada tenant nace con PROVEEDOR (enlace con OTP); CLIENTE se crea sin OTP.
  for (const [codigo, nombre, exigeOtpEnEnlace] of [
    ['PROVEEDOR', 'Proveedor', true],
    ['CLIENTE', 'Cliente', false],
  ] as const) {
    await llamar('POST', '/api/v1/tipos-vinculo', {
      llave: admin,
      cuerpo: { codigo, nombre, atributosPermitidos: [], exigeCuentaDesignada: false, exigeOtpEnEnlace },
    }).catch((error: unknown) => {
      if (!(error instanceof ErrorDeApi && error.codigo === 'tipo-vinculo-duplicado')) throw error;
    });
  }

  if (opciones.diasRut !== undefined) {
    await llamar('POST', '/api/v1/tipos-documento', {
      llave: admin,
      cuerpo: { codigo: 'RUT', nombre: null, tipoCanonico: 'RUT', reglaFrescura: { tipo: 'MaxDiasDesdeExpedicion', dias: opciones.diasRut }, diasAviso: [], diasGracia: 0 },
    }).catch(async (error: unknown) => {
      if (!(error instanceof ErrorDeApi && error.estado === 409)) throw error;
      const tipos = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/tipos-documento', { llave: admin });
      const rut = tipos.find((t) => t.codigo === 'RUT');
      if (!rut) throw error;
      await llamar('PUT', `/api/v1/tipos-documento/${rut.id}`, {
        llave: admin,
        cuerpo: { reglaFrescura: { tipo: 'MaxDiasDesdeExpedicion', dias: opciones.diasRut }, diasAviso: [], diasGracia: 0 },
      });
    });
  }

  await llamar('POST', '/api/v1/tipos-documento', {
    llave: admin,
    cuerpo: {
      codigo: SST.codigo,
      nombre: SST.nombre,
      tipoCanonico: null,
      ambito: 'PorVinculo',
      reglaFrescura: { tipo: 'MaxDiasDesdeExpedicion', dias: SST.dias },
      diasAviso: [SST.aviso],
      diasGracia: SST.gracia,
      formatosPermitidos: ['pdf'],
      modo: 'ArchivoYFormulario',
      campos: [{ clave: 'fecha_expedicion', etiqueta: 'Fecha de expedición', tipo: 'Fecha', obligatorio: true, opciones: null, ayuda: null }],
    },
  });

  const crearArea = async (empresa: Empresa, codigo: string, nombre: string, miembros: string[]): Promise<Area> => {
    const area = await llamar<{ id: string }>('POST', '/api/v1/areas', { llave: admin, empresa: empresa.id, cuerpo: { codigo, nombre } });
    await llamar('PUT', `/api/v1/areas/${area.id}/miembros`, { llave: admin, empresa: empresa.id, cuerpo: { usuarios: miembros } });
    empresa.areas[codigo] = { id: area.id, codigo, nombre };
    return empresa.areas[codigo];
  };
  const compras = await crearArea(norte, 'compras', 'Compras', [ruben.id]);
  const cumplimiento = await crearArea(norte, 'cumplimiento', 'Cumplimiento', [olga.id]);
  const revision = await crearArea(sur, 'revision', 'Revisión', [sofia.id]);

  const area = (a: Area, puedeRechazarExpediente: boolean) => ({ areaId: a.id, aprobadoresRequeridos: 1, puedeRechazarExpediente, automatica: false, slaHoras: null });
  const requisito = (tipoDocumento: string, areas: Area[]) => ({ tipoDocumento, obligatorio: true, areasQueRevisan: areas.map((a) => a.id), aprobadoresRequeridos: null });
  const publicarFlujo = async (empresa: Empresa, codigo: string, nombre: string, tipoVinculo: string, areas: ReturnType<typeof area>[], requisitos: ReturnType<typeof requisito>[]) => {
    const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
      llave: admin,
      empresa: empresa.id,
      cuerpo: { codigo, nombre, tipoVinculo, etapas: [{ orden: 1, areas }], politicaDevolucion: 'Inmediata', separacionDeFunciones: false, requisitos },
    });
    await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: admin, empresa: empresa.id });
  };
  await publicarFlujo(norte, 'HABILITACION', 'Habilitación de proveedores', 'PROVEEDOR', [area(compras, false), area(cumplimiento, true)], [
    requisito('RUT', [compras, cumplimiento]),
    requisito('CAMARA_COMERCIO', [cumplimiento]),
  ]);
  await publicarFlujo(norte, 'CLIENTES', 'Vinculación de clientes', 'CLIENTE', [area(compras, false)], [
    requisito('RUT', [compras]),
    requisito('CERTIFICACION_BANCARIA', [compras]),
  ]);
  await publicarFlujo(sur, 'PROVEEDORES_SUR', 'Proveedores de Sur', 'PROVEEDOR', [area(revision, true)], [requisito('RUT', [revision]), requisito(SST.codigo, [revision])]);

  return {
    sufijo,
    tenantId: creado.tenant.id,
    admin,
    llave: llave.valor,
    norte,
    sur,
    ana,
    ruben,
    olga,
    sofia,
    delegado: (persona) => asercion(CODIGO_SISTEMA, claves.privada, persona),
  };
}

/** Tercero independiente de los grupos: su bóveda es una sola para todas las empresas. Por defecto, persona jurídica. */
export interface Tercero {
  nombre: string;
  identificacion: { pais: string; tipo: string; numero: string; digitoVerificacion: string | null };
  correo: string;
  tipoPersona?: 'Juridica' | 'Natural';
}

export function nuevoTercero(nombre = 'Aceros del Norte S.A.S.'): Tercero {
  const identificacion = nitAleatorio();
  return { nombre, identificacion, correo: `proveedor.${identificacion.numero}.${randomUUID().slice(0, 6)}@e2e.validasoft.co` };
}

/** Persona natural con cédula de ciudadanía. */
export function nuevaPersonaNatural(nombre = 'Ana María Pérez'): Tercero {
  const numero = `10${String(Math.floor(Math.random() * 1e8)).padStart(8, '0')}`;
  return {
    nombre,
    identificacion: { pais: 'CO', tipo: 'CC', numero, digitoVerificacion: null },
    correo: `persona.${numero}.${randomUUID().slice(0, 6)}@e2e.validasoft.co`,
    tipoPersona: 'Natural',
  };
}

export interface Invitacion {
  vinculoId: string;
  terceroId: string;
  correo: string;
  nombre: string;
  empresa: Empresa;
  tipo: string;
  referencia: string;
}

/** El sistema integrado crea el vínculo (empresa, tercero, tipo) y Validasoft envía la invitación por correo. */
export async function invitar(e: Escenario, empresa: Empresa, tercero: Tercero, tipo: 'PROVEEDOR' | 'CLIENTE' = 'PROVEEDOR'): Promise<Invitacion> {
  const referencia = `${tipo.slice(0, 3)}-${tercero.identificacion.numero}-${empresa.prefijo}`;
  const vinculo = await llamar<{ id: string; terceroId: string }>('POST', '/api/v1/vinculos', {
    llave: e.llave,
    empresa: empresa.id,
    cuerpo: {
      tercero: {
        identificacion: tercero.identificacion,
        tipoPersona: tercero.tipoPersona ?? 'Juridica',
        nombre: tercero.nombre,
        nombreComercial: null,
        contactos: [{ nombre: 'Contacto comercial', correo: tercero.correo, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: tipo,
      atributos: {},
      correoInvitacion: tercero.correo,
      referenciaExterna: { sistema: CODIGO_SISTEMA, id: referencia },
      enviarInvitacion: true,
    },
  });
  return { vinculoId: vinculo.id, terceroId: vinculo.terceroId, correo: tercero.correo, nombre: tercero.nombre, empresa, tipo, referencia };
}

/** Id del expediente abierto del vínculo (para entrar directo cuando la persona no lo ve en su bandeja). */
export async function expedienteAbierto(e: Escenario, invitacion: Invitacion): Promise<string> {
  const vinculo = await llamar<{ expedienteAbiertoId: string | null }>('GET', `/api/v1/vinculos/${invitacion.vinculoId}`, {
    llave: e.admin,
    empresa: invitacion.empresa.id,
  });
  if (!vinculo.expedienteAbiertoId) throw new Error(`El vínculo ${invitacion.vinculoId} no tiene expediente abierto.`);
  return vinculo.expedienteAbiertoId;
}

/**
 * Plan de IA de la empresa (ADR-0012, PT-6) con una llave de prueba: el análisis de los documentos lo hace el proveedor
 * simulado (lee marcas `[[clave=valor]]` del archivo), así que nada sale a OpenAI. Sin plan, la empresa no recibe el análisis
 * y el cruce con la ficha es manual.
 */
export async function activarIa(e: Escenario, empresa: Empresa, incluidasMes = 20): Promise<void> {
  await llamar('PUT', `/api/v1/plataforma/tenants/${e.tenantId}/empresas/${empresa.id}/plan-ia`, {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { validacionesIncluidasMes: incluidasMes, adicionalHabilitado: false, apiKeyOpenAi: `sk-prueba-e2e-${randomUUID().replace(/-/g, '')}` },
  });
}

/**
 * Flujo de CLIENTE de la empresa con un documento y secciones de la ficha como requisitos (F2), todos para el área indicada. Como la
 * empresa aún no tiene flujo de ese tipo, queda como el flujo por defecto al publicarlo.
 */
export async function publicarFlujoConFicha(
  e: Escenario,
  empresa: Empresa,
  codigo: string,
  area: Area,
  requisitos: { tipoDocumento?: string; seccion?: string; usarComoSoporte?: boolean; vigenciaDias?: number; diasRevalidacion?: number }[],
  /** Vigencia periódica del vínculo del flujo (VV-1): días desde la última aprobación, avisos y gracia. */
  vigenciaVinculo?: { dias: number; diasAviso?: number[]; diasGracia?: number },
): Promise<string> {
  const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
    llave: e.admin,
    empresa: empresa.id,
    cuerpo: {
      codigo,
      nombre: `Flujo ${codigo}`,
      tipoVinculo: 'CLIENTE',
      etapas: [{ orden: 1, areas: [{ areaId: area.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: true, automatica: false, slaHoras: null }] }],
      politicaDevolucion: 'Inmediata',
      separacionDeFunciones: false,
      requisitos: requisitos.map((r) => ({ ...r, obligatorio: true, areasQueRevisan: [area.id], aprobadoresRequeridos: null })),
      vigenciaVinculo,
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: empresa.id });
  return flujo.id;
}

export interface ResultadoDeVigencias {
  revisados: number;
  avisados: number;
  vencidos: number;
  desbloqueados: number;
  documentosVencidos: number;
}

/**
 * Corre ya el job de vigencias de vínculos del grupo (Plataforma) como si fuera `fecha` (solo Development): avisa, vence (con su
 * renovación) o levanta los bloqueos con fecha que ya tocaban. Con `incluirDocumentos` también vence antes las versiones de
 * documentos cuya gracia terminó a esa fecha. Cada llamada avanza a lo sumo una transición por vínculo.
 */
export async function revisarVigencias(e: Escenario, fecha: Date, { incluirDocumentos = false }: { incluirDocumentos?: boolean } = {}): Promise<ResultadoDeVigencias> {
  return llamar<ResultadoDeVigencias>('POST', '/api/v1/plataforma/vigencias-de-vinculos/revisar', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { tenantId: e.tenantId, fecha: fecha.toISOString(), incluirDocumentos },
  });
}
