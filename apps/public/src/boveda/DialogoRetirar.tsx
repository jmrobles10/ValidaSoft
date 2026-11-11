import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material';
import type { ImpactoDeRetiroDto, VersionDocumentoDto } from '@validasoft/api-client';
import { Upload } from 'lucide-react';
import { useState, type ReactNode, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { useImpactoDeRetiro, useRetirarVersion } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { mensajeDeError } from '../shared/errores';
import { empresasOrdenadas, hayEmpresasAfectadas, lePesa, nombreDeSeccion, textoDeEmpresa, textoDeVersionVigente } from './impacto';

interface Props {
  documentoId: string;
  tipo: string;
  version: VersionDocumentoDto;
  /** Enlace para subir una versión corregida del documento; nulo si el tipo no se sube desde la bóveda (un formulario). */
  enlaceSubir: string | null;
  onCerrar: () => void;
}

/**
 * Retirar una versión (BO-8). Antes de pedir el motivo cuenta lo que va a pasar (RT-1): qué versión queda vigente, qué empresas
 * la recibieron, aprobaron o revisan y qué les toca, y qué secciones de su información la usan de soporte. Si a alguna empresa le
 * toca revisar de nuevo, ofrece «Subir una versión corregida» como alternativa.
 */
export function DialogoRetirar({ documentoId, tipo, version, enlaceSubir, onCerrar }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const impacto = useImpactoDeRetiro(documentoId, version.n);
  const retiro = useRetirarVersion(documentoId, version.n);
  const [motivo, setMotivo] = useState('');
  const [tocado, setTocado] = useState(false);
  const falta = !motivo.trim();
  const afectadas = impacto.data ? hayEmpresasAfectadas(impacto.data) : false;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (falta || !impacto.data) return;
    retiro.mutate(motivo.trim(), {
      onSuccess: (r) => {
        mostrar(r.binarioEliminado ? t('boveda.retiro.exitoBorrado', { n: String(version.n) }) : t('boveda.retiro.exito', { n: String(version.n) }));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={retiro.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-retiro">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-retiro">{t('boveda.retiro.titulo', { n: String(version.n), tipo })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {impacto.isPending && (
              <Stack spacing={1} aria-busy>
                <Skeleton variant="rounded" height={22} width="70%" />
                <Skeleton variant="rounded" height={64} />
              </Stack>
            )}
            {impacto.isError && (
              <Alert
                severity="error"
                action={
                  <Button color="inherit" size="small" onClick={() => void impacto.refetch()}>
                    {t('comun.reintentar')}
                  </Button>
                }
              >
                {t('boveda.retiro.errorImpacto')} {mensajeDeError(impacto.error, t)}
              </Alert>
            )}
            {impacto.data && <ImpactoDelRetiro impacto={impacto.data} tipo={tipo} />}
            {afectadas && (
              <Alert severity="warning" icon={false} sx={{ alignItems: 'center' }}>
                <Stack spacing={1}>
                  <Typography sx={{ fontSize: 14 }}>
                    {t('boveda.retiro.avisaremos')} {t('boveda.retiro.alternativa')}
                  </Typography>
                  {enlaceSubir && (
                    <Box>
                      <Button component={RouterLink} to={enlaceSubir} size="small" variant="outlined" startIcon={<Upload size={16} aria-hidden />}>
                        {t('boveda.retiro.subirCorregida')}
                      </Button>
                    </Box>
                  )}
                </Stack>
              </Alert>
            )}
            {retiro.isError && <Alert severity="error">{mensajeDeError(retiro.error, t)}</Alert>}
            {impacto.data && (
              <TextField
                label={t('boveda.retiro.motivo')}
                value={motivo}
                onChange={(e) => setMotivo(e.target.value.slice(0, 500))}
                required
                multiline
                minRows={2}
                onBlur={() => setTocado(true)}
                error={tocado && falta}
                helperText={tocado && falta ? t('boveda.retiro.requerido') : afectadas ? t('boveda.retiro.motivoAyudaEmpresas') : t('boveda.retiro.motivoAyuda')}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={retiro.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={retiro.isPending || falta || !impacto.data}>
            {t('boveda.retiro.confirmar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

/** Lo que va a pasar, en bloques separados: la versión que queda, las empresas y las secciones de la ficha. */
function ImpactoDelRetiro({ impacto, tipo }: { impacto: ImpactoDeRetiroDto; tipo: string }) {
  const { t } = useTranslation();
  const empresas = empresasOrdenadas(impacto);
  const sinNadie = empresas.length === 0 && impacto.secciones.length === 0;
  const soloSinCambios = empresas.length > 0 && !empresas.some(lePesa) && impacto.secciones.length === 0 && !impacto.esLaVigente;

  return (
    <Stack spacing={1.5}>
      <Typography sx={{ fontWeight: 600 }}>{textoDeVersionVigente(impacto, tipo, t)}</Typography>
      {sinNadie && <Typography sx={{ color: 'text.secondary' }}>{t('boveda.retiro.nadie')}</Typography>}
      {soloSinCambios && <Typography sx={{ color: 'text.secondary' }}>{t('boveda.retiro.sinAfectadas')}</Typography>}
      {empresas.length > 0 && (
        <Bloque titulo={t('boveda.retiro.empresas')}>
          {empresas.map((e) => (
            <Fila key={e.vinculoId} enfasis={lePesa(e)}>
              {textoDeEmpresa(e, impacto, tipo, t)}
            </Fila>
          ))}
        </Bloque>
      )}
      {impacto.secciones.length > 0 && (
        <Bloque titulo={t('boveda.retiro.secciones')}>
          {impacto.secciones.map((s) => (
            <Fila key={`${s.seccion}-${String(s.periodo)}`} enfasis>
              {t('boveda.retiro.seccion', { seccion: nombreDeSeccion(s, t) })}
            </Fila>
          ))}
        </Bloque>
      )}
    </Stack>
  );
}

/** Un bloque con borde y radio de las tarjetas, con su título y sus filas separadas. */
function Bloque({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <Box sx={(th) => ({ border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.md)}px`, overflow: 'hidden' })}>
      <Typography component="h3" sx={{ px: 1.5, py: 0.75, bgcolor: 'superficie.nivel2', fontWeight: 600, fontSize: 13 }}>
        {titulo}
      </Typography>
      <Box component="ul" sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {children}
      </Box>
    </Box>
  );
}

function Fila({ enfasis, children }: { enfasis: boolean; children: ReactNode }) {
  return (
    <Typography component="li" sx={{ px: 1.5, py: 1, borderTop: 1, borderColor: 'divider', fontSize: 14, color: enfasis ? 'text.primary' : 'text.secondary' }}>
      {children}
    </Typography>
  );
}
