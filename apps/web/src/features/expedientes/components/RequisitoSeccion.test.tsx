import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto, type RequisitoExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DOCUMENTO } from '../../terceros/pruebas/datos';
import { expediente, requisito, requisitoSeccion } from '../datosPrueba';
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

/** Cruce de la versión de la sección con el RUT v2: la dirección coincide y la razón social difiere (crítica). */
const CRUCE_CON_RUT = {
  documentoId: 'doc-rut',
  versionDocumento: 2,
  tipoDocumento: 'RUT',
  nombreDocumento: 'Registro Único Tributario',
  confianza: 0.97,
  legible: true,
  fecha: '2026-11-09T12:00:00Z',
  coincidencias: 1,
  discrepancias: 1,
  noVerificables: 0,
  discrepanciaCritica: true,
  campos: [
    { campo: 'direccion', etiqueta: 'Dirección', declarado: 'Cl 80 # 10-20', extraido: 'Cl 80 # 10-20', resultado: 'Coincide', critico: false, detalle: null },
    { campo: 'razon_social', etiqueta: 'Razón social', declarado: 'Aceros del Norte S.A.S.', extraido: 'FERRETERIA OTRA LTDA', resultado: 'Discrepa', critico: true, detalle: null },
  ],
};

describe('Cruce con el documento en el requisito (F5)', () => {
  it('una sección cruzada muestra, campo por campo, lo declarado contra lo que dice el documento y avisa de lo crítico', () => {
    const tarjeta = montar(requisitoSeccion({}, { cruce: { estado: 'Cruzado', discrepanciaCritica: true, cruces: [CRUCE_CON_RUT], sinCruce: [] } }));

    const cruce = tarjeta.getByRole('region', { name: 'Cruce con el documento' });
    expect(within(cruce).getByRole('alert')).toHaveTextContent('Hay diferencias en campos críticos');
    expect(within(cruce).getByText(/Confianza de la lectura: 97/)).toBeInTheDocument();
    const tabla = within(cruce).getByRole('table', { name: 'Cruce con Registro Único Tributario · versión 2' });
    const filas = within(tabla).getAllByRole('row').slice(1);
    expect(filas.map((f) => f.getAttribute('data-resultado'))).toEqual(['Coincide', 'Discrepa']);
    expect(filas[1]).toHaveTextContent('Razón socialCríticoAceros del Norte S.A.S.FERRETERIA OTRA LTDADifiere');
    expect(within(cruce).getByText('Es evidencia para la revisión: el cruce no aprueba ni observa nada por sí mismo.')).toBeInTheDocument();
  });

  it('sin la lectura automática para la empresa dice que se revise a mano, sin tabla', () => {
    const tarjeta = montar(
      requisitoSeccion(
        {},
        { cruce: { estado: 'Manual', discrepanciaCritica: false, cruces: [], sinCruce: [{ documentoId: 'doc-rut', version: 2, tipoDocumento: 'RUT', nombreDocumento: 'RUT', motivo: 'sin-analisis' }] } },
      ),
    );
    const cruce = tarjeta.getByRole('region', { name: 'Cruce con el documento' });
    expect(cruce).toHaveTextContent('Sin cruce automático: tu empresa no recibió la lectura de');
    expect(cruce).toHaveTextContent('versión 2');
    expect(within(cruce).queryByRole('table')).not.toBeInTheDocument();
  });

  it('un documento exigido muestra qué secciones respalda y el resultado del cruce de cada una', () => {
    const tarjeta = montar(
      requisito({
        soporteDeSecciones: {
          sinAnalisis: false,
          discrepanciaCritica: true,
          secciones: [
            { seccion: 'FICHA_GENERAL', nombre: 'Información general', periodo: null, version: 3, cruce: CRUCE_CON_RUT },
            { seccion: 'FICHA_TRIBUTARIA', nombre: 'Información tributaria', periodo: null, version: 1, cruce: null },
            { seccion: 'FICHA_LEGAL', nombre: 'Información legal', periodo: null, version: null, cruce: null },
          ],
        },
      }),
    );

    const soporte = tarjeta.getByRole('region', { name: 'Soporte de la información' });
    expect(within(soporte).getByRole('alert')).toHaveTextContent('Hay diferencias en campos críticos');
    const filas = within(soporte).getAllByRole('listitem');
    expect(filas[0]).toHaveTextContent('Información general (versión 3)');
    expect(filas[0]).toHaveTextContent('Difiere en campos críticos');
    expect(within(filas[0]!).getByRole('table', { name: 'Cruce con Registro Único Tributario · versión 2' })).toBeInTheDocument();
    expect(filas[1]).toHaveTextContent('Información tributaria (versión 1)Sin cruce con esta versión.');
    expect(filas[2]).toHaveTextContent('Información legal: sin diligenciar.');
  });

  it('un documento sin la lectura automática para la empresa lo avisa y compara a mano', () => {
    const tarjeta = montar(
      requisito({
        soporteDeSecciones: {
          sinAnalisis: true,
          discrepanciaCritica: false,
          secciones: [{ seccion: 'FICHA_GENERAL', nombre: 'Información general', periodo: null, version: 3, cruce: null }],
        },
      }),
    );
    const soporte = tarjeta.getByRole('region', { name: 'Soporte de la información' });
    expect(soporte).toHaveTextContent('Tu empresa no recibió la lectura automática de esta versión: compara a mano con las secciones.');
    expect(within(soporte).queryByText(/Hay diferencias en campos críticos/)).not.toBeInTheDocument();
  });
});

