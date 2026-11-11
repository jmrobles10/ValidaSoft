import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto, type RevisionAutomaticaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { expediente, requisito } from '../../expedientes/datosPrueba';
import { PanelRequisitos } from '../../expedientes/components/PanelRequisitos';
import type { ContextoRevisor } from '../../expedientes/reglas';

const CONTEXTO: ContextoRevisor = {
  scopes: new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.documentosLeer]),
  usuarioId: 'u-1',
  esMiembro: true,
};

function revision(cambios: Partial<RevisionAutomaticaDto> = {}): RevisionAutomaticaDto {
  return {
    areaId: 'a-juridica',
    areaCodigo: 'JURIDICA',
    sobreVersion: 1,
    nivel: 'Automatico',
    resultado: 'AprobadaAutomaticamente',
    sugerencia: null,
    motivos: [],
    confianza: 0.97,
    fecha: '2026-11-10T12:00:00Z',
    pendienteDeAtencion: false,
    resolucion: null,
    resueltaPor: null,
    resueltaEn: null,
    justificacionResolucion: null,
    ...cambios,
  };
}

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Opciones {
  estado?: ExpedienteDto['estado'];
  contexto?: ContextoRevisor;
  nombres?: ReadonlyMap<string, string>;
}

/** El panel de documentos del expediente con el RUT y lo que la revisión automática registró de él. */
function montar(revisiones: RevisionAutomaticaDto[], { estado = 'EnRevision', contexto = CONTEXTO, nombres = new Map() }: Opciones = {}) {
  const resoluciones: unknown[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) =>
      Promise.resolve(
        ((): Response => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      if (url.pathname.endsWith('/revision-automatica/resolver')) {
        resoluciones.push(typeof init?.body === 'string' ? JSON.parse(init.body) : null);
        return json({ ...expediente(estado), requisitos: [] });
      }
      return json([]);
        })(),
      ),
    ),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <PanelRequisitos
          expediente={{ ...expediente(estado), requisitos: [requisito({ revisionesAutomaticas: revisiones })] }}
          contexto={() => contexto}
          nombres={nombres}
          onAccion={() => undefined}
        />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { resoluciones };
}

const panel = () => within(screen.getByRole('region', { name: 'Decisión automática' }));

afterEach(() => {
  reiniciarCliente();
  useAvisos.setState({ actual: null });
});

