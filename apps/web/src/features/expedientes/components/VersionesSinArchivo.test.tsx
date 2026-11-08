import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { areaDeRequisito, expediente, requisito, requisitoFormulario, vigencia } from '../datosPrueba';
import type { ContextoRevisor } from '../reglas';
import { DialogoDecision } from './DialogoDecision';
import { PanelDecisionArea } from './PanelDecisionArea';
import { PanelRequisitos } from './PanelRequisitos';

const CONTEXTO: ContextoRevisor = {
  scopes: new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.documentosDescargar]),
  usuarioId: 'u-1',
  esMiembro: true,
};
const contexto = () => CONTEXTO;

/** RUT con archivo (por vencer) y un formulario SARLAFT sin archivo, ambos por decidir en Jurídica. */
const RUT = requisito({
  versionActual: { documentoId: 'doc-rut', n: 1, sha256: 'abc', cargadaEn: '2026-11-06T14:00:00Z', vigencia: vigencia({ estado: 'PorVencer', base: 'FechaCarga' }), clase: 'Archivo' },
});
const SARLAFT = requisitoFormulario();
const DATOS: ExpedienteDto = { ...expediente('EnRevision'), requisitos: [RUT, SARLAFT] };

function montar(contenido: ReactNode) {
  // Ninguna prueba debe pedir la descarga de una versión sin archivo: la API respondería 409 `version-sin-archivo`.
  const fetch = vi.fn(() => new Promise<Response>(() => undefined));
  configurarCliente({ fetch });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>{contenido}</QueryClientProvider>
    </ThemeProvider>,
  );
  return fetch;
}

afterEach(() => reiniciarCliente());

describe('«Documentos que pide el flujo»: datos declarados y vigencia', () => {
  it('una versión de formulario no ofrece «Ver» y muestra sus datos formateados', () => {
    montar(<PanelRequisitos expediente={DATOS} contexto={contexto} nombres={new Map()} onAccion={() => undefined} />);
    const formulario = screen.getByRole('heading', { name: 'Formulario SARLAFT' }).closest('li');
    if (!formulario) throw new Error('falta la tarjeta del formulario');
    const tarjeta = within(formulario);
    expect(tarjeta.queryByRole('button', { name: /^Ver/ })).not.toBeInTheDocument();
    expect(tarjeta.getByText(/Formulario, sin archivo/)).toBeInTheDocument();
    expect(tarjeta.getByText('Fecha de expedición')).toBeInTheDocument();
    expect(tarjeta.getByText('01/11/2026')).toBeInTheDocument();
    expect(tarjeta.getByText('1.234.567,5')).toBeInTheDocument();
    expect(tarjeta.getByText('Sí')).toBeInTheDocument();
    expect(tarjeta.getByText('Común')).toBeInTheDocument();
  });

  it('una versión con archivo sí ofrece «Ver» y avisa que está por vencer, con la base de la fecha', () => {
    montar(<PanelRequisitos expediente={DATOS} contexto={contexto} nombres={new Map()} onAccion={() => undefined} />);
    const archivo = screen.getByRole('heading', { name: 'Registro Único Tributario' }).closest('li');
    if (!archivo) throw new Error('falta la tarjeta del RUT');
    const tarjeta = within(archivo);
    expect(tarjeta.getByRole('button', { name: 'Ver Registro Único Tributario, versión 1' })).toBeInTheDocument();
    expect(tarjeta.getByRole('alert')).toHaveTextContent('Vence pronto: el 30/11/2026.');
    expect(tarjeta.getByText('Vence el 30/11/2026')).toBeInTheDocument();
    expect(tarjeta.getByText('Calculada desde la fecha en que el tercero cargó el documento.')).toBeInTheDocument();
  });
});

describe('Decisión por área: versiones sin archivo', () => {
  it('el formulario ofrece «Ver datos» (no «Ver») y abre el visor en modo solo datos sin pedir la descarga', async () => {
    const usuario = userEvent.setup();
    const fetch = montar(<PanelDecisionArea expediente={DATOS} contexto={contexto} onAccion={() => undefined} />);
    expect(screen.queryByRole('button', { name: /^Ver Formulario SARLAFT/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ver Registro Único Tributario, versión 1' })).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ver los datos de Formulario SARLAFT, versión 2' }));
    const visor = within(screen.getByRole('dialog'));
    expect(visor.getByText('Esta versión es un formulario: no tiene archivo para ver ni descargar.')).toBeInTheDocument();
    expect(visor.queryByRole('link', { name: 'Descargar' })).not.toBeInTheDocument();
    expect(visor.getByText('Activos totales')).toBeInTheDocument();
    expect(visor.getByText('1.234.567,5')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('sin `documentos:descargar` no se ofrece ver archivos, pero sí los datos del formulario', () => {
    const sinDescarga: ContextoRevisor = { ...CONTEXTO, scopes: new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar]) };
    montar(<PanelDecisionArea expediente={DATOS} contexto={() => sinDescarga} onAccion={() => undefined} />);
    expect(screen.queryByRole('button', { name: /^Ver Registro Único Tributario/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ver los datos de Formulario SARLAFT, versión 2' })).toBeInTheDocument();
  });
});

describe('Diálogo de decisión por documento', () => {
  it('muestra los datos declarados y la vigencia de la versión que se decide', () => {
    montar(
      <DialogoDecision
        dialogo={{ tipo: 'veredictoRequisito', requisito: SARLAFT, areaRequisito: areaDeRequisito(), nombreArea: 'Jurídica' }}
        expediente={DATOS}
        empresaId="emp"
        motivos={[]}
        aplica
        onCerrar={() => undefined}
      />,
    );
    const dialogo = within(screen.getByRole('dialog'));
    expect(dialogo.getByText('Datos que declaró el tercero')).toBeInTheDocument();
    expect(dialogo.getByText('¿Es gran contribuyente?')).toBeInTheDocument();
    expect(dialogo.getByText('Sí')).toBeInTheDocument();
    expect(dialogo.getByText('Vence el 30/11/2026')).toBeInTheDocument();
    expect(dialogo.queryByRole('button', { name: /^Ver/ })).not.toBeInTheDocument();
  });
});
