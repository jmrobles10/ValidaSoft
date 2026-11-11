import type { EventoHistorialDto, ExpedienteDto } from '@validasoft/api-client';
import type { EventoTimeline } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { esCO } from '../../i18n/es-CO';
import { fraseDeRevisionAutomatica } from '../revisionAutomatica/historial';
import { actorDe, campo, texto, tipoNormalizado, type ContextoHistorial } from '../vinculos/historial';

type TipoEvento = keyof typeof esCO.expedientes.historial.eventos;

function esTipoEvento(tipo: string): tipo is TipoEvento {
  return tipo in esCO.expedientes.historial.eventos;
}

/** Nombres de las áreas del expediente (copiados del flujo al abrirlo). */
export function nombresDeAreas(expediente: Pick<ExpedienteDto, 'etapas'>): ReadonlyMap<string, string> {
  return new Map(expediente.etapas.flatMap((e) => e.areas.map((a) => [a.areaId, a.nombre] as const)));
}

/** Nombres de los documentos que pide el expediente, por id de requisito. */
export function nombresDeRequisitos(expediente: Pick<ExpedienteDto, 'requisitos'>): ReadonlyMap<string, string> {
  return new Map((expediente.requisitos ?? []).map((r) => [r.id, r.tipoDocumento.nombre] as const));
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

/**
 * «El tercero retiró la versión 8 de RUT (motivo: …); vuelve a la versión 7.» (RT-3). `datos` trae el `retiro` y la `version` que vuelve:
 * el requisito calculado de una actualización que se abrió por el retiro, o el evento que cambió la versión de un expediente abierto.
 */
function textoDeRetiro(datos: Record<string, unknown>, documento: string | null, t: TFunction): string | null {
  const retiro = campo(datos, 'retiro');
  if (!retiro || typeof retiro !== 'object') return null;
  const version = campo(datos, 'version');
  return t('expedientes.historial.retiro', {
    documento: documento ?? t('expedientes.historial.documento'),
    retirada: numero(campo(retiro as Record<string, unknown>, 'numero')),
    motivo: texto(campo(retiro as Record<string, unknown>, 'motivo')) ?? '',
    n: version && typeof version === 'object' ? numero(campo(version as Record<string, unknown>, 'numero')) : '',
  });
}


function tipoDeVerificacion(tipo: string, t: TFunction): string {
  return tipo === 'EstudioDeCumplimiento' ? t('cumplimiento.tipos.EstudioDeCumplimiento') : t('cumplimiento.tipos.ListasRestrictivas');
}

/** Convierte el stream del expediente en la entrada de `LineaDeTiempo`. */
export function eventoExpedienteATimeline(
  evento: EventoHistorialDto,
  contexto: ContextoHistorial & { areas: ReadonlyMap<string, string>; requisitos?: ReadonlyMap<string, string> },
  t: TFunction,
): EventoTimeline {
  const tipo = tipoNormalizado(evento.tipo);
  const datos = evento.datos as Record<string, unknown>;
  const area = (id: unknown) => (typeof id === 'string' ? (contexto.areas.get(id) ?? id.slice(0, 8)) : '');
  const requisito = (id: unknown) => (typeof id === 'string' ? (contexto.requisitos?.get(id) ?? t('expedientes.historial.documento')) : '');
  const areas = (ids: unknown) =>
    lista(ids)
      .map(area)
      .filter(Boolean)
      .join(', ');
  // Un veredicto con origen Sistema (área automática, aprobación previa de la empresa o área sin nada que revisar) lo emite
  // la cascada dentro de la petición de otro (el tercero que subió, la persona que aprobó): el actor es Sistema.
  const porSistema = (tipo === 'veredicto_de_area_emitido' || tipo === 'veredicto_de_requisito_emitido') && campo(datos, 'origen') === 'Sistema';
  const base: EventoTimeline = {
    id: `${String(evento.version)}-${tipo}`,
    titulo: esTipoEvento(tipo) ? t(`expedientes.historial.eventos.${tipo}`) : tipo.replace(/_/g, ' '),
    fecha: evento.fecha,
    actor: porSistema ? { nombre: t('expedientes.veredictos.sistema'), tipo: 'sistema' } : actorDe(evento, contexto, t),
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
        // Con requisitos vuelven a revisión los documentos observados (RQ-7), no áreas completas.
        detalle:
          lista(campo(datos, 'requisitosReabiertos')).length > 0
            ? t('expedientes.historial.reabiertosDocumentos', { documentos: lista(campo(datos, 'requisitosReabiertos')).map(requisito).join(', ') })
            : t('expedientes.historial.reabiertas', { areas: areas(campo(datos, 'areasReabiertas')) }),
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
    case 'requisitos_calculados': {
      // Una actualización que se abrió porque el tercero retiró una versión lo dice con su motivo (RT-3); si no, los documentos que pide el flujo.
      const requisitosDelEvento = lista(campo(datos, 'requisitos')).filter((r): r is Record<string, unknown> => Boolean(r) && typeof r === 'object');
      const retiros = requisitosDelEvento.map((r) => textoDeRetiro(r, texto(campo(r, 'nombre')), t)).filter((r): r is string => r !== null);
      if (retiros.length > 0) return { ...base, titulo: retiros.join(' · ') };
      const nombres = requisitosDelEvento.map((r) => texto(campo(r, 'nombre'))).filter((n): n is string => n !== null);
      return { ...base, detalle: nombres.join(' · ') };
    }
    case 'requisitos_agregados': {
      // Una actualización abierta suma lo que cambió después de abrirse; las áreas que lo revisan vuelven a tenerlo pendiente.
      const nombres = lista(campo(datos, 'requisitos'))
        .map((r) => (r && typeof r === 'object' ? texto(campo(r as Record<string, unknown>, 'nombre')) : null))
        .filter((n): n is string => n !== null);
      return { ...base, titulo: t('expedientes.historial.agregados', { documentos: nombres.join(', ') }) };
    }
    case 'version_adjuntada_a_requisito': {
      const version = campo(datos, 'version');
      const n = version && typeof version === 'object' ? numero(campo(version as Record<string, unknown>, 'numero')) : '';
      const documento = requisito(campo(datos, 'requisitoId'));
      // Si la versión que reemplaza la retiró el tercero, la línea de tiempo lo dice con su motivo (RT-3).
      return { ...base, titulo: textoDeRetiro(datos, documento, t) ?? t('expedientes.historial.versionAdjuntada', { documento, n }) };
    }
    case 'veredictos_invalidados_por_nueva_version':
      return {
        ...base,
        titulo: t('expedientes.historial.invalidados', { documento: requisito(campo(datos, 'requisitoId')), n: numero(campo(datos, 'versionAnterior')) }),
        detalle: areas(campo(datos, 'areasAfectadas')),
      };
    case 'veredicto_de_requisito_emitido': {
      const observado = campo(datos, 'veredicto') === 'Observado';
      const motivos = textosDeMotivos(campo(datos, 'motivos'));
      const parametros = { area: area(campo(datos, 'areaId')), documento: requisito(campo(datos, 'requisitoId')), n: numero(campo(datos, 'sobreVersion')) };
      return {
        ...base,
        titulo: observado ? t('expedientes.historial.observoDocumento', parametros) : t('expedientes.historial.aproboDocumento', parametros),
        detalle: motivos.length > 0 ? t('expedientes.historial.motivos', { motivos: motivos.join(' · ') }) : null,
        motivo: texto(campo(datos, 'observacion')) ?? texto(campo(datos, 'justificacion')),
      };
    }
    case 'requisito_ratificado':
      return { ...base, titulo: t('expedientes.historial.ratificado', { documento: requisito(campo(datos, 'requisitoId')) }) };
    case 'requisito_eximido':
      return { ...base, titulo: t('expedientes.historial.eximido', { documento: requisito(campo(datos, 'requisitoId')) }), motivo: texto(campo(datos, 'justificacion')) };
    case 'veredicto_anulado_por_administrador':
      return { ...base, detalle: area(campo(datos, 'areaId')), motivo: texto(campo(datos, 'justificacion')) };
    // Desaprobar (DS-1, DS-2): desde qué etapa se revisa de nuevo y por qué.
    case 'etapa_reabierta':
    case 'expediente_desaprobado': {
      const faseInicial = campo(datos, 'faseInicial') === true;
      return {
        ...base,
        titulo: faseInicial ? t('expedientes.historial.faseInicial') : t('expedientes.historial.reabierta', { orden: numero(campo(datos, 'orden')) }),
        antes: tipo === 'expediente_desaprobado' ? 'Aprobado' : undefined,
        despues: tipo === 'expediente_desaprobado' ? 'EnRevision' : undefined,
        motivo: texto(campo(datos, 'justificacion')),
      };
    }
    // Verificaciones externas (H7): sin nombres de personas en el título; el detalle está en el panel de verificaciones.
    case 'verificaciones_requeridas':
      return {
        ...base,
        detalle: lista(campo(datos, 'verificaciones'))
          .map((v) => (v && typeof v === 'object' ? texto(campo(v as Record<string, unknown>, 'tipo')) : null))
          .filter((v): v is string => v !== null)
          .map((v) => tipoDeVerificacion(v, t))
          .join(' · '),
      };
    case 'verificacion_externa_registrada':
      return {
        ...base,
        detalle: t('expedientes.historial.consultaListas', {
          proveedor: texto(campo(datos, 'proveedor')) ?? '',
          personas: String(lista(campo(datos, 'personas')).length),
          count: lista(campo(datos, 'coincidencias')).length,
        }),
      };
    case 'coincidencia_resuelta':
      return {
        ...base,
        detalle: t(`cumplimiento.decisiones.${campo(datos, 'decision') === 'Confirmada' ? 'Confirmada' : 'Descartada'}`),
        motivo: texto(campo(datos, 'justificacion')),
      };
    case 'estudio_de_cumplimiento_registrado': {
      const estudio = campo(datos, 'estudio');
      const delEstudio = (clave: string) => (estudio && typeof estudio === 'object' ? campo(estudio as Record<string, unknown>, clave) : null);
      const concepto = delEstudio('concepto') === 'Desfavorable' ? 'Desfavorable' : 'Favorable';
      return {
        ...base,
        detalle: t('expedientes.historial.estudio', { riesgo: numero(delEstudio('nivelRiesgo')), concepto: t(`cumplimiento.conceptos.${concepto}`) }),
        motivo: texto(delEstudio('observaciones')),
      };
    }
    case 'verificacion_expirada':
      return { ...base, detalle: tipoDeVerificacion(texto(campo(datos, 'tipo')) ?? '', t) };
    case 'consulta_de_verificacion_fallida':
      return { ...base, detalle: texto(campo(datos, 'proveedor')), motivo: texto(campo(datos, 'error')) };
    // Revisión automática con IA (ADR-0012): lo que decidió el sistema y lo que una persona resolvió del muestreo.
    case 'revision_automatica_registrada':
    case 'revision_automatica_resuelta':
      return { ...base, ...fraseDeRevisionAutomatica(tipo, datos, { area, requisito }, t) };
    default:
      return base;
  }
}
