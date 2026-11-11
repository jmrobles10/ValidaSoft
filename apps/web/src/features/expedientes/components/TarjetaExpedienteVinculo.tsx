import { Alert, Button, Stack, Typography } from '@mui/material';
import { SCOPES, type VinculoDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { ArrowRight, FolderOpen } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../../shared/componentes';
import { usePermisos } from '../../../shared/usePermisos';
import { useTercero } from '../../vinculos/api';
import { useExpediente } from '../api';
import { DialogoAbrirExpediente } from '../DialogoAbrirExpediente';
import { puedeAbrirExpediente } from '../reglas';

/**
 * El expediente del vínculo: el abierto con su estado y enlace, el aviso de apertura pendiente (FL-6) y, para el
 * administrador, la apertura manual. No se muestra si no hay nada que decir.
 */
export function TarjetaExpedienteVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  const tercero = useTercero(vinculo.terceroId);
  const permisos = usePermisos(vinculo.empresaId);
  const [abriendo, setAbriendo] = useState(false);
  const abiertoId = vinculo.expedienteAbiertoId ?? '';
  const puedeLeer = permisos.puede(SCOPES.expedientesLeer);
  const expediente = useExpediente(vinculo.empresaId, puedeLeer ? abiertoId : '');
  const abrir = puedeAbrirExpediente(vinculo, permisos.scopes);

  if (!abiertoId && !vinculo.aperturaDeExpedientePendiente && !abrir) return null;

  return (
    <Tarjeta titulo={t('expedientes.enVinculo.titulo')}>
      <Stack spacing={1.5}>
        {abiertoId && (
          <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Typography sx={{ fontWeight: 500 }}>{t('expedientes.enVinculo.enCurso')}</Typography>
              {expediente.data && (
                <>
                  <Typography component="span" variant="mono" sx={{ color: 'text.secondary' }}>
                    {expediente.data.folio}
                  </Typography>
                  <ChipEstado estado={expediente.data.estado} fecha={expediente.data.actualizadoEn} />
                </>
              )}
            </Stack>
            {puedeLeer && (
              <Button component={RouterLink} to={`/e/${vinculo.empresaId}/expedientes/${abiertoId}`} variant="outlined" endIcon={<ArrowRight size={16} aria-hidden />}>
                {t('expedientes.enVinculo.ver')}
              </Button>
            )}
          </Stack>
        )}
        {!abiertoId && vinculo.aperturaDeExpedientePendiente && <Alert severity="warning">{t('expedientes.enVinculo.pendiente')}</Alert>}
        {!abiertoId && !vinculo.aperturaDeExpedientePendiente && (
          <Typography sx={{ color: 'text.secondary' }}>
            {vinculo.ultimoResultado === 'Rechazado' ? t('expedientes.enVinculo.rechazado') : t('expedientes.enVinculo.sinExpediente')}
          </Typography>
        )}
        {abrir && (
          <div>
            <Button variant="contained" startIcon={<FolderOpen size={16} aria-hidden />} onClick={() => setAbriendo(true)}>
              {t('expedientes.abrir.boton')}
            </Button>
          </div>
        )}
      </Stack>
      {abriendo && (
        <DialogoAbrirExpediente empresaId={vinculo.empresaId} vinculoId={vinculo.id} tipoVinculo={vinculo.tipoVinculo} tercero={tercero.data?.nombre ?? null} onCerrar={() => setAbriendo(false)} />
      )}
    </Tarjeta>
  );
}
