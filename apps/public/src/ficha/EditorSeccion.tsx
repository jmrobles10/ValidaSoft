import { Alert, Box, MenuItem, Stack, TextField } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import {
  aplicarCambiosPendientes,
  esErrorApi,
  useCambiosPendientes,
  useEdicionEnCurso,
  type EstadoFichaDto,
  type SeccionFichaDto,
  type SeccionGuardadaDto,
} from '@validasoft/api-client';
import { AvisoCambiosNuevos } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { CabeceraPagina } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { ALCANCES_PUBLICO } from '../notificaciones/tiempoReal';
import { useAvisos } from '../shared/useAvisos';
import { useEvaluacionSeccion } from './api';
import { BarraDeAcciones } from './BarraDeAcciones';
import { Faltantes } from './Faltantes';
import { FormularioDeSeccion } from './FormularioDeSeccion';
import { ALTO_CABECERA, entradaSeccion, MARGEN_SUPERIOR } from './movimiento';
import { ObservacionesDeEmpresas } from './ObservacionesDeEmpresas';
import { PropuestaDesdeDocumento } from './PropuestaDesdeDocumento';
import { RatificacionesDeSeccion } from './RatificacionesDeSeccion';
import { DialogoSalidaSinGuardar } from './SalidaSinGuardar';
import { enlaceSeccion, esFaltanteDeSoporte, periodosElegibles, type DefinicionSeccion } from './secciones';
import { SelectorSoporte } from './SelectorSoporte';
import { useEditorSeccion } from './useEditorSeccion';
import { useObservacionesDeSeccion } from './useObservacionesDeSeccion';
import { deLaSeccion, useRatificacionesPendientes } from './useRatificacionesPendientes';
import { useSalidaSinGuardar } from './useSalidaSinGuardar';

interface Props {
  definicion: DefinicionSeccion;
  periodo: number | null;
  estado: EstadoFichaDto;
  inicial: SeccionFichaDto;
  /** «Actualizar» ante un cambio nuevo con cambios sin guardar: vuelve a armar el formulario con lo que hay en el servidor. */
  onReiniciar?: () => void;
}

/**
 * Una sección del wizard: el título fijo arriba, lo que observó alguna empresa, lo que un documento nuevo propone para la sección
 * («Actualizar desde mi RUT…», que solo llena el formulario) y los faltantes del servidor, el formulario,
 * el soporte y la barra fija con «Guardar» (borrador) y «Guardar y continuar», que se habilita cuando la API dice que la
 * sección quedaría completa (FM-46) y avanza solo si el guardado lo confirma (FM-16, FM-17). Nunca calcula la completitud.
 * Entra con una animación corta cada vez que se cambia de sección.
 */
