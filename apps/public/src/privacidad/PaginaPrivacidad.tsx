import { Box, Button, Stack, Typography } from '@mui/material';
import { EsqueletoTarjetas, EstadoVacio } from '@validasoft/ui';
import { Plus, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useVinculosPublicos } from '../shared/api';
import { CabeceraPagina, ErrorRecuperable } from '../shared/componentes';
import { useSolicitudesHabeas } from './api';
import { DialogoNuevaSolicitud } from './DialogoNuevaSolicitud';
import { ordenarSolicitudes } from './modelo';
import { SeccionExportacion } from './SeccionExportacion';
import { TarjetaSolicitud } from './TarjetaSolicitud';

/**
 * Privacidad y mis datos (Ley 1581 de 2012, OD-1, OD-9): el tercero se lleva una copia firmada de su bóveda y ejerce sus derechos de
 * consulta, corrección y supresión ante Validasoft o ante una empresa, con su radicado, su plazo en días hábiles y la constancia de la
 * respuesta.
 */
export function PaginaPrivacidad() {
  const { t } = useTranslation();
  const solicitudes = useSolicitudesHabeas();
  const vinculos = useVinculosPublicos();
  const [creando, setCreando] = useState(false);

  return (
    <>
      <CabeceraPagina titulo={t('privacidad.titulo')} sub={t('privacidad.sub')} />
      <Stack spacing={3}>
        <SeccionExportacion />

        <Box component="section" aria-labelledby="privacidad-solicitudes">
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between', mb: 1.5 }}>
            <Box sx={{ minWidth: 0 }}>
              <Typography id="privacidad-solicitudes" variant="h3" component="h2">
                {t('privacidad.solicitudes.titulo')}
              </Typography>
              <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px' }}>{t('privacidad.solicitudes.texto')}</Typography>
            </Box>
            <Button variant="contained" onClick={() => setCreando(true)} startIcon={<Plus size={18} aria-hidden />} sx={{ width: { xs: '100%', sm: 'auto' }, flex: 'none' }}>
              {t('privacidad.solicitudes.nueva')}
            </Button>
          </Stack>
          {solicitudes.isPending ? (
            <EsqueletoTarjetas cantidad={2} lineas={3} />
          ) : solicitudes.isError ? (
            <ErrorRecuperable error={solicitudes.error} onReintentar={() => void solicitudes.refetch()} />
          ) : solicitudes.data.length === 0 ? (
            <EstadoVacio icono={ShieldCheck} titulo={t('privacidad.solicitudes.vacio')} texto={t('privacidad.solicitudes.vacioTexto')} />
          ) : (
            <Stack spacing={1.5}>
              {ordenarSolicitudes(solicitudes.data).map((s) => (
                <TarjetaSolicitud key={s.id} solicitud={s} />
              ))}
            </Stack>
          )}
        </Box>
      </Stack>
      {creando && <DialogoNuevaSolicitud vinculos={vinculos.data ?? []} solicitudes={solicitudes.data ?? []} onCerrar={() => setCreando(false)} />}
    </>
  );
}
