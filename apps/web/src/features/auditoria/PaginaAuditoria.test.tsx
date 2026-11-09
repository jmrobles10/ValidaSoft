import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CAMBIO_AREA, CAMBIO_WEBHOOK, DESCARGA_CERTIFICADO, montar, parametrosDe } from './pruebas/montar';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const conPermiso = () => (permisos.scopes = new Set([SCOPES.auditoriaLeer]));

async function filaDe(texto: string) {
  const fila = (await screen.findByText(texto)).closest('tr');
  if (!fila) throw new Error(`sin fila para ${texto}`);
  return fila;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  vi.restoreAllMocks();
});

describe('Auditoría', () => {
  it('sin auditoria:leer dice que no hay acceso y no pide nada', () => {
    const urls = montar(() => ({ datos: [], siguienteCursor: null }));
    expect(screen.getByRole('alert')).toHaveTextContent('Auditoría');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(urls).toHaveLength(0);
  });

  it('por omisión muestra los cambios de configuración: actor, entidad, acción y alcance', async () => {
    conPermiso();
    const urls = montar(() => ({ datos: [CAMBIO_AREA, CAMBIO_WEBHOOK], siguienteCursor: null }));

    const area = await filaDe('Ana Pérez');
    expect(within(area).getByText(/desde SRM/)).toBeInTheDocument();
    expect(within(area).getByText('Área')).toBeInTheDocument();
    expect(within(area).getByText(/Compras/)).toBeInTheDocument();
    expect(within(area).getByText('Modificado')).toBeInTheDocument();
    expect(within(area).getByText('Todo el grupo')).toBeInTheDocument();

    const webhook = await filaDe('Integración 0f8fad5b');
    expect(within(webhook).getByText('Creado')).toBeInTheDocument();
    expect(within(webhook).getByText('Esta empresa')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Configuración', selected: true })).toBeInTheDocument();
    expect(urls).toEqual(['/api/v1/auditoria?limite=50']);
  });

  it('la pestaña Accesos pide tipo=Acceso, conserva el actor y descarta la entidad', async () => {
    conPermiso();
    const usuario = userEvent.setup();
    const urls = montar(
      (url) => ({ datos: parametrosDe(url).tipo === 'Acceso' ? [DESCARGA_CERTIFICADO] : [CAMBIO_AREA], siguienteCursor: null }),
      '/e/emp/auditoria?entidad=Area&actor=u1',
    );
    await filaDe('Ana Pérez');
    expect(parametrosDe(urls[0] ?? '')).toEqual({ entidad: 'Area', actor: 'u1', limite: '50' });

    await usuario.click(screen.getByRole('tab', { name: 'Accesos' }));

    const descarga = await filaDe('Tercero');
    expect(within(descarga).getByText('Certificado')).toBeInTheDocument();
    expect(within(descarga).getByText('Descargado')).toBeInTheDocument();
    expect(parametrosDe(urls.at(-1) ?? '')).toEqual({ tipo: 'Acceso', actor: 'u1', limite: '50' });
    expect(screen.getByRole('searchbox', { name: 'Id del documento, certificado o vínculo' })).toBeInTheDocument();
  });

  it('los filtros viven en la URL: entidad, id del actor y rango de fechas', async () => {
    conPermiso();
    const usuario = userEvent.setup();
    const urls = montar(() => ({ datos: [CAMBIO_AREA], siguienteCursor: null }), '/e/emp/auditoria?desde=2026-11-01&hasta=2026-11-09');
    await filaDe('Ana Pérez');
    expect(parametrosDe(urls[0] ?? '')).toEqual({
      desde: new Date(2026, 10, 1).toISOString(),
      hasta: new Date(2026, 10, 9, 23, 59, 59, 999).toISOString(),
      limite: '50',
    });
    expect(screen.getByLabelText('Desde')).toHaveValue('2026-11-01');

    await usuario.click(screen.getByRole('button', { name: /Entidad/ }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Webhook' }));
    await waitFor(() => expect(parametrosDe(urls.at(-1) ?? '')).toMatchObject({ entidad: 'Webhook' }));

    await usuario.click(screen.getByRole('searchbox', { name: 'Id del actor' }));
    await usuario.paste('u-42');
    await waitFor(() => expect(parametrosDe(urls.at(-1) ?? '')).toMatchObject({ entidad: 'Webhook', actor: 'u-42' }));
  });

  it('con «desde» posterior a «hasta» lo advierte y no consulta', async () => {
    conPermiso();
    const urls = montar(() => ({ datos: [CAMBIO_AREA], siguienteCursor: null }), '/e/emp/auditoria?desde=2026-11-09&hasta=2026-11-01');
    expect(await screen.findByText(/La fecha «Desde» es posterior a «Hasta»/)).toBeInTheDocument();
    expect(urls).toHaveLength(0);
  });

  it('pagina con el cursor: «Siguiente» envía el cursor y «Anterior» vuelve a la primera página', async () => {
    conPermiso();
    const usuario = userEvent.setup();
    const urls = montar((url) =>
      parametrosDe(url).cursor === 'c2' ? { datos: [CAMBIO_WEBHOOK], siguienteCursor: null } : { datos: [CAMBIO_AREA], siguienteCursor: 'c2' },
    );
    await filaDe('Ana Pérez');

    await usuario.click(screen.getByRole('button', { name: 'Siguiente' }));
    await filaDe('Integración 0f8fad5b');
    expect(screen.getByText('Página 2')).toBeInTheDocument();
    expect(parametrosDe(urls.at(-1) ?? '')).toEqual({ limite: '50', cursor: 'c2' });
    expect(screen.getByRole('button', { name: 'Siguiente' })).toBeDisabled();

    await usuario.click(screen.getByRole('button', { name: 'Anterior' }));
    await filaDe('Ana Pérez');
    expect(screen.getByText('Página 1')).toBeInTheDocument();
  });

  it('vacío: sin filtros explica qué aparece aquí; con filtros ofrece limpiarlos', async () => {
    conPermiso();
    const usuario = userEvent.setup();
    const urls = montar(() => ({ datos: [], siguienteCursor: null }), '/e/emp/auditoria?tipo=Acceso&entidad=Documento');

    expect(await screen.findByText('Ningún registro coincide con los filtros.')).toBeInTheDocument();
    const [limpiar] = screen.getAllByRole('button', { name: 'Limpiar filtros' });
    if (!limpiar) throw new Error('sin botón para limpiar');
    await usuario.click(limpiar);

    expect(await screen.findByText('No hay registros de auditoría.')).toBeInTheDocument();
    expect(screen.getByText(/las descargas de documentos y certificados de Constructora Norte/)).toBeInTheDocument();
    expect(parametrosDe(urls.at(-1) ?? '')).toEqual({ tipo: 'Acceso', limite: '50' });
  });
});
