import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type RevisionAutomaticaConfigDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { PaginaRevisionAutomatica } from './PaginaRevisionAutomatica';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const CONFIG: RevisionAutomaticaConfigDto = {
  configurada: false,
  nivel: 'Manual',
  umbralConfianza: '0.95',
  porcentajeMuestreo: 10,
  topeMensualAprobaciones: null,
  tiposAutorizados: [],
  areasAutorizadas: [],
  esperaIaMinutos: 10,
  actualizadaEn: null,
  iaDisponible: true,
  umbralDelPlan: 0.85,
  uso: { mes: '2026-11', aprobadas: 0, muestreadas: 0, excepciones: 0, sugerencias: 0, tope: null },
  tiposAnalizables: [
    { codigo: 'CAMARA_COMERCIO', nombre: 'Certificado de existencia y representación legal' },
    { codigo: 'RUT', nombre: 'Registro Único Tributario' },
    { codigo: 'POLIZA', nombre: 'Póliza' },
  ],
  areas: [
    { id: 'a-compras', codigo: 'compras', nombre: 'Compras' },
    { id: 'a-cumplimiento', codigo: 'cumplimiento', nombre: 'Cumplimiento' },
  ],
};

const AUTOMATICA: RevisionAutomaticaConfigDto = {
  ...CONFIG,
  configurada: true,
  nivel: 'Automatico',
  topeMensualAprobaciones: 200,
  tiposAutorizados: ['RUT'],
  areasAutorizadas: ['a-compras'],
  actualizadaEn: '2026-11-10T12:00:00Z',
  uso: { mes: '2026-11', aprobadas: 170, muestreadas: 17, excepciones: 6, sugerencias: 0, tope: 200 },
};

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Cuerpo {
  nivel: string;
  umbralConfianza: number;
  porcentajeMuestreo: number;
  topeMensualAprobaciones: number | null;
  tiposAutorizados: string[];
  areasAutorizadas: string[];
  esperaIaMinutos: number;
}

function montar(config: RevisionAutomaticaConfigDto = CONFIG, { guardar = (cuerpo: Cuerpo) => json({ ...config, ...cuerpo, configurada: true }) } = {}) {
  const guardados: Cuerpo[] = [];
  const consultas: URL[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) =>
      Promise.resolve(
        ((): Response => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      consultas.push(url);
      if (url.pathname !== '/api/v1/configuracion/revision-automatica') return json([]);
      if ((init?.method ?? 'GET').toUpperCase() === 'PUT') {
        const cuerpo = JSON.parse(typeof init?.body === 'string' ? init.body : '{}') as Cuerpo;
        guardados.push(cuerpo);
        return guardar(cuerpo);
      }
      return json(config);
        })(),
      ),
    ),
  });
  const router = createMemoryRouter([{ path: '/e/:empresaId/revision-automatica', element: <PaginaRevisionAutomatica /> }], { initialEntries: ['/e/emp/revision-automatica'] });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { guardados, consultas };
}

const nivel = (nombre: string) => screen.getByRole('radio', { name: new RegExp(`^${nombre}`) });

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.setState({ actual: null });
});

