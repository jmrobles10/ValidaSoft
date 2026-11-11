import { Button, IconButton, MenuItem, Stack, TextField, Tooltip, Typography } from '@mui/material';
import type { EmpresaDto } from '@validasoft/api-client';
import { Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { aRolSso, ROLES_SSO, type FilaDeRegla, type FilaDeRol } from './modeloSso';

interface SelectoresProps {
  fila: FilaDeRol;
  empresas: readonly EmpresaDto[];
  alCambiar: (cambios: Partial<FilaDeRol>) => void;
  etiquetaRol: string;
}

/** Rol de Validasoft y dónde vale (todo el grupo o una empresa). */
function SelectoresDeRol({ fila, empresas, alCambiar, etiquetaRol }: SelectoresProps) {
  const { t } = useTranslation();
  return (
    <>
      <TextField select size="small" label={etiquetaRol} value={fila.rol} onChange={(e) => alCambiar({ rol: aRolSso(e.target.value) })} sx={{ minWidth: 190, flex: 1 }}>
        {ROLES_SSO.map((r) => (
          <MenuItem key={r} value={r}>
            {t(`usuarios.roles.${r}`)}
          </MenuItem>
        ))}
      </TextField>
      <TextField select size="small" label={t('sso.roles.donde')} value={fila.empresaId} onChange={(e) => alCambiar({ empresaId: e.target.value })} sx={{ minWidth: 190, flex: 1 }}>
        <MenuItem value="">{t('sso.roles.todoElGrupo')}</MenuItem>
        {empresas.map((e) => (
          <MenuItem key={e.id} value={e.id}>
            {e.nombre}
          </MenuItem>
        ))}
      </TextField>
    </>
  );
}

/** Los roles de quien llega sin un grupo que dé alguno. */
export function RolesPorOmision({ filas, empresas, alCambiar }: { filas: FilaDeRol[]; empresas: readonly EmpresaDto[]; alCambiar: (filas: FilaDeRol[]) => void }) {
  const { t } = useTranslation();
  const actualizar = (i: number, cambios: Partial<FilaDeRol>) => alCambiar(filas.map((f, j) => (i === j ? { ...f, ...cambios } : f)));
  return (
    <Stack spacing={1}>
      {filas.map((fila, i) => (
        <Stack key={i} direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <SelectoresDeRol fila={fila} empresas={empresas} alCambiar={(c) => actualizar(i, c)} etiquetaRol={t('sso.roles.rol')} />
          <Tooltip title={t('sso.roles.quitar')}>
            <IconButton size="small" aria-label={t('sso.roles.quitar')} onClick={() => alCambiar(filas.filter((_, j) => j !== i))}>
              <X size={16} aria-hidden />
            </IconButton>
          </Tooltip>
        </Stack>
      ))}
      <Button size="small" variant="text" startIcon={<Plus size={14} aria-hidden />} onClick={() => alCambiar([...filas, { rol: 'Revisor', empresaId: '' }])} sx={{ alignSelf: 'flex-start' }}>
        {t('sso.roles.agregarPorOmision')}
      </Button>
    </Stack>
  );
}

/** Un grupo del proveedor de identidad (escrito tal como llega en su reclamo) y el rol que da. */
export function ReglasDeGrupos({ filas, empresas, alCambiar }: { filas: FilaDeRegla[]; empresas: readonly EmpresaDto[]; alCambiar: (filas: FilaDeRegla[]) => void }) {
  const { t } = useTranslation();
  const actualizar = (i: number, cambios: Partial<FilaDeRegla>) => alCambiar(filas.map((f, j) => (i === j ? { ...f, ...cambios } : f)));
  return (
    <Stack spacing={1}>
      {filas.length === 0 && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('sso.reglas.vacio')}
        </Typography>
      )}
      {filas.map((fila, i) => (
        <Stack key={i} direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ alignItems: { md: 'center' } }}>
          <TextField size="small" label={t('sso.reglas.grupo')} value={fila.grupo} onChange={(e) => actualizar(i, { grupo: e.target.value })} sx={{ minWidth: 190, flex: 1 }} slotProps={{ htmlInput: { maxLength: 300 } }} />
          <Typography aria-hidden sx={{ color: 'text.secondary', display: { xs: 'none', md: 'block' } }}>
            →
          </Typography>
          <Stack direction="row" spacing={1} sx={{ flex: 2, minWidth: 0 }}>
            <SelectoresDeRol fila={fila} empresas={empresas} alCambiar={(c) => actualizar(i, c)} etiquetaRol={t('sso.reglas.da')} />
            <Tooltip title={t('sso.reglas.quitar')}>
              <IconButton size="small" aria-label={t('sso.reglas.quitar')} onClick={() => alCambiar(filas.filter((_, j) => j !== i))} sx={{ alignSelf: 'center' }}>
                <X size={16} aria-hidden />
              </IconButton>
            </Tooltip>
          </Stack>
        </Stack>
      ))}
      <Button size="small" variant="text" startIcon={<Plus size={14} aria-hidden />} onClick={() => alCambiar([...filas, { grupo: '', rol: 'Revisor', empresaId: '' }])} sx={{ alignSelf: 'flex-start' }}>
        {t('sso.reglas.agregar')}
      </Button>
    </Stack>
  );
}
