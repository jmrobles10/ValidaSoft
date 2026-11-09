import { Box, Button, Chip, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type ApiKeyDto } from '@validasoft/api-client';
import { EstadoVacio, FilasEsqueleto, formatearFecha, MarcoTabla, visualmenteOculto } from '@validasoft/ui';
import { Ban, CalendarX, KeyRound, Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { EstadoActivo } from '../EstadoActivo';
import { useApiKeys, useRevocarApiKey, useSistemas } from './api';
import { DialogoApiKey } from './DialogoApiKey';
import { DialogoClaveCreada } from './DialogoClaveCreada';
import { claveVisible, estadoApiKey, textoAlcance } from './modelo';
import { ResumenPermisos } from './ResumenApiKey';

const COLUMNAS = 8;

/** API keys del grupo (docs/07 §4): quién llama a la API, con qué permisos y en qué empresas. */
export function PaginaApiKeys() {
  const { t } = useTranslation();
  const { empresa, empresas } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.apiKeysAdministrar);
  const veSistemas = permisos.puede(SCOPES.sistemasAdministrar);
  const consulta = useApiKeys(puede);
  const sistemas = useSistemas(puede && veSistemas);
  const revocar = useRevocarApiKey();
  const mostrar = useAvisos((s) => s.mostrar);
  const [creando, setCreando] = useState<string | null>(null);
  const [creada, setCreada] = useState<{ llave: ApiKeyDto; valor: string } | null>(null);
  const [porRevocar, setPorRevocar] = useState<ApiKeyDto | null>(null);

  if (!puede) return <SinPermiso seccion={t('nav.api-keys')} />;

  const ahora = new Date();
  const llaves = [...(consulta.data ?? [])].sort((a, b) => b.creadaEn.localeCompare(a.creadaEn));
  const nombreSistema = (id: string | null | undefined) => (id ? (sistemas.data?.find((s) => s.id === id)?.codigo ?? '—') : '—');

  const confirmarRevocar = (llave: ApiKeyDto) =>
    revocar.mutate(llave.id, {
      onSuccess: () => mostrar(t('apiKeys.revocada', { nombre: llave.nombre })),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setPorRevocar(null),
    });

  return (
    <>
      <EncabezadoPagina
        titulo={t('apiKeys.titulo')}
        subtitulo={t('apiKeys.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setCreando(crypto.randomUUID())}>
            {t('apiKeys.nueva')}
          </Button>
        }
      />
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('apiKeys.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('apiKeys.columnas.nombre')}</TableCell>
                <TableCell sx={{ width: 120 }}>{t('apiKeys.columnas.clave')}</TableCell>
                <TableCell>{t('apiKeys.columnas.permisos')}</TableCell>
                <TableCell>{t('apiKeys.columnas.alcance')}</TableCell>
                <TableCell sx={{ width: 90 }}>{t('apiKeys.columnas.sistema')}</TableCell>
                <TableCell sx={{ width: 110 }}>{t('apiKeys.columnas.expira')}</TableCell>
                <TableCell sx={{ width: 100 }}>{t('apiKeys.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 96 }}>
                  <Box component="span" sx={visualmenteOculto}>
                    {t('comun.acciones')}
                  </Box>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={COLUMNAS} filas={3} />}
              {consulta.data && llaves.length === 0 && (
                <TableRow>
                  <TableCell colSpan={COLUMNAS} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={KeyRound} titulo={t('apiKeys.vacio')} texto={t('apiKeys.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {llaves.map((k) => {
                const estado = estadoApiKey(k, ahora);
                return (
                  <TableRow key={k.id}>
                    <TableCell>
                      <Stack direction="row" spacing={0.75} alignItems="center" sx={{ minWidth: 0 }}>
                        <Typography component="span" noWrap sx={{ fontWeight: 500, fontSize: 14 }}>
                          {k.nombre}
                        </Typography>
                        {k.sandbox && <Chip size="small" variant="outlined" label={t('apiKeys.sandbox')} />}
                      </Stack>
                    </TableCell>
                    <TableCell>
                      <Typography variant="monoTabla">{claveVisible(k)}</Typography>
                    </TableCell>
                    <TableCell>
                      <ResumenPermisos scopes={k.scopes} />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{textoAlcance(k, empresas, t('comun.todoElTenant'))}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="monoTabla">{nombreSistema(k.sistemaId)}</Typography>
                    </TableCell>
                    <TableCell>
                      {k.expiraEn ? <Typography variant="monoTabla">{formatearFecha(k.expiraEn)}</Typography> : <Typography variant="body2" color="text.secondary">{t('apiKeys.noExpira')}</Typography>}
                    </TableCell>
                    <TableCell>
                      <EstadoActivo activo={estado === 'vigente'} texto={t(`apiKeys.estados.${estado}`)} icono={estado === 'vencida' ? CalendarX : undefined} />
                    </TableCell>
                    <TableCell align="right">
                      <Button variant="text" size="small" onClick={() => setPorRevocar(k)} aria-label={t('apiKeys.revocarA', { nombre: k.nombre })} sx={{ color: 'estado.error.texto' }}>
                        {t('apiKeys.revocar')}
                      </Button>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {creando && (
        <DialogoApiKey
          claveIdempotencia={creando}
          empresas={empresas.filter((e) => e.activa)}
          sistemas={veSistemas ? (sistemas.data ?? []).filter((s) => s.activo) : null}
          onCerrar={() => setCreando(null)}
          onCreada={(llave) => {
            setCreando(null);
            if (llave.valor) setCreada({ llave, valor: llave.valor });
            else mostrar(t('apiKeys.creadaSinValor'), 'warning');
          }}
        />
      )}
      {creada && <DialogoClaveCreada llave={creada.llave} valor={creada.valor} empresas={empresas} onCerrar={() => setCreada(null)} />}
      {porRevocar && (
        <DialogoConfirmacion
          titulo={t('apiKeys.revocarA', { nombre: porRevocar.nombre })}
          subtitulo={claveVisible(porRevocar)}
          mensaje={t('apiKeys.confirmarRevocar')}
          confirmar={t('apiKeys.revocar')}
          icono={<Ban size={16} aria-hidden />}
          ocupado={revocar.isPending}
          onConfirmar={() => confirmarRevocar(porRevocar)}
          onCerrar={() => setPorRevocar(null)}
        />
      )}
    </>
  );
}
