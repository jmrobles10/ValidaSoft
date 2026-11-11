import { llamar } from './api';
import { CODIGO_SISTEMA, type Area, type Empresa, type Escenario, type Invitacion, type Persona, type Tercero } from './semilla';

/**
 * Flujos y expedientes armados por API para las pruebas del ciclo de vida (desaprobar, reintegrar, secuencia de etapas,
 * aprobadores y separación de funciones): sin documentos, así que cada área decide sobre el expediente completo (FL-1) y la
 * prueba se concentra en lo que cambia la revisión, no en cargar archivos.
 */

export interface AreaDeEtapa {
  area: Area;
  /** Aprobadores distintos que pide el área (1 por omisión). */
  aprobadores?: number;
  puedeRechazar?: boolean;
}

export interface OpcionesDeFlujo {
  separacionDeFunciones?: boolean;
  /** Reintento del tercero tras un rechazo (DS-4); por omisión no se permite. */
  reintento?: { permitido: boolean; diasEspera: number };
}

/** Tipo de vínculo propio del grupo, sin OTP en el enlace (el tercero entra directo con el enlace del correo). */
export async function crearTipoDeVinculo(e: Escenario, codigo: string, nombre: string): Promise<void> {
  await llamar('POST', '/api/v1/tipos-vinculo', {
    llave: e.admin,
    cuerpo: { codigo, nombre, atributosPermitidos: [], exigeCuentaDesignada: false, exigeOtpEnEnlace: false },
  });
}

/** Área nueva de la empresa con sus miembros (los usuarios deben tener rol en ella). */
export async function crearArea(e: Escenario, empresa: Empresa, codigo: string, nombre: string, miembros: Persona[]): Promise<Area> {
  const area = await llamar<{ id: string }>('POST', '/api/v1/areas', { llave: e.admin, empresa: empresa.id, cuerpo: { codigo, nombre } });
  await llamar('PUT', `/api/v1/areas/${area.id}/miembros`, { llave: e.admin, empresa: empresa.id, cuerpo: { usuarios: miembros.map((m) => m.id) } });
  empresa.areas[codigo] = { id: area.id, codigo, nombre };
  return empresa.areas[codigo];
}

/**
 * Publica un flujo sin documentos para `tipoVinculo` (queda por defecto si es el primero de ese tipo): una etapa por elemento
 * de `etapas`, en secuencia; las áreas de una misma etapa revisan en paralelo.
 */
export async function publicarFlujoSinDocumentos(
  e: Escenario,
  empresa: Empresa,
  codigo: string,
  tipoVinculo: string,
  etapas: AreaDeEtapa[][],
  { separacionDeFunciones = false, reintento }: OpcionesDeFlujo = {},
): Promise<string> {
  const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
    llave: e.admin,
    empresa: empresa.id,
    cuerpo: {
      codigo,
      nombre: `Flujo ${codigo}`,
      tipoVinculo,
      etapas: etapas.map((areas, i) => ({
        orden: i + 1,
        areas: areas.map((a) => ({
          areaId: a.area.id,
          aprobadoresRequeridos: a.aprobadores ?? 1,
          puedeRechazarExpediente: a.puedeRechazar ?? false,
          automatica: false,
          slaHoras: null,
        })),
      })),
      politicaDevolucion: 'Inmediata',
      separacionDeFunciones,
      requisitos: [],
      reintento: reintento ?? null,
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: empresa.id });
  return flujo.id;
}

/**
 * El sistema integrado crea el vínculo y envía la invitación. Con `delegado` lo hace esa persona (queda como quien invitó:
 * cuenta para la separación de funciones).
 */
export async function invitarVinculo(e: Escenario, empresa: Empresa, tercero: Tercero, tipo: string, delegado?: Persona): Promise<Invitacion> {
  const referencia = `${tipo.slice(0, 3)}-${tercero.identificacion.numero}-${empresa.prefijo}`;
  const vinculo = await llamar<{ id: string; terceroId: string }>('POST', '/api/v1/vinculos', {
    llave: e.llave,
    empresa: empresa.id,
    delegado: delegado ? e.delegado(delegado) : undefined,
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

/** Un administrador abre el expediente del vínculo sin esperar a que el tercero acepte (FL-6); devuelve su id. */
export async function abrirExpedienteManual(e: Escenario, invitacion: Invitacion): Promise<string> {
  const expediente = await llamar<{ id: string }>('POST', `/api/v1/vinculos/${invitacion.vinculoId}/expedientes`, {
    llave: e.admin,
    empresa: invitacion.empresa.id,
    cuerpo: {},
  });
  return expediente.id;
}

export interface DatosDeExpediente {
  estado: string;
  etapaActual: number | null;
  etapas: { orden: number; estado: string; areas: { nombre: string; estado: string; aprobaciones: number; aprobadoresRequeridos: number }[] }[];
  desaprobaciones?: number;
}

export async function leerExpediente(e: Escenario, empresa: Empresa, expedienteId: string): Promise<DatosDeExpediente> {
  return llamar<DatosDeExpediente>('GET', `/api/v1/expedientes/${expedienteId}`, { llave: e.admin, empresa: empresa.id });
}

export interface DatosDeVinculo {
  estado: string;
  expedienteAbiertoId: string | null;
}

export async function leerVinculo(e: Escenario, invitacion: Invitacion): Promise<DatosDeVinculo> {
  return llamar<DatosDeVinculo>('GET', `/api/v1/vinculos/${invitacion.vinculoId}`, { llave: e.admin, empresa: invitacion.empresa.id });
}

export interface DatosDeCertificado {
  actual: { emision: number; estado: string; bloqueante: boolean; expedienteId: string } | null;
  emisiones: { emision: number; estado: string }[];
}

export async function leerCertificado(e: Escenario, invitacion: Invitacion): Promise<DatosDeCertificado> {
  return llamar<DatosDeCertificado>('GET', `/api/v1/vinculos/${invitacion.vinculoId}/certificado`, { llave: e.admin, empresa: invitacion.empresa.id });
}

/** El área aprueba el expediente (flujo sin documentos), en nombre de `persona`, por la API como lo haría el sistema integrado. */
export async function aprobarPorApi(e: Escenario, invitacion: Invitacion, expedienteId: string, area: Area, persona: Persona): Promise<void> {
  await llamar('POST', `/api/v1/expedientes/${expedienteId}/areas/${area.id}/veredicto`, {
    llave: e.llave,
    empresa: invitacion.empresa.id,
    delegado: e.delegado(persona),
    cuerpo: { veredicto: 'Aprobado' },
  });
}
