import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type ExpedienteDto, type RegistrarEstudioCumplimientoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { expediente } from '../../expedientes/datosPrueba';
import { adjuntoDePrueba, estudio, listas, matrizDePrueba, resultadoDeMatriz } from '../datosPrueba';
import { problema, renderizar, simularApi, type LlamadaApi } from '../pruebasApi';
import { DialogoEstudio } from './DialogoEstudio';

const MATRIZ = '/api/v1/configuracion/matriz-riesgo';
const CALCULAR = '/api/v1/expedientes/e1/cumplimiento/matriz/calcular';
const ADJUNTOS = '/api/v1/expedientes/e1/cumplimiento/adjuntos';

/** Expediente cuya única verificación pendiente es el estudio: registrarlo lo aprueba. */
const ULTIMO_QUE_FALTA: ExpedienteDto = { ...expediente('EnRevision'), esperandoVerificaciones: true, verificaciones: [listas({ estado: 'Satisfecha', coincidencias: [] }), estudio()] };

function montar(manejadores: Record<string, (llamada: LlamadaApi) => unknown>, datos: ExpedienteDto = ULTIMO_QUE_FALTA) {
  const llamadas = simularApi(manejadores);
  const onGuardar = vi.fn<(dto: RegistrarEstudioCumplimientoDto) => void>();
  renderizar(<DialogoEstudio expediente={datos} empresaId="emp" vigenciaDias={30} ocupado={false} error={null} onGuardar={onGuardar} onCerrar={vi.fn()} />);
  return { llamadas, onGuardar };
}

const conMatriz = (activa: boolean) => ({ [`GET ${MATRIZ}`]: () => matrizDePrueba({ activa, esPorDefecto: !activa, version: activa ? 3 : 0 }) });

afterEach(() => {
  reiniciarCliente();
  vi.unstubAllGlobals();
});

describe('Estudio del oficial sin matriz activa', () => {
  it('conserva el formulario a mano con los tres valores de debida diligencia y avisa que es lo último que falta', async () => {
    const { onGuardar } = montar(conMatriz(false));
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByRole('button', { name: '1 · Bajo' });
    expect(dialogo.queryByText('Valoración de riesgo')).not.toBeInTheDocument();
    expect(dialogo.getByText('La empresa no usa matriz de riesgo: elige el nivel y la debida diligencia a mano.')).toBeInTheDocument();
    expect(dialogo.getByText(/Es lo último que falta: el expediente se aprueba/)).toBeInTheDocument();

    await userEvent.click(dialogo.getByRole('combobox', { name: 'Debida diligencia' }));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Simplificada', 'Ordinaria', 'Intensificada']);
    await userEvent.click(screen.getByRole('option', { name: 'Ordinaria' }));
    await userEvent.click(dialogo.getByRole('button', { name: '2 · Medio' }));
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(onGuardar).toHaveBeenCalledWith(expect.objectContaining({ nivelRiesgo: 2, debidaDiligencia: 'Ordinaria', concepto: 'Favorable' }));
  });

  it('un concepto desfavorable cambia el aviso: la verificación falla y no aprueba', async () => {
    montar(conMatriz(false));
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByRole('button', { name: '1 · Bajo' });
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Desfavorable' }));
    expect(dialogo.getByText(/la verificación queda fallida y el expediente no se aprueba/)).toBeInTheDocument();
  });
});

