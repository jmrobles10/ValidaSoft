import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type VinculoPublicoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { expedienteConSecciones, FINANCIERA_PENDIENTE, GENERAL_OBSERVADA } from '../expediente/pruebasSeccion';
import { escribir, estadoFicha, montarFicha, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

const VINCULO: VinculoPublicoDto = {
  id: 'v1',
  empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  estado: 'EnValidacion',
  invitacionAceptada: true,
  invitacionAceptadaEn: '2026-11-01T15:00:00Z',
  expedienteId: 'e1',
};

const GENERAL = seccion('FICHA_GENERAL', {
  version: 2,
  versiones: 2,
  datos: { razon_social: 'Aceros S.A.S.', tipo_persona: 'Juridica', direccion: 'Calle 45 # 10-20', pais: 'CO' },
  completitud: { completa: true, faltantes: [], reglaVersion: 1 },
});

describe('Mi información: la observación de una empresa (F2)', () => {
  it('se muestra arriba del formulario y marca los campos señalados hasta que el tercero los toca', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      vinculos: [VINCULO],
      expedientes: { e1: expedienteConSecciones([GENERAL_OBSERVADA, FINANCIERA_PENDIENTE]) },
    });

    const aviso = await screen.findByRole('alert', { name: 'Constructora Norte pidió corregir esta sección' });
    expect(aviso).toHaveTextContent('Un dato no coincide con el soporte.');
    expect(aviso).toHaveTextContent('“La razón social no coincide con el RUT.”');
    expect(aviso).toHaveTextContent('Datos por corregir: Razón social, Dirección');

    const razonSocial = screen.getByRole('textbox', { name: 'Razón social' });
    expect(razonSocial).toHaveAttribute('aria-invalid', 'true');
    expect(razonSocial).toHaveAccessibleDescription('Tu empresa pidió corregir este dato.');
    expect(screen.getByRole('textbox', { name: 'Dirección' })).toHaveAccessibleDescription('Tu empresa pidió corregir este dato.');
    expect(screen.getByRole('textbox', { name: 'Correo electrónico' })).not.toHaveAttribute('aria-invalid', 'true');

    await usuario.clear(razonSocial);
    await escribir(usuario, razonSocial, 'Aceros del Norte S.A.S.');
    expect(razonSocial).not.toHaveAttribute('aria-invalid', 'true');
  });

  it('sin observaciones abiertas de esta sección no muestra nada', async () => {
    montarFicha('/ficha/tributaria', {
      estado: estadoFicha(),
      vinculos: [VINCULO],
      expedientes: { e1: expedienteConSecciones([GENERAL_OBSERVADA]) },
    });
    expect(await screen.findByRole('form', { name: 'Información tributaria' })).toBeInTheDocument();
    expect(screen.queryByRole('alert', { name: /pidió corregir/ })).not.toBeInTheDocument();
  });
});
