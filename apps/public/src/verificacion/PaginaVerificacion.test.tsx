import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type CertificadoPublicoDto, type ClavesDeCertificadosDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { certificadoSellado } from '../pruebas/certificados';
import { PaginaVerificacion } from './PaginaVerificacion';

const json = (cuerpo: unknown, status = 200) =>
  new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': status < 400 ? 'application/json' : 'application/problem+json' } });

type Respuesta = () => Response;

function urlDe(entrada: RequestInfo | URL): string {
  if (typeof entrada === 'string') return entrada;
  return entrada instanceof URL ? entrada.href : entrada.url;
}

/** Monta la página como la abre el QR; `certificado` y `claves` son lo que responden la API y `.well-known`. */
function montar(ruta: string, certificado: Respuesta, claves: Respuesta) {
  const fetch = vi.fn((entrada: RequestInfo | URL) => Promise.resolve(urlDe(entrada).includes('/.well-known/') ? claves() : certificado()));
  configurarCliente({ fetch });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/verificar/:folio" element={<PaginaVerificacion />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return fetch;
}

const ruta = (c: CertificadoPublicoDto) => `/verificar/${c.folio}?huella=${c.hashContenido.slice(0, 12)}`;
const montarSellado = (c: CertificadoPublicoDto, claves: ClavesDeCertificadosDto) => montar(ruta(c), () => json(c), () => json(claves));

afterEach(() => reiniciarCliente());

