import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { SCOPES, type DominioDeCorreoDto } from '@validasoft/api-client';
import { CabeceraEsqueleto, ChipEstado, DosColumnas, EsqueletoFormulario, EsqueletoTarjeta, FaltaParaEnviar, formatearFechaHora } from '@validasoft/ui';
import { MailCheck, RefreshCw, Trash2 } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, EncabezadoPagina, ErrorRecuperable, IconoCarga, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useDominioDeCorreo, useFijarDominio, useQuitarDominio, useVerificarDominio } from './api';
import {
  borradorDesdeDominio,
  chipDeDominio,
  cuerpoDominio,
  direccionDeVistaPrevia,
  estadoDelDominio,
  hayCambiosDeDominio,
  LONGITUD_MAXIMA_NOMBRE,
  PARTE_LOCAL_POR_DEFECTO,
  registrosPendientes,
  validarDominio,
  type BorradorDominio,
  type ErrorDominio,
} from './modelo';
import { RegistrosDns } from './RegistrosDns';

/** Lo que se ve de cómo sale hoy el correo: el remitente en uso y, mientras el dominio no esté verificado, el aviso de que sale el de la plataforma. */
function RemitenteEnUso({ dominio, empresa }: { dominio: DominioDeCorreoDto; empresa: string }) {
  const { t } = useTranslation();
  const estado = estadoDelDominio(dominio);
  return (
    <Tarjeta titulo={t('dominioCorreo.comoSale')}>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>{t('dominioCorreo.remitenteHoy', { empresa })}</Typography>
      <Typography variant="mono" component="p" sx={{ m: 0, mb: 1.25, fontSize: 14, lineHeight: '20px', overflowWrap: 'anywhere' }}>
        {dominio.remitenteEnUso}
      </Typography>
      {estado === 'Verificado' ? (
        <Typography sx={{ fontSize: 13, lineHeight: '18px', color: 'text.secondary' }}>{t('dominioCorreo.propioActivo')}</Typography>
      ) : (
        <Alert severity="info">{t('dominioCorreo.respaldo', { plataforma: dominio.remitenteDeLaPlataforma })}</Alert>
      )}
    </Tarjeta>
  );
}

