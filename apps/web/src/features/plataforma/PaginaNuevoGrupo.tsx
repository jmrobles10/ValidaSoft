import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from '@mui/material';
import type { TenantCreadoDto } from '@validasoft/api-client';
import { DosColumnas, FaltaParaEnviar } from '@validasoft/ui';
import { useState, type ReactNode, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, EnlaceVolver, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePaises } from '../configuracion/empresas/api';
import { borradorEmpresaNuevo, type BorradorEmpresa } from '../configuracion/empresas/modelo';
import { useCrearGrupo } from './api';
import { borradorGrupoNuevo, cuerpoAltaGrupo, slugDesdeNombre, validarGrupo, type BorradorGrupo, type ErrorGrupo } from './modelo';
import { ResultadoAlta } from './ResultadoAlta';

const MONO = { '& input, & textarea': { fontFamily: (th: { typography: { mono: { fontFamily?: string } } }) => th.typography.mono.fontFamily } };

function Seccion({ titulo, ayuda, children }: { titulo: string; ayuda: string; children: ReactNode }) {
  return (
    <Tarjeta titulo={titulo} etiqueta={titulo}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 2 }}>
        {ayuda}
      </Typography>
      <Stack spacing={2}>{children}</Stack>
    </Tarjeta>
  );
}

