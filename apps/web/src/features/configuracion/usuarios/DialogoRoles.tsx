import { Alert, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, IconButton, MenuItem, Stack, TextField, Tooltip } from '@mui/material';
import type { EmpresaDto, RolAsignadoDto } from '@validasoft/api-client';
import { Plus, X } from 'lucide-react';
import { useState, type FormEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../../shared/errores';
import { aFilas, ALCANCE_TENANT, aRoles, ROLES, type FilaRol } from './roles';

export interface DialogoRolesProps {
  titulo: string;
  empresas: readonly EmpresaDto[];
  rolesIniciales: readonly RolAsignadoDto[];
  /** Campos extra antes de los roles (nombre y correo al crear). */
  encabezado?: ReactNode;
  guardando: boolean;
  error: unknown;
  onGuardar: (roles: RolAsignadoDto[]) => void;
  onCerrar: () => void;
}

/** Editor de roles por empresa o para todo el grupo (ID-2). */
export function DialogoRoles({ titulo, empresas, rolesIniciales, encabezado, guardando, error, onGuardar, onCerrar }: DialogoRolesProps) {
  const { t } = useTranslation();
  const [filas, setFilas] = useState<FilaRol[]>(() => aFilas(rolesIniciales));

  const cambiar = (indice: number, cambios: Partial<FilaRol>) =>
    setFilas((actuales) => actuales.map((f, i) => (i === indice ? { ...f, ...cambios } : f)));

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    onGuardar(aRoles(filas));
  };

  return (
    <Dialog open onClose={guardando ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-roles">
      <form onSubmit={enviar}>
        <DialogTitle id="titulo-roles">{titulo}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {encabezado}
            {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
            {filas.length === 0 && <DialogContentText>{t('usuarios.sinRoles')}</DialogContentText>}
            {filas.map((fila, indice) => (
              <Stack key={indice} direction="row" spacing={1} alignItems="center">
                <TextField
                  select
                  size="small"
                  label={t('usuarios.rol')}
                  value={fila.rol}
                  onChange={(e) => cambiar(indice, { rol: e.target.value as FilaRol['rol'] })}
                  sx={{ flex: 1 }}
                >
                  {ROLES.map((r) => (
                    <MenuItem key={r} value={r}>
                      {t(`usuarios.roles.${r}`)}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  size="small"
                  label={t('usuarios.alcance')}
                  value={fila.alcance}
                  onChange={(e) => cambiar(indice, { alcance: e.target.value })}
                  sx={{ flex: 1.4 }}
                >
                  <MenuItem value={ALCANCE_TENANT}>{t('comun.todoElTenant')}</MenuItem>
                  {empresas.map((e) => (
                    <MenuItem key={e.id} value={e.id}>
                      {e.nombre}
                    </MenuItem>
                  ))}
                </TextField>
                <Tooltip title={t('usuarios.quitarRol')}>
                  <IconButton aria-label={t('usuarios.quitarRol')} onClick={() => setFilas((a) => a.filter((_, i) => i !== indice))}>
                    <X size={16} />
                  </IconButton>
                </Tooltip>
              </Stack>
            ))}
            <Button
              startIcon={<Plus size={16} />}
              onClick={() => setFilas((a) => [...a, { rol: 'Revisor', alcance: empresas[0]?.id ?? ALCANCE_TENANT }])}
              sx={{ alignSelf: 'flex-start' }}
            >
              {t('usuarios.agregarRol')}
            </Button>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onCerrar} disabled={guardando}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={guardando}>
            {t('comun.guardar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
