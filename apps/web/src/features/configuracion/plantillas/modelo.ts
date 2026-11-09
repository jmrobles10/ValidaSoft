import { esErrorApi, type ActualizarPlantillaNotificacionDto, type PlantillaNotificacionDto } from '@validasoft/api-client';

/** Igual que `PlantillaNotificacion.MaximoAsunto` y `MaximoTexto` del dominio (PL-1). */
export const MAXIMO_ASUNTO = 150;
export const MAXIMO_TEXTO = 2000;

export type CampoPlantilla = 'asunto' | 'cabecera' | 'pie';

export const CAMPOS: readonly CampoPlantilla[] = ['asunto', 'cabecera', 'pie'];

export type BorradorPlantilla = Record<CampoPlantilla, string>;

export const maximoDe = (campo: CampoPlantilla) => (campo === 'asunto' ? MAXIMO_ASUNTO : MAXIMO_TEXTO);

export function borradorDe(plantilla: Pick<PlantillaNotificacionDto, CampoPlantilla>): BorradorPlantilla {
  return { asunto: plantilla.asunto ?? '', cabecera: plantilla.cabecera ?? '', pie: plantilla.pie ?? '' };
}

/** Lo que se envía al guardar o previsualizar: los textos vacíos van nulos (vuelven al predeterminado). */
export function cuerpoDe(borrador: BorradorPlantilla): ActualizarPlantillaNotificacionDto {
  const valor = (texto: string) => (texto.trim() ? texto : null);
  return { asunto: valor(borrador.asunto), cabecera: valor(borrador.cabecera), pie: valor(borrador.pie) };
}

export const personalizada = (plantilla: Pick<PlantillaNotificacionDto, CampoPlantilla>) =>
  plantilla.asunto !== null || plantilla.cabecera !== null || plantilla.pie !== null;

/** `{{empresa}}`: se arma aquí porque i18next interpolaría las llaves de un texto traducido. */
export const marcador = (variable: string) => `{{${variable}}}`;

/** Inserta la variable en la selección (o al final) y devuelve el texto y dónde queda el cursor. */
export function insertarVariable(texto: string, variable: string, inicio: number | null, fin: number | null) {
  const desde = inicio ?? texto.length;
  const hasta = fin ?? desde;
  const insertado = marcador(variable);
  return { texto: texto.slice(0, desde) + insertado + texto.slice(hasta), cursor: desde + insertado.length };
}

const CODIGOS_DEL_ASUNTO = new Set(['asunto-invalido', 'asunto-demasiado-largo']);

/** Campo al que pertenece un 422 de la API (extensión `campo`, o el asunto por su código); nulo si es general. */
export function campoDelError(error: unknown): CampoPlantilla | null {
  if (!esErrorApi(error) || error.estado !== 422) return null;
  const campo = error.extensiones.campo;
  if (typeof campo === 'string' && (CAMPOS as readonly string[]).includes(campo)) return campo as CampoPlantilla;
  return CODIGOS_DEL_ASUNTO.has(error.codigo) ? 'asunto' : null;
}
