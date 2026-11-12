import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { describe, expect, it, vi } from 'vitest';
import type { DeclaracionPep } from '../tipos';
import { datosBasicos, type DatosBasicos } from './declaracion';
import { DialogoDeclaracionPep } from './DialogoDeclaracionPep';

// jsdom no tiene lienzo: la firma se simula con un botón que entrega un PNG cualquiera.
vi.mock('./FirmaPad', () => ({
  FirmaPad: ({ valor, onCambiar, error }: { valor: string | null | undefined; onCambiar: (png: string | null) => void; error?: string | null }) => (
    <div role="group" aria-label="Firma">
      <button type="button" onClick={() => onCambiar('iVBORw0KGgo=')}>
        Simular firma
      </button>
      <span data-testid="firma">{valor ?? 'sin firma'}</span>
      {error && <span role="alert">{error}</span>}
    </div>
  ),
}));

const NATURAL: DatosBasicos = datosBasicos({ tipo: 'natural', nombres: 'Ana María', apellidos: 'Gómez', tipoIdentificacion: 'CC', numero: '1020304050', direccion: 'Carrera 7 # 32-16' });
const REPRESENTANTE: DatosBasicos = datosBasicos({
  tipo: 'persona',
  persona: { rol: 'RepresentanteLegal', tipo_identificacion: 'CC', numero: '1020304050', nombres: 'Marta Lucía', apellidos: 'Ríos Duarte' },
});

/** Una declaración completa guardada antes (el juramento hay que marcarlo de nuevo al abrir). */
const PREVIA: DeclaracionPep = {
  nacionalidad: 'CO',
  cargo: 'Alcalde',
  institucion: 'Alcaldía de Soacha',
  pais: 'CO',
  fecha_vinculacion: '2020-01-01',
  vigente: true,
  cuentas_exterior: false,
  vinculo_con_pep: { tiene: false },
  acepta_juramento: true,
  firma_en_calidad: 'NombrePropio',
  firma_png: 'iVBORw0KGgo=',
  firma_huella: 'abc',
  fecha_firma: '2026-11-01',
  firma_ip: '10.0.0.1',
};

function montar(props: { basicos?: DatosBasicos; previa?: DeclaracionPep | null; mensajeServidor?: (ruta: string) => string | null } = {}) {
  const onConfirmar = vi.fn();
  const onSalir = vi.fn();
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <DialogoDeclaracionPep basicos={props.basicos ?? NATURAL} previa={props.previa ?? null} mensajeServidor={props.mensajeServidor} onConfirmar={onConfirmar} onSalir={onSalir} />
    </ThemeProvider>,
  );
  return { onConfirmar, onSalir, usuario: userEvent.setup() };
}

const dialogo = () => screen.getByRole('dialog', { name: 'Declaración jurada PEP' });
const confirmar = (usuario: ReturnType<typeof userEvent.setup>) => usuario.click(within(dialogo()).getByRole('button', { name: 'Confirmar declaración' }));
const marcarJuramento = (usuario: ReturnType<typeof userEvent.setup>) => usuario.click(within(dialogo()).getByRole('checkbox', { name: /^Declaro bajo juramento/ }));

/** Escribe `ddmmaaaa` en un campo de fecha de la aplicación (sus secciones día, mes y año). */
async function escribirFecha(usuario: ReturnType<typeof userEvent.setup>, etiqueta: string, digitos: string) {
  await usuario.click(within(screen.getByRole('group', { name: etiqueta })).getByRole('spinbutton', { name: 'Día' }));
  await usuario.keyboard(digitos);
}

async function escribir(usuario: ReturnType<typeof userEvent.setup>, campo: HTMLElement, texto: string) {
  await usuario.click(campo);
  await usuario.paste(texto);
}

