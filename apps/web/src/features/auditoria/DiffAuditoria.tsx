import { Box, Button, Stack, Typography } from '@mui/material';
import type { DiffCampoDto } from '@validasoft/api-client';
import { ArrowRight } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { COLOR_CAMBIO, esUnoDe, LARGO_RECORTE, textoValor, TIPOS_CAMBIO } from './modelo';

/** Un valor del diff en monoespaciada; si pasa de `LARGO_RECORTE` caracteres se recorta con «Ver más». */
function ValorJson({ valor, tachado = false }: { valor: unknown; tachado?: boolean }) {
  const { t } = useTranslation();
  const [completo, setCompleto] = useState(false);
  const texto = textoValor(valor);
  if (texto === null) {
    return (
      <Typography component="span" variant="body2" sx={{ color: 'text.secondary', fontStyle: 'italic' }}>
        {t('auditoria.detalle.vacioValor')}
      </Typography>
    );
  }
  const largo = texto.length > LARGO_RECORTE;
  return (
    <Box component="span" sx={{ minWidth: 0 }}>
      <Typography component="span" variant="mono" sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap', textDecoration: tachado ? 'line-through' : 'none' }}>
        {largo && !completo ? `${texto.slice(0, LARGO_RECORTE)}…` : texto}
      </Typography>
      {largo && (
        <Button variant="text" size="small" onClick={() => setCompleto((v) => !v)} sx={{ ml: 0.5, minWidth: 0, py: 0 }}>
          {completo ? t('auditoria.detalle.verMenos') : t('auditoria.detalle.verMas')}
        </Button>
      )}
    </Box>
  );
}

/** Diferencias campo por campo de un cambio de configuración, con el color del tipo de cambio. */
export function DiffAuditoria({ diff }: { diff: readonly DiffCampoDto[] }) {
  const { t } = useTranslation();
  if (diff.length === 0) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('auditoria.detalle.sinCambios')}
      </Typography>
    );
  }
  return (
    <Stack component="ul" spacing={1} sx={{ listStyle: 'none', p: 0, m: 0 }}>
      {diff.map((d) => {
        const conocido = esUnoDe(TIPOS_CAMBIO, d.tipoCambio) ? d.tipoCambio : null;
        const color = conocido ? COLOR_CAMBIO[conocido] : 'divider';
        return (
          <Box
            component="li"
            key={d.campo}
            aria-label={`${t('auditoria.detalle.campo')} ${d.campo}`}
            sx={{ pl: 1.5, py: 0.75, borderLeft: 3, borderColor: color, minWidth: 0 }}
          >
            <Stack direction="row" spacing={1} sx={{ alignItems: 'baseline', mb: 0.25 }}>
              <Typography component="span" variant="mono" sx={{ fontWeight: 600, overflowWrap: 'anywhere' }}>
                {d.campo}
              </Typography>
              <Typography component="span" variant="caption" sx={{ color }}>
                {conocido ? t(`auditoria.detalle.tiposCambio.${conocido}`) : d.tipoCambio}
              </Typography>
            </Stack>
            <Box sx={{ display: 'flex', alignItems: 'baseline', flexWrap: 'wrap', gap: 0.75 }}>
              {conocido !== 'Agregado' && <ValorJson valor={d.antes} tachado={conocido === 'Eliminado'} />}
              {conocido === 'Modificado' && (
                <Box component="span" sx={{ color: 'text.secondary', display: 'inline-flex', alignSelf: 'center' }}>
                  <ArrowRight size={14} strokeWidth={1.75} aria-hidden />
                </Box>
              )}
              {conocido !== 'Eliminado' && <ValorJson valor={d.despues} />}
            </Box>
          </Box>
        );
      })}
    </Stack>
  );
}
