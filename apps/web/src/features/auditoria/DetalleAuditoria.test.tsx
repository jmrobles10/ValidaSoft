import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CAMBIO_AREA, DESCARGA_CERTIFICADO, LARGO, montar } from './pruebas/montar';

vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: new Set([SCOPES.auditoriaLeer]), puede: (s: string) => s === SCOPES.auditoriaLeer }) }));

const campo = (nombre: string) => screen.getByRole('listitem', { name: `Campo ${nombre}` });

afterEach(() => {
  reiniciarCliente();
  vi.restoreAllMocks();
});

describe('Detalle de auditoría', () => {
  it('un cambio de configuración muestra el diff campo por campo, la justificación y copia el correlationId', async () => {
    const usuario = userEvent.setup();
    montar(() => ({ datos: [CAMBIO_AREA], siguienteCursor: null }));

    await usuario.click(await screen.findByText('Ana Pérez'));
    const panel = await screen.findByRole('dialog', { name: 'Modificado · Área' });

    expect(within(campo('nombre')).getByText('Compras')).toBeInTheDocument();
    expect(within(campo('nombre')).getByText('Compras y suministros')).toBeInTheDocument();
    expect(within(campo('nombre')).getByText('Modificado')).toBeInTheDocument();
    expect(within(campo('responsable')).getByText('Agregado')).toBeInTheDocument();
    expect(within(campo('responsable')).getByText('u7')).toBeInTheDocument();
    expect(within(campo('activo')).getByText('Eliminado')).toBeInTheDocument();
    expect(within(campo('activo')).getByText('true')).toBeInTheDocument();

    // Los valores largos se recortan y «Ver más» los muestra completos.
    const reglas = campo('reglas');
    expect(within(reglas).queryByText(LARGO)).not.toBeInTheDocument();
    await usuario.click(within(reglas).getByRole('button', { name: 'Ver más' }));
    expect(within(reglas).getByText(LARGO)).toBeInTheDocument();

    expect(within(panel).getByText('Reorganización del área')).toBeInTheDocument();
    expect(within(panel).getByText('Todo el grupo')).toBeInTheDocument();
    await usuario.click(within(panel).getByRole('button', { name: 'Copiar' }));
    expect(await within(panel).findByRole('button', { name: 'Copiado' })).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe('corr-123');
  });

  it('un acceso muestra qué se descargó, el motivo, la IP, el navegador y lleva al vínculo', async () => {
    const usuario = userEvent.setup();
    montar(() => ({ datos: [DESCARGA_CERTIFICADO], siguienteCursor: null }), '/e/emp/auditoria?tipo=Acceso');

    await usuario.click(await screen.findByText('VS-2026-000001', { exact: false }));
    const panel = await screen.findByRole('dialog', { name: 'Descargado · Certificado' });

    expect(within(panel).getByText('VS-2026-000001')).toBeInTheDocument();
    expect(within(panel).getByText('Verificación pública')).toBeInTheDocument();
    expect(within(panel).getByText('181.50.1.2')).toBeInTheDocument();
    expect(within(panel).getByText('Mozilla/5.0 (Windows NT 10.0) Firefox/140.0')).toBeInTheDocument();
    expect(within(panel).queryByText('Cambios campo por campo')).not.toBeInTheDocument();

    await usuario.click(within(panel).getByRole('link', { name: 'Ver el vínculo' }));
    expect(await screen.findByText('Vínculo v9')).toBeInTheDocument();
  });
});
