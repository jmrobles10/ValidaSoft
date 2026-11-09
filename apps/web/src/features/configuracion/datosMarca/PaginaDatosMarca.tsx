import { Box, Button, Stack, TextField, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarEmpresa, SCOPES } from '@validasoft/api-client';
import { Dato, derivarPaletaEmpresa, RejillaDatos } from '@validasoft/ui';
import { ImageUp, Trash2 } from 'lucide-react';
import { useState, type ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { borradorDe, cuerpoActualizar, errorDeLogo, hayCambios, leerComoDataUri, validarMarca, type BorradorMarca } from './modelo';

/**
 * Datos y marca de la empresa activa (P2ConfigEmpresa, CF-1): la identificación y el prefijo se ven pero no cambian; el
 * contacto de soporte, el color y el logo son lo que el tercero ve en su portal y en los correos.
 */
export function PaginaDatosMarca() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.empresasAdministrar);
  const original = borradorDe(empresa);
  const [borrador, setBorrador] = useState<BorradorMarca>(original);
  const [errorLogo, setErrorLogo] = useState<string | null>(null);
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useMutation({
    mutationFn: () => actualizarEmpresa(empresa.id, cuerpoActualizar(original, borrador), { empresa: null }),
    onSuccess: () => mostrar(t('datosMarca.guardado')),
    onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
    onSettled: () => invalidarRuta(cliente, '/api/v1/empresas'),
  });

  if (!puede) return <SinPermiso seccion={t('nav.datos-marca')} />;

  const errores = validarMarca(borrador);
  const cambiar = (cambio: Partial<BorradorMarca>) => setBorrador((b) => ({ ...b, ...cambio }));
  const color = /^#[0-9A-Fa-f]{6}$/.test(borrador.colorMarca.trim()) ? borrador.colorMarca.trim() : null;
  const paleta = color ? derivarPaletaEmpresa(color, 'light') : null;

  const alElegirLogo = async (evento: ChangeEvent<HTMLInputElement>) => {
    const archivo = evento.target.files?.[0];
    evento.target.value = '';
    if (!archivo) return;
    const error = errorDeLogo(archivo);
    setErrorLogo(error ? t(`datosMarca.errores.${error}`) : null);
    if (!error) cambiar({ logoUrl: await leerComoDataUri(archivo) });
  };

  return (
    <>
      <EncabezadoPagina
        titulo={empresa.nombre}
        subtitulo={t('datosMarca.ayuda')}
        acciones={
          <Button variant="contained" disabled={!hayCambios(original, borrador) || errores.length > 0 || guardar.isPending} onClick={() => guardar.mutate()}>
            {t('datosMarca.guardar')}
          </Button>
        }
      />
      <Stack spacing={2}>
        <Tarjeta titulo={t('datosMarca.datos')}>
          <RejillaDatos>
            <Dato etiqueta={t('datosMarca.razonSocial')}>{empresa.nombre}</Dato>
            <Dato etiqueta={t('datosMarca.identificacion')}>
              <Typography component="span" variant="mono">{`${empresa.identificacion.tipo} ${empresa.identificacion.numero}`}</Typography>
            </Dato>
            <Dato etiqueta={t('datosMarca.prefijo')}>
              <Typography component="span" variant="mono">{empresa.prefijoFolio}</Typography>
            </Dato>
          </RejillaDatos>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            {t('datosMarca.prefijoAyuda')}
          </Typography>
          <TextField
            label={t('datosMarca.contactoSoporte')}
            value={borrador.contactoSoporte}
            onChange={(e) => cambiar({ contactoSoporte: e.target.value })}
            error={errores.includes('contacto-soporte-invalido')}
            helperText={errores.includes('contacto-soporte-invalido') ? t('datosMarca.errores.contacto-soporte-invalido') : t('datosMarca.contactoAyuda')}
            type="email"
            size="small"
            fullWidth
            sx={{ mt: 2, maxWidth: 420 }}
          />
        </Tarjeta>

        <Tarjeta titulo={t('datosMarca.marca')}>
          <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'minmax(0,1fr) 280px' }, gap: 2, alignItems: 'start' }}>
            <Stack spacing={2}>
              <Stack direction="row" spacing={1.5} alignItems="center">
                <Box
                  sx={(th) => ({ width: 48, height: 48, flex: 'none', display: 'grid', placeItems: 'center', borderRadius: `${String(th.radio.sm)}px`, border: 1, borderColor: 'divider', bgcolor: 'superficie.nivel2', overflow: 'hidden' })}
                >
                  {borrador.logoUrl ? <Box component="img" src={borrador.logoUrl} alt={t('datosMarca.logoAlt', { empresa: empresa.nombre })} sx={{ maxWidth: '100%', maxHeight: '100%' }} /> : null}
                </Box>
                <Button component="label" variant="outlined" size="small" startIcon={<ImageUp size={16} aria-hidden />}>
                  {t('datosMarca.cambiarLogo')}
                  <input hidden type="file" accept="image/svg+xml,image/png" onChange={(e) => void alElegirLogo(e)} data-testid="entrada-logo" />
                </Button>
                {borrador.logoUrl && (
                  <Button variant="text" size="small" startIcon={<Trash2 size={16} aria-hidden />} onClick={() => cambiar({ logoUrl: '' })}>
                    {t('datosMarca.quitarLogo')}
                  </Button>
                )}
              </Stack>
              <Typography variant="body2" color={errorLogo ? 'error' : 'text.secondary'} role={errorLogo ? 'alert' : undefined}>
                {errorLogo ?? t('datosMarca.logoAyuda')}
              </Typography>
              <TextField
                label={t('datosMarca.color')}
                value={borrador.colorMarca}
                onChange={(e) => cambiar({ colorMarca: e.target.value })}
                placeholder="#E07A1F"
                error={errores.includes('color-marca-invalido')}
                helperText={errores.includes('color-marca-invalido') ? t('datosMarca.errores.color-marca-invalido') : t('datosMarca.colorAyuda')}
                size="small"
                sx={{ maxWidth: 200 }}
                slotProps={{ htmlInput: { spellCheck: false, style: { fontFamily: 'var(--font-mono, monospace)' } } }}
              />
            </Stack>
            <Box aria-label={t('datosMarca.vistaPrevia')} role="group" sx={(th) => ({ p: 2, border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.md)}px`, bgcolor: 'background.default' })}>
              <Typography variant="overline" color="text.secondary" component="p">
                {t('datosMarca.vistaPrevia')}
              </Typography>
              <Typography sx={{ fontSize: 14, mb: 1 }}>{t('datosMarca.vistaPreviaTexto', { empresa: empresa.nombre })}</Typography>
              <Stack direction="row" spacing={1} alignItems="center">
                <Box component="span" sx={(th) => ({ px: 1.5, py: 0.75, borderRadius: `${String(th.radio.sm)}px`, fontSize: 14, fontWeight: 600, bgcolor: paleta?.accion ?? 'primary.main', color: paleta?.textoAccion ?? 'primary.contrastText' })}>
                  {t('datosMarca.vistaPreviaBoton')}
                </Box>
                <Box component="span" sx={{ fontSize: 14, fontWeight: 500, color: paleta?.enlace ?? 'primary.main' }}>
                  {t('datosMarca.vistaPreviaEnlace')}
                </Box>
              </Stack>
            </Box>
          </Box>
        </Tarjeta>
      </Stack>
    </>
  );
}
