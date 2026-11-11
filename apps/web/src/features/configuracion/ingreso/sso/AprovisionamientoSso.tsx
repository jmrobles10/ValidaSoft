import { Box, FormControlLabel, MenuItem, Stack, Switch, TextField, Typography } from '@mui/material';
import type { EmpresaDto } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../../shared/componentes';
import { ReglasDeGrupos, RolesPorOmision } from './ReglasSso';
import type { FormularioSso, ModoSso } from './modeloSso';

interface Props {
  formulario: FormularioSso;
  alCambiar: (cambios: Partial<FormularioSso>) => void;
}

/** Un interruptor con su explicación debajo (misma forma que «Passkey para todos»). */
function Interruptor({ marcado, titulo, ayuda, alCambiar }: { marcado: boolean; titulo: ReactNode; ayuda: string; alCambiar: (valor: boolean) => void }) {
  return (
    <Box>
      <FormControlLabel control={<Switch checked={marcado} onChange={(e) => alCambiar(e.target.checked)} />} label={<Typography sx={{ fontWeight: 600 }}>{titulo}</Typography>} />
      <Typography variant="body2" sx={{ color: 'text.secondary', ml: 6 }}>
        {ayuda}
      </Typography>
    </Box>
  );
}

/** Cuándo se usa el SSO: encendido y convivencia con el código del correo (PI-5). */
export function AccesoSso({ formulario, alCambiar, tieneDominioVerificado }: Props & { tieneDominioVerificado: boolean }) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('sso.acceso.titulo')}>
      <Stack spacing={2}>
        <Interruptor marcado={formulario.activo} titulo={t('sso.acceso.activar')} ayuda={tieneDominioVerificado ? t('sso.acceso.activarAyuda') : t('sso.acceso.activarSinDominio')} alCambiar={(activo) => alCambiar({ activo })} />
        <TextField
          select
          size="small"
          label={t('sso.acceso.modo')}
          value={formulario.modo}
          onChange={(e) => alCambiar({ modo: e.target.value === 'Exclusivo' ? 'Exclusivo' : 'Complementario' })}
          helperText={t(`sso.acceso.modosAyuda.${formulario.modo}`)}
        >
          {(['Complementario', 'Exclusivo'] satisfies ModoSso[]).map((m) => (
            <MenuItem key={m} value={m}>
              {t(`sso.acceso.modos.${m}`)}
            </MenuItem>
          ))}
        </TextField>
      </Stack>
    </Tarjeta>
  );
}

/** Quién se crea y con qué roles (PI-4): primer ingreso, roles por omisión, reglas de grupos del proveedor y sincronización. */
export function AprovisionamientoSso({ formulario, empresas, alCambiar }: Props & { empresas: readonly EmpresaDto[] }) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('sso.aprovisionamiento.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('sso.aprovisionamiento.ayuda')}
      </Typography>
      <Stack spacing={2.5}>
        <Interruptor marcado={formulario.aprovisionamientoJit} titulo={t('sso.aprovisionamiento.jit')} ayuda={t('sso.aprovisionamiento.jitAyuda')} alCambiar={(aprovisionamientoJit) => alCambiar({ aprovisionamientoJit })} />
        <Stack spacing={1}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {t('sso.aprovisionamiento.rolesPorOmision')}
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('sso.aprovisionamiento.rolesPorOmisionAyuda')}
          </Typography>
          <RolesPorOmision filas={formulario.rolesPorOmision} empresas={empresas} alCambiar={(rolesPorOmision) => alCambiar({ rolesPorOmision })} />
        </Stack>
        <Stack spacing={1}>
          <Typography variant="body2" sx={{ fontWeight: 600 }}>
            {t('sso.aprovisionamiento.reglas')}
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('sso.aprovisionamiento.reglasAyuda')}
          </Typography>
          <TextField
            size="small"
            label={t('sso.aprovisionamiento.reclamo')}
            helperText={t('sso.aprovisionamiento.reclamoAyuda')}
            value={formulario.reclamoDeGrupos}
            onChange={(e) => alCambiar({ reclamoDeGrupos: e.target.value })}
            sx={{ maxWidth: 320 }}
            slotProps={{ htmlInput: { maxLength: 100 } }}
          />
          <ReglasDeGrupos filas={formulario.reglas} empresas={empresas} alCambiar={(reglas) => alCambiar({ reglas })} />
        </Stack>
        <Interruptor marcado={formulario.sincronizarRoles} titulo={t('sso.aprovisionamiento.sincronizar')} ayuda={t('sso.aprovisionamiento.sincronizarAyuda')} alCambiar={(sincronizarRoles) => alCambiar({ sincronizarRoles })} />
      </Stack>
    </Tarjeta>
  );
}