export function EditorSeccion({ definicion, periodo, estado, inicial, onReiniciar }: Props) {
  const { t } = useTranslation();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const mostrar = useAvisos((s) => s.mostrar);
  // Lo que alguna empresa observó de esta sección (F2): arriba del formulario, con sus campos marcados.
  const observaciones = useObservacionesDeSeccion(definicion.codigo, periodo);
  const senalados = observaciones.flatMap((o) => o.observacion.campos ?? []);
  // Lo que alguna empresa le pide ratificar de esta sección en la renovación de su vínculo (VV-3).
  const ratificaciones = deLaSeccion(useRatificacionesPendientes(), definicion.codigo, periodo);
  const editor = useEditorSeccion(definicion.codigo, periodo, inicial, senalados.length > 0 ? { campos: senalados, mensaje: t('ficha.observada.campoSenalado') } : null);
  // Cada guardado incompleto lleva el foco (y la vista) a la lista de faltantes.
  const [senalFaltantes, setSenalFaltantes] = useState(0);
  const nombre = periodo ? t('ficha.nombreConPeriodo', { nombre: inicial.nombre, periodo: String(periodo) }) : inicial.nombre;
  const paso = estado.orden.indexOf(definicion.codigo) + 1;
  const rutaActual = enlaceSeccion(definicion.codigo, periodo);
  const salida = useSalidaSinGuardar(editor.sinGuardar);
  // Con cambios sin guardar, el tiempo real no pisa lo escrito: avisa y deja elegir (NT-12).
  useEdicionEnCurso(ALCANCES_PUBLICO.ficha, editor.sinGuardar);
  const hayCambiosNuevos = useCambiosPendientes(ALCANCES_PUBLICO.ficha);
  const evaluacion = useEvaluacionSeccion(definicion.codigo, periodo, editor.cuerpo);

  const alGuardar = (continuar: boolean) => (respuesta: SeccionGuardadaDto) => {
    const completa = respuesta.completitud.completa;
    mostrar(
      !respuesta.creada ? t('ficha.sinCambios', { seccion: nombre }) : completa ? t('ficha.guardadaCompleta', { seccion: nombre }) : t('ficha.guardadaBorrador', { seccion: nombre }),
      completa ? 'success' : 'info',
    );
    if (continuar && completa) {
      const siguiente = respuesta.siguienteSeccion;
      salida.permitirSalida();
      void navegar(siguiente ? enlaceSeccion(siguiente) : '/ficha');
      return;
    }
    // Se queda en la sección: los faltantes exactos quedan arriba del formulario.
    if (!completa) setSenalFaltantes((n) => n + 1);
  };

  // El año va en la misma fila que la fecha de corte (campos cortos): lo pinta el formulario de la sección.
  const selectorPeriodo =
    periodo !== null ? (
      <TextField
        select
        label={t('ficha.financiera.periodo')}
        value={periodo}
        onChange={(e) => void navegar(enlaceSeccion(definicion.codigo, Number(e.target.value)))}
        helperText={t('ficha.financiera.periodoAyuda')}
        disabled={editor.guardando}
        fullWidth
      >
        {periodosElegibles(periodo).map((anio) => (
          <MenuItem key={anio} value={anio}>
            {anio}
          </MenuItem>
        ))}
      </TextField>
    ) : undefined;

  const errorArriba = editor.errorCampo
    ? t('ficha.errorCampo', { detalle: editor.errorCampo.mensaje })
    : editor.errorGeneral
      ? mensajeDeError(editor.errorGeneral, t)
      : null;
  const faltaSoporte = editor.faltantes.find(esFaltanteDeSoporte)?.mensaje ?? null;

  return (
    <>
      {/* Solo el contenido se anima: la barra va fuera, porque el transform de la animación volvería relativa a esta caja
          la barra fija del celular. Sin «fill» la caja no conserva el transform al terminar. */}
      <Box sx={{ animation: `${entradaSeccion} 280ms cubic-bezier(0.2, 0, 0, 1) backwards` }}>
        <DialogoSalidaSinGuardar bloqueo={salida.bloqueo} seccion={nombre} />
        {/* El título queda a la vista al desplazarse (en el celular lo dice la barra del panel). Su fondo sube hasta la
            cabecera, así que se pega justo donde empieza y no se corre. */}
        <Box
          sx={{
            position: { md: 'sticky' },
            top: { md: ALTO_CABECERA },
            zIndex: 3,
            bgcolor: 'background.default',
            pt: { md: `${String(MARGEN_SUPERIOR.sm)}px` },
            // Un poco más ancho que la columna: tapa la orilla de los radios, que MUI corre hacia afuera.
            mx: { md: -2 },
            px: { md: 2 },
            mt: { md: `-${String(MARGEN_SUPERIOR.sm)}px` },
          }}
        >
          <CabeceraPagina titulo={nombre} sub={paso > 0 ? t('ficha.paso', { n: String(paso), total: String(estado.orden.length) }) : undefined} />
        </Box>
        <Stack spacing={2.5} component="form" noValidate aria-label={nombre} onSubmit={(e) => e.preventDefault()}>
          {hayCambiosNuevos && (
            <AvisoCambiosNuevos
              titulo={t('comun.cambiosNuevos.titulo')}
              texto={t('comun.cambiosNuevos.texto')}
              accion={t('comun.cambiosNuevos.accion')}
              onActualizar={() => void aplicarCambiosPendientes(cliente, ALCANCES_PUBLICO.ficha).then(() => onReiniciar?.())}
            />
          )}
          <ObservacionesDeEmpresas observaciones={observaciones} />
          <RatificacionesDeSeccion pendientes={ratificaciones} />
          {errorArriba && <Alert severity="error">{errorArriba}</Alert>}
          <PropuestaDesdeDocumento
            codigo={definicion.codigo}
            periodo={periodo}
            soportes={editor.soportes}
            onUsarValores={editor.cambiar}
            onCambiarSoportes={editor.cambiarSoportes}
            deshabilitado={editor.guardando}
          />
          <Faltantes faltantes={editor.faltantes} senal={senalFaltantes} />
          <FormularioDeSeccion codigo={definicion.codigo} estado={estado} editor={editor} selectorPeriodo={selectorPeriodo} />
          <SelectorSoporte
            definicion={definicion}
            soportes={editor.soportes}
            retirados={editor.soportesRetirados}
            onCambiar={editor.cambiarSoportes}
            faltaSoporte={faltaSoporte}
            error={editor.errorEn('soportes')}
            volver={rutaActual}
            deshabilitado={editor.guardando}
          />
        </Stack>
      </Box>
      <BarraDeAcciones
        prevista={evaluacion.data ?? null}
        errorPrevista={esErrorApi(evaluacion.error) && evaluacion.error.esValidacion ? evaluacion.error : null}
        sinRevision={evaluacion.isError && !(esErrorApi(evaluacion.error) && evaluacion.error.esValidacion)}
        pendiente={evaluacion.pendiente}
        sinGuardar={editor.sinGuardar}
        guardando={editor.guardando}
        onGuardar={() => editor.guardar(alGuardar(false))}
        onContinuar={() => editor.guardar(alGuardar(true))}
      />
    </>
  );
}