describe('verificación pública del certificado', () => {
  it('vigente: estado, datos, descarga y autenticidad comprobada en el navegador', async () => {
    const { certificado, claves } = await certificadoSellado();
    const fetch = montarSellado(certificado, claves);

    expect(await screen.findByRole('heading', { name: 'Este certificado está vigente' })).toBeInTheDocument();
    // Un certificado vigente se «confirma» con la animación de éxito de marca (decorativa: el estado ya está en texto).
    expect(document.querySelector('svg g.pop')).not.toBeNull();
    expect(screen.queryByRole('img', { name: 'Listo' })).not.toBeInTheDocument();
    expect(screen.getByText(/Constructora Verificación S\.A\.S\. validó los documentos de Cliente de Verificación S\.A\.S\..*Vence el 08\/12\/2026\./)).toBeInTheDocument();
    expect(screen.getByText('NIT 90*****67')).toBeInTheDocument();
    expect(screen.getByText('VERIF-C-2026-000001-1')).toBeInTheDocument();
    expect(screen.getByText('Emisión 1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar PDF' })).toBeEnabled();

    expect(await screen.findByText('Auténtico: el contenido está íntegro y lo selló Validasoft.')).toBeInTheDocument();
    expect(screen.getByText('El contenido sellado da exactamente la huella del certificado.')).toBeInTheDocument();
    expect(screen.getByText(/La huella del enlace \([0-9a-f]{12}\) es la de este contenido\./)).toBeInTheDocument();
    expect(screen.getByText('Lo selló la clave vigente de Validasoft (vs-prueba).')).toBeInTheDocument();
    expect(screen.getByText('La firma digital (Ed25519) corresponde a este contenido.')).toBeInTheDocument();

    // La consulta lleva el folio y la huella del QR; las claves salen de la ruta fija de esta página.
    const urls = fetch.mock.calls.map(([entrada]) => urlDe(entrada));
    expect(urls).toContain(`/public/v1/verificar/VERIF-C-2026-000001-1?huella=${certificado.hashContenido.slice(0, 12)}`);
    expect(urls).toContain('/.well-known/validasoft-certificados.json');
  });

  it('en verificación (VR-11): sigue vigente, lo dice sin alarma, con su fecha de vencimiento y ofrece el PDF', async () => {
    const { certificado, claves } = await certificadoSellado({ estado: 'ConPendientes', bloqueante: false, enVerificacion: true, pdfDescargable: true, estadoDesde: '2026-11-20T15:00:00Z' });
    montarSellado(certificado, claves);

    expect(await screen.findByRole('heading', { name: 'Este certificado está en verificación' })).toBeInTheDocument();
    expect(screen.getByText(/Desde el 20\/11\/2026 Constructora Verificación S\.A\.S\. verifica de nuevo información que el titular actualizó\. Sigue vigente hasta su fecha de vencimiento/)).toBeInTheDocument();
    expect(screen.getByText(/Vence el /)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar PDF' })).toBeEnabled();
    expect(screen.queryByText('Este certificado tiene pendientes')).not.toBeInTheDocument();
  });

  it('con pendientes que ya bloquean sigue diciendo «tiene pendientes» y no ofrece el PDF', async () => {
    const { certificado, claves } = await certificadoSellado({ estado: 'ConPendientes', bloqueante: true, enVerificacion: false, pdfDescargable: false, estadoDesde: '2026-11-20T15:00:00Z' });
    montarSellado(certificado, claves);

    expect(await screen.findByRole('heading', { name: 'Este certificado tiene pendientes' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Descargar PDF' })).not.toBeInTheDocument();
  });

  it('revocado: lo dice sin rodeos y no ofrece el PDF', async () => {
    const { certificado, claves } = await certificadoSellado({ estado: 'Revocado', bloqueante: true, pdfDescargable: false, estadoDesde: '2026-11-20T15:00:00Z' });
    montarSellado(certificado, claves);

    expect(await screen.findByRole('heading', { name: 'Este certificado fue revocado' })).toBeInTheDocument();
    expect(document.querySelector('svg g.pop')).toBeNull(); // sin celebración cuando no es vigente
    expect(screen.getByText('Dejó de valer el 20/11/2026 y no puede volver a usarse. Constructora Verificación S.A.S. puede decirte el motivo.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Descargar PDF' })).not.toBeInTheDocument();
    expect(screen.getByText('El PDF solo se entrega mientras el certificado está vigente.')).toBeInTheDocument();
    expect(await screen.findByText('Auténtico: el contenido está íntegro y lo selló Validasoft.')).toBeInTheDocument();
  });

  it('reemplazado: enlace a la emisión actual', async () => {
    const { certificado, claves } = await certificadoSellado({ estado: 'Reemplazado', reemplazadoPor: 'VERIF-C-2026-000001-2', pdfDescargable: false });
    montarSellado(certificado, claves);

    expect(await screen.findByRole('heading', { name: 'Hay una emisión más reciente' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver la emisión actual (VERIF-C-2026-000001-2)' })).toHaveAttribute('href', '/verificar/VERIF-C-2026-000001-2');
  });

  it('firma inválida: no confíes, con el paso que falló', async () => {
    const { certificado } = await certificadoSellado();
    const { claves: otraClave } = await certificadoSellado();
    montarSellado(certificado, otraClave);

    expect(await screen.findByText('No confíes en este certificado: la verificación falló.')).toBeInTheDocument();
    expect(screen.getByText('La firma digital no corresponde a este contenido.')).toBeInTheDocument();
    expect(screen.getByText('El contenido sellado da exactamente la huella del certificado.')).toBeInTheDocument();
  });

  it('sin las claves públicas (p. ej. una página HTML): verificación incompleta y se puede reintentar', async () => {
    const { certificado } = await certificadoSellado();
    montar(ruta(certificado), () => json(certificado), () => new Response('<!doctype html>', { headers: { 'Content-Type': 'text/html' } }));

    expect(await screen.findByText('No pudimos completar la verificación del sello. Inténtalo de nuevo en unos minutos.')).toBeInTheDocument();
    expect(screen.getByText('No pudimos obtener las claves públicas de Validasoft.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('folio inexistente (404): mensaje claro y sin reintentar', async () => {
    montar('/verificar/VERIF-C-2026-999999?huella=5a7e0c1d9f22', () => json({ status: 404, codigo: 'certificado-no-encontrado', detail: 'No existe.' }, 404), () => json({}));

    expect(await screen.findByRole('heading', { name: 'No encontramos este certificado' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
  });

  it('servicio no disponible: lo dice y permite reintentar', async () => {
    const usuario = userEvent.setup();
    let intentos = 0;
    const certificado = await certificadoSellado();
    montar(
      ruta(certificado.certificado),
      () => (++intentos === 1 ? json({ status: 503, codigo: 'error-interno', detail: 'Caído.' }, 503) : json(certificado.certificado)),
      () => json(certificado.claves),
    );

    expect(await screen.findByRole('heading', { name: 'La verificación no está disponible' })).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByRole('heading', { name: 'Este certificado está vigente' })).toBeInTheDocument();
  });
});
