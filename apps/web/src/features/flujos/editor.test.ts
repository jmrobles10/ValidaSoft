import type { AreaDto, FlujoVersionDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  areaNueva,
  areasElegibles,
  borradorDesdeVersion,
  borradorVacio,
  erroresBloqueantes,
  etapasADto,
  firmaBorrador,
  limpiarAreasDeRequisitos,
  moverEtapa,
  requisitoNuevo,
  requisitosADto,
  validarFlujo,
  type BorradorFlujo,
  type RequisitoEnEditor,
} from './editor';

const areaDto = (id: string, miembros = 2, activa = true): AreaDto => ({ id, empresaId: 'emp', codigo: id.toUpperCase(), nombre: id, miembros: Array.from({ length: miembros }, (_, i) => `u${String(i)}`), activa });
const AREAS = [areaDto('juridica'), areaDto('compras', 1), areaDto('archivo', 1, false)];

function borrador(cambios: Partial<BorradorFlujo> = {}): BorradorFlujo {
  return {
    ...borradorVacio(),
    codigo: 'PROV_ESTANDAR',
    nombre: 'Proveedores estándar',
    tipoVinculo: 'PROVEEDOR',
    etapas: [{ clave: 'e1', areas: [areaNueva('juridica'), areaNueva('compras')] }],
    ...cambios,
  };
}

const codigos = (b: BorradorFlujo, nuevo = true) => validarFlujo(b, AREAS, nuevo).map((e) => `${e.codigo}@${e.campo}`);

describe('validarFlujo', () => {
  it('acepta dos áreas en paralelo en una etapa', () => {
    expect(validarFlujo(borrador(), AREAS, true)).toEqual([]);
  });

  it('exige código con formato, nombre y tipo al crear; al editar no valida código ni tipo', () => {
    const b = borrador({ codigo: '1mal', nombre: ' ', tipoVinculo: '' });
    expect(codigos(b)).toEqual(['codigo-invalido@codigo', 'tipo-requerido@tipoVinculo', 'nombre-requerido@nombre']);
    expect(codigos(b, false)).toEqual(['nombre-requerido@nombre']);
    expect(codigos(borrador({ codigo: 'prov_estandar' }))).toEqual([]);
  });

  it('señala etapas vacías, áreas sin elegir, repetidas e inactivas', () => {
    const b = borrador({
      etapas: [
        { clave: 'e1', areas: [areaNueva('juridica'), areaNueva('')] },
        { clave: 'e2', areas: [] },
        { clave: 'e3', areas: [areaNueva('juridica'), areaNueva('archivo')] },
      ],
    });
    expect(codigos(b)).toEqual(['area-sin-elegir@area.0.1', 'etapa-sin-areas@etapa.1', 'area-repetida@area.2.0', 'area-invalida@area.2.1']);
    expect(codigos(borrador({ etapas: [] }))).toEqual(['etapas-requeridas@etapas']);
  });

  it('aprobadores entre 1 y 10; más aprobadores que miembros es solo un aviso', () => {
    const b = borrador({ etapas: [{ clave: 'e1', areas: [{ ...areaNueva('juridica'), aprobadoresRequeridos: 11 }, { ...areaNueva('compras'), aprobadoresRequeridos: 2 }] }] });
    const errores = validarFlujo(b, AREAS, true);
    expect(errores.map((e) => e.codigo)).toEqual(['aprobadores-invalidos', 'aprobadores-sin-miembros']);
    expect(erroresBloqueantes(errores).map((e) => e.codigo)).toEqual(['aprobadores-invalidos']);
  });

  it('un área automática no rechaza y su SLA debe ser positivo', () => {
    const b = borrador({ etapas: [{ clave: 'e1', areas: [{ ...areaNueva('juridica'), automatica: true, puedeRechazarExpediente: true, slaHoras: 0 }] }] });
    expect(codigos(b)).toEqual(['sla-invalido@area.0.0', 'area-automatica-no-rechaza@area.0.0']);
  });
});

