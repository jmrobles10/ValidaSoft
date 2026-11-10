import { Alert, Box, Button, MenuItem, Stack, TextField } from '@mui/material';
import type { EstadoFichaDto, SeccionFichaDto, SeccionGuardadaDto } from '@validasoft/api-client';
import { ArrowLeft } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useNavigate } from 'react-router';
import { CabeceraPagina } from '../shared/componentes';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { Faltantes } from './Faltantes';
import { FormularioDeSeccion } from './FormularioDeSeccion';
import { enlaceSeccion, esFaltanteDeSoporte, periodosElegibles, type DefinicionSeccion } from './secciones';
import { SelectorSoporte } from './SelectorSoporte';
import { useEditorSeccion } from './useEditorSeccion';

interface Props {
  definicion: DefinicionSeccion;
  periodo: number | null;
  estado: EstadoFichaDto;
  inicial: SeccionFichaDto;
}

/**
 * Una sección del wizard: faltantes del servidor arriba, el formulario, el soporte y «Guardar» (borrador) o «Guardar y
 * continuar», que avanza solo si el servidor dice que quedó completa (FM-16, FM-17). Nunca calcula la completitud.
 */
export function EditorSeccion({ definicion, periodo, estado, inicial }: Props) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const mostrar = useAvisos((s) => s.mostrar);
  const editor = useEditorSeccion(definicion.codigo, periodo, inicial);
  // Cada guardado incompleto lleva el foco (y la vista) a la lista de faltantes.
  const [senalFaltantes, setSenalFaltantes] = useState(0);
  const nombre = periodo ? t('ficha.nombreConPeriodo', { nombre: inicial.nombre, periodo: String(periodo) }) : inicial.nombre;
  const paso = estado.orden.indexOf(definicion.codigo) + 1;
  const rutaActual = enlaceSeccion(definicion.codigo, periodo);

  const alGuardar = (continuar: boolean) => (respuesta: SeccionGuardadaDto) => {
    const completa = respuesta.completitud.completa;
    mostrar(
      !respuesta.creada ? t('ficha.sinCambios', { seccion: nombre }) : completa ? t('ficha.guardadaCompleta', { seccion: nombre }) : t('ficha.guardadaBorrador', { seccion: nombre }),
      completa ? 'success' : 'info',
    );
    if (continuar && completa) {
      const siguiente = respuesta.siguienteSeccion;
      void navegar(siguiente ? enlaceSeccion(siguiente) : '/ficha');
      return;
    }
    // Se queda en la sección: los faltantes exactos quedan arriba del formulario.
    if (!completa) setSenalFaltantes((n) => n + 1);
  };

  const errorArriba = editor.errorCampo
    ? t('ficha.errorCampo', { detalle: editor.errorCampo.mensaje })
    : editor.errorGeneral
      ? mensajeDeError(editor.errorGeneral, t)
      : null;
  const faltaSoporte = editor.faltantes.find(esFaltanteDeSoporte)?.mensaje ?? null;

  return (
    <>
      <Button component={RouterLink} to="/ficha" variant="text" startIcon={<ArrowLeft size={18} aria-hidden />} sx={{ mb: 1, ml: -1, color: 'text.secondary' }}>
        {t('ficha.volverResumen')}
      </Button>
      <CabeceraPagina titulo={nombre} sub={paso > 0 ? t('ficha.paso', { n: String(paso), total: String(estado.orden.length) }) : undefined} />
      <Stack spacing={2.5} component="form" noValidate aria-label={nombre} onSubmit={(e) => e.preventDefault()}>
        {periodo !== null && (
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
        )}
        {errorArriba && <Alert severity="error">{errorArriba}</Alert>}
        <Faltantes faltantes={editor.faltantes} senal={senalFaltantes} />
        <FormularioDeSeccion codigo={definicion.codigo} estado={estado} editor={editor} />
        <SelectorSoporte
          definicion={definicion}
          soportes={editor.soportes}
          onCambiar={editor.cambiarSoportes}
          faltaSoporte={faltaSoporte}
          error={editor.errorEn('soportes')}
          volver={rutaActual}
          deshabilitado={editor.guardando}
        />
        <Box sx={{ display: 'flex', flexDirection: { xs: 'column-reverse', sm: 'row' }, gap: 1.5, justifyContent: 'flex-end', pt: 1 }}>
          <Button variant="outlined" size="large" disabled={editor.guardando} onClick={() => editor.guardar(alGuardar(false))} sx={{ minHeight: 48 }}>
            {editor.guardando ? t('ficha.guardando') : t('ficha.guardar')}
          </Button>
          <Button type="submit" variant="contained" size="large" disabled={editor.guardando} onClick={() => editor.guardar(alGuardar(true))} sx={{ minHeight: 48 }}>
            {t('ficha.guardarContinuar')}
          </Button>
        </Box>
      </Stack>
    </>
  );
}
