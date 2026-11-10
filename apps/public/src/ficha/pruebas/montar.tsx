import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  configurarCliente,
  type DocumentoBovedaDto,
  type EntradaCatalogoDto,
  type EstadoFichaDto,
  type EstadoSeccionFichaDto,
  type ExpedientePublicoDto,
  type FaltanteSeccionDto,
  type SeccionFichaDto,
  type SeccionGuardadaDto,
  type VinculoPublicoDto,
} from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { vi } from 'vitest';
import { PaginaFicha } from '../PaginaFicha';
import { PaginaSeccion } from '../PaginaSeccion';
import { Ubicacion } from './Ubicacion';

export const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

const NOMBRES: Record<string, string> = {
  FICHA_GENERAL: 'Información general',
  FICHA_TRIBUTARIA: 'Información tributaria',
  FICHA_LEGAL: 'Información legal',
  FICHA_FINANCIERA: 'Información financiera',
  FICHA_SST: 'Seguridad y salud en el trabajo',
  FICHA_ESPECIALIDADES: 'Especialidades',
};

export const ORDEN_JURIDICA = ['FICHA_GENERAL', 'FICHA_TRIBUTARIA', 'FICHA_LEGAL', 'FICHA_FINANCIERA', 'FICHA_SST', 'FICHA_ESPECIALIDADES'];
export const ORDEN_NATURAL = ['FICHA_GENERAL', 'FICHA_TRIBUTARIA', 'FICHA_SST', 'FICHA_ESPECIALIDADES'];

export function filaEstado(seccion: string, cambios: Partial<EstadoSeccionFichaDto> = {}): EstadoSeccionFichaDto {
  return { seccion, nombre: NOMBRES[seccion] ?? seccion, periodo: null, obligatoria: true, version: null, completa: false, faltantes: [], reglaVersion: null, actualizada: null, ...cambios };
}

export function estadoFicha(cambios: Partial<EstadoFichaDto> = {}): EstadoFichaDto {
  const orden = cambios.orden ?? ORDEN_JURIDICA;
  return { terceroId: 't1', tipoPersona: 'Juridica', orden, siguienteSeccion: orden[0] ?? null, completa: false, secciones: orden.map((s) => filaEstado(s)), ...cambios };
}

export function seccion(codigo: string, cambios: Partial<SeccionFichaDto> = {}): SeccionFichaDto {
  return { seccion: codigo, nombre: NOMBRES[codigo] ?? codigo, periodo: null, version: null, datos: null, soportes: [], completitud: null, huella: null, origen: null, fecha: null, versiones: 0, esquemaVersion: 1, ...cambios };
}

export function guardada(codigo: string, faltantes: FaltanteSeccionDto[], siguienteSeccion: string | null, datos: Record<string, unknown> = {}): SeccionGuardadaDto {
  const completitud = { completa: faltantes.length === 0, faltantes, reglaVersion: 1 };
  return { version: 1, creada: true, completitud, siguienteSeccion, seccion: seccion(codigo, { version: 1, datos, completitud, versiones: 1 }) };
}

export function documento(id: string, tipo: string, nombre: string, cambios: Partial<DocumentoBovedaDto> = {}): DocumentoBovedaDto {
  return {
    id,
    tipo: { codigo: tipo, nombre, tipoDocumentoTenantId: null },
    ambito: 'Global',
    clase: 'Archivo',
    vinculoId: null,
    versiones: 2,
    porEmpresa: [],
    versionActual: {
      documentoId: id,
      n: 2,
      estado: 'Actual',
      cargadaEn: '2026-10-01T15:00:00Z',
      cargadaPor: 'tercero',
      clase: 'Archivo',
      nombreOriginal: `${tipo}.pdf`,
      mime: 'application/pdf',
      tamanoBytes: 1000,
      paginas: 1,
      sha256: 'abc',
      motivoDevolucion: null,
      detalleDevolucion: null,
      hechos: null,
    },
    ...cambios,
  };
}

export interface Peticion {
  metodo: string;
  ruta: string;
  parametros: URLSearchParams;
  cuerpo: unknown;
}

interface Escenario {
  estado: EstadoFichaDto;
  secciones?: Record<string, SeccionFichaDto>;
  documentos?: DocumentoBovedaDto[];
  catalogos?: Record<string, EntradaCatalogoDto[]>;
  /** Respuesta del PUT de la sección. */
  alGuardar?: (p: Peticion) => Response;
  /** Vínculos del tercero y sus expedientes (las observaciones de las empresas, F2). */
  vinculos?: VinculoPublicoDto[];
  expedientes?: Record<string, ExpedientePublicoDto>;
}

/** Monta el wizard en `ruta` con una API falsa; devuelve los PUT que recibió. */
export function montarFicha(ruta: string, escenario: Escenario) {
  const guardados: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      const peticion: Peticion = { metodo, ruta: url.pathname, parametros: url.searchParams, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null };
      if (metodo === 'PUT') {
        guardados.push(peticion);
        return Promise.resolve(escenario.alGuardar?.(peticion) ?? json(500, {}));
      }
      if (url.pathname === '/public/v1/ficha') return Promise.resolve(json(200, escenario.estado));
      if (url.pathname.startsWith('/public/v1/ficha/')) {
        const codigo = url.pathname.split('/').pop() ?? '';
        return Promise.resolve(json(200, escenario.secciones?.[codigo] ?? seccion(codigo)));
      }
      if (url.pathname === '/public/v1/vinculos' && escenario.vinculos) return Promise.resolve(json(200, escenario.vinculos));
      const expediente = url.pathname.startsWith('/public/v1/expedientes/') ? escenario.expedientes?.[url.pathname.split('/').pop() ?? ''] : undefined;
      if (expediente) return Promise.resolve(json(200, expediente));
      if (url.pathname === '/public/v1/boveda') return Promise.resolve(json(200, { terceroId: 't1', documentos: escenario.documentos ?? [] }));
      if (url.pathname.startsWith('/public/v1/catalogos/')) {
        const catalogo = url.pathname.split('/').pop() ?? '';
        const q = (url.searchParams.get('q') ?? '').toLowerCase();
        const datos = (escenario.catalogos?.[catalogo] ?? []).filter((e) => !q || e.codigo.toLowerCase().startsWith(q) || e.nombre.toLowerCase().includes(q));
        return Promise.resolve(json(200, { datos, siguienteCursor: null }));
      }
      return Promise.resolve(json(404, { status: 404, codigo: 'no-encontrado', detail: url.pathname }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/ficha" element={<PaginaFicha />} />
            <Route path="/ficha/:seccion" element={<PaginaSeccion />} />
            <Route path="*" element={null} />
          </Routes>
          <Ubicacion />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return guardados;
}

/** Pega el texto en vez de teclearlo letra por letra: la suite completa corre en paralelo y teclear es lento. */
export async function escribir(usuario: ReturnType<typeof userEvent.setup>, campo: HTMLElement, texto: string) {
  await usuario.click(campo);
  await usuario.paste(texto);
}