describe('etapasADto y edición', () => {
  it('numera las etapas en el orden del editor y normaliza las automáticas (1 aprobador, sin rechazo)', () => {
    const etapas = [
      { clave: 'b', areas: [{ ...areaNueva('compras'), automatica: true, aprobadoresRequeridos: 4, puedeRechazarExpediente: true }] },
      { clave: 'a', areas: [{ ...areaNueva('juridica'), aprobadoresRequeridos: 2, puedeRechazarExpediente: true, slaHoras: 48 }] },
    ];
    expect(etapasADto(etapas)).toEqual([
      { orden: 1, areas: [{ areaId: 'compras', aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: true, slaHoras: null }] },
      { orden: 2, areas: [{ areaId: 'juridica', aprobadoresRequeridos: 2, puedeRechazarExpediente: true, automatica: false, slaHoras: 48 }] },
    ]);
  });

  it('mueve etapas sin salirse de los bordes', () => {
    const etapas = [
      { clave: 'a', areas: [] },
      { clave: 'b', areas: [] },
    ];
    expect(moverEtapa(etapas, 1, -1).map((e) => e.clave)).toEqual(['b', 'a']);
    expect(moverEtapa(etapas, 0, -1).map((e) => e.clave)).toEqual(['a', 'b']);
  });

  it('el selector ofrece las áreas activas que no están en el flujo, más la de la fila', () => {
    const etapas = [{ clave: 'e1', areas: [areaNueva('juridica'), areaNueva('')] }];
    expect(areasElegibles(AREAS, etapas, '').map((a) => a.id)).toEqual(['compras']);
    expect(areasElegibles(AREAS, etapas, 'juridica').map((a) => a.id)).toEqual(['juridica', 'compras']);
  });

  it('una versión cargada en el editor conserva su firma (sin cambios sin guardar)', () => {
    const version: FlujoVersionDto = {
      id: 'v',
      version: 2,
      estado: 'Borrador',
      etapas: [{ orden: 1, areas: [{ areaId: 'juridica', aprobadoresRequeridos: 2, puedeRechazarExpediente: true, automatica: false, slaHoras: null }] }],
      politicaDevolucion: 'AlCerrarArea',
      separacionDeFunciones: true,
      creadaEn: '2026-11-06T14:00:00Z',
      publicadaEn: null,
      retiradaEn: null,
    };
    const cargado = borradorDesdeVersion({ codigo: 'PROV', nombre: 'Proveedores', tipoVinculo: 'PROVEEDOR' }, version);
    expect(firmaBorrador(cargado)).toBe(firmaBorrador(borradorDesdeVersion({ codigo: 'PROV', nombre: 'Proveedores', tipoVinculo: 'PROVEEDOR' }, version)));
    expect(firmaBorrador({ ...cargado, separacionDeFunciones: false })).not.toBe(firmaBorrador(cargado));
    expect(cargado.politicaDevolucion).toBe('AlCerrarArea');
  });

  it('la firma no depende del id que el servidor da a los requisitos: tras «Crear borrador» no hay cambios sin guardar', () => {
    const recienCreado = borrador({ requisitos: [{ ...requisitoNuevo('rut'), id: null }] });
    const delServidor = borrador({ requisitos: [{ ...requisitoNuevo('rut'), id: 'asignado-por-el-servidor' }] });
    expect(firmaBorrador(delServidor)).toBe(firmaBorrador(recienCreado));
    expect(firmaBorrador({ ...delServidor, requisitos: [{ ...requisitoNuevo('rut'), id: 'asignado-por-el-servidor', obligatorio: false }] })).not.toBe(firmaBorrador(delServidor));
  });
});