/** Alta de un grupo operable de punta a punta (PT-5): grupo, primera empresa, administrador y sistema integrado. */
export function PaginaNuevoGrupo() {
  const { t } = useTranslation();
  const [clave] = useState(() => crypto.randomUUID());
  const [b, setB] = useState<BorradorGrupo>(() => borradorGrupoNuevo(borradorEmpresaNuevo()));
  const [tocados, setTocados] = useState<ReadonlySet<ErrorGrupo>>(new Set());
  const [alta, setAlta] = useState<TenantCreadoDto | null>(null);
  const paises = usePaises(true);
  const crear = useCrearGrupo(clave);

  if (alta) return <ResultadoAlta alta={alta} />;

  const tipos = paises.data?.find((p) => p.codigo === b.empresa.pais)?.tiposIdentificacion ?? [];
  const tipo = tipos.find((x) => x.codigo === b.empresa.tipo);
  const errores = validarGrupo(b, tipo);
  const primero = errores[0];
  const mensaje = (e: ErrorGrupo) => t(`plataforma.alta.errores.${e}`, { tipo: tipo?.nombre ?? b.empresa.tipo });
  const error = (...codigos: ErrorGrupo[]) => {
    const e = errores.find((x) => codigos.includes(x) && tocados.has(x));
    return e ? mensaje(e) : null;
  };
  const tocar = (...codigos: ErrorGrupo[]) => setTocados((previos) => new Set([...previos, ...codigos]));
  const cambiar = (parcial: Partial<BorradorGrupo>) => setB((previo) => ({ ...previo, ...parcial }));
  const cambiarEmpresa = (parcial: Partial<BorradorEmpresa>) => setB((previo) => ({ ...previo, empresa: { ...previo.empresa, ...parcial } }));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (primero) return;
    crear.mutate(cuerpoAltaGrupo(b), { onSuccess: setAlta });
  };

  const campo = (etiqueta: string, valor: string, alCambiar: (v: string) => void, codigos: ErrorGrupo[], extra: { ayuda?: string; mono?: boolean; filas?: number; requerido?: boolean } = {}) => {
    const e = error(...codigos);
    return (
      <TextField
        required={extra.requerido ?? true}
        label={etiqueta}
        value={valor}
        onChange={(ev) => alCambiar(ev.target.value)}
        onBlur={() => tocar(...codigos)}
        error={e !== null}
        helperText={e ?? extra.ayuda}
        multiline={extra.filas !== undefined}
        minRows={extra.filas}
        slotProps={{ htmlInput: { spellCheck: false, autoComplete: 'off' } }}
        sx={extra.mono ? MONO : undefined}
      />
    );
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <EncabezadoPagina
        antes={<EnlaceVolver a="/plataforma/grupos">{t('plataforma.alta.volver')}</EnlaceVolver>}
        titulo={t('plataforma.alta.titulo')}
        subtitulo={t('plataforma.alta.subtitulo')}
        acciones={
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            {primero && <FaltaParaEnviar>{mensaje(primero)}</FaltaParaEnviar>}
            <Button type="submit" variant="contained" disabled={crear.isPending || primero !== undefined} startIcon={crear.isPending ? <IconoCarga /> : undefined}>
              {t('plataforma.alta.crear')}
            </Button>
          </Stack>
        }
      />
      {crear.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(crear.error, t)}
        </Alert>
      )}
      {paises.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(paises.error, t)}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <Seccion titulo={t('plataforma.alta.grupo')} ayuda={t('plataforma.alta.grupoAyuda')}>
              {campo(t('plataforma.alta.nombre'), b.nombre, (v) => cambiar({ nombre: v, ...(!b.slugManual && { slug: slugDesdeNombre(v) }) }), ['grupo-nombre-requerido'])}
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                {campo(t('plataforma.alta.slug'), b.slug, (v) => cambiar({ slug: v.toLowerCase(), slugManual: true }), ['slug-invalido'], { ayuda: t('plataforma.alta.slugAyuda'), mono: true })}
                {campo(t('plataforma.alta.zona'), b.zonaHoraria, (v) => cambiar({ zonaHoraria: v }), [], { mono: true })}
              </Stack>
            </Seccion>
            <Seccion titulo={t('plataforma.alta.empresa')} ayuda={t('plataforma.alta.empresaAyuda')}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                <TextField select required label={t('empresasGrupo.campos.pais')} value={paises.data ? b.empresa.pais : ''} onChange={(e) => cambiarEmpresa({ pais: e.target.value, tipo: '' })} sx={{ flex: 1, minWidth: 0 }}>
                  {(paises.data ?? []).map((p) => (
                    <MenuItem key={p.codigo} value={p.codigo}>
                      {p.nombre}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField select required label={t('empresasGrupo.campos.tipo')} value={tipo ? b.empresa.tipo : ''} onChange={(e) => cambiarEmpresa({ tipo: e.target.value })} sx={{ flex: 1.4, minWidth: 0 }}>
                  {tipos.map((x) => (
                    <MenuItem key={x.codigo} value={x.codigo}>
                      {x.codigo} · {x.nombre}
                    </MenuItem>
                  ))}
                </TextField>
              </Stack>
              {campo(t('empresasGrupo.campos.numero'), b.empresa.numero, (v) => cambiarEmpresa({ numero: v }), ['empresa-numero-requerido', 'empresa-numero-invalido'], {
                ayuda: tipo?.tieneDv ? t('empresasGrupo.campos.numeroAyudaDv') : undefined,
                mono: true,
              })}
              {campo(t('empresasGrupo.campos.razonSocial'), b.empresa.nombre, (v) => cambiarEmpresa({ nombre: v }), ['empresa-nombre-requerido'])}
              {campo(t('empresasGrupo.campos.prefijo'), b.empresa.prefijo, (v) => cambiarEmpresa({ prefijo: v.toUpperCase().replace(/[^A-Z0-9]/g, '') }), ['empresa-prefijo-invalido'], {
                ayuda: t('empresasGrupo.prefijoNoCambia'),
                mono: true,
              })}
            </Seccion>
          </>
        }
        derecha={
          <>
            <Seccion titulo={t('plataforma.alta.administrador')} ayuda={t('plataforma.alta.administradorAyuda')}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                {campo(t('plataforma.alta.adminNombre'), b.adminNombre, (v) => cambiar({ adminNombre: v }), ['admin-nombre-requerido'])}
                {campo(t('plataforma.alta.adminCorreo'), b.adminCorreo, (v) => cambiar({ adminCorreo: v }), ['admin-correo-invalido'])}
              </Stack>
              {campo(t('plataforma.alta.adminIdExterno'), b.adminIdExterno, (v) => cambiar({ adminIdExterno: v }), [], {
                ayuda: t('plataforma.alta.adminIdExternoAyuda'),
                mono: true,
                requerido: false,
              })}
            </Seccion>
            <Seccion titulo={t('plataforma.alta.sistema')} ayuda={t('plataforma.alta.sistemaAyuda')}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
                {campo(t('plataforma.alta.sistemaCodigo'), b.sistemaCodigo, (v) => cambiar({ sistemaCodigo: v.toUpperCase() }), ['sistema-codigo-invalido'], {
                  ayuda: t('plataforma.alta.sistemaCodigoAyuda'),
                  mono: true,
                })}
                {campo(t('plataforma.alta.sistemaNombre'), b.sistemaNombre, (v) => cambiar({ sistemaNombre: v }), ['sistema-nombre-requerido'])}
              </Stack>
              {campo(t('plataforma.alta.sistemaClave'), b.sistemaClave, (v) => cambiar({ sistemaClave: v }), ['clave-publica-requerida'], {
                ayuda: t('plataforma.alta.sistemaClaveAyuda'),
                mono: true,
                filas: 4,
              })}
              {campo(t('plataforma.alta.sistemaOrigenes'), b.sistemaOrigenes, (v) => cambiar({ sistemaOrigenes: v }), ['origen-invalido'], {
                ayuda: t('plataforma.alta.sistemaOrigenesAyuda'),
                mono: true,
                filas: 2,
                requerido: false,
              })}
            </Seccion>
          </>
        }
      />
    </Box>
  );
}