describe('Estudio del oficial con matriz activa', () => {
  const calculando = (llamadas: LlamadaApi[]) => llamadas.filter((l) => l.ruta === CALCULAR);

  async function responderSector(opcion = 'Sector de riesgo medio') {
    await userEvent.click(await screen.findByRole('combobox', { name: 'Sector o actividad económica' }));
    await userEvent.click(screen.getByRole('option', { name: opcion }));
  }

  it('pide los factores manuales, calcula en vivo y registra con el nivel de la matriz sin mandar nivel', async () => {
    const { llamadas, onGuardar } = montar({ ...conMatriz(true), [`POST ${CALCULAR}`]: () => resultadoDeMatriz() });
    const dialogo = within(await screen.findByRole('dialog'));
    expect(await dialogo.findByText('Valoración de riesgo')).toBeInTheDocument();
    // Los automáticos no se preguntan: solo el factor manual.
    expect(dialogo.queryByRole('combobox', { name: 'Jurisdicción del tercero' })).not.toBeInTheDocument();
    expect(dialogo.queryByRole('button', { name: '1 · Bajo' })).not.toBeInTheDocument();
    expect(dialogo.getByText('Responde todos los factores para ver el resultado.')).toBeInTheDocument();
    expect(calculando(llamadas)).toHaveLength(0);

    await responderSector();
    expect(await dialogo.findByText('42,5 de 100')).toBeInTheDocument();
    expect(calculando(llamadas)[0]?.cuerpo).toEqual({ respuestas: [{ factor: 'sector', opcion: 'medio' }] });
    const resultado = within(dialogo.getByLabelText('Resultado de la matriz'));
    expect(resultado.getByText('2 · Medio')).toBeInTheDocument();
    expect(resultado.getByText('Ordinaria')).toBeInTheDocument();
    expect(resultado.getByText('Se vuelve a revisar cada 12 meses.')).toBeInTheDocument();
    expect(resultado.getByText('Ver cómo se calculó')).toBeInTheDocument();

    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(onGuardar).toHaveBeenCalledTimes(1);
    expect(onGuardar.mock.calls[0]?.[0]).toMatchObject({
      nivelRiesgo: null,
      debidaDiligencia: 'Ordinaria',
      concepto: 'Favorable',
      respuestas: [{ factor: 'sector', opcion: 'medio' }],
    });
    expect(onGuardar.mock.calls[0]?.[0].justificacionDeNivel).toBeUndefined();
  });

  it('sin responder no deja registrar y lo dice', async () => {
    const { onGuardar } = montar({ ...conMatriz(true), [`POST ${CALCULAR}`]: () => resultadoDeMatriz() });
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByText('Valoración de riesgo');
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(dialogo.getByText('Responde todos los factores de la matriz.')).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();
  });

  it('muestra el piso que subió el nivel', async () => {
    montar({ ...conMatriz(true), [`POST ${CALCULAR}`]: () => resultadoDeMatriz({ puntaje: 30, nivel: 3, nivelPorPuntaje: 1, debidaDiligencia: 'Ampliada', mesesRevision: 6, forzadoPor: 'Coincidencias en listas restrictivas: Coincidencia confirmada' }) });
    const dialogo = within(await screen.findByRole('dialog'));
    await responderSector('Sector de bajo riesgo');
    expect(await dialogo.findByText(/El piso de «Coincidencias en listas restrictivas: Coincidencia confirmada» sube el nivel: por puntaje sería 1 · Bajo\./)).toBeInTheDocument();
    expect(dialogo.getByText('3 · Alto')).toBeInTheDocument();
  });

  it('ajustar el nivel exige justificación y la debida diligencia no baja del mínimo del nivel', async () => {
    const { onGuardar } = montar({ ...conMatriz(true), [`POST ${CALCULAR}`]: () => resultadoDeMatriz() });
    const dialogo = within(await screen.findByRole('dialog'));
    await responderSector();
    await dialogo.findByText('42,5 de 100');
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));

    await userEvent.click(dialogo.getByRole('switch', { name: 'Ajustar el nivel' }));
    await userEvent.click(dialogo.getByRole('button', { name: '3 · Alto' }));
    // Con nivel Alto la debida diligencia sube a intensificada y las más ligeras quedan deshabilitadas.
    const select = dialogo.getByRole('combobox', { name: 'Debida diligencia' });
    expect(select).toHaveTextContent('Intensificada');
    await userEvent.click(select);
    expect(screen.getByRole('option', { name: 'Simplificada' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByRole('option', { name: 'Ordinaria' })).toHaveAttribute('aria-disabled', 'true');
    await userEvent.keyboard('{Escape}');

    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(dialogo.getByText('Explica el ajuste del nivel (al menos 10 caracteres).')).toBeInTheDocument();
    expect(onGuardar).not.toHaveBeenCalled();

    await userEvent.click(dialogo.getByRole('textbox', { name: /Justificación del ajuste/ }));
    await userEvent.paste('Opera con efectivo en zona de frontera.');
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(onGuardar.mock.calls[0]?.[0]).toMatchObject({
      nivelRiesgo: 3,
      debidaDiligencia: 'Ampliada',
      justificacionDeNivel: 'Opera con efectivo en zona de frontera.',
    });
  }, 60_000);

  it('un error al calcular se dice y no deja registrar', async () => {
    montar({ ...conMatriz(true), [`POST ${CALCULAR}`]: () => problema(422, 'matriz-no-activa') });
    const dialogo = within(await screen.findByRole('dialog'));
    await responderSector();
    expect(await dialogo.findByText(/No pudimos calcular el resultado\. La empresa no tiene una matriz de riesgo activa/)).toBeInTheDocument();
  });

  it('con un error al leer la matriz no muestra el formulario: lo manda la API y no se puede adivinar', async () => {
    montar({ [`GET ${MATRIZ}`]: () => problema(403, 'prohibido') });
    const dialogo = within(await screen.findByRole('dialog'));
    expect(await dialogo.findByText(/No pudimos leer la matriz de riesgo\./)).toBeInTheDocument();
    expect(dialogo.queryByRole('combobox', { name: 'Concepto' })).not.toBeInTheDocument();
    expect(dialogo.getByRole('button', { name: 'Registrar estudio' })).toBeDisabled();
  });
});