describe('Configuración › Revisión automática', () => {
  it('sin configuracion:leer dice que no hay acceso y no pide nada', () => {
    const { consultas } = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Revisión automática');
    expect(consultas).toHaveLength(0);
  });

  it('mientras carga muestra un esqueleto, no un texto de espera', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar();
    expect(screen.getByRole('heading', { level: 1, name: 'Revisión automática' })).toBeInTheDocument();
    expect(document.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(screen.queryByText(/Cargando/)).not.toBeInTheDocument();
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });
  });

  it('sin configuración guardada es Manual: tres niveles con qué hacen y a quién avisan, y nada más que decidir', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar();
    const grupo = within(await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' }));
    expect(grupo.getAllByRole('radio')).toHaveLength(3);
    expect(nivel('Manual')).toBeChecked();
    expect(grupo.getByText('La IA analiza y solo informa. Cada área revisa y decide todo, como hasta ahora.')).toBeInTheDocument();
    expect(grupo.getByText('La IA sugiere aprobar o revisar cada documento, pero nunca aprueba. Decide siempre una persona.')).toBeInTheDocument();
    expect(grupo.getByText('La IA aprueba sola lo que no tiene excepciones. Una persona confirma una muestra y atiende el resto.')).toBeInTheDocument();
    expect(grupo.getByText('Avisa a: Los revisores del área, en Excepciones.')).toBeInTheDocument();
    // Manual no tiene parámetros ni uso que mostrar.
    expect(screen.queryByText('Confianza mínima')).not.toBeInTheDocument();
    expect(screen.queryByText(/^Uso de /)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('Asistido pide solo la confianza mínima y se guarda sin avisos', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    const { guardados } = montar();
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });

    await usuario.click(nivel('Asistido'));
    expect(screen.getByRole('textbox', { name: 'Confianza mínima (%)' })).toHaveValue('95');
    expect(screen.queryByRole('combobox', { name: 'Tipos de documento' })).not.toBeInTheDocument();
    expect(screen.queryByText(/En Automático el sistema aprobará/)).not.toBeInTheDocument();

    await usuario.clear(screen.getByRole('textbox', { name: 'Confianza mínima (%)' }));
    await usuario.type(screen.getByRole('textbox', { name: 'Confianza mínima (%)' }), '90');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]).toMatchObject({ nivel: 'Asistido', umbralConfianza: 0.9, porcentajeMuestreo: 10, topeMensualAprobaciones: null, tiposAutorizados: [], areasAutorizadas: [], esperaIaMinutos: 10 });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Guardaste la revisión automática: nivel Asistido.'));
    // Ya guardado, no hay nada pendiente.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled());
  });

  it('la confianza mínima fuera de 85 a 100 se explica y bloquea el guardado', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar();
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });
    await usuario.click(nivel('Asistido'));
    await usuario.clear(screen.getByRole('textbox', { name: 'Confianza mínima (%)' }));
    await usuario.type(screen.getByRole('textbox', { name: 'Confianza mínima (%)' }), '80');
    expect(screen.getByText('Escribe un número de 85 a 100.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('un campo numérico solo deja escribir dígitos (sin controles nativos)', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar(AUTOMATICA);
    const tope = await screen.findByRole('textbox', { name: 'Tope mensual de aprobaciones' });
    await usuario.clear(tope);
    await usuario.type(tope, '3a5.0-');
    expect(tope).toHaveValue('350');
    expect(document.querySelector('input[type="number"]')).toBeNull();
  });

  it('Automático avisa que aprueba sin que una persona vea, exige tipos y áreas y confirma antes de guardar', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    const { guardados } = montar();
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });

    await usuario.click(nivel('Automático'));
    expect(screen.getByText('En Automático el sistema aprobará documentos sin que una persona los vea, salvo el muestreo.')).toBeInTheDocument();
    expect(screen.getByText('Las excepciones y la muestra llegan a los revisores del área en la pestaña Excepciones.')).toBeInTheDocument();
    // Lo que manda siempre a una persona.
    const siempre = within(screen.getByText('Pasa siempre a una persona cuando hay').parentElement!);
    expect(siempre.getByText('Confianza baja')).toBeInTheDocument();
    expect(siempre.getByText('Señales de manipulación')).toBeInTheDocument();
    // Sin tipos ni áreas no se puede guardar, y se dice qué falta.
    expect(screen.getByText('Elige al menos un tipo de documento.')).toBeInTheDocument();
    expect(screen.getByText('Elige al menos un área.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.click(screen.getByRole('combobox', { name: 'Tipos de documento' }));
    await usuario.click(screen.getByRole('option', { name: 'Registro Único Tributario' }));
    await usuario.click(screen.getByRole('option', { name: 'Póliza' }));
    await usuario.keyboard('{Escape}');
    await usuario.click(screen.getByRole('combobox', { name: 'Áreas cuya revisión hace el sistema' }));
    await usuario.click(screen.getByRole('option', { name: 'Seleccionar todos' }));
    await usuario.keyboard('{Escape}');
    expect(screen.queryByText('Elige al menos un tipo de documento.')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    // Antes de guardar avisa qué pasará.
    const dialogo = await screen.findByRole('dialog', { name: 'Activar la aprobación automática' });
    expect(within(dialogo).getByText(/aprobará sin que una persona los vea los documentos sin excepciones de 2 tipos de documento en 2 áreas, con una confianza de al menos 95 %/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/confirmará el 10 % de lo aprobado y atenderá las excepciones en la pestaña Excepciones/)).toBeInTheDocument();
    expect(guardados).toHaveLength(0);

    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar y activar' }));
    await waitFor(() => expect(guardados).toHaveLength(1));
    expect(guardados[0]).toMatchObject({ nivel: 'Automatico', umbralConfianza: 0.95, porcentajeMuestreo: 10, topeMensualAprobaciones: null, esperaIaMinutos: 10 });
    expect([...(guardados[0]?.tiposAutorizados ?? [])].sort()).toEqual(['POLIZA', 'RUT']);
    expect([...(guardados[0]?.areasAutorizadas ?? [])].sort()).toEqual(['a-compras', 'a-cumplimiento']);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Guardaste la revisión automática: nivel Automático.');
  });

  it('cancelar el aviso no guarda nada', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    const { guardados } = montar({ ...CONFIG, tiposAutorizados: ['RUT'], areasAutorizadas: ['a-compras'] });
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });
    await usuario.click(nivel('Automático'));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Activar la aprobación automática' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(guardados).toHaveLength(0);
  });

  it('ya en Automático muestra los parámetros guardados y el uso del mes con la barra del tope en ámbar desde el 80 %', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar(AUTOMATICA);
    expect(await screen.findByRole('textbox', { name: 'Tope mensual de aprobaciones' })).toHaveValue('200');
    expect(screen.getByRole('textbox', { name: 'Muestra para confirmar (%)' })).toHaveValue('10');
    expect(screen.getByRole('textbox', { name: 'Espera del análisis (minutos)' })).toHaveValue('10');
    expect(screen.getByRole('combobox', { name: 'Tipos de documento' })).toHaveTextContent('Registro Único Tributario');
    expect(screen.getByRole('combobox', { name: 'Áreas cuya revisión hace el sistema' })).toHaveTextContent('Compras');

    const uso = within(screen.getByRole('region', { name: 'Uso de Noviembre de 2026' }));
    expect(uso.getByText('170')).toBeInTheDocument();
    expect(uso.getByText('Aprobadas por el sistema')).toBeInTheDocument();
    expect(uso.getByText('17')).toBeInTheDocument();
    expect(uso.getByText('170 de 200 aprobaciones del tope mensual')).toBeInTheDocument();
    const barra = uso.getByRole('progressbar');
    expect(barra).toHaveAttribute('aria-valuenow', '85');
    expect(barra).toHaveAttribute('data-nivel', 'alerta');
    expect(uso.getByText(/Llevas el 85\s?% del tope/)).toBeInTheDocument();
  });

  it('descartar los cambios vuelve a lo guardado', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar(AUTOMATICA);
    const tope = await screen.findByRole('textbox', { name: 'Tope mensual de aprobaciones' });
    await usuario.type(tope, '5');
    expect(tope).toHaveValue('2005');
    await usuario.click(screen.getByRole('button', { name: 'Descartar cambios' }));
    expect(screen.getByRole('textbox', { name: 'Tope mensual de aprobaciones' })).toHaveValue('200');
    expect(screen.queryByRole('button', { name: 'Descartar cambios' })).not.toBeInTheDocument();
  });

  it('sin plan de IA con llave explica que hace falta, enlaza a Plan y uso de IA y no deja elegir Automático', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar({ ...CONFIG, iaDisponible: false });
    expect(await screen.findByText('Esta empresa no tiene el plan de IA activo con su llave')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver Plan y uso de IA' })).toHaveAttribute('href', '/e/emp/uso-ia');
    expect(nivel('Automático')).toBeDisabled();
    expect(screen.getByText('Necesita plan de IA activo')).toBeInTheDocument();
    expect(nivel('Asistido')).toBeEnabled();
  });

  it('si la API rechaza el guardado, muestra el motivo en español y conserva lo editado', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.configuracionLeer, SCOPES.configuracionEscribir]);
    montar({ ...CONFIG, tiposAutorizados: ['RUT'], areasAutorizadas: ['a-compras'] }, {
      guardar: () => json({ type: 'about:blank', title: 'No procesable', status: 422, detail: 'Falta el plan.', codigo: 'ia-no-disponible' }, 422),
    });
    await screen.findByRole('radiogroup', { name: 'Nivel de revisión automática' });
    await usuario.click(nivel('Automático'));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await usuario.click(await screen.findByRole('button', { name: 'Guardar y activar' }));

    expect(await screen.findByText(/Para aprobar automáticamente la empresa necesita su plan de IA activo y con la llave de OpenAI/)).toBeInTheDocument();
    expect(nivel('Automático')).toBeChecked();
  });

  it('sin configuracion:escribir se ve todo pero nada se puede cambiar', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar(AUTOMATICA);
    expect(await screen.findByText(/Puedes ver esta configuración, pero cambiarla necesita permiso de escritura/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument();
    for (const radio of screen.getAllByRole('radio')) expect(radio).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Tope mensual de aprobaciones' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Tipos de documento' })).toHaveAttribute('aria-disabled', 'true');
  });
});
