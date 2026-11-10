import { keepPreviousData, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  buscarEnCatalogoPublico,
  evaluarSeccionFicha,
  getBuscarEnCatalogoPublicoQueryOptions,
  getObtenerFichaQueryKey,
  getObtenerFichaQueryOptions,
  getObtenerSeccionFichaQueryKey,
  getObtenerSeccionFichaQueryOptions,
  guardarSeccionFicha,
  type CompletitudSeccionDto,
  type EntradaCatalogoDto,
  type GuardarSeccionDto,
  type SeccionGuardadaDto,
} from '@validasoft/api-client';
import { useDiferido } from '../shared/useDiferido';

export type Catalogo = 'ciudades' | 'actividades-economicas' | 'responsabilidades-dian' | 'especialidades';

/** Estado de la ficha: orden por tipo de persona, la siguiente sección incompleta y el estado de cada una (FM-21). */
export const useEstadoFicha = () => useQuery(getObtenerFichaQueryOptions());

const parametrosPeriodo = (periodo: number | null) => (periodo === null ? undefined : { periodo });

/** Versión vigente de la sección (o vacía). Sin refresco automático: el tercero está llenando el formulario. */
export const useSeccionFicha = (codigo: string, periodo: number | null) =>
  useQuery({ ...getObtenerSeccionFichaQueryOptions(codigo, parametrosPeriodo(periodo)), staleTime: Infinity });

/**
 * Guarda la sección (borrador o completa). La respuesta trae la completitud que calculó el servidor y la siguiente
 * sección del wizard: la sección queda en caché tal como la guardó la API y el estado de la ficha se vuelve a pedir.
 */
export function useGuardarSeccion(codigo: string, periodo: number | null) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: GuardarSeccionDto) => guardarSeccionFicha(codigo, cuerpo, parametrosPeriodo(periodo)),
    onSuccess: (respuesta: SeccionGuardadaDto) => {
      cliente.setQueryData(getObtenerSeccionFichaQueryKey(codigo, parametrosPeriodo(periodo)), respuesta.seccion);
      void cliente.invalidateQueries({ queryKey: getObtenerFichaQueryKey() });
    },
  });
}

/**
 * Completitud que tendría la sección con lo que hay en el formulario, sin guardarla (FM-46): la evalúa la API con la misma
 * regla del guardado. `cuerpo` es el JSON de lo que se guardaría; se consulta medio segundo después del último cambio y,
 * mientras llega la respuesta, se conserva la anterior. `pendiente` = aún no refleja lo último escrito.
 */
export function useEvaluacionSeccion(codigo: string, periodo: number | null, cuerpo: string) {
  const diferido = useDiferido(cuerpo, 500);
  const consulta = useQuery<CompletitudSeccionDto>({
    queryKey: ['/public/v1/ficha', codigo, 'evaluacion', periodo, diferido],
    queryFn: ({ signal }) => evaluarSeccionFicha(codigo, JSON.parse(diferido) as GuardarSeccionDto, parametrosPeriodo(periodo), undefined, signal),
    placeholderData: keepPreviousData,
    staleTime: Infinity,
    retry: false,
  });
  return { ...consulta, pendiente: diferido !== cuerpo || consulta.isFetching };
}

/** Búsqueda en un catálogo grande (ciudades, CIIU) por prefijo de código o parte del nombre. */
export const useBusquedaCatalogo = (catalogo: Catalogo, q: string) =>
  useQuery({ ...getBuscarEnCatalogoPublicoQueryOptions(catalogo, { q: q.trim() || undefined, limite: 20 }), staleTime: 5 * 60_000 });

/** Las entradas de los códigos ya elegidos (para mostrar su nombre): una búsqueda por código, en caché. */
export function useEntradasCatalogo(catalogo: Catalogo, codigos: readonly string[]): Map<string, EntradaCatalogoDto> {
  const consultas = useQueries({
    queries: codigos.map((codigo) => ({ ...getBuscarEnCatalogoPublicoQueryOptions(catalogo, { q: codigo, limite: 20 }), staleTime: Infinity })),
  });
  const entradas = new Map<string, EntradaCatalogoDto>();
  codigos.forEach((codigo, i) => {
    const entrada = consultas[i]?.data?.datos.find((e) => e.codigo === codigo);
    if (entrada) entradas.set(codigo, entrada);
  });
  return entradas;
}

/** Un catálogo corto completo (responsabilidades DIAN, especialidades), recorriendo sus páginas de 200. */
export function useCatalogoCompleto(catalogo: Catalogo) {
  return useQuery({
    queryKey: ['/public/v1/catalogos', catalogo, 'completo'],
    staleTime: Infinity,
    queryFn: async ({ signal }) => {
      const entradas: EntradaCatalogoDto[] = [];
      let cursor: string | undefined;
      do {
        const pagina = await buscarEnCatalogoPublico(catalogo, { limite: 200, cursor }, undefined, signal);
        entradas.push(...pagina.datos);
        cursor = pagina.siguienteCursor ?? undefined;
      } while (cursor);
      return entradas;
    },
  });
}

/** Sin tildes ni mayúsculas, para buscar «bogota» y encontrar «Bogotá». */
export const sinTildes = (texto: string) =>
  texto
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase();

/** «Bogotá, D.C. · Bogotá» para ciudades; «4111 · Construcción de edificios residenciales» para los demás. */
export const etiquetaEntrada = (catalogo: Catalogo, e: Pick<EntradaCatalogoDto, 'codigo' | 'nombre' | 'departamento'>) =>
  catalogo === 'ciudades' ? (e.departamento && e.departamento !== e.nombre ? `${e.nombre} · ${e.departamento}` : e.nombre) : `${e.codigo} · ${e.nombre}`;
