import type { ExpedientePublicoDto, RequisitoPublicoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { destinoFijo, rutaSegura } from '../boveda/parametrosSubida';
import { accionDeRequisito, debeRefrescar, enlaceDeSubida, observacionesDeRequisito, observacionesGenerales, requisitosPorGrupo } from './requisitos';

const requisito = (id: string, estado: RequisitoPublicoDto['estado'], cambios: Partial<RequisitoPublicoDto> = {}): RequisitoPublicoDto => ({
  id,
  tipoDocumento: { id: `td-${id}`, codigo: id.toUpperCase(), nombre: id, tipoCanonico: id.toUpperCase(), ambito: 'Global' },
  obligatorio: true,
  estado,
  versionActual: estado === 'Pendiente' ? null : { documentoId: `doc-${id}`, n: 1, sha256: 'x', cargadaEn: '2026-11-06T12:00:00Z' },
  observaciones: [],
  ...cambios,
});

const observacion = (requisitoCodigo: string | null) => ({ motivos: [{ codigo: 'incompleto', texto: 'Está incompleto.' }], observacion: null, fecha: '2026-11-06T13:00:00Z', requisito: requisitoCodigo });

function expediente(estado: ExpedientePublicoDto['estado'], requisitos: RequisitoPublicoDto[], observaciones = [observacion(null)]): ExpedientePublicoDto {
  return {
    id: 'e1',
    folio: 'NORTE-E-2026-000001',
    empresa: { id: 'emp', nombre: 'Constructora Norte S.A.S.', logoUrl: null },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado,
    observaciones,
    puedeReenviar: estado === 'Observado',
    reenvios: 0,
    actualizadoEn: '2026-11-06T13:00:00Z',
    requisitos,
    requisitosFaltantes: requisitos.filter((r) => r.estado === 'Pendiente').length,
  };
}

describe('documentos que pide la empresa', () => {
  it('agrupa en «te toca» (observado primero), «lo revisa la empresa» y «listo»', () => {
    const grupos = requisitosPorGrupo(
      expediente('Observado', [requisito('rut', 'Aprobado'), requisito('camara', 'Pendiente'), requisito('poliza', 'Observado'), requisito('renta', 'EnRevision'), requisito('ef', 'Eximido')]),
    );
    expect(grupos['te-toca'].map((r) => r.id)).toEqual(['poliza', 'camara']);
    expect(grupos['en-revision'].map((r) => r.id)).toEqual(['renta']);
    expect(grupos.listo.map((r) => r.id)).toEqual(['rut', 'ef']);
  });

  it('ofrece subir lo que falta, corregir lo observado y reemplazar lo cargado; nada si está cerrado', () => {
    const abierto = expediente('Abierto', []);
    expect(accionDeRequisito({ estado: 'Pendiente' }, abierto)).toBe('subir');
    expect(accionDeRequisito({ estado: 'Observado' }, abierto)).toBe('corregir');
    expect(accionDeRequisito({ estado: 'Cargado' }, abierto)).toBe('reemplazar');
    expect(accionDeRequisito({ estado: 'Aprobado' }, abierto)).toBeNull();
    expect(accionDeRequisito({ estado: 'Pendiente' }, expediente('Aprobado', []))).toBeNull();
  });

  it('un documento aprobado que está por vencer o vencido se puede renovar aunque la revisión esté aprobada', () => {
    const porVencer = { estado: 'Aprobado' as const, versionActual: { vigencia: { estado: 'PorVencer' as const } } };
    const vigente = { estado: 'Aprobado' as const, versionActual: { vigencia: { estado: 'Vigente' as const } } };
    expect(accionDeRequisito(porVencer, expediente('Aprobado', []))).toBe('reemplazar');
    expect(accionDeRequisito({ ...porVencer, versionActual: { vigencia: { estado: 'Vencida' as const } } }, expediente('Aprobado', []))).toBe('reemplazar');
    expect(accionDeRequisito(vigente, expediente('Aprobado', []))).toBeNull();
    expect(accionDeRequisito(porVencer, expediente('Rechazado', []))).toBeNull();
  });

  it('separa las observaciones de cada documento de las del expediente completo', () => {
    const camara = requisito('camara', 'Observado');
    const exp = expediente('Observado', [camara], [observacion(null), observacion('CAMARA')]);
    expect(observacionesGenerales(exp)).toHaveLength(1);
    expect(observacionesDeRequisito(camara, exp).map((o) => o.requisito)).toEqual(['CAMARA']);
  });

  it('arma el enlace de subida: canónico global a la bóveda, tipo propio con su vínculo, el requisito y la vuelta a la revisión', () => {
    const base = 'nombre=camara&volver=%2Fexpediente%2Fe1&expediente=e1&requisito=camara';
    expect(enlaceDeSubida(requisito('camara', 'Pendiente'), null, 'e1')).toBe(`/boveda/subir?${base}&tipo=CAMARA`);
    // Con el vínculo como contexto, la API aplica las reglas de archivo que configuró la empresa.
    expect(enlaceDeSubida(requisito('camara', 'Pendiente'), 'v1', 'e1')).toBe(`/boveda/subir?${base}&tipo=CAMARA&vinculoId=v1`);
    const propio = requisito('anexo', 'Pendiente', { tipoDocumento: { id: 'td-anexo', codigo: 'ANEXO', nombre: 'Anexo', tipoCanonico: null, ambito: 'PorVinculo' } });
    expect(enlaceDeSubida(propio, 'v1', 'e1')).toBe(
      '/boveda/subir?nombre=Anexo&volver=%2Fexpediente%2Fe1&expediente=e1&requisito=anexo&tipoDocumentoTenantId=td-anexo&vinculoId=v1',
    );
    expect(enlaceDeSubida(propio, null, 'e1')).toBeNull();
  });

  it('se refresca rápido mientras falten documentos o esté devuelto, y despacio mientras la empresa revisa', () => {
    expect(debeRefrescar(expediente('Abierto', []))).toBe(4000);
    expect(debeRefrescar(expediente('Observado', []))).toBe(4000);
    expect(debeRefrescar(expediente('EnRevision', []))).toBe(30_000);
    expect(debeRefrescar(expediente('Aprobado', []))).toBe(false);
    expect(debeRefrescar(undefined)).toBe(false);
  });
});

describe('parámetros de «Subir documento»', () => {
  it('lee el destino fijado por un requisito', () => {
    expect(destinoFijo(new URLSearchParams('tipo=RUT&nombre=RUT'))).toEqual({ destino: { tipoCanonico: 'RUT' }, nombre: 'RUT' });
    expect(destinoFijo(new URLSearchParams('tipo=RUT&nombre=RUT&vinculoId=v1'))).toEqual({ destino: { tipoCanonico: 'RUT', vinculoId: 'v1' }, nombre: 'RUT' });
    expect(destinoFijo(new URLSearchParams('tipoDocumentoTenantId=t1&vinculoId=v1&nombre=Anexo'))).toEqual({ destino: { tipoDocumentoTenantId: 't1', vinculoId: 'v1' }, nombre: 'Anexo' });
    expect(destinoFijo(new URLSearchParams('documento=d1'))).toBeNull();
  });

  it('solo vuelve a rutas de la app', () => {
    expect(rutaSegura('/expediente/e1')).toBe('/expediente/e1');
    expect(rutaSegura('//evil.example')).toBeNull();
    expect(rutaSegura('https://evil.example')).toBeNull();
    expect(rutaSegura(null)).toBeNull();
  });
});
