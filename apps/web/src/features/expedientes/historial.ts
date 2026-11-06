import type { EventoHistorialDto, ExpedienteDto } from '@validasoft/api-client';
import type { EventoTimeline } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { esCO } from '../../i18n/es-CO';
import { actorDe, campo, texto, tipoNormalizado, type ContextoHistorial } from '../vinculos/historial';

type TipoEvento = keyof typeof esCO.expedientes.historial.eventos;

function esTipoEvento(tipo: string): tipo is TipoEvento {
  return tipo in esCO.expedientes.historial.eventos;
}

/** Nombres de las áreas del expediente (copiados del flujo al abrirlo). */
export function nombresDeAreas(expediente: Pick<ExpedienteDto, 'etapas'>): ReadonlyMap<string, string> {
  return new Map(expediente.etapas.flatMap((e) => e.areas.map((a) => [a.areaId, a.nombre] as const)));
}

function numero(valor: unknown): string {
  return typeof valor === 'number' ? String(valor) : '';
}

function lista(valor: unknown): unknown[] {
  return Array.isArray(valor) ? (valor as unknown[]) : [];
}

/** Motivos aplicados (`{ codigo, textoAlTercero }`) → sus textos al tercero. */
function textosDeMotivos(valor: unknown): string[] {
  return lista(valor)
    .map((m) => (m && typeof m === 'object' ? texto(campo(m as Record<string, unknown>, 'textoAlTercero')) : null))
    .filter((m): m is string => m !== null);
}

/** Convierte el stream del expediente en la entrada de `LineaDeTiempo`. */
export function eventoExpedienteATimeline(
  evento: EventoHistorialDto,
  contexto: ContextoHistorial & { areas: ReadonlyMap<string, string> },
  t: TFunction,
): EventoTimeline {
  const tipo = tipoNormalizado(evento.tipo);
  const datos = evento.datos as Record<string, unknown>;
  const area = (id: unknown) => (typeof id === 'string' ? (contexto.areas.get(id) ?? id.slice(0, 8)) : '');
  const areas = (ids: unknown) =>
    lista(ids)
      .map(area)
      .filter(Boolean)
      .join(', ');
  const base: EventoTimeline = {
    id: `${String(evento.version)}-${tipo}`,
    titulo: esTipoEvento(tipo) ? t(`expedientes.historial.eventos.${tipo}`) : tipo.replace(/_/g, ' '),
    fecha: evento.fecha,
    actor: actorDe(evento, contexto, t),
    sistema: evento.sistema,
  };

  switch (tipo) {
    case 'expediente_abierto':
      return { ...base, despues: 'Abierto', detalle: texto(campo(datos, 'folio')) };
    case 'flujo_seleccionado':
      return {
        ...base,
        detalle: t('expedientes.historial.flujo', { nombre: texto(campo(datos, 'nombre')) ?? '', version: numero(campo(datos, 'version')) }),
      };
    case 'expediente_enviado_a_revision':
      return { ...base, antes: 'Abierto', despues: 'EnRevision' };
    case 'etapa_activada':
      return { ...base, detalle: `${t('expedientes.historial.etapa', { orden: numero(campo(datos, 'orden')) })} · ${areas(campo(datos, 'areas'))}` };
    case 'etapa_aprobada':
      return { ...base, detalle: t('expedientes.historial.etapa', { orden: numero(campo(datos, 'orden')) }) };
    case 'veredicto_de_area_emitido': {
      const observado = campo(datos, 'veredicto') === 'Observado';
      const motivos = textosDeMotivos(campo(datos, 'motivos'));
      const nombreArea = area(campo(datos, 'areaId'));
      return {
        ...base,
        titulo: observado ? t('expedientes.historial.observo', { area: nombreArea }) : t('expedientes.historial.aprobo', { area: nombreArea }),
        detalle: motivos.length > 0 ? t('expedientes.historial.motivos', { motivos: motivos.join(' · ') }) : null,
        motivo: texto(campo(datos, 'observacion')) ?? texto(campo(datos, 'justificacion')),
      };
    }
    case 'expediente_observado':
      return { ...base, antes: 'EnRevision', despues: 'Observado', detalle: areas(campo(datos, 'areasQueObservaron')) };
    case 'expediente_reenviado':
      return {
        ...base,
        antes: 'Observado',
        despues: 'EnRevision',
        detalle: t('expedientes.historial.reabiertas', { areas: areas(campo(datos, 'areasReabiertas')) }),
        motivo: texto(campo(datos, 'respuesta')),
      };
    case 'revision_reanudada':
      return { ...base, antes: 'Observado', despues: 'EnRevision' };
    case 'expediente_aprobado':
      return { ...base, antes: 'EnRevision', despues: 'Aprobado' };
    case 'expediente_rechazado': {
      const motivos = textosDeMotivos(campo(datos, 'motivos'));
      return {
        ...base,
        despues: 'Rechazado',
        detalle: [area(campo(datos, 'areaId')), motivos.length > 0 ? t('expedientes.historial.motivos', { motivos: motivos.join(' · ') }) : null]
          .filter(Boolean)
          .join(' · '),
        motivo: texto(campo(datos, 'justificacion')),
      };
    }
    case 'expediente_anulado':
      return { ...base, despues: 'Anulado', motivo: texto(campo(datos, 'motivo')) };
    case 'veredicto_anulado_por_administrador':
      return { ...base, detalle: area(campo(datos, 'areaId')), motivo: texto(campo(datos, 'justificacion')) };
    default:
      return base;
  }
}
