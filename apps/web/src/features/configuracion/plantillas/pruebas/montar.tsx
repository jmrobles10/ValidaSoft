import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { configurarCliente, type PlantillaNotificacionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { vi } from 'vitest';
import { PaginaPlantillas } from '../PaginaPlantillas';

export const INVITACION: PlantillaNotificacionDto = {
  evento: 'invitacion',
  canal: 'correo',
  nombre: 'Invitación',
  descripcion: 'Invitación de la empresa con el enlace de acceso para validar el vínculo.',
  asuntoPorDefecto: 'Constructora Norte te invita a validar tu vínculo como proveedor',
  asunto: null,
  cabecera: null,
  pie: null,
  variablesPermitidas: ['empresa', 'tipoVinculo'],
  version: 0,
  actualizadaEn: null,
};

export const VENCIDO: PlantillaNotificacionDto = {
  ...INVITACION,
  evento: 'vencido',
  nombre: 'Documento vencido',
  descripcion: 'Un documento venció para la empresa y pasó el plazo de gracia.',
  asuntoPorDefecto: 'Tu RUT venció para Constructora Norte',
  asunto: '{{documento}} vencido',
  pie: 'Compras · 601 555 0101',
  variablesPermitidas: ['empresa', 'documento'],
  version: 3,
  actualizadaEn: '2026-11-09T10:00:00Z',
};

export interface Peticion {
  metodo: string;
  url: string;
  cuerpo: unknown;
}

export const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

/** Vista previa falsa: refleja el asunto y la cabecera recibidos, como haría la API con los datos de ejemplo. */
export function previaDe(cuerpo: { asunto: string | null; cabecera: string | null; pie: string | null }) {
  const asunto = (cuerpo.asunto ?? 'Constructora Norte te invita').replaceAll('{{empresa}}', 'Constructora Norte');
  return json({ asunto, texto: [cuerpo.cabecera, 'Cuerpo del correo.', cuerpo.pie].filter(Boolean).join('\n\n'), html: `<p>${cuerpo.cabecera ?? ''}</p><p>Cuerpo</p>` });
}

/** API falsa: `responder` decide la respuesta de cada petición (la lista por defecto); guarda método, URL y cuerpo. */
export function montar(responder: (p: Peticion) => Response | undefined = () => undefined, plantillas = [INVITACION, VENCIDO]) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const peticion = { metodo: init?.method ?? 'GET', url, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null };
      peticiones.push(peticion);
      const propia = responder(peticion);
      if (propia) return Promise.resolve(propia);
      if (url.endsWith('/vista-previa')) return Promise.resolve(previaDe(peticion.cuerpo as Parameters<typeof previaDe>[0]));
      return Promise.resolve(json(plantillas));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <PaginaPlantillas />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}