describe('Archivos del estudio', () => {
  const subida = {
    [`PUT ${ADJUNTOS}`]: () => ({ cargaId: 'carga-1', carga: { metodo: 'PUT', url: 'https://almacen.test/subida', cabeceras: { 'Content-Type': 'application/pdf' }, expira: '2026-11-11T16:00:00Z' } }),
    [`POST ${ADJUNTOS}/carga-1/confirmar`]: () => adjuntoDePrueba({ id: 'adj-9', nombre: 'soporte.pdf' }),
  };

  it('sube el archivo en dos pasos, lo lista, deja quitarlo y lo manda citado en el estudio', async () => {
    const almacen = vi.fn(() => Promise.resolve(new Response(null, { status: 200 })));
    vi.stubGlobal('fetch', almacen);
    const { llamadas, onGuardar } = montar({ ...conMatriz(false), ...subida });
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByRole('button', { name: '1 · Bajo' });
    expect(dialogo.getByText('PDF o imagen, hasta 15 MB cada uno. Quedan guardados en el expediente y en el estudio.')).toBeInTheDocument();

    const archivo = new File(['%PDF-1.4'], 'soporte.pdf', { type: 'application/pdf' });
    await userEvent.upload(screen.getByTestId('entrada-Estudio'), archivo);
    expect(await dialogo.findByText('soporte.pdf')).toBeInTheDocument();
    expect(llamadas.find((l) => l.metodo === 'PUT')?.cuerpo).toEqual({ nombreArchivo: 'soporte.pdf', tamanoBytes: 8, tipoMime: 'application/pdf', proposito: 'Estudio' });
    expect(almacen).toHaveBeenCalledWith('https://almacen.test/subida', expect.objectContaining({ method: 'PUT', body: archivo }));
    expect(llamadas.find((l) => l.ruta.endsWith('/confirmar'))?.cuerpo).toEqual({ nombreArchivo: 'soporte.pdf', proposito: 'Estudio' });

    await userEvent.click(dialogo.getByRole('button', { name: '1 · Bajo' }));
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(onGuardar.mock.calls[0]?.[0].adjuntos).toEqual(['adj-9']);

    await userEvent.click(dialogo.getByRole('button', { name: 'Quitar soporte.pdf' }));
    expect(dialogo.queryByText('soporte.pdf')).not.toBeInTheDocument();
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(onGuardar.mock.calls[1]?.[0].adjuntos).toBeUndefined();
  });

  it('avisa antes de subir lo que no es PDF o imagen, o pesa más de 15 MB', async () => {
    const { llamadas } = montar({ ...conMatriz(false), ...subida });
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByRole('button', { name: '1 · Bajo' });
    await userEvent.upload(screen.getByTestId('entrada-Estudio'), new File(['x'], 'datos.zip', { type: 'application/zip' }), { applyAccept: false });
    expect(await dialogo.findByText('Solo se admiten PDF e imágenes (PNG, JPG o WEBP).')).toBeInTheDocument();
    expect(llamadas.some((l) => l.metodo === 'PUT')).toBe(false);
  });

  it('muestra el texto del servidor si rechaza el archivo', async () => {
    montar({ ...conMatriz(false), [`PUT ${ADJUNTOS}`]: () => problema(422, 'adjuntos-demasiados') });
    const dialogo = within(await screen.findByRole('dialog'));
    await dialogo.findByRole('button', { name: '1 · Bajo' });
    await userEvent.upload(screen.getByTestId('entrada-Estudio'), new File(['%PDF'], 'otro.pdf', { type: 'application/pdf' }));
    await waitFor(() => expect(dialogo.getByText('El expediente admite hasta 20 archivos de cumplimiento.')).toBeInTheDocument());
  });
});
