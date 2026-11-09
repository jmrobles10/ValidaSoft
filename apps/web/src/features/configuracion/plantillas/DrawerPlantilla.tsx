import { Alert, Box, Button, Divider, Drawer, IconButton, Stack, Typography } from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarPlantillaNotificacion, type ActualizarPlantillaNotificacionDto, type PlantillaNotificacionDto } from '@validasoft/api-client';
import { RotateCcw, X } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, IconoCarga } from '../../../shared/componentes';
import { invalidarRuta } from '../../../shared/consultas';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { CamposPlantilla } from './CamposPlantilla';
import { borradorDe, campoDelError, cuerpoDe, personalizada, type BorradorPlantilla, type CampoPlantilla } from './modelo';
import { VistaPreviaCorreo } from './VistaPreviaCorreo';

const PREDETERMINADO: ActualizarPlantillaNotificacionDto = { asunto: null, cabecera: null, pie: null };

/** Editar asunto, cabecera y pie de un correo con vista previa en vivo; «Restaurar predeterminado» guarda todo vacío. */
export function DrawerPlantilla({
  empresaId,
  plantilla,
  puedeEscribir,
  onCerrar,
}: {
  empresaId: string;
  plantilla: PlantillaNotificacionDto;
  puedeEscribir: boolean;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const mostrar = useAvisos((s) => s.mostrar);
  const [borrador, setBorrador] = useState<BorradorPlantilla>(() => borradorDe(plantilla));
  const [restaurando, setRestaurando] = useState(false);

  const guardar = useMutation({
    mutationFn: (cuerpo: ActualizarPlantillaNotificacionDto) =>
      actualizarPlantillaNotificacion(plantilla.evento, plantilla.canal, cuerpo, { empresa: empresaId }),
    onSuccess: async (_, cuerpo) => {
      await invalidarRuta(cliente, '/api/v1/plantillas-notificacion');
      mostrar(cuerpo === PREDETERMINADO ? t('plantillas.restaurado') : t('plantillas.guardado'));
      onCerrar();
    },
    onSettled: () => setRestaurando(false),
  });

  const cambiar = (campo: CampoPlantilla, valor: string) => {
    if (guardar.isError) guardar.reset();
    setBorrador((previo) => ({ ...previo, [campo]: valor }));
  };

  const campoConError = campoDelError(guardar.error);
  const errores: Partial<Record<CampoPlantilla, string>> = campoConError ? { [campoConError]: mensajeDeError(guardar.error, t) } : {};
  const sinCambios = JSON.stringify(cuerpoDe(borrador)) === JSON.stringify(cuerpoDe(borradorDe(plantilla)));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!puedeEscribir || sinCambios) return;
    guardar.mutate(cuerpoDe(borrador));
  };

  const titulo = t(puedeEscribir ? 'plantillas.editarTitulo' : 'plantillas.verTitulo', { nombre: plantilla.nombre });

  return (
    <Drawer
      anchor="right"
      open
      onClose={guardar.isPending ? undefined : onCerrar}
      slotProps={{ paper: { role: 'dialog', 'aria-modal': true, 'aria-labelledby': 'titulo-plantilla', sx: { width: 640, maxWidth: '100%' } } }}
    >
      <Box component="form" onSubmit={enviar} noValidate sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 2.5, py: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Box>
            <Typography id="titulo-plantilla" variant="h4" component="h2">
              {titulo}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {plantilla.descripcion}
            </Typography>
          </Box>
          <IconButton aria-label={t('comun.cerrar')} onClick={onCerrar} disabled={guardar.isPending}>
            <X size={18} strokeWidth={1.75} />
          </IconButton>
        </Stack>
        <Stack spacing={2.5} sx={{ p: 2.5, flex: 1, overflowY: 'auto' }}>
          {!puedeEscribir && <Alert severity="info">{t('plantillas.soloLectura')}</Alert>}
          {guardar.isError && !campoConError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
          <CamposPlantilla
            asuntoPorDefecto={plantilla.asuntoPorDefecto}
            variables={plantilla.variablesPermitidas}
            borrador={borrador}
            errores={errores}
            soloLectura={!puedeEscribir}
            onCambiar={cambiar}
          />
          <Divider />
          <VistaPreviaCorreo empresaId={empresaId} evento={plantilla.evento} nombre={plantilla.nombre} borrador={borrador} />
        </Stack>
        {puedeEscribir && (
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', px: 2.5, py: 1.5, borderTop: 1, borderColor: 'divider' }}>
            {personalizada(plantilla) && (
              <Button variant="text" color="error" startIcon={<RotateCcw size={16} aria-hidden />} onClick={() => setRestaurando(true)} disabled={guardar.isPending}>
                {t('plantillas.restaurar')}
              </Button>
            )}
            <Box sx={{ flex: 1 }} />
            <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
              {t('comun.cancelar')}
            </Button>
            <Button type="submit" variant="contained" disabled={guardar.isPending || sinCambios} startIcon={guardar.isPending && !restaurando ? <IconoCarga /> : undefined}>
              {t('comun.guardar')}
            </Button>
          </Stack>
        )}
      </Box>
      {restaurando && (
        <DialogoConfirmacion
          titulo={t('plantillas.restaurarTitulo')}
          mensaje={t('plantillas.restaurarMensaje', { nombre: plantilla.nombre })}
          confirmar={t('plantillas.restaurarConfirmar')}
          icono={<RotateCcw size={16} aria-hidden />}
          ocupado={guardar.isPending}
          onConfirmar={() => guardar.mutate(PREDETERMINADO)}
          onCerrar={() => setRestaurando(false)}
        />
      )}
    </Drawer>
  );
}
