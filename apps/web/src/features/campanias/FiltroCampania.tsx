import { Box, Chip, MenuItem, TextField, Typography } from '@mui/material';
import type { TipoVinculoDto } from '@validasoft/api-client';
import { CampoNumero } from '@validasoft/ui';
import { Check } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { alternarEstado, diasValidos, ESTADOS_DE_CAMPANIA, type BorradorDeFiltro } from './modelo';

/**
 * Filtro de la campaña: «vencen en N días», tipo de vínculo (si la persona puede ver los tipos) y los estados del vínculo como
 * chips que se marcan o desmarcan. Cualquier cambio avisa a quien lo usa para descartar la vista previa anterior.
 */
export function FiltroCampania({
  borrador,
  tipos,
  deshabilitado = false,
  onCambiar,
}: {
  borrador: BorradorDeFiltro;
  /** Tipos de vínculo activos; `undefined` si la persona no los puede ver (se renuncia a elegir tipo). */
  tipos: readonly TipoVinculoDto[] | undefined;
  deshabilitado?: boolean;
  onCambiar: (nuevo: BorradorDeFiltro) => void;
}) {
  const { t } = useTranslation();
  const errorDias = !diasValidos(borrador.dias);
  const sinEstados = borrador.estados.length === 0;

  return (
    <Box
      sx={{
        display: 'grid',
        gap: 2,
        alignItems: 'start',
        gridTemplateColumns: tipos
          ? { xs: 'minmax(0, 1fr)', sm: '180px minmax(0, 1fr)', lg: '180px 280px minmax(0, 1fr)' }
          : { xs: 'minmax(0, 1fr)', sm: '180px minmax(0, 1fr)' },
      }}
    >
      <CampoNumero
        etiqueta={t('campanias.nuevaCampania.dias')}
        valor={borrador.dias}
        maxDigitos={3}
        deshabilitado={deshabilitado}
        error={errorDias}
        ayuda={errorDias ? t('campanias.nuevaCampania.diasInvalido') : t('campanias.nuevaCampania.diasAyuda')}
        onCambiar={(dias) => onCambiar({ ...borrador, dias })}
      />
      {tipos && (
        <TextField
          select
          label={t('campanias.nuevaCampania.tipo')}
          value={borrador.tipoVinculoId}
          disabled={deshabilitado}
          onChange={(e) => onCambiar({ ...borrador, tipoVinculoId: e.target.value })}
          slotProps={{ select: { displayEmpty: true } }}
        >
          <MenuItem value="">{t('campanias.nuevaCampania.todosLosTipos')}</MenuItem>
          {tipos
            .filter((tipo) => tipo.activo)
            .map((tipo) => (
              <MenuItem key={tipo.id} value={tipo.id}>
                {tipo.nombre}
              </MenuItem>
            ))}
        </TextField>
      )}
      <Box role="group" aria-labelledby="estados-de-campania" sx={{ gridColumn: tipos ? { sm: '1 / -1', lg: 'auto' } : 'auto', minWidth: 0 }}>
        <Typography id="estados-de-campania" variant="body2" sx={{ fontSize: 13, lineHeight: '18px', fontWeight: 500, mb: 0.5 }}>
          {t('campanias.nuevaCampania.estados')}
        </Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
          {ESTADOS_DE_CAMPANIA.map((estado) => {
            const marcado = borrador.estados.includes(estado);
            return (
              <Chip
                key={estado}
                clickable
                disabled={deshabilitado}
                variant="outlined"
                aria-pressed={marcado}
                icon={marcado ? <Check size={14} strokeWidth={2} aria-hidden /> : undefined}
                label={t(`campanias.estadosVinculo.${estado}`)}
                onClick={() => onCambiar({ ...borrador, estados: alternarEstado(borrador.estados, estado) })}
                sx={{
                  height: 32,
                  fontSize: 13,
                  borderRadius: (tema) => `${String(tema.radio.sm)}px`,
                  borderColor: marcado ? 'acento.main' : 'borde.control',
                  bgcolor: marcado ? 'acento.tinte' : 'superficie.nivel1',
                  color: marcado ? 'acento.main' : 'text.primary',
                }}
              />
            );
          })}
        </Box>
        {sinEstados && (
          <Typography variant="caption" role="alert" sx={{ color: 'estado.error.texto', display: 'block', mt: 0.5 }}>
            {t('campanias.nuevaCampania.estadosVacio')}
          </Typography>
        )}
      </Box>
    </Box>
  );
}
