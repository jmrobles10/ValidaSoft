import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type ObservacionDeSeccionDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { observacionesDeSeccion } from './useObservacionesDeSeccion';
import { escribir, estadoFicha, montarFicha, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

const GENERAL = seccion('FICHA_GENERAL', {
  version: 2,
  versiones: 2,
  datos: { razon_social: 'Aceros S.A.S.', tipo_persona: 'Juridica', direccion: 'Calle 45 # 10-20', pais: 'CO' },
  completitud: { completa: true, faltantes: [], reglaVersion: 1 },
});

/** Lo que llega en `observaciones` del estado de la ficha (FD-2): una empresa observó la información general. */
const OBSERVACION_GENERAL: ObservacionDeSeccionDto = {
  empresa: 'Constructora Norte',
  expedienteId: 'e1',
  seccion: 'FICHA_GENERAL',
  periodo: null,
  observacion: {
    motivos: [{ codigo: 'dato-incorrecto', texto: 'Un dato no coincide con el soporte.' }],
    observacion: 'La razón social no coincide con el RUT.',
    fecha: '2026-11-08T15:00:00Z',
    requisito: 'FICHA_GENERAL',
    campos: ['razon_social', 'direccion'],
  },
};

describe('Mi información: la observación de una empresa (F2)', () => {
  it('llega con el estado de la ficha, se muestra arriba del formulario y marca los campos señalados hasta que el tercero los toca', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', { estado: estadoFicha({ observaciones: [OBSERVACION_GENERAL] }), secciones: { FICHA_GENERAL: GENERAL } });

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
    montarFicha('/ficha/tributaria', { estado: estadoFicha({ observaciones: [OBSERVACION_GENERAL] }) });
    expect(await screen.findByRole('form', { name: 'Información tributaria' })).toBeInTheDocument();
    expect(screen.queryByRole('alert', { name: /pidió corregir/ })).not.toBeInTheDocument();
  });
});

describe('observacionesDeSeccion', () => {
  const FINANCIERA_2025: ObservacionDeSeccionDto = { ...OBSERVACION_GENERAL, seccion: 'FICHA_FINANCIERA', periodo: 2025 };

  it('filtra por sección y, en la financiera, por periodo', () => {
    const estado = { observaciones: [OBSERVACION_GENERAL, FINANCIERA_2025] };
    expect(observacionesDeSeccion(estado, 'FICHA_GENERAL', null).map((o) => o.empresa)).toEqual(['Constructora Norte']);
    expect(observacionesDeSeccion(estado, 'FICHA_FINANCIERA', 2025)).toHaveLength(1);
    expect(observacionesDeSeccion(estado, 'FICHA_FINANCIERA', 2024)).toEqual([]);
    expect(observacionesDeSeccion(estado, 'FICHA_LEGAL', null)).toEqual([]);
  });

  it('un estado sin observaciones (o de una API anterior) no tiene ninguna', () => {
    expect(observacionesDeSeccion({}, 'FICHA_GENERAL', null)).toEqual([]);
    expect(observacionesDeSeccion({ observaciones: null }, 'FICHA_GENERAL', null)).toEqual([]);
  });
});