describe('requisitos del flujo (RQ-1)', () => {
  const tipo = (id: string, cambios: Partial<TipoDocumentoTenantDto> = {}): TipoDocumentoTenantDto => ({
    id,
    codigo: id.toUpperCase(),
    nombre: id,
    tipoCanonico: id.toUpperCase(),
    versionCanonico: 1,
    ambito: 'Global',
    clase: 'Archivo',
    textoAyuda: null,
    reglaFrescura: { tipo: 'NoAplica', dias: null },
    diasAviso: [],
    diasGracia: 0,
    diasRevalidacion: 0,
    activo: true,
    creadoEn: '2026-11-06T12:00:00Z',
    ...cambios,
  });
  const TIPOS = [tipo('rut'), tipo('camara'), tipo('sagrilaft', { clase: 'Formulario' })];
  const req = (tipoDocumentoTenantId: string, areasQueRevisan: string[] = [], cambios: Partial<RequisitoEnEditor> = {}): RequisitoEnEditor => ({
    ...requisitoNuevo(tipoDocumentoTenantId),
    areasQueRevisan,
    ...cambios,
  });
  const errores = (b: BorradorFlujo) => validarFlujo(b, AREAS, false, TIPOS).map((e) => `${e.codigo}@${e.campo}`);

  it('sin requisitos el flujo decide por área (FL-1); con ellos, toda área debe revisar al menos uno', () => {
    expect(errores(borrador())).toEqual([]);
    expect(errores(borrador({ requisitos: [req('rut', ['compras'])] }))).toEqual(['area-sin-requisitos@area.0.0']);
    expect(errores(borrador({ requisitos: [req('rut', ['compras']), req('camara', ['juridica'])] }))).toEqual([]);
    // Sin áreas elegidas lo revisan todas las del flujo.
    expect(errores(borrador({ requisitos: [req('rut')] }))).toEqual([]);
  });

  it('señala tipo sin elegir, repetido, inválido (formulario), área fuera del flujo y aprobadores fuera de rango', () => {
    const b = borrador({
      requisitos: [req(''), req('rut'), req('rut'), req('sagrilaft'), req('camara', ['archivo']), req('camara', [], { aprobadoresRequeridos: 11 })],
    });
    expect(errores(b)).toEqual([
      'requisito-sin-tipo@requisito.0',
      'requisito-duplicado@requisito.2',
      'requisito-invalido@requisito.3',
      'requisito-area-invalida@requisito.4',
      'requisito-duplicado@requisito.5',
      'requisito-aprobadores-invalidos@requisito.5',
    ]);
  });

  it('al quitar un área del flujo, sale también de los requisitos', () => {
    const b = borrador({ requisitos: [req('rut', ['juridica', 'compras'])] });
    const sinCompras = limpiarAreasDeRequisitos({ ...b, etapas: [{ clave: 'e1', areas: [areaNueva('juridica')] }] });
    expect(sinCompras.requisitos[0]?.areasQueRevisan).toEqual(['juridica']);
  });

  it('una versión con requisitos va y vuelve del editor sin cambios (conserva el id del requisito)', () => {
    const version: FlujoVersionDto = {
      id: 'v1',
      version: 1,
      estado: 'Publicada',
      politicaDevolucion: 'Inmediata',
      separacionDeFunciones: false,
      creadaEn: '2026-11-06T12:00:00Z',
      publicadaEn: null,
      retiradaEn: null,
      etapas: [{ orden: 1, areas: [{ areaId: 'juridica', aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: false, slaHoras: null }] }],
      requisitos: [{ id: 'r1', tipoDocumentoTenantId: 'rut', tipoDocumento: 'RUT', obligatorio: true, areasQueRevisan: [], aprobadoresRequeridos: 2 }],
    };
    const b = borradorDesdeVersion({ codigo: 'P', nombre: 'P', tipoVinculo: 'PROVEEDOR' }, version);
    expect(requisitosADto(b.requisitos)).toEqual([
      { id: 'r1', tipoDocumentoTenantId: 'rut', tipoDocumento: null, obligatorio: true, areasQueRevisan: [], aprobadoresRequeridos: 2, condicion: null },
    ]);
  });
});
