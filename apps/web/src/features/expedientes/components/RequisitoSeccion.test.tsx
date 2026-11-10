import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto, type RequisitoExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DOCUMENTO } from '../../terceros/pruebas/datos';
import { expediente, requisitoSeccion } from '../datosPrueba';
import type { ContextoRevisor } from '../reglas';
import { PanelRequisitos } from './PanelRequisitos';

const CONTEXTO: ContextoRevisor = {
  scopes: new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.documentosLeer, SCOPES.documentosDescargar]),
  usuarioId: 'u-1',
  esMiembro: true,
};

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

/** El panel con un requisito de sección; los documentos del vínculo traen el RUT (doc d1, versión 2) como soporte. */
function montar(requisito: RequisitoExpedienteDto, estado: ExpedienteDto['estado'] = 'EnRevision') {
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = entrada instanceof Request ? entrada.url : entrada.toString();
      // La descarga del soporte no responde: basta con que el visor se abra.
      return url.endsWith('/api/v1/vinculos/vin/documentos') ? Promise.resolve(json([DOCUMENTO])) : new Promise<Response>(() => undefined);
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <PanelRequisitos expediente={{ ...expediente(estado), requisitos: [requisito] }} contexto={() => CONTEXTO} nombres={new Map()} onAccion={() => undefined} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  const tarjeta = screen.getByRole('heading', { name: requisito.tipoDocumento.nombre }).closest('li');
  if (!tarjeta) throw new Error('falta la tarjeta de la sección');
  return within(tarjeta);
}

afterEach(() => reiniciarCliente());

describe('Requisito de sección en el expediente (F2)', () => {
  it('pendiente: dice lo que le falta para la empresa con sus motivos legibles, sin «Ver» ni archivo', () => {
    const tarjeta = montar(
      requisitoSeccion(
        { estado: 'Pendiente', versionActual: null },
        {
          completitud: {
            completa: false,
            faltantes: [
              { campo: 'razon_social', regla: null, mensaje: 'Falta la razón social.' },
              { campo: null, regla: 'soporte-requerido', mensaje: 'Falta el soporte de la información general.' },
            ],
            reglaVersion: 1,
          },
          versionEvaluada: 1,
          datos: null,
          soportes: [],
        },
      ),
      'Abierto',
    );
    const faltan = tarjeta.getByRole('alert', { name: 'Le faltan 2 datos para la empresa' });
    expect(faltan).toHaveTextContent('Falta la razón social.');
    expect(faltan).toHaveTextContent('Falta el soporte de la información general.');
    expect(tarjeta.getByText('Exige soporte')).toBeInTheDocument();
    expect(tarjeta.getByText('Sin soporte.')).toBeInTheDocument();
    expect(tarjeta.queryByText('El tercero aún no lo carga.')).not.toBeInTheDocument();
    expect(tarjeta.queryByRole('button', { name: /^Ver/ })).not.toBeInTheDocument();
    expect(screen.getByText(/Falta 1 documento obligatorio: Información general/)).toBeInTheDocument();
  });

  it('completa: muestra los datos de la versión en solo lectura y abre el soporte compartido en el visor', async () => {
    const usuario = userEvent.setup();
    const tarjeta = montar(requisitoSeccion({}, { soportes: [{ documentoId: 'd1', version: 2, tipoDocumento: 'RUT' }, { documentoId: 'd9', version: 1, tipoDocumento: 'SOPORTE_FICHA' }] }));

    expect(tarjeta.getByText('Completa para la empresa')).toBeInTheDocument();
    expect(tarjeta.getByText('Evaluada sobre la versión 3.')).toBeInTheDocument();
    const datos = tarjeta.getByRole('region', { name: 'Datos de la versión 3' });
    expect(datos).toHaveTextContent('Razón socialAceros del Norte S.A.S.');
    expect(datos).toHaveTextContent('Certificado ISOSí');
    // El adjunto propio de la ficha no está en los documentos del vínculo: no se ofrece abrirlo.
    expect(tarjeta.getByText('Soporte del tercero · versión 1 (no compartido con este vínculo)')).toBeInTheDocument();

    await usuario.click(await tarjeta.findByRole('button', { name: 'Ver el soporte RUT, versión 2' }));
    expect(await screen.findByRole('dialog')).toHaveTextContent('RUT · Versión 2');
  });

  it('actualización: compara contra la versión aceptada solo con lo que cambió, con filas de personas legibles', () => {
    const tarjeta = montar(
      requisitoSeccion(
        {},
        {
          versionAceptada: 2,
          cambios: [
            { campo: 'razon_social', etiqueta: 'Razón social', anterior: 'Aceros S.A.S.', nuevo: 'Aceros del Norte S.A.S.', tipoCambio: 'Modificado' },
            { campo: 'pagina_web', etiqueta: 'Página web', anterior: null, nuevo: 'https://aceros.co', tipoCambio: 'Agregado' },
            { campo: 'certificado_iso', etiqueta: 'Certificado ISO', anterior: 'Sí', nuevo: null, tipoCambio: 'Quitado' },
            { campo: 'personas[1]', etiqueta: 'Persona relacionada', anterior: null, nuevo: 'Socio: Juan Pérez (CC 79123456)', tipoCambio: 'Agregado' },
            { campo: 'personas[0].porcentaje', etiqueta: 'Socio: Ana Gómez (CC 1020) · Porcentaje de participación', anterior: '40', nuevo: '35.5', tipoCambio: 'Modificado' },
          ],
        },
      ),
    );
    const tabla = tarjeta.getByRole('table', { name: 'Qué cambió frente a la versión 2 aceptada' });
    expect(within(tabla).getAllByRole('columnheader').map((c) => c.textContent)).toEqual(['Campo', 'Antes', 'Ahora']);
    const filas = within(tabla).getAllByRole('row').slice(1);
    expect(filas.map((f) => f.getAttribute('data-tipo-cambio'))).toEqual(['Modificado', 'Agregado', 'Quitado', 'Agregado', 'Modificado']);
    expect(filas[0]).toHaveTextContent('Razón socialAceros S.A.S.Aceros del Norte S.A.S.');
    expect(filas[1]).toHaveTextContent('Página web—https://aceros.co');
    // Lo quitado va tachado.
    expect(within(filas[2]!).getByText('Sí')).toHaveStyle({ textDecoration: 'line-through' });
    expect(filas[3]).toHaveTextContent('Se agregó Persona relacionada—Socio: Juan Pérez (CC 79123456)');
    expect(filas[4]).toHaveTextContent('Se modificó Socio: Ana Gómez (CC 1020) · Porcentaje de participación40 %35,5 %');
  });
});
