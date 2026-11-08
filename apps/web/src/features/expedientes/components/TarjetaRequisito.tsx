import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import { SCOPES, type AreaDeRequisitoDto, type AreaExpedienteDto, type ExpedienteDto, type RequisitoExpedienteDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { Clock, FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { hashCorto } from '../../documentos/api';
import { ResumenVersion } from '../../documentos/ResumenVersion';
import type { DocumentoEnVisor } from '../../documentos/VisorDocumento';
import type { ContextoRevisor, DialogoExpediente } from '../reglas';
import { bloqueoRequisito, documentoEnVisor, puedeEximir, veredictosInvalidados, veredictosVigentesDeRequisito, type BloqueoRequisito } from '../requisitos';
import { BotonVerVersion } from './BotonVerVersion';

/** Bloqueos que se explican a quien revisa (los demás no dicen nada útil: sin permiso, cerrado). */
const BLOQUEOS_EXPLICADOS = ['expediente-en-carga', 'expediente-observado', 'vinculo-bloqueado', 'sin-version', 'area-esperando', 'no-es-miembro', 'ya-voto'] as const;
type BloqueoExplicado = (typeof BLOQUEOS_EXPLICADOS)[number];

function seExplica(bloqueo: BloqueoRequisito | null): bloqueo is BloqueoExplicado {
  return (BLOQUEOS_EXPLICADOS as readonly string[]).includes(bloqueo ?? '');
}

export interface TarjetaRequisitoProps {
  expediente: ExpedienteDto;
  requisito: RequisitoExpedienteDto;
  areas: ReadonlyMap<string, AreaExpedienteDto>;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  nombres: ReadonlyMap<string, string>;
  puedeVer: boolean;
  onVer: (documento: DocumentoEnVisor) => void;
  onAccion: (dialogo: DialogoExpediente) => void;
  /** Muestra «Aprobar u observar» por documento (acción secundaria; la principal es la decisión del área). */
  decidirPorDocumento?: boolean;
}

/**
 * Un documento que pide el flujo: estado, versión vigente (con «Ver» en el visor si trae archivo), su vigencia, los
 * datos que declaró el tercero y, por cada área que lo revisa, su
 * estado, sus veredictos sobre la versión y «Aprobar u observar» cuando le toca a quien mira (RQ-4).
 */
export function TarjetaRequisito({ expediente, requisito, areas, contexto, nombres, puedeVer, onVer, onAccion, decidirPorDocumento = false }: TarjetaRequisitoProps) {
  const { t } = useTranslation();
  const version = requisito.versionActual;
  const visor = documentoEnVisor(requisito);
  const documento = requisito.tipoDocumento.nombre;
  const eximir = puedeEximir(expediente, requisito, contexto(null).scopes);

  return (
    <Box component="li" sx={{ listStyle: 'none', border: 1, borderColor: requisito.estado === 'Observado' ? 'estado.advertencia.borde' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}>
      <Stack direction="row" spacing={1} alignItems="flex-start" useFlexGap flexWrap="wrap">
        <FileText size={18} strokeWidth={1.75} aria-hidden style={{ flex: 'none', marginTop: 2, opacity: 0.7 }} />
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px' }}>
            {documento}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
            {requisito.obligatorio ? t('expedientes.requisitos.obligatorio') : t('expedientes.requisitos.opcional')}
            {version && (
              <>
                {' · '}
                {t('expedientes.requisitos.version', { n: String(version.n), fecha: formatearFecha(version.cargadaEn) })}
                {visor?.sinArchivo && ` · ${t('expedientes.datosDeclarados.formulario')}`}
                {' · '}
                <Typography component="span" variant="mono" title={version.sha256} sx={{ fontSize: 12 }}>
                  {t('expedientes.requisitos.hash', { hash: hashCorto(version.sha256) })}
                </Typography>
              </>
            )}
          </Typography>
        </Box>
        <ChipEstado estado={requisito.estado} />
        {/* Una versión de solo formulario no tiene archivo que ver: sus datos van abajo, en la misma tarjeta. */}
        {visor && !visor.sinArchivo && <BotonVerVersion documento={visor} puedeVerArchivo={puedeVer} onVer={onVer} />}
        {eximir && (
          <Button size="small" variant="text" onClick={() => onAccion({ tipo: 'eximir', requisito })} sx={{ color: 'text.secondary' }}>
            {t('expedientes.requisitos.eximirAccion')}
          </Button>
        )}
      </Stack>

      {visor && (
        <Box sx={{ mt: 1 }}>
          <ResumenVersion datos={visor.datosDeclarados ?? []} vigencia={visor.vigencia} limite={6} />
        </Box>
      )}
      {!version && requisito.estado === 'Pendiente' && (
        <Typography color="text.secondary" sx={{ fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.sinVersion')}
        </Typography>
      )}
      {requisito.reenvios > 0 && (
        <Typography color="text.secondary" sx={{ fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.versionesNuevas', { count: requisito.reenvios })}
        </Typography>
      )}
      {requisito.exencion && (
        <Typography sx={{ fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.eximido', {
            persona: nombres.get(requisito.exencion.por) ?? t('expedientes.requisitos.persona'),
            fecha: formatearFecha(requisito.exencion.fecha),
            justificacion: requisito.exencion.justificacion,
          })}
        </Typography>
      )}

      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, mt: 1.25 }}>
        {requisito.areas.map((areaRequisito) => (
          <AreaDelRequisito
            key={areaRequisito.areaId}
            expediente={expediente}
            requisito={requisito}
            areaRequisito={areaRequisito}
            area={areas.get(areaRequisito.areaId) ?? null}
            contexto={contexto}
            nombres={nombres}
            onAccion={onAccion}
            decidirPorDocumento={decidirPorDocumento}
          />
        ))}
      </Stack>
    </Box>
  );
}

function AreaDelRequisito({
  expediente,
  requisito,
  areaRequisito,
  area,
  contexto,
  nombres,
  onAccion,
  decidirPorDocumento,
}: Pick<TarjetaRequisitoProps, 'expediente' | 'requisito' | 'contexto' | 'nombres' | 'onAccion' | 'decidirPorDocumento'> & {
  areaRequisito: AreaDeRequisitoDto;
  area: AreaExpedienteDto | null;
}) {
  const { t } = useTranslation();
  const ctx = contexto(area);
  const nombreArea = area?.nombre ?? areaRequisito.codigo;
  const bloqueo = bloqueoRequisito(expediente, requisito, areaRequisito, ctx);
  const explicar = decidirPorDocumento && seExplica(bloqueo) && ctx.scopes.has(SCOPES.expedientesRevisar) ? bloqueo : null;
  const nombre = (id: string | null | undefined) =>
    id && id === ctx.usuarioId ? t('expedientes.veredictos.tu') : ((id && nombres.get(id)) ?? t('expedientes.requisitos.persona'));

  return (
    <Box component="li" sx={{ listStyle: 'none', pl: 1.5, borderLeft: 2, borderColor: 'divider' }}>
      <Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap">
        <Typography sx={{ fontWeight: 500, fontSize: 13, flex: '1 1 auto' }}>{nombreArea}</Typography>
        {areaRequisito.estado === 'Esperando' ? (
          <Chip size="small" variant="outlined" icon={<Clock size={14} aria-hidden />} label={t('expedientes.requisitos.areaEsperando')} sx={{ color: 'text.secondary' }} />
        ) : (
          <ChipEstado estado={areaRequisito.estado} />
        )}
        <Typography component="span" variant="monoTabla" color="text.secondary">
          {t('bandeja.aprobaciones', { aprobaciones: String(areaRequisito.aprobaciones), requeridas: String(areaRequisito.aprobadoresRequeridos) })}
        </Typography>
        {bloqueo === null && decidirPorDocumento && (
          <Button
            size="small"
            variant="contained"
            onClick={() => onAccion({ tipo: 'veredictoRequisito', requisito, areaRequisito, nombreArea })}
            aria-label={t('expedientes.requisitos.decidirAccesible', { documento: requisito.tipoDocumento.nombre, area: nombreArea })}
          >
            {t('expedientes.requisitos.decidir')}
          </Button>
        )}
      </Stack>
      {veredictosVigentesDeRequisito(areaRequisito).map((v) => (
        <Typography key={v.id} sx={{ fontSize: 13, mt: 0.5 }}>
          {nombre(v.usuario)} · {t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()} · {t('expedientes.requisitos.sobreVersion', { n: String(v.sobreVersion ?? '—') })}
          {v.observacion ? ` · “${v.observacion}”` : ''}
        </Typography>
      ))}
      {veredictosInvalidados(areaRequisito).map((v) => (
        <Typography key={v.id} color="text.secondary" sx={{ fontSize: 12, mt: 0.5, textDecoration: 'line-through' }}>
          {nombre(v.usuario)} · {t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()} · {t('expedientes.requisitos.invalidado', { n: String(v.sobreVersion ?? '—') })}
        </Typography>
      ))}
      {explicar && (
        <Typography color="text.secondary" sx={{ fontSize: 12, mt: 0.5 }}>
          {t(`expedientes.requisitos.bloqueos.${explicar}`)}
        </Typography>
      )}
    </Box>
  );
}
