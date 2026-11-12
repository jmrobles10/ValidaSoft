import type { DocumentoBovedaDto, VigenciaDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { formatearDiaCivil } from '@validasoft/ui';
import { estadoDeVigencia, requiereAtencion, type EstadoVigencia } from '../vigencia/vigencia';
import { enlaceSubirVersion, sePuedeSubirVersion } from './versiones';

/** Lo que vence de un documento con una empresa (cada una calcula su propia vigencia, TD-6). */
interface VencimientoConEmpresa {
  empresa: string;
  estado: Exclude<EstadoVigencia, 'SinVencimiento'>;
  vigencia: VigenciaDto;
}

/** Una línea del bloque «Por renovar» (AvisoConsolidado de docs/06): un documento y qué hacer con él. */
export interface LineaPorRenovar {
  documentoId: string;
  documento: string;
  /** Qué pasa con cada empresa y antes de cuándo renovarlo, sin el nombre del documento. */
  texto: string;
  /** La vigencia más urgente entre las empresas, para el chip. */
  vigencia: VigenciaDto;
  /** Enlace para subir la versión nueva; `null` si se actualiza desde la revisión (formularios). */
  subir: string | null;
  /** `aaaa-mm-dd` más próxima entre las empresas; ordena las líneas. */
  fecha: string | null;
}

const URGENCIA: Readonly<Record<VencimientoConEmpresa['estado'], number>> = { Vencida: 0, EnGracia: 1, PorVencer: 2, Vigente: 3 };

/** `aaaa-mm-dd` sin hora → «31/10», para el plazo («Renueva antes del 31/10»): el año ya va en la fecha de cada empresa. */
function diaYMes(valor: string): string {
  return formatearDiaCivil(valor).slice(0, 5);
}

/** Las fechas `aaaa-mm-dd` se ordenan como texto; sin fecha va al final. */
function compararFechas(a: string | null, b: string | null): number {
  if (a === b) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  return a < b ? -1 : 1;
}

/**
 * Las empresas que reciben este documento y le calcularon una fecha. Las vigentes también cuentan: renovarlo antes las
 * deja al día a todas. Las que no lo reciben, las que no lo hacen vencer y un estado que la API agregue después, no.
 */
function vencimientos(documento: DocumentoBovedaDto): VencimientoConEmpresa[] {
  const lista: VencimientoConEmpresa[] = [];
  for (const c of documento.porEmpresa) {
    if (!c.compartido || c.version === null || !c.vigencia) continue;
    const estado = estadoDeVigencia(c.vigencia);
    if (!estado || estado === 'SinVencimiento' || (estado === 'Vigente' && !c.vigencia.vigenteHasta)) continue;
    lista.push({ empresa: c.empresa.nombre, estado, vigencia: c.vigencia });
  }
  return lista.sort(
    (a, b) => compararFechas(a.vigencia.vigenteHasta, b.vigencia.vigenteHasta) || URGENCIA[a.estado] - URGENCIA[b.estado] || a.empresa.localeCompare(b.empresa, 'es'),
  );
}

type FraseEmpresa = 'vencePara' | 'venceProntoPara' | 'vencioPara' | 'vencidoPara' | 'enGracia';

/** Cuando dos empresas seguidas llevan la misma frase, la segunda no repite el verbo: «vence para A el … y para B el …». */
const CONTINUACION: Readonly<Record<FraseEmpresa, 'paraEl' | 'para' | 'conHasta'>> = {
  vencePara: 'paraEl',
  vencioPara: 'paraEl',
  venceProntoPara: 'para',
  vencidoPara: 'para',
  enGracia: 'conHasta',
};

/** «vence para Constructora Norte el 31/10/2026», «está en el periodo de gracia con … hasta el …», «venció para …». */
function fraseEmpresa({ estado, vigencia }: VencimientoConEmpresa): { frase: FraseEmpresa; fecha: string | null } {
  const { vigenteHasta, finDeGracia } = vigencia;
  if (estado === 'EnGracia' && finDeGracia) return { frase: 'enGracia', fecha: finDeGracia };
  if (estado === 'EnGracia' || estado === 'Vencida') return vigenteHasta ? { frase: 'vencioPara', fecha: vigenteHasta } : { frase: 'vencidoPara', fecha: null };
  return vigenteHasta ? { frase: 'vencePara', fecha: vigenteHasta } : { frase: 'venceProntoPara', fecha: null };
}

function textosEmpresas(lista: readonly VencimientoConEmpresa[], t: TFunction): string[] {
  let anterior: FraseEmpresa | null = null;
  return lista.map((v) => {
    const { frase, fecha } = fraseEmpresa(v);
    const clave = frase === anterior ? CONTINUACION[frase] : frase;
    anterior = frase;
    return t(`boveda.porRenovar.${clave}`, { empresa: v.empresa, fecha: fecha ? formatearDiaCivil(fecha) : '' });
  });
}

/**
 * Con dos o más empresas, antes de cuándo renovarlo para quedar al día con todas: la fecha más próxima en que deja de
 * servirle a alguna (su vencimiento, o el fin de la gracia si ya venció). Si para alguna ya venció del todo, o no hay
 * fecha, «cuanto antes».
 */
function cierre(lista: readonly VencimientoConEmpresa[], t: TFunction): string | null {
  if (lista.length < 2) return null;
  const clave = lista.length === 2 ? 'dos' : lista.length === 3 ? 'tres' : 'todas';
  const empresas = t(`boveda.porRenovar.conLas.${clave}`);
  let plazo: string | null = null;
  for (const v of lista) {
    const limite = v.estado === 'Vencida' ? null : v.estado === 'EnGracia' ? v.vigencia.finDeGracia : v.vigencia.vigenteHasta;
    if (limite === null) return t('boveda.porRenovar.renuevaYa', { empresas });
    if (plazo === null || limite < plazo) plazo = limite;
  }
  return plazo === null ? t('boveda.porRenovar.renuevaYa', { empresas }) : t('boveda.porRenovar.renuevaAntes', { fecha: diaYMes(plazo), empresas });
}

/** Une «a», «a y b», «a, b y c» como se dice en español. */
function enumerar(partes: readonly string[]): string {
  return new Intl.ListFormat('es-CO', { style: 'long', type: 'conjunction' }).format(partes);
}

/**
 * El aviso consolidado de la bóveda (docs/02 §8.3): una línea por documento que vence pronto, está en gracia o venció
 * para alguna empresa, con lo que pasa con cada una y antes de cuándo renovarlo. Primero lo que vence antes.
 */
export function lineasPorRenovar(documentos: readonly DocumentoBovedaDto[], t: TFunction): LineaPorRenovar[] {
  const lineas: LineaPorRenovar[] = [];
  for (const documento of documentos) {
    const lista = vencimientos(documento);
    if (!lista.some((v) => requiereAtencion(v.vigencia))) continue;
    const masUrgente = lista.reduce((a, b) => (URGENCIA[b.estado] < URGENCIA[a.estado] ? b : a));
    const final = cierre(lista, t);
    const texto = `${enumerar(textosEmpresas(lista, t))}.`;
    lineas.push({
      documentoId: documento.id,
      documento: documento.tipo.nombre,
      texto: final ? `${texto} ${final}` : texto,
      vigencia: masUrgente.vigencia,
      subir: sePuedeSubirVersion(documento) ? enlaceSubirVersion(documento) : null,
      fecha: lista[0]?.vigencia.vigenteHasta ?? null,
    });
  }
  return lineas.sort((a, b) => compararFechas(a.fecha, b.fecha) || a.documento.localeCompare(b.documento, 'es'));
}
