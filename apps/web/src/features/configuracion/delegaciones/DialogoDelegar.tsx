import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Skeleton, Stack, TextField, Typography } from '@mui/material';
import type { AreaDto, CandidatoDeDelegacionDto } from '@validasoft/api-client';
import { CampoFecha, FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { UserRoundCheck } from 'lucide-react';
import { useMemo, useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { fechaNegocio } from '../../informes/modelo';
import { useCandidatosDeDelegacion, useCrearDelegacion } from './api';
import { aCrearDto, areasDeLaPersona, borradorInicial, faltasDeDelegacion, formatearDia, listaDeAreas, MAXIMO_MOTIVO, type BorradorDelegacion } from './modelo';

interface Props {
  empresaId: string;
  /** Quien está en sesión: de él son las áreas que se delegan, salvo que un administrador delegue por otra persona. */
  yoId: string;
  esAdministrador: boolean;
  /** Áreas de la empresa; `undefined` mientras cargan. */
  areas: readonly AreaDto[] | undefined;
  onCerrar: () => void;
}

/**
 * Delegar la revisión (OP-7): a quién, desde cuándo y hasta cuándo, y qué áreas (ninguna elegida = todas las de quien
 * delega). Mientras se llena, el aviso dice exactamente qué va a pasar y a quién; solo «Delegar» crea la delegación.
 */
export function DialogoDelegar({ empresaId, yoId, esAdministrador, areas, onCerrar }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const candidatos = useCandidatosDeDelegacion(empresaId, true);
  const crear = useCrearDelegacion(empresaId);
  const hoy = useMemo(() => fechaNegocio(), []);
  const [borrador, setBorrador] = useState<BorradorDelegacion>(() => borradorInicial(hoy));

  const personas = candidatos.data ?? [];
  const deId = borrador.enNombreDe || yoId;
  const enNombreDeOtra = deId !== yoId;
  const cargandoAreas = areas === undefined;
  const deLaPersona = areasDeLaPersona(areas ?? [], deId);
  const nombreDe = (id: string) => personas.find((p) => p.id === id)?.nombre ?? '';
  const delegado = personas.find((p) => p.id === borrador.aUsuarioId);
  const elegibles = personas.filter((p) => p.id !== deId);
  const faltas = faltasDeDelegacion(borrador, hoy, cargandoAreas ? 1 : deLaPersona.length);
  const completa = faltas.length === 0 && !cargandoAreas;

  const cambiar = (cambios: Partial<BorradorDelegacion>) => setBorrador((previo) => ({ ...previo, ...cambios }));
  const alternarArea = (id: string) =>
    cambiar({ areas: borrador.areas.includes(id) ? borrador.areas.filter((a) => a !== id) : [...borrador.areas, id] });

  const nombresDeAreas = borrador.areas.length > 0 ? deLaPersona.filter((a) => borrador.areas.includes(a.id)).map((a) => a.nombre) : deLaPersona.map((a) => a.nombre);
  const frasesDeAreas = borrador.areas.length > 0
    ? t('delegaciones.dialogo.avisoAreas', { count: nombresDeAreas.length, lista: listaDeAreas(nombresDeAreas) })
    : t(enNombreDeOtra ? 'delegaciones.dialogo.avisoTodasLasAreasAjena' : 'delegaciones.dialogo.avisoTodasLasAreas', { lista: listaDeAreas(nombresDeAreas) });
  const aviso = delegado
    ? t(enNombreDeOtra ? 'delegaciones.dialogo.avisoAjena' : 'delegaciones.dialogo.aviso', {
        persona: delegado.nombre,
        de: nombreDe(deId),
        areas: frasesDeAreas,
        desde: borrador.desde ? formatearDia(borrador.desde) : '',
        hasta: borrador.hasta ? formatearDia(borrador.hasta) : '',
      })
    : null;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!completa || !delegado) return;
    crear.mutate(aCrearDto(borrador), {
      onSuccess: () => {
        mostrar(
          t(enNombreDeOtra ? 'delegaciones.dialogo.exitoAjena' : 'delegaciones.dialogo.exito', {
            persona: delegado.nombre,
            de: nombreDe(deId),
            desde: formatearDia(borrador.desde),
          }),
        );
        onCerrar();
      },
    });
  };

  const rangoConError = faltas.includes('rango') || faltas.includes('largo') || faltas.includes('hoy');
  // El rango ya se explica bajo «Hasta»: junto al botón solo va lo que falta llenar.
  const primeraFalta = faltas.find((f) => f !== 'rango' && f !== 'largo' && f !== 'hoy');

  return (
    <Dialog open onClose={crear.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 560, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-delegar">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-delegar">{enNombreDeOtra ? t('delegaciones.dialogo.tituloEnNombreDe') : t('delegaciones.dialogo.titulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 0.5 }}>
            {crear.isError && <Alert severity="error">{mensajeDeError(crear.error, t)}</Alert>}
            {candidatos.isError && <Alert severity="error">{mensajeDeError(candidatos.error, t)}</Alert>}
            {esAdministrador && (
              <SelectorDePersona
                etiqueta={t('delegaciones.dialogo.enNombreDe')}
                ayuda={t('delegaciones.dialogo.enNombreDeAyuda')}
                valor={borrador.enNombreDe}
                personas={personas.filter((p) => p.id !== yoId)}
                primera={{ valor: '', etiqueta: t('delegaciones.dialogo.enNombreDeYo') }}
                // Cambia de quién son las áreas: lo elegido antes ya no vale, y a quien delega no se le delega a sí mismo.
                onCambiar={(id) => cambiar({ enNombreDe: id, areas: [], aUsuarioId: borrador.aUsuarioId === id ? '' : borrador.aUsuarioId })}
              />
            )}
            <SelectorDePersona
              etiqueta={t('delegaciones.dialogo.a')}
              ayuda={elegibles.length === 0 && candidatos.isSuccess ? t('delegaciones.dialogo.sinCandidatos') : t('delegaciones.dialogo.aAyuda')}
              valor={borrador.aUsuarioId}
              personas={elegibles}
              primera={{ valor: '', etiqueta: t('delegaciones.dialogo.aSeleccione'), deshabilitada: true }}
              requerido
              onCambiar={(id) => cambiar({ aUsuarioId: id })}
            />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <CampoFecha
                etiqueta={t('delegaciones.dialogo.desde')}
                valor={borrador.desde}
                onCambiar={(iso) => cambiar({ desde: iso })}
                requerido
                minimo={hoy}
                fullWidth
                sx={{ flex: 1, minWidth: 0 }}
              />
              <CampoFecha
                etiqueta={t('delegaciones.dialogo.hasta')}
                valor={borrador.hasta}
                onCambiar={(iso) => cambiar({ hasta: iso })}
                requerido
                minimo={borrador.desde || hoy}
                error={rangoConError}
                ayuda={rangoConError ? t(`delegaciones.dialogo.faltan.${faltas.includes('largo') ? 'largo' : faltas.includes('hoy') ? 'hoy' : 'rango'}`) : t('delegaciones.dialogo.hastaAyuda')}
                fullWidth
                sx={{ flex: 1, minWidth: 0 }}
              />
            </Stack>
            <Box>
              <Typography variant="body2" component="h3" sx={{ mb: 0.75 }}>
                {t('delegaciones.dialogo.areas')}
              </Typography>
              {cargandoAreas ? (
                <Stack direction="row" spacing={1} role="status" aria-busy="true" aria-label={t('delegaciones.dialogo.areas')}>
                  {[72, 88, 64].map((ancho) => (
                    <Skeleton key={ancho} variant="rounded" width={ancho} height={24} />
                  ))}
                </Stack>
              ) : deLaPersona.length === 0 ? (
                <Alert severity="warning">{t(enNombreDeOtra ? 'delegaciones.dialogo.areasVacias' : 'delegaciones.dialogo.areasVaciasPropias')}</Alert>
              ) : (
                <>
                  <Stack direction="row" useFlexGap spacing={1} sx={{ flexWrap: 'wrap' }}>
                    {deLaPersona.map((area) => {
                      const elegida = borrador.areas.includes(area.id);
                      return (
                        <Chip
                          key={area.id}
                          size="small"
                          label={area.nombre}
                          clickable
                          aria-pressed={elegida}
                          color={elegida ? 'primary' : 'default'}
                          variant={elegida ? 'filled' : 'outlined'}
                          onClick={() => alternarArea(area.id)}
                        />
                      );
                    })}
                  </Stack>
                  <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.75 }}>
                    {t(enNombreDeOtra ? 'delegaciones.dialogo.areasAyudaAjena' : 'delegaciones.dialogo.areasAyuda', { lista: listaDeAreas(deLaPersona.map((a) => a.nombre)) })}
                  </Typography>
                </>
              )}
            </Box>
            <TextField
              label={t('delegaciones.dialogo.motivo')}
              value={borrador.motivo}
              onChange={(e) => cambiar({ motivo: e.target.value.slice(0, MAXIMO_MOTIVO) })}
              helperText={t('delegaciones.dialogo.motivoAyuda')}
              multiline
              minRows={2}
            />
            {aviso && completa && (
              <Alert severity="info" role="status" icon={<UserRoundCheck size={18} aria-hidden />}>
                {aviso}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {primeraFalta && <FaltaParaEnviar>{t(`delegaciones.dialogo.faltan.${primeraFalta}`)}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={crear.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={!completa || crear.isPending} startIcon={crear.isPending ? <IconoCarga /> : undefined}>
            {t('delegaciones.dialogo.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}

/** Select de una persona (nombre y correo); la primera opción es el «ninguna» o el «yo». */
function SelectorDePersona({
  etiqueta,
  ayuda,
  valor,
  personas,
  primera,
  requerido = false,
  onCambiar,
}: {
  etiqueta: string;
  ayuda: string;
  valor: string;
  personas: readonly CandidatoDeDelegacionDto[];
  primera: { valor: string; etiqueta: string; deshabilitada?: boolean };
  requerido?: boolean;
  onCambiar: (id: string) => void;
}) {
  return (
    <TextField
      select
      label={etiqueta}
      value={valor}
      onChange={(e) => onCambiar(e.target.value)}
      required={requerido}
      helperText={ayuda}
      slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
    >
      <MenuItem value={primera.valor} disabled={primera.deshabilitada}>
        {primera.etiqueta}
      </MenuItem>
      {personas.map((p) => (
        <MenuItem key={p.id} value={p.id}>
          {p.nombre}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', ml: 1 }}>
            {p.correo}
          </Typography>
        </MenuItem>
      ))}
    </TextField>
  );
}
