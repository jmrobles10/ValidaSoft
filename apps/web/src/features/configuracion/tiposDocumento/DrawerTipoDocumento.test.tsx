import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type TipoDocumentoTenantDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DrawerTipoDocumento } from './DrawerTipoDocumento';

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

const EXISTENTE: TipoDocumentoTenantDto = {
  id: 'td1',
  codigo: 'CERT_NORTE',
  nombre: 'Certificado Norte',
  tipoCanonico: null,
  versionCanonico: null,
  ambito: 'PorVinculo',
  clase: 'Archivo',
  textoAyuda: null,
  reglaFrescura: { tipo: 'Ninguna', dias: null, regla: null },
  diasAviso: [30],
  diasGracia: 0,
  diasRevalidacion: 15,
  activo: true,
  creadoEn: '2026-11-08T20:41:38Z',
  formatosPermitidos: [],
  tamanoMaximoMb: null,
  paginasMaximas: null,
  modo: 'Archivo',
  campos: [],
  reglasEfectivas: { formatos: ['application/pdf'], tamanoMaximoBytes: 20 * 1024 * 1024, paginasMaximas: 50, descripcion: 'Certificado Norte admite solo PDF de hasta 20 MB' },
  plantilla: null,
  cargaSoportada: true,
};

function montar(tipo: TipoDocumentoTenantDto | null, responder: (url: string, metodo: string, cuerpo: unknown) => Response) {
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
  const onCerrar = vi.fn();
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <DrawerTipoDocumento tipoDocumento={tipo} claveIdempotencia="clave-1" onCerrar={onCerrar} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { peticiones, onCerrar };
}

afterEach(() => reiniciarCliente());

describe('Tipo de documento: crear y editar', () => {
  it('crear un formulario: «Crear tipo» queda deshabilitado hasta tener código, nombre y un campo válido', async () => {
    const usuario = userEvent.setup();
    // Los textos se pegan: teclear ~50 caracteres en el drawer pasaba del tiempo en la suite completa, y lo que se prueba
    // es la validación del formulario, no el tecleo.
    const escribir = async (campo: HTMLElement, texto: string) => {
      await usuario.click(campo);
      await usuario.paste(texto);
    };
    const { peticiones, onCerrar } = montar(null, (url, metodo) => (metodo === 'POST' ? json({ ...EXISTENTE, nombre: 'Declaración de fondos' }, 201) : json(url.includes('catalogos') ? [] : {})));
    const crear = screen.getByRole('button', { name: 'Crear tipo' });
    expect(crear).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Obligatorio.');

    await escribir(screen.getByRole('textbox', { name: /Código/ }), 'decl fondos');
    await escribir(screen.getByRole('textbox', { name: /^Nombre/ }), 'Declaración de fondos');
    await usuario.click(screen.getByRole('combobox', { name: 'Se entrega como' }));
    await usuario.click(screen.getByRole('option', { name: 'Formulario' }));
    expect(screen.getByRole('status')).toHaveTextContent('Agrega al menos un campo al formulario.');

    await usuario.click(screen.getByRole('button', { name: 'Agregar fecha de expedición' }));
    await usuario.click(screen.getByRole('button', { name: 'Agregar campo' }));
    const campos = screen.getByRole('list', { name: 'Campos del formulario' });
    const segundo = within(campos).getAllByRole('listitem')[1];
    if (!segundo) throw new Error('falta el segundo campo');
    await escribir(within(segundo).getByRole('textbox', { name: /Etiqueta/ }), 'Origen de los fondos');
    expect(within(segundo).getByRole('textbox', { name: /Clave/ })).toHaveValue('origen_de_los_fondos');

    await waitFor(() => expect(crear).toBeEnabled());
    await usuario.click(crear);
    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(peticiones[0]?.cuerpo).toMatchObject({
      codigo: 'DECL_FONDOS',
      modo: 'Formulario',
      ambito: 'PorVinculo',
      campos: [
        { clave: 'fecha_expedicion', tipo: 'Fecha', obligatorio: true },
        { clave: 'origen_de_los_fondos', etiqueta: 'Origen de los fondos', tipo: 'Texto' },
      ],
    });
  }, 20_000);

  it('editar: muestra lo que ve el proveedor y el 422 de la API con su detalle', async () => {
    const usuario = userEvent.setup();
    montar(EXISTENTE, (url, metodo) =>
      metodo === 'PUT'
        ? json({ codigo: 'limite-sobre-el-global', detail: 'El peso máximo supera el límite general de 20 MB.', status: 422 }, 422)
        : json(url.includes('plantilla') ? { tipoDocumentoId: 'td1', vigente: null, historial: [] } : []),
    );
    expect(screen.getByText(/Lo que ve el proveedor: «Certificado Norte admite solo PDF de hasta 20 MB»/)).toBeInTheDocument();
    await usuario.type(screen.getByRole('textbox', { name: /Peso máximo/ }), '50');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(await screen.findByText(/supera el límite general de 20 MB/)).toBeInTheDocument();
    expect(screen.getByText('Sin plantilla.')).toBeInTheDocument();
  });
});
