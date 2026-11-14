import { QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { debeRefrescar } from '../expediente/requisitos';
import {
  aceptarInvitacion,
  descargarDocumentoBoveda,
  designarCuenta,
  descargarPlantillaRequisito,
  enviarFormularioBoveda,
  esErrorApi,
  getListarCuentasBancariasQueryKey,
  getListarCuentasBancariasQueryOptions,
  getListarVinculosPublicosQueryOptions,
  getObtenerBovedaQueryOptions,
  getObtenerDocumentoBovedaQueryOptions,
  getObtenerExpedientePublicoQueryOptions,
  getObtenerImpactoDeRetiroQueryOptions,
  getObtenerInvitacionPublicaQueryOptions,
  ratificarRequisito,
  reenviarExpedientePublico,
  reintentarPostulacion,
  registrarCuentaBancaria,
  retirarCuentaBancaria,
  retirarVersionDocumento,
  type ConsentimientoDto,
  type DescargaDto,
  type EnviarFormularioDto,
  type ExpedientePublicoDto,
  type RegistrarCuentaBancariaDto,
  type RequisitoPublicoDto,
} from '@validasoft/api-client';

/** No se reintenta lo que el servidor ya rechazó (4xx): solo fallas de red o 5xx, hasta dos veces. */
export function debeReintentar(intentos: number, error: unknown): boolean {
  if (esErrorApi(error) && error.estado < 500) return false;
  return intentos < 2;
}

export function crearClienteConsultas(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: debeReintentar, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
}

/** Todo lo del tercero vive bajo `/public/v1`: al cambiar algo, se refresca entero (son pocas consultas). */
function useRefrescarTodo() {
  const cliente = useQueryClient();
  return () => {
    void cliente.invalidateQueries();
  };
}

export const useVinculosPublicos = () => useQuery(getListarVinculosPublicosQueryOptions());

export const useInvitacion = (vinculoId: string) => useQuery(getObtenerInvitacionPublicaQueryOptions(vinculoId));

export function useAceptarInvitacion(vinculoId: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({
    mutationFn: (consentimientos: ConsentimientoDto[]) => aceptarInvitacion(vinculoId, { consentimientos, otpCodigo: null }),
    onSettled: refrescar,
  });
}

/** La bóveda del tercero. Con `habilitado` falso no se pide (una sesión aislada o un token embebido de un expediente no la alcanzan: responde 403). */
export const useBoveda = (habilitado = true) => useQuery({ ...getObtenerBovedaQueryOptions(), enabled: habilitado });

export const useDocumentoBoveda = (documentoId: string, habilitado: boolean) =>
  useQuery({ ...getObtenerDocumentoBovedaQueryOptions(documentoId), enabled: habilitado });

/**
 * Qué pasará si el tercero retira la versión (RT-1): se pide al abrir el diálogo, antes del motivo. Es solo lectura, así que no se
 * guarda entre aperturas: cada vez se calcula con lo que las empresas tienen en ese momento.
 */
export const useImpactoDeRetiro = (documentoId: string, numero: number) =>
  useQuery({ ...getObtenerImpactoDeRetiroQueryOptions(documentoId, numero), staleTime: 0, gcTime: 0 });

export function useRetirarVersion(documentoId: string, numero: number) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (motivo: string) => retirarVersionDocumento(documentoId, numero, { motivo }), onSettled: refrescar });
}

/** La URL prefirmada (5 min, `Content-Disposition: attachment`) se abre en el momento: el navegador descarga el archivo. */
export function abrirDescarga(descarga: DescargaDto): DescargaDto {
  const enlace = document.createElement('a');
  enlace.href = descarga.url;
  enlace.rel = 'noopener';
  enlace.download = descarga.nombre;
  document.body.append(enlace);
  enlace.click();
  enlace.remove();
  return descarga;
}

export function useDescargarVersion() {
  return useMutation({
    mutationFn: async ({ documentoId, numero }: { documentoId: string; numero: number }) => abrirDescarga(await descargarDocumentoBoveda(documentoId, numero)),
  });
}