describe('Requisito de sección: un cambio sin soporte (FM-15, FD-4)', () => {
  const CAMBIOS = [{ campo: 'razon_social', etiqueta: 'Razón social', anterior: 'Aceros del Norte S.A.S.', nuevo: 'Aceros del Sur S.A.S.', tipoCambio: 'Modificado' }];

  it('lo marca con los campos que cambiaron y avisa que aprobarlo pide una justificación si es crítico', () => {
    const tarjeta = montar(requisitoSeccion({}, { sinSoporte: { critico: true, cambios: CAMBIOS } }));
    const aviso = tarjeta.getByRole('alert', { name: 'Cambio sin soporte' });
    expect(aviso).toHaveTextContent('El tercero cambió datos críticos sin un documento nuevo que los respalde.');
    expect(aviso).toHaveTextContent('Razón social');
    expect(aviso).toHaveTextContent('escribe una justificación');
  });

  it('uno que no es crítico solo se marca, sin pedir justificación', () => {
    const tarjeta = montar(requisitoSeccion({}, { sinSoporte: { critico: false, cambios: [{ ...CAMBIOS[0]!, campo: 'direccion', etiqueta: 'Dirección' }] } }));
    const aviso = tarjeta.getByRole('alert', { name: 'Cambio sin soporte' });
    expect(aviso).toHaveTextContent('El tercero cambió estos datos sin un documento nuevo que los respalde.');
    expect(aviso).not.toHaveTextContent('justificación');
  });

  it('sin marca de la API no hay aviso', () => {
    const tarjeta = montar(requisitoSeccion());
    expect(tarjeta.queryByRole('alert', { name: 'Cambio sin soporte' })).not.toBeInTheDocument();
  });
});

describe('Documento que respalda las cuentas: la certificación bancaria (FD-5)', () => {
  const CRUCE_CUENTA = {
    documentoId: 'd-cert',
    versionDocumento: 1,
    tipoDocumento: 'CERTIFICACION_BANCARIA',
    nombreDocumento: 'Certificación bancaria',
    confianza: 0.93,
    legible: true,
    fecha: '2026-11-08T14:00:00Z',
    coincidencias: 1,
    discrepancias: 1,
    noVerificables: 0,
    discrepanciaCritica: true,
    campos: [
      { campo: 'numero', etiqueta: 'Número de la cuenta', declarado: '****5678', extraido: '****9999', resultado: 'Discrepa', critico: true, detalle: null },
      { campo: 'banco', etiqueta: 'Banco', declarado: 'Bancolombia', extraido: 'Bancolombia S.A.', resultado: 'Coincide', critico: false, detalle: null },
    ],
  };

  it('muestra cada cuenta con su nombre enmascarado y su cruce, sin llamarla «sección»', () => {
    const tarjeta = montar(
      requisito({
        tipoDocumento: { id: 'tc', codigo: 'CERTIFICACION_BANCARIA', nombre: 'Certificación bancaria', tipoCanonico: 'CERTIFICACION_BANCARIA', ambito: 'Global', modo: 'Archivo' },
        soporteDeSecciones: {
          sinAnalisis: false,
          discrepanciaCritica: true,
          secciones: [
            { seccion: 'CUENTAS_BANCARIAS', nombre: 'Cuenta de Bancolombia ****5678', periodo: null, version: null, cruce: CRUCE_CUENTA },
            { seccion: 'CUENTAS_BANCARIAS', nombre: 'Cuenta de Davivienda ****1111', periodo: null, version: null, cruce: null },
          ],
        },
      }),
    );
    const soporte = tarjeta.getByRole('region', { name: 'Soporte de la información' });
    const filas = within(soporte).getAllByRole('listitem');
    expect(filas[0]).toHaveTextContent('Cuenta de Bancolombia ****5678');
    expect(filas[0]).not.toHaveTextContent('sin diligenciar');
    expect(filas[0]).toHaveTextContent('Difiere en campos críticos');
    expect(filas[0]).toHaveTextContent('****9999');
    // Una cuenta sin análisis para la empresa dice que no hay cruce con esta versión, no que esté «sin diligenciar».
    expect(filas[1]).toHaveTextContent('Cuenta de Davivienda ****1111Sin cruce con esta versión.');
    expect(filas[1]).not.toHaveTextContent('sin diligenciar');
  });
});
