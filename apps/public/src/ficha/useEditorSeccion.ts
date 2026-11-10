import { esErrorApi, type ErrorApi, type FaltanteSeccionDto, type JsonElement, type SeccionFichaDto, type SeccionGuardadaDto, type SoporteSeccionDto } from '@validasoft/api-client';
import { useState } from 'react';
import { useGuardarSeccion } from './api';
import { estaSenalado } from './etiquetas';
import { claveDeRuta, type CodigoSeccion } from './secciones';
import type { PersonaRelacionada } from './tipos';

/** Error de formato (422) que la API señaló en un campo: `campo` es la ruta snake_case (`personas[0].numero`). */
export interface ErrorDeCampo {
  campo: string;
  mensaje: string;
}

/** Lo que no edita el tercero (lo fija la API con su registro, FM-19) no se envía. */
export function datosParaEnviar(codigo: CodigoSeccion, datos: object): JsonElement {
  const copia: Record<string, unknown> = { ...datos };
  if (codigo === 'FICHA_GENERAL') {
    delete copia.tipo_persona;
    delete copia.tipo_identificacion;
    delete copia.numero;
    delete copia.dv;
  }
  if (codigo === 'FICHA_FINANCIERA') delete copia.periodo;
  if (codigo === 'FICHA_LEGAL' && Array.isArray(copia.personas)) {
    copia.personas = (copia.personas as PersonaRelacionada[]).map((persona) => {
      const sinDv: PersonaRelacionada = { ...persona };
      delete sinDv.dv;
      return sinDv;
    });
  }
  return copia;
}

/** Campos que una empresa señaló al observar la sección (F2) y el mensaje con que se marcan en el formulario. */
export interface Senalados {
  campos: readonly string[];
  mensaje: string;
}

/**
 * Estado del formulario de una sección: los datos (snake_case), los soportes y lo último que dijo el servidor — sus
 * faltantes exactos (FM-16) y el error 422 de un campo —, más los campos que una empresa señaló al observarla. Un campo
 * que el tercero vuelve a tocar deja de marcarse hasta el siguiente guardado. La completitud nunca se calcula aquí.
 */
export function useEditorSeccion(codigo: CodigoSeccion, periodo: number | null, inicial: SeccionFichaDto, senalados: Senalados | null = null) {
  const [datos, setDatos] = useState<object>(() => inicial.datos ?? {});
  const [soportes, setSoportes] = useState<SoporteSeccionDto[]>(() => inicial.soportes);
  const [faltantes, setFaltantes] = useState<FaltanteSeccionDto[]>(() => inicial.completitud?.faltantes ?? []);
  const [erroresCampo, setErroresCampo] = useState<ErrorDeCampo[]>([]);
  const errorCampo = erroresCampo[0] ?? null;
  const [editados, setEditados] = useState<ReadonlySet<string>>(() => new Set());
  const guardado = useGuardarSeccion(codigo, periodo);

  const marcar = (claves: string[]) => setEditados((previos) => new Set([...previos, ...claves]));

  const cambiar = (cambio: object) => {
    setDatos((d) => ({ ...d, ...cambio }));
    marcar(Object.keys(cambio));
  };

  const cambiarSoportes = (nuevos: SoporteSeccionDto[]) => {
    setSoportes(nuevos);
    marcar(['soportes']);
  };

  const mensaje = (ruta: string): string | null => {
    if (editados.has(claveDeRuta(ruta))) return null;
    const error = erroresCampo.find((e) => e.campo === ruta);
    if (error) return error.mensaje;
    const faltante = faltantes.find((f) => f.campo === ruta)?.mensaje;
    if (faltante) return faltante;
    return senalados && estaSenalado(ruta, senalados.campos) ? senalados.mensaje : null;
  };

  /** El error 422 de cualquier ruta bajo una clave (`soportes[1]`, `personas[0].numero`), si no se ha vuelto a tocar. */
  const errorEn = (clave: string): string | null =>
    editados.has(clave)
      ? null
      : (erroresCampo.find((e) => claveDeRuta(e.campo) === clave)?.mensaje ?? (senalados && estaSenalado(clave, senalados.campos) ? senalados.mensaje : null));

  const guardar = (alGuardar: (respuesta: SeccionGuardadaDto) => void) => {
    guardado.mutate(
      // El tipo de cada soporte lo pone el servidor: se envía solo qué versión de qué documento.
      { datos: datosParaEnviar(codigo, datos), soportes: soportes.map(({ documentoId, version }) => ({ documentoId, version })) },
      {
        onSuccess: (respuesta) => {
          setDatos(respuesta.seccion.datos ?? {});
          setSoportes(respuesta.seccion.soportes);
          setFaltantes(respuesta.completitud.faltantes);
          setErroresCampo([]);
          setEditados(new Set());
          alGuardar(respuesta);
        },
        onError: (error) => {
          setErroresCampo(esErrorApi(error) && error.esValidacion ? erroresDeCampo(error) : []);
          setEditados(new Set());
        },
      },
    );
  };

  // Un error que no es de un campo (sesión, red, 5xx…) va arriba del formulario.
  const errorGeneral = guardado.isError && !errorCampo ? guardado.error : null;

  return { datos, cambiar, soportes, cambiarSoportes, faltantes, errorCampo, errorGeneral, mensaje, errorEn, guardar, guardando: guardado.isPending };
}

export type EditorSeccion = ReturnType<typeof useEditorSeccion>;

/**
 * Todos los errores de formato del 422, en el orden en que llegan (el primero va arriba del formulario). Si la API no trae
 * la lista, se usa el campo principal (`extensiones.campo`) con el detalle.
 */
function erroresDeCampo(error: ErrorApi): ErrorDeCampo[] {
  const lista = Object.entries(error.errores).flatMap(([campo, mensajes]) => mensajes.slice(0, 1).map((mensaje) => ({ campo, mensaje })));
  const principal = typeof error.extensiones.campo === 'string' ? error.extensiones.campo : null;
  if (lista.length === 0) return principal ? [{ campo: principal, mensaje: error.detalle }] : [];
  return principal ? [...lista.filter((e) => e.campo === principal), ...lista.filter((e) => e.campo !== principal)] : lista;
}
