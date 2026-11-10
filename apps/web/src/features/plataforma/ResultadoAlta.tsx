import { Alert, Box, Button, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import type { TenantCreadoDto } from '@validasoft/api-client';
import { Check, Copy, Sparkles } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { EncabezadoPagina, Tarjeta } from '../../shared/componentes';

function ValorCopiable({ etiqueta, ayuda, valor }: { etiqueta: string; ayuda: string; valor: string }) {
  const { t } = useTranslation();
  const [copiado, setCopiado] = useState(false);
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(valor);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  };

  return (
    <Stack spacing={0.75}>
      <Typography variant="h4" component="h3">
        {etiqueta}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', px: 1.5, py: 1, bgcolor: 'superficie.nivel2', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
        <Box component="code" aria-label={etiqueta} data-secreto="" sx={{ flex: 1, minWidth: 0, fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 13, overflowWrap: 'anywhere' }}>
          {valor}
        </Box>
        <Tooltip title={copiado ? t('plataforma.alta.listo.copiada') : t('plataforma.alta.listo.copiar')}>
          <IconButton aria-label={`${t('plataforma.alta.listo.copiar')}: ${etiqueta}`} onClick={() => void copiar()}>
            {copiado ? <Check size={18} aria-hidden /> : <Copy size={18} aria-hidden />}
          </IconButton>
        </Tooltip>
      </Stack>
      <Typography variant="caption" sx={{ color: 'text.secondary' }}>
        {ayuda}
      </Typography>
    </Stack>
  );
}

/** Lo que el alta entrega una sola vez: las dos API keys (la del sistema y la de administración) y el siguiente paso. */
export function ResultadoAlta({ alta }: { alta: TenantCreadoDto }) {
  const { t } = useTranslation();
  const empresa = alta.empresa;
  return (
    <>
      <EncabezadoPagina titulo={t('plataforma.alta.listo.titulo', { nombre: alta.tenant.nombre })} subtitulo={alta.tenant.slug} />
      <Stack spacing={2} sx={{ maxWidth: 760 }}>
        <Alert severity="warning">{t('plataforma.alta.listo.aviso')}</Alert>
        <Tarjeta>
          <Stack spacing={2.5}>
            {alta.apiKeySistema?.valor && alta.sistema && (
              <ValorCopiable
                etiqueta={t('plataforma.alta.listo.llaveSistema', { sistema: alta.sistema.codigo })}
                ayuda={t('plataforma.alta.listo.llaveSistemaAyuda')}
                valor={alta.apiKeySistema.valor}
              />
            )}
            {alta.apiKeyAdministrador.valor && (
              <ValorCopiable etiqueta={t('plataforma.alta.listo.llaveAdministracion')} ayuda={t('plataforma.alta.listo.llaveAdministracionAyuda')} valor={alta.apiKeyAdministrador.valor} />
            )}
          </Stack>
        </Tarjeta>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          {empresa && (
            <Button
              component={RouterLink}
              to={`/plataforma/grupos/${alta.tenant.id}/empresas/${empresa.id}/plan-ia`}
              variant="contained"
              startIcon={<Sparkles size={16} aria-hidden />}
            >
              {t('plataforma.alta.listo.planIa', { empresa: empresa.nombre })}
            </Button>
          )}
          <Button component={RouterLink} to="/plataforma/grupos" variant="outlined">
            {t('plataforma.alta.listo.volver')}
          </Button>
        </Stack>
      </Stack>
    </>
  );
}
