import { Alert, Button, Card, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { formatearFechaHora } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { CabeceraPagina, EsqueletoTarjetas } from '../shared/componentes';
import { extraerDeCorreo, leerCorreo, listarCorreos, type ResumenCorreo } from './correos';

/** `/dev/correos` (solo `vite dev`): las invitaciones y los códigos que llegaron a Mailpit, para abrir el enlace aquí. */
export function PaginaCorreos() {
  const { t } = useTranslation();
  const correos = useQuery({ queryKey: ['__mailpit'], queryFn: () => listarCorreos(), refetchInterval: 5000 });

  return (
    <>
      <CabeceraPagina titulo={t('dev.titulo')} sub={t('dev.sub')} />
      {correos.isPending ? (
        <EsqueletoTarjetas />
      ) : correos.isError ? (
        <Alert severity="error">{t('dev.error')}</Alert>
      ) : correos.data.length === 0 ? (
        <Alert severity="info">{t('dev.vacio')}</Alert>
      ) : (
        <Stack spacing={1}>
          {correos.data.map((c) => (
            <Correo key={c.id} correo={c} />
          ))}
        </Stack>
      )}
    </>
  );
}

function Correo({ correo }: { correo: ResumenCorreo }) {
  const { t } = useTranslation();
  const cuerpo = useQuery({ queryKey: ['__mailpit', correo.id], queryFn: () => leerCorreo(correo.id), staleTime: Infinity });
  const { enlace, codigo } = extraerDeCorreo(cuerpo.data ?? '');
  return (
    <Card sx={{ p: 1.5 }}>
      <Typography sx={{ fontWeight: 600, fontSize: 14 }}>{correo.asunto}</Typography>
      <Typography color="text.secondary" sx={{ fontSize: 13 }}>
        {correo.para} · {formatearFechaHora(correo.fecha)}
      </Typography>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 1 }}>
        {enlace && (
          <Button component={RouterLink} to={enlace} variant="contained" size="small">
            {t('dev.abrir')}
          </Button>
        )}
        {codigo && (
          <Typography variant="mono" sx={{ fontSize: 18 }}>
            {t('dev.codigo', { codigo })}
          </Typography>
        )}
      </Stack>
    </Card>
  );
}
