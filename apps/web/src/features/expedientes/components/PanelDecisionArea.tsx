import { Box, Button, Stack, Typography } from '@mui/material';
import { SCOPES, type AreaExpedienteDto, type ExpedienteDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { Check, TriangleAlert } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { ChipVigencia } from '../../documentos/VigenciaDocumento';
import { VisorDocumento, type DocumentoEnVisor } from '../../documentos/VisorDocumento';
import { ChipEstadoArea } from '../componentes';
import { bloqueoDecisionArea, documentosDelArea, documentosPorDecidir, type BloqueoDecisionArea } from '../decisionArea';
import type { ContextoRevisor, DialogoExpediente } from '../reglas';
import { documentoEnVisor, tieneRequisitos } from '../requisitos';
import { BotonVerVersion } from './BotonVerVersion';

/** Bloqueos que se explican a quien revisa; los demás (sin permiso, cerrado) no dicen nada útil. */
const EXPLICADOS = ['expediente-observado', 'vinculo-bloqueado', 'area-esperando', 'no-es-miembro', 'nada-por-decidir'] as const;

function seExplica(bloqueo: BloqueoDecisionArea | null): bloqueo is (typeof EXPLICADOS)[number] {
  return (EXPLICADOS as readonly string[]).includes(bloqueo ?? '');
}

/**
 * La decisión de cada área de la etapa en curso, como en el SRM: sus documentos con versión, vigencia y «Ver» (o «Ver
 * datos» si la versión es solo un formulario), y una sola
 * decisión: «Aprobar área (n documentos)» u «Observar». El detalle por documento queda en «Documentos que pide el
 * flujo» («Decidir por documento»). Solo en flujos con requisitos.
 */
export function PanelDecisionArea({
  expediente,
  contexto,
  onAccion,
}: {
  expediente: ExpedienteDto;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  onAccion: (dialogo: DialogoExpediente) => void;
}) {
  const { t } = useTranslation();
  const [abierto, setAbierto] = useState<DocumentoEnVisor | null>(null);
  if (!tieneRequisitos(expediente) || !['EnRevision', 'Observado'].includes(expediente.estado)) return null;
  const etapa = expediente.etapas.find((e) => e.orden === expediente.etapaActual);
  const areas = (etapa?.areas ?? []).filter((a) => documentosDelArea(expediente, a.areaId).length > 0);
  const puedeVer = contexto(null).scopes.has(SCOPES.documentosDescargar);
  if (areas.length === 0) return null;

  return (
    <Tarjeta titulo={t('expedientes.decisionArea.titulo')}>
      <Stack spacing={1.5}>
        {areas.map((area) => {
          const ctx = contexto(area);
          const bloqueo = bloqueoDecisionArea(expediente, area, ctx);
          const porDecidir = documentosPorDecidir(area);
          const explicar = seExplica(bloqueo) && ctx.scopes.has(SCOPES.expedientesRevisar) ? bloqueo : null;
          return (
            <Box key={area.areaId} component="section" aria-label={area.nombre} sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}>
              <Stack direction="row" spacing={1} alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
                <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14 }}>
                  {area.nombre}
                </Typography>
                <ChipEstadoArea estado={area.estado} desde={area.pendienteDesde} />
              </Stack>
              <Stack component="ul" spacing={0.75} sx={{ p: 0, m: 0, listStyle: 'none' }}>
                {documentosDelArea(expediente, area.areaId).map(({ requisito, areaRequisito }) => {
                  const version = requisito.versionActual;
                  const documento = documentoEnVisor(requisito);
                  return (
                    <Stack component="li" key={requisito.id} direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap">
                      <Typography sx={{ fontSize: 13, flex: '1 1 auto', minWidth: 0 }} noWrap>
                        {requisito.tipoDocumento.nombre}
                        {version && (
                          <Typography component="span" color="text.secondary" sx={{ fontSize: 12 }}>
                            {` · v${String(version.n)}`}
                          </Typography>
                        )}
                      </Typography>
                      {version?.vigencia && <ChipVigencia vigencia={version.vigencia} />}
                      {areaRequisito.estado !== 'Esperando' && <ChipEstado estado={areaRequisito.estado} />}
                      {documento && <BotonVerVersion documento={documento} puedeVerArchivo={puedeVer} variante="text" onVer={setAbierto} />}
                    </Stack>
                  );
                })}
              </Stack>
              {bloqueo === null && (
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
                  <Button variant="contained" size="small" startIcon={<Check size={14} aria-hidden />} onClick={() => onAccion({ tipo: 'aprobarArea', area, documentos: porDecidir })}>
                    {t('expedientes.decisionArea.aprobar', { count: porDecidir.length })}
                  </Button>
                  <Button variant="outlined" color="warning" size="small" startIcon={<TriangleAlert size={14} aria-hidden />} onClick={() => onAccion({ tipo: 'observarArea', area })}>
                    {t('expedientes.decisionArea.observar')}
                  </Button>
                </Stack>
              )}
              {explicar && (
                <Typography color="text.secondary" sx={{ fontSize: 12, mt: 1 }}>
                  {t(`expedientes.decisionArea.bloqueos.${explicar}`)}
                </Typography>
              )}
            </Box>
          );
        })}
      </Stack>
      {abierto && <VisorDocumento empresaId={expediente.empresaId} vinculoId={expediente.vinculoId} documento={abierto} onCerrar={() => setAbierto(null)} />}
    </Tarjeta>
  );
}