function Formulario({ empresaId, empresa, dominio }: { empresaId: string; empresa: string; dominio: DominioDeCorreoDto }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const fijar = useFijarDominio(empresaId);
  const verificar = useVerificarDominio(empresaId);
  const quitar = useQuitarDominio(empresaId);
  const [b, setB] = useState<BorradorDominio>(() => borradorDesdeDominio(dominio));
  const [tocado, setTocado] = useState(false);
  const [quitando, setQuitando] = useState(false);
  const errores = validarDominio(b);
  const cambios = hayCambiosDeDominio(b, dominio);
  const estado = estadoDelDominio(dominio);
  const vistaPrevia = direccionDeVistaPrevia(b);
  const cambiar = (parcial: Partial<BorradorDominio>) => setB((p) => ({ ...p, ...parcial }));
  const mensajeDeDominio = (e: ErrorDominio) => (e === 'nombre-invalido' ? t('dominioCorreo.errores.nombre-invalido', { maximo: String(LONGITUD_MAXIMA_NOMBRE) }) : t(`dominioCorreo.errores.${e}`));

  const guardar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setTocado(true);
    if (errores.length > 0 || !cambios) return;
    fijar.mutate(cuerpoDominio(b), {
      onSuccess: (guardado) => {
        mostrar(t('dominioCorreo.guardado'));
        setB(borradorDesdeDominio(guardado));
        setTocado(false);
      },
    });
  };

  return (
    <>
      <EncabezadoPagina
        titulo={t('dominioCorreo.titulo')}
        subtitulo={t('dominioCorreo.subtitulo', { empresa })}
        acciones={
          estado !== 'sin-dominio' && (
            <Button variant="text" color="error" size="small" startIcon={<Trash2 size={14} aria-hidden />} onClick={() => setQuitando(true)}>
              {t('dominioCorreo.quitar')}
            </Button>
          )
        }
      />
      {dominio.simulado && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          {t('dominioCorreo.simulado')}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <Tarjeta titulo={t('dominioCorreo.dominio')}>
              <Box component="form" onSubmit={guardar} noValidate>
                <Stack spacing={2}>
                  <TextField
                    label={t('dominioCorreo.campoDominio')}
                    value={b.dominio}
                    onChange={(e) => cambiar({ dominio: e.target.value })}
                    placeholder="proveedores.miempresa.com"
                    error={tocado && errores.includes('dominio-invalido')}
                    helperText={tocado && errores.includes('dominio-invalido') ? mensajeDeDominio('dominio-invalido') : t('dominioCorreo.campoDominioAyuda')}
                    slotProps={{ htmlInput: { autoComplete: 'off', spellCheck: false, autoCapitalize: 'none' } }}
                  />
                  <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>
                    <TextField
                      label={t('dominioCorreo.campoParteLocal')}
                      value={b.parteLocal}
                      onChange={(e) => cambiar({ parteLocal: e.target.value })}
                      placeholder={PARTE_LOCAL_POR_DEFECTO}
                      error={tocado && errores.includes('parte-local-invalida')}
                      helperText={tocado && errores.includes('parte-local-invalida') ? mensajeDeDominio('parte-local-invalida') : t('dominioCorreo.campoParteLocalAyuda')}
                      slotProps={{ htmlInput: { autoComplete: 'off', spellCheck: false, autoCapitalize: 'none' } }}
                    />
                    <TextField
                      label={t('dominioCorreo.campoNombre')}
                      value={b.nombre}
                      onChange={(e) => cambiar({ nombre: e.target.value })}
                      placeholder={empresa}
                      error={tocado && errores.includes('nombre-invalido')}
                      helperText={tocado && errores.includes('nombre-invalido') ? mensajeDeDominio('nombre-invalido') : t('dominioCorreo.campoNombreAyuda')}
                      slotProps={{ htmlInput: { autoComplete: 'off', maxLength: LONGITUD_MAXIMA_NOMBRE + 20 } }}
                    />
                  </Box>
                  {vistaPrevia && cambios && (
                    <Typography sx={{ fontSize: 13, color: 'text.secondary' }}>
                      {t('dominioCorreo.saldraDe')}{' '}
                      <Typography variant="mono" component="span" sx={{ fontSize: 13, color: 'text.primary', overflowWrap: 'anywhere' }}>
                        {b.nombre.trim() || empresa} &lt;{vistaPrevia}&gt;
                      </Typography>
                    </Typography>
                  )}
                  {fijar.isError && <Alert severity="error">{mensajeDeError(fijar.error, t)}</Alert>}
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', justifyContent: 'flex-end' }}>
                    {tocado && errores[0] && <FaltaParaEnviar>{mensajeDeDominio(errores[0])}</FaltaParaEnviar>}
                    {cambios && estado !== 'sin-dominio' && <Typography sx={{ fontSize: 12, color: 'text.secondary' }}>{t('dominioCorreo.cambiarReinicia')}</Typography>}
                    <Button type="submit" variant="contained" disabled={fijar.isPending || !cambios} startIcon={fijar.isPending ? <IconoCarga /> : undefined}>
                      {estado === 'sin-dominio' ? t('dominioCorreo.registrar') : t('dominioCorreo.guardar')}
                    </Button>
                  </Stack>
                </Stack>
              </Box>
            </Tarjeta>

            {estado !== 'sin-dominio' && (
              <Tarjeta
                titulo={t('dominioCorreo.registros')}
                acciones={<ChipEstado estado={chipDeDominio(estado)} etiqueta={t(`dominioCorreo.estados.${estado}`)} />}
              >
                <Typography sx={{ fontSize: 14, lineHeight: '20px', color: 'text.secondary', mb: 1.25 }}>{t('dominioCorreo.registrosAyuda', { dominio: dominio.dominio ?? '' })}</Typography>
                <RegistrosDns dominio={dominio} />
                <Stack direction="row" spacing={1.5} useFlexGap sx={{ mt: 1.5, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'space-between' }}>
                  <Typography sx={{ fontSize: 12, lineHeight: '16px', color: 'text.secondary' }}>
                    {dominio.ultimaVerificacionEn
                      ? t('dominioCorreo.ultimaVerificacion', { fecha: formatearFechaHora(dominio.ultimaVerificacionEn), count: registrosPendientes(dominio) })
                      : t('dominioCorreo.sinVerificar')}
                  </Typography>
                  <Button
                    variant="outlined"
                    size="small"
                    onClick={() => verificar.mutate(undefined, { onSuccess: (r) => mostrar(r.estado === 'Verificado' ? t('dominioCorreo.verificado') : t('dominioCorreo.aunFaltan')) })}
                    disabled={verificar.isPending}
                    startIcon={verificar.isPending ? <IconoCarga /> : <RefreshCw size={14} aria-hidden />}
                  >
                    {t('dominioCorreo.verificar')}
                  </Button>
                </Stack>
                {verificar.isError && (
                  <Alert severity="error" sx={{ mt: 1.5 }}>
                    {mensajeDeError(verificar.error, t)}
                  </Alert>
                )}
              </Tarjeta>
            )}
          </>
        }
        derecha={
          <>
            <RemitenteEnUso dominio={dominio} empresa={empresa} />
            <Tarjeta titulo={t('dominioCorreo.paraQue')}>
              <Stack component="ul" spacing={0.75} sx={{ m: 0, pl: 2.25, fontSize: 14, lineHeight: '20px' }}>
                {(['spf', 'dkim', 'dmarc'] as const).map((clave) => (
                  <li key={clave}>{t(`dominioCorreo.explicacion.${clave}`)}</li>
                ))}
              </Stack>
            </Tarjeta>
          </>
        }
      />
      {quitando && (
        <DialogoConfirmacion
          titulo={t('dominioCorreo.quitarTitulo', { dominio: dominio.dominio ?? '' })}
          mensaje={t('dominioCorreo.quitarTexto', { empresa, plataforma: dominio.remitenteDeLaPlataforma })}
          confirmar={t('dominioCorreo.quitar')}
          icono={<MailCheck size={16} aria-hidden />}
          ocupado={quitar.isPending}
          onConfirmar={() =>
            quitar.mutate(undefined, {
              onSuccess: () => {
                mostrar(t('dominioCorreo.quitado'));
                setQuitando(false);
                setB({ dominio: '', parteLocal: '', nombre: '' });
              },
              onError: () => setQuitando(false),
            })
          }
          onCerrar={() => setQuitando(false)}
        />
      )}
    </>
  );
}

/**
 * Dominio de correo propio de la empresa (OD-18…OD-22): el remitente desde el que sale su correo, los registros DNS (SPF, DKIM y
 * DMARC) que hay que crear y su verificación. Mientras no esté verificado, el correo sale con el remitente de la plataforma.
 */
export function PaginaDominioCorreo() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.empresasAdministrar);
  const consulta = useDominioDeCorreo(empresa.id, puede);

  if (!puede) return <SinPermiso seccion={t('nav.dominio-correo')} />;
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  if (!consulta.data) {
    return (
      <>
        <CabeceraEsqueleto />
        <DosColumnas izquierda={<EsqueletoFormulario campos={3} conTarjeta />} derecha={<EsqueletoTarjeta lineas={4} />} />
      </>
    );
  }

  // Al guardar o quitar, el formulario se rehace con lo que devolvió el servidor.
  return <Formulario key={`${empresa.id}:${consulta.data.dominio ?? ''}`} empresaId={empresa.id} empresa={empresa.nombre} dominio={consulta.data} />;
}
