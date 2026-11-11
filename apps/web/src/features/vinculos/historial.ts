import type { EstadoVinculo, EventoHistorialDto } from '@validasoft/api-client';
import { formatearFecha, type EventoTimeline, type TipoActor } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { esCO } from '../../i18n/es-CO';
import { formatearFechaDeclarada } from '../documentos/formatoDatos';

/** Orden del enum `EstadoVinculo` del dominio: los eventos lo guardan como número. */
const ORDEN_ESTADOS: readonly EstadoVinculo[] = ['Invitado', 'EnValidacion', 'Activo', 'PorVencer', 'Vencido', 'Bloqueado', 'Desvinculado', 'Anulado'];

type TipoConocido = Exclude<keyof typeof esCO.historial, 'referencia' | 'invitacion' | 'hasta' | 'actor' | 'vigencia'>;

/**
 * Los datos del evento llegan como los serializa Marten (PascalCase y enums numéricos), distinto del resto de la
 * API (camelCase y enums textuales); se leen sin importar mayúsculas para no depender de eso.
 */
export function campo(datos: Record<string, unknown>, nombre: string): unknown {
  const buscado = nombre.toLowerCase();
  for (const [clave, valor] of Object.entries(datos)) if (clave.toLowerCase() === buscado) return valor;
  return undefined;
}

export function estadoDe(valor: unknown): EstadoVinculo | null {
  if (typeof valor === 'number') return ORDEN_ESTADOS[valor] ?? null;
  if (typeof valor === 'string') return ORDEN_ESTADOS.find((e) => e.toLowerCase() === valor.toLowerCase()) ?? null;
  return null;
}

export function texto(valor: unknown): string | null {
  return typeof valor === 'string' && valor.trim() ? valor : null;
}

/** Tipo de Marten (`vinculo_bloqueado`, o `VinculoBloqueado` según la versión) → clave de texto. */
export function tipoNormalizado(tipo: string): string {
  return tipo
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .toLowerCase()
    .replace(/[^a-z0-9_]/g, '');
}

function esTipoConocido(tipo: string): tipo is TipoConocido {
  return tipo in esCO.historial && !['referencia', 'invitacion', 'hasta', 'actor', 'vigencia'].includes(tipo);
}

export interface ContextoHistorial {
  /** Usuario de la sesión: sus eventos dicen "Tú". */
  usuarioActual: string | null;
  /** Nombres conocidos de usuarios (si la persona puede listarlos). */
  nombres?: ReadonlyMap<string, string>;
}

export function actorDe(evento: EventoHistorialDto, contexto: ContextoHistorial, t: TFunction): { nombre: string; tipo: TipoActor } {
  const id = evento.actorId ?? '';
  switch (evento.actorTipo) {
    case 'Usuario':
      return {
        nombre: id === contexto.usuarioActual ? t('historial.actor.tu') : (contexto.nombres?.get(id) ?? t('historial.actor.Usuario')),
        tipo: 'persona',
      };
    case 'Integracion':
      return { nombre: t('historial.actor.Integracion'), tipo: 'integracion' };
    case 'Sistema':
      return { nombre: t('historial.actor.Sistema'), tipo: 'sistema' };
    case 'Plataforma':
      return { nombre: t('historial.actor.Plataforma'), tipo: 'integracion' };
    case 'Tercero':
      return { nombre: t('historial.actor.Tercero'), tipo: 'tercero' };
    default:
      return { nombre: t('historial.actor.desconocido'), tipo: 'integracion' };
  }
}