describe('Declaración jurada PEP: el diálogo', () => {
  it('trae la sección 1 de solo lectura con lo que ya se sabe de quien declara', () => {
    montar();
    const basicos = within(screen.getByRole('region', { name: '1. Datos básicos' }));
    expect(basicos.getByRole('textbox', { name: 'Nombre completo o razón social' })).toHaveValue('Ana María Gómez');
    expect(basicos.getByRole('textbox', { name: 'Documento' })).toHaveValue('CC 1020304050');
    expect(basicos.getByRole('textbox', { name: 'Relación con la empresa' })).toHaveValue('Proveedor');
    expect(basicos.getByRole('textbox', { name: 'Dirección' })).toHaveAttribute('readonly');
    expect(basicos.getByRole('textbox', { name: 'Dirección' })).toHaveValue('Carrera 7 # 32-16');
  });

  it('sin diligenciar nada, confirmar marca lo obligatorio y no entrega nada', async () => {
    const { usuario, onConfirmar } = montar();
    await confirmar(usuario);

    expect(screen.getByText('Revisa los datos marcados antes de confirmar.')).toBeInTheDocument();
    const condicion = within(screen.getByRole('region', { name: '2. Condición PEP' }));
    const cargo = condicion.getByRole('textbox', { name: 'Cargo desempeñado' });
    expect(cargo).toHaveAttribute('aria-invalid', 'true');
    expect(cargo).toHaveAccessibleDescription('Este dato es obligatorio');
    expect(condicion.getByRole('textbox', { name: 'Institución' })).toHaveAttribute('aria-invalid', 'true');
    // Las dos preguntas sí/no sin responder también se marcan.
    expect(condicion.getAllByText('Este dato es obligatorio').length).toBeGreaterThanOrEqual(5);
    expect(within(dialogo()).getByText('Marca la declaración bajo juramento')).toBeInTheDocument();
    expect(within(dialogo()).getByText('La firma es obligatoria')).toBeInTheDocument();
    expect(onConfirmar).not.toHaveBeenCalled();
  });

  it('diligenciada completa entrega la declaración con sus datos básicos y sin los sellos del servidor', async () => {
    const { usuario, onConfirmar } = montar();
    await usuario.click(screen.getByRole('combobox', { name: 'Nacionalidad' }));
    await usuario.click(await screen.findByRole('option', { name: 'Colombia' }));
    await escribir(usuario, screen.getByRole('textbox', { name: 'Cargo desempeñado' }), 'Alcalde');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Institución' }), 'Alcaldía de Soacha');
    await usuario.click(screen.getByRole('combobox', { name: 'País de la institución' }));
    await usuario.click(await screen.findByRole('option', { name: 'Colombia' }));
    await escribirFecha(usuario, 'Fecha de vinculación', '01012020');
    await usuario.click(screen.getByRole('checkbox', { name: 'Vigente' }));
    await usuario.click(within(screen.getByRole('radiogroup', { name: /cuentas financieras en el exterior/ })).getByRole('radio', { name: 'No' }));
    await usuario.click(within(screen.getByRole('radiogroup', { name: '¿Tiene algún vínculo familiar o societario con una PEP?' })).getByRole('radio', { name: 'No' }));
    await usuario.click(screen.getByRole('button', { name: 'Simular firma' }));
    await marcarJuramento(usuario);
    await confirmar(usuario);

    expect(onConfirmar).toHaveBeenCalledTimes(1);
    const entregada = onConfirmar.mock.calls[0]?.[0] as DeclaracionPep;
    expect(entregada).toMatchObject({
      relacion_empresa: 'Proveedor',
      nombre: 'Ana María Gómez',
      tipo_identificacion: 'CC',
      numero: '1020304050',
      direccion: 'Carrera 7 # 32-16',
      nacionalidad: 'CO',
      cargo: 'Alcalde',
      institucion: 'Alcaldía de Soacha',
      pais: 'CO',
      fecha_vinculacion: '2020-01-01',
      vigente: true,
      fecha_desvinculacion: null,
      cuentas_exterior: false,
      vinculo_con_pep: { tiene: false },
      acepta_juramento: true,
      firma_en_calidad: 'NombrePropio',
      firma_png: 'iVBORw0KGgo=',
    });
    expect(entregada).not.toHaveProperty('firma_huella');
    expect(entregada).not.toHaveProperty('fecha_firma');
    expect(entregada).not.toHaveProperty('firma_ip');
  });

  it('«Vigente» esconde la fecha de desvinculación y quitarlo la vuelve a pedir', async () => {
    const { usuario } = montar();
    const condicion = within(screen.getByRole('region', { name: '2. Condición PEP' }));
    expect(condicion.getByRole('group', { name: 'Fecha de desvinculación' })).toBeInTheDocument();
    await usuario.click(condicion.getByRole('checkbox', { name: 'Vigente' }));
    expect(condicion.queryByRole('group', { name: 'Fecha de desvinculación' })).not.toBeInTheDocument();
    await usuario.click(condicion.getByRole('checkbox', { name: 'Vigente' }));
    expect(condicion.getByRole('group', { name: 'Fecha de desvinculación' })).toBeInTheDocument();
  });

  it('abre precargado con una declaración previa, pide marcar de nuevo el juramento y conserva la firma guardada', async () => {
    const { usuario, onConfirmar } = montar({ previa: PREVIA });
    expect(screen.getByRole('textbox', { name: 'Cargo desempeñado' })).toHaveValue('Alcalde');
    expect(within(dialogo()).getByRole('checkbox', { name: /^Declaro bajo juramento/ })).not.toBeChecked();
    expect(screen.getByTestId('firma')).toHaveTextContent('iVBORw0KGgo=');

    await confirmar(usuario);
    expect(onConfirmar).not.toHaveBeenCalled();
    expect(within(dialogo()).getByText('Marca la declaración bajo juramento')).toBeInTheDocument();

    await marcarJuramento(usuario);
    await confirmar(usuario);
    expect(onConfirmar).toHaveBeenCalledTimes(1);
  });

  it('cada familiar es todo o nada: la fila vacía no estorba y la incompleta se marca', async () => {
    const { usuario, onConfirmar } = montar({ previa: PREVIA });
    await marcarJuramento(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agregar familiar' }));
    await usuario.click(screen.getByRole('button', { name: 'Agregar familiar' }));
    // Solo la primera fila con algo escrito; la segunda queda vacía.
    const primera = within(screen.getByRole('listitem', { name: 'Familiar 1' }));
    await escribir(usuario, primera.getByRole('textbox', { name: 'Nombre completo' }), 'Luis Gómez');
    await confirmar(usuario);

    expect(onConfirmar).not.toHaveBeenCalled();
    expect(primera.getByRole('textbox', { name: 'Parentesco' })).toHaveAttribute('aria-invalid', 'true');
    expect(primera.getByRole('textbox', { name: 'Parentesco' })).toHaveAccessibleDescription('Completa este dato o quita la fila');
    expect(within(screen.getByRole('listitem', { name: 'Familiar 2' })).getByRole('textbox', { name: 'Nombre completo' })).not.toHaveAttribute('aria-invalid', 'true');

    // Quitar la fila incompleta deja pasar la declaración (la vacía se descarta).
    await usuario.click(screen.getByRole('button', { name: 'Quitar el familiar 1' }));
    await confirmar(usuario);
    expect(onConfirmar).toHaveBeenCalledTimes(1);
    expect((onConfirmar.mock.calls[0]?.[0] as DeclaracionPep).familiares ?? null).toBeNull();
  });

  it('admite hasta 30 familiares y 30 sociedades y avisa al llegar al máximo', () => {
    const familiar = { nombre: 'Luis', parentesco: 'Primo', tipo_identificacion: 'CC', numero: '1' };
    const sociedad = { nombre: 'X SAS', identificacion: '9', calidad: 'Socio' as const };
    montar({ previa: { ...PREVIA, familiares: Array.from({ length: 30 }, () => familiar), sociedades: Array.from({ length: 30 }, () => sociedad) } });
    expect(screen.getByRole('button', { name: 'Agregar familiar' })).toBeDisabled();
    expect(screen.getByText('Llegaste al máximo de 30 familiares.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Agregar sociedad' })).toBeDisabled();
    expect(screen.getByText('Llegaste al máximo de 30 sociedades.')).toBeInTheDocument();
  });

  it('una sociedad con calidad «Otra» pide «¿Cuál?»', async () => {
    const { usuario, onConfirmar } = montar({ previa: PREVIA });
    await marcarJuramento(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agregar sociedad' }));
    const fila = within(screen.getByRole('listitem', { name: 'Sociedad 1' }));
    await escribir(usuario, fila.getByRole('textbox', { name: 'Nombre o razón social' }), 'Fiducia del Norte');
    await escribir(usuario, fila.getByRole('textbox', { name: 'Identificación' }), '900123');
    await usuario.click(fila.getByRole('combobox', { name: 'En calidad de' }));
    await usuario.click(screen.getByRole('option', { name: 'Otra' }));
    await confirmar(usuario);

    expect(onConfirmar).not.toHaveBeenCalled();
    expect(fila.getByRole('textbox', { name: '¿Cuál?' })).toHaveAccessibleDescription('Completa este dato o quita la fila');
  });

  it('el vínculo con otras PEP: con «Sí» abre una fila que pide sus datos y su periodo', async () => {
    const { usuario, onConfirmar } = montar({ previa: PREVIA });
    await marcarJuramento(usuario);
    const vinculo = within(screen.getByRole('region', { name: '3. Vínculo familiar o asociados cercanos a una PEP' }));
    expect(vinculo.queryByRole('textbox', { name: 'Nombre de la PEP' })).not.toBeInTheDocument();
    await usuario.click(vinculo.getByRole('radio', { name: 'Sí' }));
    await confirmar(usuario);

    expect(onConfirmar).not.toHaveBeenCalled();
    // La fila en blanco no cuenta: falta al menos una PEP relacionada completa.
    expect(vinculo.getByText('Este dato es obligatorio')).toBeInTheDocument();
    await escribir(usuario, vinculo.getByRole('textbox', { name: 'Nombre de la PEP' }), 'Carlos Ruiz');
    await confirmar(usuario);
    expect(vinculo.getByRole('textbox', { name: 'Cargo de la PEP' })).toHaveAttribute('aria-invalid', 'true');
    expect(vinculo.getByRole('textbox', { name: 'Entidad' })).toHaveAttribute('aria-invalid', 'true');
    expect(vinculo.getByRole('combobox', { name: 'Tipo de vínculo' })).toHaveAccessibleDescription('Este dato es obligatorio');
  });

  it('con cuentas en el exterior pide al menos un país', async () => {
    const { usuario, onConfirmar } = montar({ previa: PREVIA });
    await marcarJuramento(usuario);
    await usuario.click(within(screen.getByRole('radiogroup', { name: /cuentas financieras en el exterior/ })).getByRole('radio', { name: 'Sí' }));
    await confirmar(usuario);
    expect(onConfirmar).not.toHaveBeenCalled();
    expect(screen.getByRole('combobox', { name: 'Países de las cuentas' })).toHaveAccessibleDescription('Elige al menos un país');
  });

  it('el representante legal escribe quién firma; a nombre propio no se pide', async () => {
    const { usuario, onConfirmar } = montar({ basicos: REPRESENTANTE, previa: { ...PREVIA, direccion: 'Calle 1 # 2-3' } });
    await marcarJuramento(usuario);
    expect(screen.queryByRole('textbox', { name: 'Nombre completo del firmante' })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('combobox', { name: 'Firma en calidad de' }));
    await usuario.click(screen.getByRole('option', { name: 'Representante legal' }));
    await confirmar(usuario);

    expect(onConfirmar).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'Nombre completo del firmante' })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('textbox', { name: 'Número de documento del firmante' })).toHaveAttribute('aria-invalid', 'true');

    await escribir(usuario, screen.getByRole('textbox', { name: 'Nombre completo del firmante' }), 'Pedro Pérez');
    await usuario.click(screen.getByRole('combobox', { name: 'Tipo de documento del firmante' }));
    await usuario.click(screen.getByRole('option', { name: 'CC' }));
    await escribir(usuario, screen.getByRole('textbox', { name: 'Número de documento del firmante' }), '80123456');
    await confirmar(usuario);
    expect(onConfirmar).toHaveBeenCalledTimes(1);
    expect(onConfirmar.mock.calls[0]?.[0]).toMatchObject({ firma_en_calidad: 'RepresentanteLegal', firmante_nombre: 'Pedro Pérez', firmante_tipo_identificacion: 'CC', firmante_numero: '80123456' });
  });

  it('una persona de la información legal escribe su dirección y elige su calidad al firmar (con representante legal); la natural no tiene representante legal', () => {
    montar({ basicos: REPRESENTANTE });
    expect(within(screen.getByRole('region', { name: '1. Datos básicos' })).getByRole('textbox', { name: 'Dirección' })).not.toHaveAttribute('readonly');
    expect(screen.getByRole('combobox', { name: 'Firma en calidad de' })).toBeInTheDocument();
  });

  it('la persona natural firma a nombre propio o por apoderado (nunca como representante legal) y el apoderado escribe su nombre y documento', async () => {
    const { usuario, onConfirmar } = montar({ previa: { ...PREVIA } });
    await usuario.click(screen.getByRole('combobox', { name: 'Firma en calidad de' }));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Nombre propio', 'Apoderado']);
    await usuario.click(screen.getByRole('option', { name: 'Apoderado' }));
    await marcarJuramento(usuario);
    await confirmar(usuario);

    expect(onConfirmar).not.toHaveBeenCalled();
    expect(screen.getByRole('textbox', { name: 'Nombre completo del firmante' })).toHaveAttribute('aria-invalid', 'true');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Nombre completo del firmante' }), 'Pedro Gil');
    await usuario.click(screen.getByRole('combobox', { name: 'Tipo de documento del firmante' }));
    await usuario.click(screen.getByRole('option', { name: 'CC' }));
    await escribir(usuario, screen.getByRole('textbox', { name: 'Número de documento del firmante' }), '80123456');
  });

  it('las PEP relacionadas son una lista: agregar, completar por tipo y quitar (de 1 a 30)', async () => {
    const pep = { tipo: 'Familiar', relacion: 'Hermano(a) · 2.° consanguinidad', nombre: 'Carlos Ruiz', cargo: 'Senador', entidad: 'Senado', pais: 'CO', fecha_vinculacion: '2022-08-01', vigente: true } as const;
    const { usuario } = montar({ previa: { ...PREVIA, vinculo_con_pep: { tiene: true, vinculos: [pep] } } });
    const seccion = within(screen.getByRole('region', { name: '3. Vínculo familiar o asociados cercanos a una PEP' }));
    const lista = seccion.getByRole('list', { name: 'PEP relacionadas' });
    expect(within(lista).getAllByRole('listitem')).toHaveLength(1);
    expect(within(lista).getByRole('textbox', { name: 'Nombre de la PEP' })).toHaveValue('Carlos Ruiz');

    await usuario.click(seccion.getByRole('button', { name: 'Agregar PEP relacionada' }));
    const segunda = within(screen.getByRole('listitem', { name: 'PEP relacionada 2' }));
    // La relación depende del tipo: sin tipo no se elige; un asociado cercano solo tiene «socio o asociado comercial».
    expect(segunda.getByRole('combobox', { name: 'Parentesco o relación con la PEP' })).toHaveAttribute('aria-disabled', 'true');
    await usuario.click(segunda.getByRole('combobox', { name: 'Tipo de vínculo' }));
    await usuario.click(screen.getByRole('option', { name: 'Asociado cercano' }));
    await usuario.click(segunda.getByRole('combobox', { name: 'Parentesco o relación con la PEP' }));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Socio(a) o asociado(a) comercial']);
    await usuario.keyboard('{Escape}');

    await usuario.click(seccion.getByRole('button', { name: 'Quitar la PEP relacionada 2' }));
    expect(within(lista).getAllByRole('listitem')).toHaveLength(1);
  });

  it('muestra el error del servidor de un campo aunque no se haya intentado confirmar', () => {
    montar({ mensajeServidor: (ruta) => (ruta === 'cargo' ? 'Falta «Cargo».' : null) });
    expect(screen.getByRole('textbox', { name: 'Cargo desempeñado' })).toHaveAccessibleDescription('Falta «Cargo».');
  });

  describe('salir sin confirmar', () => {
    it('sin declaración guardada pregunta y, si sale, avisa que la respuesta vuelve a No', async () => {
      const { usuario, onSalir } = montar();
      await usuario.click(within(dialogo()).getByRole('button', { name: 'Cerrar' }));
      const aviso = screen.getByRole('dialog', { name: 'Declaración incompleta' });
      expect(aviso).toHaveTextContent('Si sales sin declarar, tu respuesta vuelve a No.');
      expect(onSalir).not.toHaveBeenCalled();

      await usuario.click(within(aviso).getByRole('button', { name: 'Seguir diligenciando' }));
      // El diálogo del aviso se cierra con su animación; mientras, el de la declaración sigue oculto para los lectores de pantalla.
      await vi.waitFor(() => expect(screen.queryByRole('dialog', { name: 'Declaración incompleta' })).not.toBeInTheDocument());
      expect(onSalir).not.toHaveBeenCalled();

      await usuario.click(await screen.findByRole('button', { name: 'Cerrar' }));
      await usuario.click(within(screen.getByRole('dialog', { name: 'Declaración incompleta' })).getByRole('button', { name: 'Salir sin declarar' }));
      expect(onSalir).toHaveBeenCalledTimes(1);
    });

    it('con una declaración guardada y sin cambios sale directo', async () => {
      const { usuario, onSalir } = montar({ previa: PREVIA });
      await usuario.click(within(dialogo()).getByRole('button', { name: 'Cerrar' }));
      expect(onSalir).toHaveBeenCalledTimes(1);
    });

    it('con una declaración guardada y cambios pregunta si descarta', async () => {
      const { usuario, onSalir } = montar({ previa: PREVIA });
      await escribir(usuario, screen.getByRole('textbox', { name: 'Cargo desempeñado' }), ' mayor');
      await usuario.click(within(dialogo()).getByRole('button', { name: 'Cerrar' }));
      const aviso = screen.getByRole('dialog', { name: 'Descartar cambios' });
      expect(onSalir).not.toHaveBeenCalled();
      await usuario.click(within(aviso).getByRole('button', { name: 'Descartar cambios' }));
      expect(onSalir).toHaveBeenCalledTimes(1);
    });
  });
});