describe('Decisión de la revisión automática en el documento', () => {
  it('Asistido: «La IA sugiere aprobar», sin ningún botón', () => {
    montar([revision({ nivel: 'Asistido', resultado: 'Sugerida', sugerencia: 'Aprobar' })]);
    expect(panel().getByText('Jurídica')).toBeInTheDocument();
    expect(panel().getByText('La IA sugiere aprobar')).toBeInTheDocument();
    expect(panel().queryByRole('button')).not.toBeInTheDocument();
  });

  it('Asistido: «La IA sugiere revisar» con sus motivos en español', () => {
    montar([
      revision({
        nivel: 'Asistido',
        resultado: 'Sugerida',
        sugerencia: 'Revisar',
        motivos: [
          { codigo: 'confianza-baja', texto: 'La confianza del análisis (70 %) está bajo el umbral de la empresa (95 %).' },
          { codigo: 'discrepancias-con-la-ficha', texto: 'El documento discrepa de la ficha del tercero.' },
        ],
      }),
    ]);
    expect(panel().getByText('La IA sugiere revisar:')).toBeInTheDocument();
    expect(within(panel().getByRole('list', { name: 'Motivos' })).getAllByRole('listitem').map((m) => m.textContent)).toEqual(['Confianza baja', 'Discrepancias con la ficha']);
  });

  it('Automático aprobado: «Aprobado por el sistema (confianza 97 %)», sin pendiente ni botones', () => {
    montar([revision()]);
    expect(panel().getByText(/^Aprobado por el sistema \(confianza 97\s?%\)$/)).toBeInTheDocument();
    expect(panel().queryByText('Pendiente de confirmar')).not.toBeInTheDocument();
    expect(panel().queryByRole('button')).not.toBeInTheDocument();
  });

  it('con muestreo pendiente se puede confirmar: manda la decisión y avisa', async () => {
    const usuario = userEvent.setup();
    const { resoluciones } = montar([revision({ resultado: 'AprobadaConMuestreo', pendienteDeAtencion: true })]);
    expect(panel().getByText('Pendiente de confirmar')).toBeInTheDocument();

    await usuario.click(panel().getByRole('button', { name: /^Confirmar la aprobación de Registro Único Tributario/ }));
    await waitFor(() => expect(resoluciones).toEqual([{ areaId: 'a-juridica', decision: 'Confirmar', justificacion: null }]));
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Confirmaste la aprobación automática de Registro Único Tributario.'));
  });

  it('con muestreo pendiente se puede revertir con una justificación', async () => {
    const usuario = userEvent.setup();
    const { resoluciones } = montar([revision({ resultado: 'AprobadaConMuestreo', pendienteDeAtencion: true })]);
    await usuario.click(panel().getByRole('button', { name: /^Revertir la aprobación de Registro Único Tributario/ }));

    const dialogo = await screen.findByRole('dialog', { name: /Revertir la aprobación automática/ });
    expect(within(dialogo).getByText(/vuelve a revisión humana/)).toBeInTheDocument();
    await usuario.type(within(dialogo).getByRole('textbox', { name: /Justificación/ }), 'El RUT no es el vigente.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Revertir aprobación' }));
    await waitFor(() => expect(resoluciones).toEqual([{ areaId: 'a-juridica', decision: 'Revertir', justificacion: 'El RUT no es el vigente.' }]));
  });

  it('un muestreo ya resuelto dice quién lo resolvió y, si lo revirtió, por qué; sin botones', () => {
    montar(
      [revision({ resultado: 'AprobadaConMuestreo', resolucion: 'Revertida', resueltaPor: '0194f6c2-4b1a-7c3e-8d2f-000000000002', justificacionResolucion: 'El RUT no es el vigente.' })],
      { nombres: new Map([['0194f6c2-4b1a-7c3e-8d2f-000000000002', 'Ana Gómez']]) },
    );
    expect(panel().getByText('Revertida por Ana Gómez · Justificación: El RUT no es el vigente.')).toBeInTheDocument();
    expect(panel().queryByText('Pendiente de confirmar')).not.toBeInTheDocument();
    expect(panel().queryByRole('button')).not.toBeInTheDocument();
  });

  it('una excepción abierta dice que requiere una persona y por qué', () => {
    montar([
      revision({
        resultado: 'Excepcion',
        confianza: 0.6,
        pendienteDeAtencion: true,
        motivos: [{ codigo: 'alertas-de-manipulacion', texto: 'El análisis detectó señales de manipulación.' }],
      }),
    ]);
    expect(panel().getByText('Requiere una persona:')).toBeInTheDocument();
    expect(within(panel().getByRole('list', { name: 'Motivos' })).getByText('Señales de manipulación')).toBeInTheDocument();
    expect(panel().queryByRole('button')).not.toBeInTheDocument();
  });

  it('una excepción que ya se cerró queda en pasado', () => {
    montar([revision({ resultado: 'Excepcion', motivos: [{ codigo: 'ilegible', texto: 'No es legible.' }] })]);
    expect(panel().getByText('Pasó a una persona:')).toBeInTheDocument();
  });

  it('fuera del alcance de la revisión automática no se muestra nada', () => {
    montar([revision({ resultado: 'SinAlcance', motivos: [{ codigo: 'tipo-no-autorizado', texto: 'El tipo no está autorizado.' }] })]);
    expect(screen.queryByRole('region', { name: 'Decisión automática' })).not.toBeInTheDocument();
    // El documento se sigue viendo como siempre.
    expect(screen.getByRole('heading', { name: 'Registro Único Tributario' })).toBeInTheDocument();
  });

  it.each([
    ['sin permiso para revisar', { ...CONTEXTO, scopes: new Set([SCOPES.expedientesLeer]) }],
    ['sin ser miembro del área', { ...CONTEXTO, esMiembro: false }],
  ])('%s no hay botones, pero sí se ve que está pendiente', (_caso, contexto) => {
    montar([revision({ resultado: 'AprobadaConMuestreo', pendienteDeAtencion: true })], { contexto });
    expect(panel().getByText('Pendiente de confirmar')).toBeInTheDocument();
    expect(panel().queryByRole('button')).not.toBeInTheDocument();
  });

  it('con el expediente ya aprobado se puede confirmar pero no revertir', () => {
    montar([revision({ resultado: 'AprobadaConMuestreo', pendienteDeAtencion: true })], { estado: 'Aprobado' });
    expect(panel().getByRole('button', { name: /^Confirmar la aprobación/ })).toBeInTheDocument();
    expect(panel().queryByRole('button', { name: /^Revertir/ })).not.toBeInTheDocument();
  });

  it('en Manual (sin registros) la tarjeta no muestra la región', () => {
    montar([]);
    expect(screen.queryByRole('region', { name: 'Decisión automática' })).not.toBeInTheDocument();
  });
});