/** Convierte el historial del vínculo (stream de eventos) en la entrada de `LineaDeTiempo`. */
export function eventoATimeline(evento: EventoHistorialDto, contexto: ContextoHistorial, t: TFunction): EventoTimeline {
  const tipo = tipoNormalizado(evento.tipo);
  const datos = evento.datos as Record<string, unknown>;
  const base: EventoTimeline = {
    id: `${String(evento.version)}-${tipo}`,
    titulo: esTipoConocido(tipo) ? t(`historial.${tipo}`) : tipo.replace(/_/g, ' '),
    fecha: evento.fecha,
    actor: actorDe(evento, contexto, t),
    sistema: evento.sistema,
  };

  const motivo = texto(campo(datos, 'motivo'));
  const justificacion = texto(campo(datos, 'justificacion'));

  switch (tipo) {
    case 'vinculo_creado': {
      const referencia = campo(datos, 'referenciaExterna');
      if (referencia && typeof referencia === 'object') {
        const r = referencia as Record<string, unknown>;
        return { ...base, detalle: t('historial.referencia', { sistema: texto(campo(r, 'sistema')) ?? '', id: texto(campo(r, 'id')) ?? '' }) };
      }
      return base;
    }
    case 'referencia_externa_agregada':
      return { ...base, detalle: t('historial.referencia', { sistema: texto(campo(datos, 'sistema')) ?? '', id: texto(campo(datos, 'id')) ?? '' }) };
    case 'invitacion_enviada': {
      const expira = texto(campo(datos, 'expira'));
      return {
        ...base,
        detalle: t('historial.invitacion', { correo: texto(campo(datos, 'correoDestino')) ?? '', expira: expira ? formatearFecha(expira) : '—' }),
      };
    }
    case 'vinculo_bloqueado': {
      const hasta = texto(campo(datos, 'hasta'));
      return {
        ...base,
        antes: estadoDe(campo(datos, 'estadoPrevio')),
        despues: 'Bloqueado',
        motivo,
        detalle: hasta ? t('historial.hasta', { fecha: formatearFecha(hasta) }) : null,
      };
    }
    case 'vinculo_desbloqueado':
      return { ...base, antes: 'Bloqueado', despues: estadoDe(campo(datos, 'estadoRestituido')), motivo: justificacion };
    case 'vinculo_desvinculado':
      return { ...base, antes: estadoDe(campo(datos, 'estadoPrevio')), despues: 'Desvinculado', motivo };
    case 'vinculo_reintegrado':
      return { ...base, antes: 'Desvinculado', despues: estadoDe(campo(datos, 'estadoResultante')), motivo: justificacion };
    case 'vinculo_anulado':
      return { ...base, antes: estadoDe(campo(datos, 'estadoPrevio')), despues: 'Anulado', motivo };
    case 'expediente_vinculado':
      return { ...base, antes: 'Invitado', despues: 'EnValidacion', detalle: texto(campo(datos, 'folio')) };
    case 'vinculo_activado':
      return { ...base, antes: 'EnValidacion', despues: 'Activo' };
    // Vigencia periódica del vínculo (VV-1): la fecha que fijó la aprobación, el aviso previo y el vencimiento.
    case 'vigencia_del_vinculo_fijada': {
      const hasta = texto(campo(datos, 'vigenteHasta'));
      return { ...base, detalle: hasta ? t('historial.vigencia.vigenteHasta', { fecha: formatearFechaDeclarada(hasta) }) : t('historial.vigencia.sinVigencia') };
    }
    case 'vinculo_por_vencer': {
      const hasta = texto(campo(datos, 'vigenteHasta'));
      return { ...base, antes: 'Activo', despues: 'PorVencer', detalle: hasta ? t('historial.vigencia.venceEl', { fecha: formatearFechaDeclarada(hasta) }) : null };
    }
    case 'vinculo_vencido':
      return { ...base, despues: 'Vencido', motivo: texto(campo(datos, 'documento')) };
    case 'expediente_cerrado': {
      const resultado = texto(campo(datos, 'resultado'));
      return resultado === 'Aprobado' || resultado === 'Rechazado' || resultado === 'Anulado' || resultado === 'Caducado' ? { ...base, despues: resultado } : base;
    }
    default:
      return { ...base, motivo: motivo ?? justificacion };
  }
}
