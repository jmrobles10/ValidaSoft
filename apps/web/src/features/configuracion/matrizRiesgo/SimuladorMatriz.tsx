import { Box, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChipNivelRiesgo } from '../../cumplimiento/components/ChipNivelRiesgo';
import { claveRiesgo, formatearPuntaje } from '../../cumplimiento/modelo';
import { calcularEjemplo, type BorradorMatriz } from './modelo';

/** Prueba de la matriz que se está editando: una opción por factor y el resultado que daría. No guarda nada. */
export function SimuladorMatriz({ borrador }: { borrador: BorradorMatriz }) {
  const { t } = useTranslation();
  const [elecciones, setElecciones] = useState<Record<string, string>>({});
  const factores = borrador.factores.filter((f) => f.opciones.length > 0);
  const resultado = calcularEjemplo(borrador, elecciones);

  if (!resultado) return <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('matrizRiesgo.prueba.sinFactores')}</Typography>;

  return (
    <Stack spacing={2}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('matrizRiesgo.prueba.ayuda')}</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 1.5 }}>
        {factores.map((f) => (
          <TextField
            key={f.clave}
            select
            label={f.nombre.trim() || t('matrizRiesgo.factores.sinNombre')}
            value={f.opciones.some((o) => o.clave === elecciones[f.clave]) ? elecciones[f.clave] : (f.opciones[0]?.clave ?? '')}
            onChange={(e) => setElecciones((previas) => ({ ...previas, [f.clave]: e.target.value }))}
          >
            {f.opciones.map((o) => (
              <MenuItem key={o.clave} value={o.clave}>
                {o.nombre.trim() || t('matrizRiesgo.factores.sinOpcion')}
              </MenuItem>
            ))}
          </TextField>
        ))}
      </Box>
      <Box
        role="status"
        aria-label={t('matrizRiesgo.prueba.titulo')}
        sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(3, minmax(0, 1fr))' }, gap: 2, border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}
      >
        <Stack spacing={0.25}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('matrizRiesgo.prueba.puntaje')}
          </Typography>
          <Typography sx={{ fontSize: 20, fontWeight: 600 }}>{formatearPuntaje(resultado.puntaje)}</Typography>
        </Stack>
        <Stack spacing={0.5} sx={{ alignItems: 'flex-start' }}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('matrizRiesgo.prueba.nivel')}
          </Typography>
          <ChipNivelRiesgo nivel={resultado.nivel} />
        </Stack>
        <Stack spacing={0.25} sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' } }}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('matrizRiesgo.prueba.debida')}
          </Typography>
          <Typography sx={{ fontSize: 14 }}>{t(`cumplimiento.debida.${resultado.debidaDiligencia}`)}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('matrizRiesgo.prueba.revision', { meses: String(resultado.mesesRevision) })}</Typography>
        </Stack>
        {resultado.forzadoPor && (
          <Typography sx={{ gridColumn: '1 / -1', color: 'text.secondary', fontSize: 12 }}>
            {t('matrizRiesgo.prueba.piso', { causa: resultado.forzadoPor, nivel: t(`cumplimiento.riesgos.${claveRiesgo(resultado.nivelPorPuntaje)}`) })}
          </Typography>
        )}
      </Box>
    </Stack>
  );
}
