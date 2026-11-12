import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type TextoLegalDto, type TipoIdentificacionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DialogoEmpresa } from './DialogoEmpresa';
import { FilaTextoLegal } from './FilaTextoLegal';
import { borradorEmpresaNuevo, cuerpoCrearEmpresa, ejemploFolio, identificacionVisible, numeroNormalizado, validarEmpresa, validarTexto } from './modelo';

const NIT: TipoIdentificacionDto = { pais: 'CO', codigo: 'NIT', nombre: 'Número de Identificación Tributaria', aplicaA: 'Ambas', patron: '^[0-9]{6,15}$', tieneDv: true };
const PAISES = [{ codigo: 'CO', nombre: 'Colombia', tiposIdentificacion: [NIT] }];

const VIGENTE: TextoLegalDto = {
  id: 'tl1',
  ambito: 'Empresa',
  empresaId: 'e1',
  tipo: 'AutorizacionEmpresa',
  version: 2,
  hash: 'abc',
  contenido: 'Autorizo el tratamiento de mis datos.',
  publicadoEn: '2026-10-01T15:00:00Z',
  publicadoPor: 'u1',
};

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(ui: ReactNode, responder: (url: string, metodo: string, cuerpo: unknown) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
      return Promise.resolve(responder(url, metodo, cuerpo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => reiniciarCliente());

describe('modelo de empresas', () => {
  it('valida identificación, razón social y prefijo como el dominio', () => {
    const base = { ...borradorEmpresaNuevo(), numero: '900.123.456-8', nombre: 'Constructora Norte', prefijo: 'norte' };
    expect(validarEmpresa(base, NIT)).toEqual([]);
    expect(numeroNormalizado('900.123.456-8', NIT)).toBe('900123456');
    expect(validarEmpresa({ ...base, numero: '12AB' }, NIT)).toEqual(['numero-invalido']);
    expect(validarEmpresa({ ...base, prefijo: 'N' }, NIT)).toEqual(['prefijo-invalido']);
    expect(validarEmpresa({ ...base, prefijo: 'NORTE-1' }, NIT)).toEqual(['prefijo-invalido']);
    expect(validarEmpresa(borradorEmpresaNuevo(), NIT)).toEqual(['numero-requerido', 'nombre-requerido', 'prefijo-invalido']);
    expect(cuerpoCrearEmpresa(base)).toEqual({
      identificacion: { pais: 'CO', tipo: 'NIT', numero: '900.123.456-8', digitoVerificacion: null },
      nombre: 'Constructora Norte',
      prefijoFolio: 'NORTE',
      logoUrl: null,
      replyTo: null,
    });
  });

  it('muestra la identificación con su dígito y un folio de ejemplo', () => {
    expect(identificacionVisible({ pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' })).toBe('NIT 900123456-8');
    expect(identificacionVisible({ pais: 'CO', tipo: 'CC', numero: '1020304', digitoVerificacion: null })).toBe('CC 1020304');
    expect(ejemploFolio('norte', 2026)).toBe('NORTE-E-2026-000123');
  });

  it('un texto legal no se publica vacío, demasiado largo ni igual al vigente', () => {
    expect(validarTexto('  ', null)).toBe('contenido-requerido');
    expect(validarTexto('x'.repeat(100_001), null)).toBe('demasiado-largo');
    expect(validarTexto(` ${VIGENTE.contenido} `, VIGENTE)).toBe('sin-cambios');
    expect(validarTexto('Texto nuevo', VIGENTE)).toBeNull();
  });
});

describe('agregar una empresa', () => {
  it('valida el NIT con el patrón del catálogo y crea la empresa con el prefijo en mayúsculas', async () => {
    const usuario = userEvent.setup();
    const onCreada = vi.fn();
    const peticiones = montar(<DialogoEmpresa claveIdempotencia="idem-1" onCerrar={() => undefined} onCreada={onCreada} />, (url, metodo, cuerpo) =>
      url.includes('/catalogos/paises') ? json(PAISES) : metodo === 'POST' ? json({ id: 'e9', activa: true, ...(cuerpo as object) }, 201) : json({}),
    );
    const agregar = screen.getByRole('button', { name: 'Agregar empresa' });
    expect(agregar).toBeDisabled();

    const numero = screen.getByRole('textbox', { name: /Número de identificación/ });
    await usuario.type(numero, '12AB');
    await usuario.tab();
    // En el campo (ya tocado) y junto al botón deshabilitado.
    expect(await screen.findAllByText('El número no tiene el formato de Número de Identificación Tributaria.')).toHaveLength(2);
    await usuario.clear(numero);
    await usuario.type(numero, '900123456-8');
    await usuario.type(screen.getByRole('textbox', { name: /Razón social/ }), 'Constructora Norte');
    await usuario.type(screen.getByRole('textbox', { name: /Prefijo de folio/ }), 'norte-1');
    expect(screen.getByRole('textbox', { name: /Prefijo de folio/ })).toHaveValue('NORTE1');
    expect(screen.getByText(/NORTE1-E-\d{4}-000123/)).toBeInTheDocument();

    await waitFor(() => expect(agregar).toBeEnabled());
    await usuario.click(agregar);
    await waitFor(() => expect(onCreada).toHaveBeenCalled());
    expect(peticiones[0]).toMatchObject({
      url: '/api/v1/empresas',
      metodo: 'POST',
      cuerpo: { identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456-8' }, nombre: 'Constructora Norte', prefijoFolio: 'NORTE1' },
    });
  });
});

describe('textos legales de una empresa', () => {
  it('muestra la versión vigente y publica una nueva solo tras confirmar que los terceros deberán aceptarla', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(
      <ul>
        <FilaTextoLegal empresaId="e1" empresaNombre="Constructora Norte" ruta="autorizacion" vigente={VIGENTE} puedeEscribir />
      </ul>,
      (_url, _metodo, cuerpo) => json({ ...VIGENTE, version: 3, contenido: (cuerpo as { contenido: string }).contenido }),
    );
    expect(screen.getByText('Autorización de tratamiento de datos')).toBeInTheDocument();
    expect(screen.getByText(/Versión 2/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Publicar versión nueva' }));
    expect(screen.getByText(/deberán aceptarla de nuevo/)).toBeInTheDocument();
    const publicar = screen.getByRole('button', { name: 'Publicar versión 3' });
    expect(publicar).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Es igual a la versión vigente');

    const campo = screen.getByRole('textbox', { name: 'Texto de la versión 3' });
    await usuario.clear(campo);
    await usuario.type(campo, 'Autorizo el tratamiento de mis datos y su transferencia.');
    await usuario.click(publicar);
    const dialogo = await screen.findByRole('dialog');
    expect(within(dialogo).getByText(/los terceros deberán aceptarla de nuevo/)).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);

    await usuario.click(within(dialogo).getByRole('button', { name: 'Publicar versión 3' }));
    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]).toEqual({
      url: '/api/v1/empresas/e1/textos-legales/autorizacion',
      metodo: 'PUT',
      cuerpo: { contenido: 'Autorizo el tratamiento de mis datos y su transferencia.' },
    });
  });

  it('sin permiso de escritura solo deja leer el texto', async () => {
    const usuario = userEvent.setup();
    montar(
      <ul>
        <FilaTextoLegal empresaId="e1" empresaNombre="Constructora Norte" ruta="autorizacion" vigente={VIGENTE} puedeEscribir={false} />
      </ul>,
      () => json({}),
    );
    expect(screen.queryByRole('button', { name: /Publicar/ })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Ver texto' }));
    expect(screen.getByText(VIGENTE.contenido)).toBeInTheDocument();
  });
});