/** Formato (plantilla) vigente del tipo de un requisito: «descárgalo, llénalo, fírmalo y súbelo» (TD-5). */
export function useDescargarPlantilla() {
  return useMutation({
    mutationFn: async ({ expedienteId, requisitoId }: { expedienteId: string; requisitoId: string }) =>
      abrirDescarga(await descargarPlantillaRequisito(expedienteId, requisitoId)),
  });
}

/** Un requisito con la revisión a la que pertenece (empresa, id para la plantilla). */
export interface RequisitoEnContexto {
  expediente: ExpedientePublicoDto;
  requisito: RequisitoPublicoDto;
}

/**
 * Un requisito de la revisión, para subir o llenar lo que pide con sus reglas. Sin refresco automático (a diferencia de
 * la revisión): el tercero está llenando un formulario o eligiendo un archivo.
 */
export function useRequisitoPublico(expedienteId: string | null, requisitoId: string | null) {
  return useQuery({
    ...getObtenerExpedientePublicoQueryOptions(expedienteId ?? ''),
    enabled: Boolean(expedienteId && requisitoId),
    select: (expediente: ExpedientePublicoDto): RequisitoEnContexto | null => {
      const requisito = expediente.requisitos?.find((r) => r.id === requisitoId);
      return requisito ? { expediente, requisito } : null;
    },
  });
}

/** Envía un tipo `Formulario` (TD-4): nace una versión sin archivo que se comparte con el vínculo. */
export function useEnviarFormulario() {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (cuerpo: EnviarFormularioDto) => enviarFormularioBoveda(cuerpo), onSuccess: refrescar });
}

/**
 * Se refresca solo mientras falten documentos o esté devuelto: la versión subida llega por una cola (RQ-2).
 * `habilitado = false` no consulta (p. ej. «Mis empresas» con la invitación sin aceptar).
 */
export const useExpedientePublico = (id: string, habilitado = true) =>
  useQuery({ ...getObtenerExpedientePublicoQueryOptions(id), enabled: habilitado, refetchInterval: (consulta) => debeRefrescar(consulta.state.data) });

export function useReenviarExpediente(id: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (respuesta: string) => reenviarExpedientePublico(id, { respuesta: respuesta.trim() || null }), onSettled: refrescar });
}

/** «Volver a postularme» tras un rechazo (DS-4): abre un expediente inicial nuevo; vínculos y expedientes cambian. */
export function useReintentarPostulacion(vinculoId: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: () => reintentarPostulacion(vinculoId), onSettled: refrescar });
}

/** Ratifica en una renovación que el documento no cambió (RN-2): expediente, bóveda y vínculos cambian. */
export function useRatificarRequisito(expedienteId: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (requisitoId: string) => ratificarRequisito(expedienteId, requisitoId), onSettled: refrescar });
}

/** Tras subir una versión: bóveda, documento y vínculos cambian. */
export function useAlTerminarSubida() {
  return useRefrescarTodo();
}

/** Las cuentas solo cambian por lo que hace el tercero en «Mis cuentas»: basta refrescar su lista (trae las designaciones). */
function useRefrescarCuentas() {
  const cliente = useQueryClient();
  return () => {
    void cliente.invalidateQueries({ queryKey: getListarCuentasBancariasQueryKey() });
  };
}

/** Cuentas bancarias del tercero (CB-1), con la empresa y el estado de cada designación (CB-2). */
export const useCuentasBancarias = () => useQuery(getListarCuentasBancariasQueryOptions());

export function useRegistrarCuenta() {
  const refrescar = useRefrescarCuentas();
  return useMutation({ mutationFn: (cuerpo: RegistrarCuentaBancariaDto) => registrarCuentaBancaria(cuerpo), onSuccess: refrescar });
}

/** 409 `cuenta-en-uso` si está designada o por aprobar con alguna empresa. */
export function useRetirarCuenta() {
  const refrescar = useRefrescarCuentas();
  return useMutation({ mutationFn: (cuentaId: string) => retirarCuentaBancaria(cuentaId), onSettled: refrescar });
}

/** Propone la cuenta para cobrarle a la empresa del vínculo; 422 `certificacion-requerida` sin certificación en la bóveda. */
export function useDesignarCuenta(vinculoId: string) {
  const refrescar = useRefrescarCuentas();
  return useMutation({ mutationFn: (cuentaId: string) => designarCuenta(vinculoId, { cuentaId }), onSettled: refrescar });
}
