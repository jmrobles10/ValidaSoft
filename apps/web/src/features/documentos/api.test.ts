import type { DocumentoVistaEmpresaDto, VersionDocumentoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { hashCorto, tipoDeVisor, versionVisible } from './api';

const version = (n: number): VersionDocumentoDto => ({
  documentoId: 'd1',
  n,
  estado: 'Actual',
  cargadaEn: '2026-11-06T12:00:00Z',
  cargadaPor: 'Tercero',
  clase: 'Archivo',
  nombreOriginal: `rut-v${String(n)}.pdf`,
  mime: 'application/pdf',
  tamanoBytes: 1000,
  paginas: 1,
  sha256: 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
  motivoDevolucion: null,
  detalleDevolucion: null,
  hechos: null,
});

const documento = (versionActual: number | null, ns: number[]): DocumentoVistaEmpresaDto => ({
  documentoId: 'd1',
  tipo: { codigo: 'RUT', nombre: 'Registro Único Tributario', tipoDocumentoTenantId: null },
  ambito: 'Global',
  versionActual,
  versionesCompartidas: ns.map(version),
});

describe('documentos del vínculo', () => {
  it('el visor abre PDF e imágenes; lo demás solo se descarga', () => {
    expect(tipoDeVisor('application/pdf')).toBe('pdf');
    expect(tipoDeVisor('image/webp')).toBe('imagen');
    expect(tipoDeVisor('application/zip')).toBe('otro');
  });

  it('muestra la versión vigente para la empresa o, si no la tiene, la más reciente compartida', () => {
    expect(versionVisible(documento(2, [1, 2]))?.n).toBe(2);
    expect(versionVisible(documento(3, [1, 2]))?.n).toBe(2);
    expect(versionVisible(documento(null, []))).toBeNull();
  });

  it('acorta el hash a 12 caracteres', () => {
    expect(hashCorto('ba7816bf8f01cfea414140de5dae2223')).toBe('ba7816bf8f01…');
  });
});
