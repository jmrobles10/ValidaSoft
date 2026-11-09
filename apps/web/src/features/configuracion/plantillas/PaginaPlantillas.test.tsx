import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { json, montar } from './pruebas/montar';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const conPermisos = (...scopes: string[]) => (permisos.scopes = new Set(scopes));
const editor = () => conPermisos(SCOPES.configuracionLeer, SCOPES.configuracionEscribir);
const RUTA = '/api/v1/plantillas-notificacion';

async function filaDe(texto: string) {
  const fila = (await screen.findByText(texto)).closest('tr');
  if (!fila) throw new Error(`sin fila para ${texto}`);
  return fila;
}

async function abrir(nombre: string) {
  const usuario = userEvent.setup();
  await usuario.click(within(await filaDe(nombre)).getByRole('button'));
  return { usuario, drawer: await screen.findByRole('dialog', { name: new RegExp(nombre) }) };
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
  vi.restoreAllMocks();
});

describe('Plantillas de correo', () => {
  it('sin configuracion:leer dice que no hay acceso y no pide nada', () => {
    const peticiones = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Plantillas de correo');
    expect(peticiones).toHaveLength(0);
  });

  it('lista los correos con su estado y versión; sin escritura solo deja verlos', async () => {
    conPermisos(SCOPES.configuracionLeer);
    montar();

    const invitacion = await filaDe('Invitación');
    expect(within(invitacion).getByText('Predeterminada')).toBeInTheDocument();
    expect(within(invitacion).getByText('Sin cambios')).toBeInTheDocument();
    const vencido = await filaDe('Documento vencido');
    expect(within(vencido).getByText('Personalizada')).toBeInTheDocument();
    expect(within(vencido).getByText('v3')).toBeInTheDocument();

    const { drawer } = await abrir('Documento vencido');
    expect(within(drawer).getByText(/no cambiarlas/)).toBeInTheDocument();
    expect(within(drawer).getByRole('textbox', { name: 'Asunto' })).toBeDisabled();
    expect(within(drawer).queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument();
    expect(within(drawer).queryByText('{{documento}}')).not.toBeInTheDocument();
  });

  it('edita asunto y cabecera con una variable insertada y guarda con PUT', async () => {
    editor();
    const peticiones = montar(({ metodo }) => (metodo === 'PUT' ? json({}) : undefined));
    const { usuario, drawer } = await abrir('Invitación');
    expect(within(drawer).getByText(/Si lo dejas vacío sale el de siempre: «Constructora Norte te invita/)).toBeInTheDocument();

    await usuario.click(within(drawer).getByRole('textbox', { name: 'Asunto' }));
    await usuario.paste('{{empresa}} te espera');
    await usuario.click(within(drawer).getByRole('textbox', { name: 'Cabecera' }));
    await usuario.paste('Hola desde ');
    await usuario.click(within(drawer).getByRole('button', { name: '{{empresa}}' }));
    expect(within(drawer).getByRole('textbox', { name: 'Cabecera' })).toHaveValue('Hola desde {{empresa}}');
    expect(within(drawer).getByText('22 / 2000')).toBeInTheDocument();

    await usuario.click(within(drawer).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    const put = peticiones.find((p) => p.metodo === 'PUT');
    expect(put?.url).toBe(`${RUTA}/invitacion/correo`);
    expect(put?.cuerpo).toEqual({ asunto: '{{empresa}} te espera', cabecera: 'Hola desde {{empresa}}', pie: null });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Plantilla guardada. Los próximos correos saldrán así.');
  });

  it('un 422 de la API se muestra en su campo', async () => {
    editor();
    const detalle = 'Este correo no admite {{folio}}. Se permiten: {{empresa}}, {{tipoVinculo}}.';
    montar(({ metodo }) =>
      metodo === 'PUT' ? json({ codigo: 'variable-no-permitida', detail: detalle, status: 422, campo: 'pie', permitidas: ['empresa'] }, 422) : undefined,
    );
    const { usuario, drawer } = await abrir('Invitación');

    await usuario.click(within(drawer).getByRole('textbox', { name: 'Pie' }));
    await usuario.paste('Folio {{folio}}');
    await usuario.click(within(drawer).getByRole('button', { name: 'Guardar' }));

    const pie = within(drawer).getByRole('textbox', { name: 'Pie' });
    await waitFor(() => expect(pie).toHaveAttribute('aria-invalid', 'true'));
    expect(within(drawer).getByText(detalle)).toBeInTheDocument();
    expect(within(drawer).getByRole('textbox', { name: 'Asunto' })).toHaveAttribute('aria-invalid', 'false');
    expect(screen.getByRole('dialog', { name: /Invitación/ })).toBeInTheDocument();
  });

  it('restaurar el predeterminado pide confirmación y guarda todo vacío', async () => {
    editor();
    const peticiones = montar(({ metodo }) => (metodo === 'PUT' ? json({}) : undefined));
    const { usuario, drawer } = await abrir('Documento vencido');
    expect(within(drawer).getByRole('textbox', { name: 'Asunto' })).toHaveValue('{{documento}} vencido');

    await usuario.click(within(drawer).getByRole('button', { name: 'Restaurar predeterminado' }));
    const confirmacion = await screen.findByRole('dialog', { name: '¿Volver al correo predeterminado?' });
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
    await usuario.click(within(confirmacion).getByRole('button', { name: 'Restaurar' }));

    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'PUT')?.cuerpo).toEqual({ asunto: null, cabecera: null, pie: null }));
    expect(peticiones.find((p) => p.metodo === 'PUT')?.url).toBe(`${RUTA}/vencido/correo`);
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('El correo volvió al texto predeterminado.'));
  });

  it('la vista previa se arma con lo que se está escribiendo, en un marco sin scripts y en texto plano', async () => {
    editor();
    const peticiones = montar();
    const { usuario, drawer } = await abrir('Invitación');
    await waitFor(() => expect(within(drawer).getByTestId('asunto-previa')).toHaveTextContent('Asunto: Constructora Norte te invita'));

    await usuario.click(within(drawer).getByRole('textbox', { name: 'Asunto' }));
    await usuario.paste('{{empresa}} te espera');
    await usuario.click(within(drawer).getByRole('textbox', { name: 'Cabecera' }));
    await usuario.paste('Bienvenido');

    await waitFor(() => expect(within(drawer).getByTestId('asunto-previa')).toHaveTextContent('Asunto: Constructora Norte te espera'));
    const previas = peticiones.filter((p) => p.url.endsWith('/vista-previa'));
    expect(previas.at(-1)).toEqual({ metodo: 'POST', url: `${RUTA}/invitacion/correo/vista-previa`, cuerpo: { asunto: '{{empresa}} te espera', cabecera: 'Bienvenido', pie: null } });
    expect(previas.length).toBeLessThanOrEqual(3); // diferida: no una petición por tecla
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);

    const marco = within(drawer).getByTitle('Vista previa del correo «Invitación»');
    expect(marco).toHaveAttribute('sandbox', '');
    expect(marco.getAttribute('srcdoc')).toContain('<p>Bienvenido</p>');

    await usuario.click(within(drawer).getByRole('button', { name: 'Texto plano' }));
    expect(within(drawer).getByText(/Bienvenido\s+Cuerpo del correo\./)).toBeInTheDocument();
  });
});
