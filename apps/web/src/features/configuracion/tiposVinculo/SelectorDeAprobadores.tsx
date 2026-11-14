import { Alert, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useAreas } from '../areas/api';
import { ROLES_QUE_APRUEBAN, type AprobadoresBorrador } from './modelo';

/**
 * Quién aprueba las invitaciones del tipo (decisión AI): un rol o un área. Las áreas son de cada empresa y el tipo es del grupo, así que se guarda el código del área y se
 * busca en la empresa de cada vínculo; las opciones son las de la empresa activa.
 */
export function SelectorDeAprobadores({ valor, onCambiar, error }: { valor: AprobadoresBorrador; onCambiar: (valor: AprobadoresBorrador) => void; error: string | null }) {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const areas = useAreas(empresa.id);
  const codigos = (areas.data ?? []).filter((a) => a.activa).map((a) => ({ codigo: a.codigo, nombre: a.nombre }));
  // Si el área que ya estaba guardada no es de esta empresa (u otra empresa la tiene), se conserva como opción para no perderla al guardar.
  const opcionesArea = valor.valor && !codigos.some((a) => a.codigo === valor.valor) ? [{ codigo: valor.valor, nombre: valor.valor }, ...codigos] : codigos;

  return (
    <Stack spacing={1.5} sx={{ pl: { sm: 5.5 } }}>
      <ToggleButtonGroup
        exclusive
        size="small"
        aria-label={t('invitaciones.config.quienAprueba')}
        value={valor.tipo}
        onChange={(_, tipo: 'Rol' | 'Area' | null) => {
          if (tipo) onCambiar({ tipo, valor: '' });
        }}
      >
        <ToggleButton value="Rol">{t('invitaciones.config.modoRol')}</ToggleButton>
        <ToggleButton value="Area">{t('invitaciones.config.modoArea')}</ToggleButton>
      </ToggleButtonGroup>
      {valor.tipo === 'Area' && areas.isSuccess && opcionesArea.length === 0 && <Alert severity="info">{t('invitaciones.config.areaSinOpciones')}</Alert>}
      <TextField
        select
        required
        label={valor.tipo === 'Rol' ? t('invitaciones.config.rol') : t('invitaciones.config.area')}
        value={valor.valor}
        onChange={(e) => onCambiar({ ...valor, valor: e.target.value })}
        error={error !== null}
        helperText={error ?? (valor.tipo === 'Area' ? t('invitaciones.config.areaAyuda') : undefined)}
        slotProps={{ select: { displayEmpty: true } }}
      >
        <MenuItem value="" disabled>
          {t('invitaciones.config.seleccionar')}
        </MenuItem>
        {valor.tipo === 'Rol'
          ? ROLES_QUE_APRUEBAN.map((rol) => (
              <MenuItem key={rol} value={rol}>
                {t(`invitaciones.config.roles.${rol}`)}
              </MenuItem>
            ))
          : opcionesArea.map((area) => (
              <MenuItem key={area.codigo} value={area.codigo}>
                {area.nombre === area.codigo ? area.codigo : `${area.nombre} (${area.codigo})`}
              </MenuItem>
            ))}
      </TextField>
    </Stack>
  );
}
