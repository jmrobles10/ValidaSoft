import { Box, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import type { DominioDeCorreoDto, RegistroDnsDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { Check, Copy } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { comoFinalidad, comprobacionDe } from './modelo';

/** Un valor en monoespaciado con su botón de copiar: lo que se pega en el panel del DNS. */
function ValorCopiable({ etiqueta, valor }: { etiqueta: string; valor: string }) {
  const { t } = useTranslation();
  const [copiado, setCopiado] = useState(false);
  const copiar = () => {
    void navigator.clipboard
      .writeText(valor)
      .then(() => {
        setCopiado(true);
        window.setTimeout(() => setCopiado(false), 1800);
      })
      .catch(() => undefined);
  };
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>{etiqueta}</Typography>
      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'flex-start' }}>
        <Typography variant="mono" component="p" sx={{ m: 0, flex: 1, minWidth: 0, fontSize: 13, lineHeight: '20px', overflowWrap: 'anywhere' }}>
          {valor}
        </Typography>
        <Tooltip title={copiado ? t('dominioCorreo.copiado') : t('dominioCorreo.copiar')}>
          <IconButton size="small" onClick={copiar} aria-label={t('dominioCorreo.copiarAccesible', { etiqueta })} sx={{ flex: 'none', mt: '-2px' }}>
            {copiado ? <Check size={14} aria-hidden /> : <Copy size={14} aria-hidden />}
          </IconButton>
        </Tooltip>
      </Stack>
    </Box>
  );
}

function Registro({ registro, dominio }: { registro: RegistroDnsDto; dominio: DominioDeCorreoDto }) {
  const { t } = useTranslation();
  const comprobacion = comprobacionDe(dominio, registro.finalidad);
  const finalidad = comoFinalidad(registro.finalidad);
  const estado = comprobacion === undefined ? 'sinVerificar' : comprobacion.correcto ? 'correcto' : comprobacion.encontrado ? 'distinto' : 'faltante';
  return (
    <Box component="li" sx={(th) => ({ listStyle: 'none', p: 1.5, border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.sm)}px` })}>
      <Stack direction="row" useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', columnGap: 1, rowGap: 0.5, mb: 1 }}>
        <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px' }}>
          {finalidad ? t(`dominioCorreo.finalidades.${finalidad}.nombre`) : registro.finalidad}
        </Typography>
        <Typography variant="mono" sx={{ fontSize: 12, color: 'text.secondary' }}>
          {t('dominioCorreo.registroTipo', { tipo: registro.tipo })}
        </Typography>
        <Box sx={{ ml: 'auto' }}>
          <ChipEstado
            estado={estado === 'correcto' ? 'Aprobado' : estado === 'sinVerificar' ? 'Pendiente' : 'Rechazado'}
            etiqueta={t(`dominioCorreo.comprobacion.${estado}`)}
          />
        </Box>
      </Stack>
      <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', mb: 1 }}>{finalidad ? t(`dominioCorreo.finalidades.${finalidad}.para`) : ''}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 1.25 }}>
        <ValorCopiable etiqueta={t('dominioCorreo.registroNombre')} valor={registro.nombre} />
        <ValorCopiable etiqueta={t('dominioCorreo.registroValor')} valor={registro.valor} />
      </Box>
      {comprobacion && !comprobacion.correcto && (
        <Typography sx={{ mt: 1, fontSize: 12, lineHeight: '16px', color: 'error.main', overflowWrap: 'anywhere' }}>
          {comprobacion.encontrado ? t('dominioCorreo.encontrado', { valor: comprobacion.encontrado }) : t('dominioCorreo.noEncontrado')}
        </Typography>
      )}
    </Box>
  );
}

/** Los registros DNS que hay que crear en el panel del dominio (SPF, DKIM y DMARC) y lo que halló la última verificación en cada uno. */
export function RegistrosDns({ dominio }: { dominio: DominioDeCorreoDto }) {
  const { t } = useTranslation();
  return (
    <Box component="ul" aria-label={t('dominioCorreo.registros')} sx={{ m: 0, p: 0, display: 'flex', flexDirection: 'column', gap: 1.25 }}>
      {dominio.registros.map((r) => (
        <Registro key={r.finalidad} registro={r} dominio={dominio} />
      ))}
    </Box>
  );
}
