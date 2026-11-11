import { Box, Chip, Skeleton, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { esErrorApi, getObtenerMiCuentaQueryOptions, type MiCuentaDto } from '@validasoft/api-client';
import { Dato, DosColumnas, PaginaEstado, RejillaDatos } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { UserX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useEmpresasPorId } from './useEmpresasPorId';
import { TarjetaPasskeys } from './TarjetaPasskeys';
import { TarjetaSesiones } from './TarjetaSesiones';

function EsqueletoDeCuenta() {
  const { t } = useTranslation();
  return (
    <Stack spacing={2} role="status" aria-busy aria-label={t('comun.cargando')}>
      <Skeleton variant="rounded" width={240} height={28} />
      <Skeleton variant="rounded" height={140} />
      <Skeleton variant="rounded" height={180} />
    </Stack>
  );
}

/** Cómo entró la persona (`Codigo`, `Passkey` o `CodigoYPasskey`); un método nuevo de la API se muestra tal cual. */
function textoDeMetodo(metodo: string, t: TFunction): string {
  switch (metodo) {
    case 'Codigo':
      return t('cuenta.metodos.Codigo');
    case 'Passkey':
      return t('cuenta.metodos.Passkey');
    case 'CodigoYPasskey':
      return t('cuenta.metodos.CodigoYPasskey');
    default:
      return metodo;
  }
}

function TarjetaPerfil({ cuenta }: { cuenta: MiCuentaDto }) {
  const { t } = useTranslation();
  const empresas = useEmpresasPorId(cuenta.tipo !== 'Operador');
  const roles = cuenta.roles.map((r) =>
    r.empresaId ? t('cuenta.rolEn', { rol: t(`usuarios.roles.${r.rol}`), empresa: empresas.get(r.empresaId) ?? '—' }) : t('cuenta.rolEnTodoElGrupo', { rol: t(`usuarios.roles.${r.rol}`) }),
  );
  return (
    <Tarjeta titulo={t('cuenta.perfil')}>
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('cuenta.correo')}>{cuenta.correo}</Dato>
        <Dato etiqueta={t('cuenta.grupo')}>{cuenta.grupo ?? t('cuenta.grupoPlataforma')}</Dato>
        <Dato etiqueta={t('cuenta.entraste')}>{textoDeMetodo(cuenta.metodo, t)}</Dato>
        <Dato etiqueta={t('cuenta.roles')}>
          {cuenta.tipo === 'Operador' ? (
            t('cuenta.grupoPlataforma')
          ) : roles.length === 0 ? (
            <Typography component="span" sx={{ color: 'text.secondary' }}>
              {t('cuenta.sinRoles')}
            </Typography>
          ) : (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
              {roles.map((rol) => (
                <Chip key={rol} size="small" variant="outlined" label={rol} />
              ))}
            </Box>
          )}
        </Dato>
      </RejillaDatos>
    </Tarjeta>
  );
}

/** Mi cuenta (ING-3): quién soy, cómo entré, mis passkeys y mis sesiones abiertas. Solo para quienes entraron directo a Validasoft. */
export function PaginaMiCuenta() {
  const { t } = useTranslation();
  const consulta = useQuery(getObtenerMiCuentaQueryOptions({ request: { empresa: null } }));

  if (consulta.isError && esErrorApi(consulta.error) && consulta.error.codigo === 'solo-ingreso-directo')
    return <PaginaEstado icono={UserX} titulo={t('cuenta.soloDirectoTitulo')} texto={t('cuenta.soloDirecto')} />;

  return (
    <>
      <EncabezadoPagina titulo={consulta.data ? consulta.data.nombre : t('cuenta.titulo')} subtitulo={t('cuenta.subtitulo')} />
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <EsqueletoDeCuenta />}
      {consulta.data && (
        <DosColumnas
          izquierda={
            <>
              <TarjetaPerfil cuenta={consulta.data} />
              <TarjetaSesiones />
            </>
          }
          derecha={<TarjetaPasskeys exigePasskey={consulta.data.exigePasskey} />}
        />
      )}
    </>
  );
}
