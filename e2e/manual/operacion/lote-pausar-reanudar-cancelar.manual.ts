import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, nuevoTercero, type Escenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

interface LoteApi {
  id: string;
  estado: string;
  totales: { filas: number; procesadas: number; pendientes: number; exitosas: number; fallidas: number };
}

/** Filas válidas de un lote (terceros nuevos con NIT y correo únicos). */
function filasDeProveedores(cantidad: number, prefijo: string) {
  return Array.from({ length: cantidad }, (_, i) => {
    const t = nuevoTercero(`${prefijo} ${String(i + 1)} S.A.S.`);
    return {
      tipoIdentificacion: 'NIT',
      numeroIdentificacion: t.identificacion.numero,
      digitoVerificacion: t.identificacion.digitoVerificacion === null ? null : String(t.identificacion.digitoVerificacion),
      nombre: t.nombre,
      correo: t.correo,
      tipoVinculo: 'PROVEEDOR',
    };
  });
}

/** Crea el lote como lo hace el botón «Invitar» del asistente (la creación se documenta en `operacion/invitacion-por-lotes`). */
async function crearLote(e: Escenario, nombre: string, cantidad: number): Promise<LoteApi> {
  return llamar<LoteApi>('POST', '/api/v1/invitaciones/lotes', { llave: e.admin, empresa: e.norte.id, cuerpo: { nombre, filas: filasDeProveedores(cantidad, nombre) } });
}

const leerLote = (e: Escenario, id: string) => llamar<LoteApi>('GET', `/api/v1/invitaciones/lotes/${id}`, { llave: e.admin, empresa: e.norte.id });

/** Lo que hace Plataforma al guardar «Plan y límites» de un grupo: aquí, solo el límite de vínculos y el bloqueo al llegar a él. */
async function fijarLimiteDeVinculos(e: Escenario, limite: number): Promise<void> {
  await llamar('PUT', `/api/v1/plataforma/tenants/${e.tenantId}/plan`, {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { codigo: 'empresarial', limites: { usuarios: null, empresas: null, vinculosActivos: limite, almacenamientoMb: null, llamadasApiMes: null }, bloqueoSuave: true },
  });
}

const MAILPIT = process.env.E2E_MAILPIT ?? 'http://localhost:8025';

/** Espera a que dejen de llegar correos a Mailpit (las invitaciones de un lote grande salen por una cola). */
async function esperarQueSeAsienteElCorreo(maximoSegundos = 90): Promise<void> {
  const aceptados = async (): Promise<number> => {
    try {
      const info = (await (await fetch(`${MAILPIT}/api/v1/info`)).json()) as { RuntimeStats?: { SMTPAccepted?: number } };
      return info.RuntimeStats?.SMTPAccepted ?? 0;
    } catch {
      return 0;
    }
  };
  let anterior = await aceptados();
  let quietos = 0;
  for (let i = 0; i < maximoSegundos && quietos < 3; i++) {
    await new Promise((r) => setTimeout(r, 1000));
    const actual = await aceptados();
    quietos = actual - anterior <= 2 ? quietos + 1 : 0;
    anterior = actual;
  }
}

/**
 * Vínculos que cuentan para el límite del plan (los que no están anulados ni desvinculados). Un lote cancelado termina las filas que tenía en curso y
 * sus totales no las recogen, así que se lee el uso del grupo y se espera a que no cambie en dos lecturas seguidas.
 */
async function vinculosDelGrupo(e: Escenario): Promise<number> {
  const leer = async () =>
    (await llamar<{ mediciones: { metrica: string; usado: number }[] }>('GET', '/api/v1/tenant/uso', { llave: e.admin })).mediciones.find((m) => m.metrica === 'VinculosActivos')?.usado ?? 0;
  let anterior = await leer();
  for (let i = 0; i < 30; i++) {
    await new Promise((r) => setTimeout(r, 1000));
    const actual = await leer();
    if (actual === anterior) return actual;
    anterior = actual;
  }
  throw new Error('El conteo de vínculos del grupo no se asienta.');
}

/** Espera a que el lote deje de procesarse (lo hace solo, en segundo plano). */
async function esperarFinDelLote(e: Escenario, id: string): Promise<LoteApi> {
  for (let intento = 0; intento < 120; intento++) {
    const lote = await leerLote(e, id);
    if (lote.estado !== 'Procesando') return lote;
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error('El lote no terminó a tiempo.');
}

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Pausa, reanuda y cancela un lote, y reintenta sus filas fallidas',
    resumen:
      'La administradora sigue un lote grande mientras se procesa, lo pausa, lo reanuda y lo cancela antes de que termine. Con otro lote que quedó con filas fallidas por el límite del plan, ve qué falló, amplía el plan y reintenta solo las fallidas.',
    precondiciones: [
      'Un grupo con Constructora Norte y su administradora Ana.',
      'Un lote de 3000 invitaciones recién creado, todavía en proceso.',
      'Un segundo lote de 8 filas cuyo grupo llegó al límite de vínculos del plan a mitad de camino.',
    ],
    relacionados: ['operacion/invitacion-por-lotes', 'configuracion/plan-del-grupo', 'vinculos/lista-y-filtros'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grande = await crearLote(e, 'Proveedores de obra', 3000);
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `invitaciones/lotes/${grande.id}`);
    const estado = (nombre: string) => app.locator(`[data-estado="${nombre}"]`);
    await expect(app.getByRole('heading', { level: 1, name: 'Proveedores de obra' })).toBeVisible();
    await expect(estado('Procesando')).toBeVisible();

    // 1. El lote en proceso.
    await paso(page, {
      titulo: 'Sigue un lote mientras se procesa',
      explicacion:
        'Un lote nuevo se procesa solo, en segundo plano, de a tandas de filas. El estado «Procesando» (1) y la barra (2) dicen cuántas filas van de cuántas, y la pantalla se refresca sola mientras el lote está en curso; ' +
        'abajo, los totales por estado: invitadas, fallidas, pendientes, con errores y repetidas. Mientras corre puedes pausarlo o cancelarlo.',
      resaltar: [estado('Procesando'), app.getByRole('progressbar')],
      configuracion: { ajuste: 'Permiso de vínculos (escribir)', ruta: 'Configuración › Usuarios y roles (/usuarios)', efecto: 'Sin él se ve el avance del lote, pero no los botones de pausar, reanudar, cancelar ni reintentar.' },
    });

    // 2. Pausar.
    await app.getByRole('button', { name: 'Pausar', exact: true }).click();
    await expect(app.getByText('Lote pausado.')).toBeVisible();
    await expect(estado('Pausado')).toBeVisible();
    const reanudar = app.getByRole('button', { name: 'Reanudar', exact: true });
    await expect(reanudar).toBeVisible();
    await paso(page, {
      titulo: 'Pausa el lote',
      explicacion:
        'Con «Pausar», el lote deja de tomar filas nuevas: la fila que se estaba procesando termina y las demás quedan «Pendientes». El estado pasa a «Pausado» (1) y en lugar de «Pausar» aparece «Reanudar» (2). ' +
        'Úsalo para detener un envío masivo mientras revisas algo, sin perder lo ya invitado.',
      resaltar: [estado('Pausado'), reanudar],
      avisos: true,
    });

    // 3. Reanudar.
    await reanudar.click();
    await expect(app.getByText('Lote reanudado.')).toBeVisible();
    await expect(estado('Procesando')).toBeVisible();
    await paso(page, {
      titulo: 'Reanuda el lote',
      explicacion: '«Reanudar» continúa con las filas pendientes desde donde quedó, sin repetir las que ya se invitaron. El estado vuelve a «Procesando» (1) y el avance sigue subiendo.',
      resaltar: [estado('Procesando')],
      avisos: true,
    });

    // 4. Cancelar: el diálogo.
    await app.getByRole('button', { name: 'Cancelar lote', exact: true }).click();
    const dialogo = app.getByRole('dialog', { name: 'Cancelar el lote' });
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Cancela el lote antes de que termine',
      explicacion:
        'Cancelar pide confirmación y avisa qué pasa: las filas que aún no se procesaron no recibirán invitación y lo ya invitado se mantiene. ' +
        'Un lote cancelado no se puede reanudar; si quedaron terceros por invitar, se crea otro lote con ellos.',
      resaltar: dialogo,
    });

    // 5. Cancelado: lo pendiente queda «Cancelada».
    await dialogo.getByRole('button', { name: 'Cancelar lote', exact: true }).click();
    await expect(app.getByText('Lote cancelado.')).toBeVisible();
    await expect(estado('Cancelado')).toBeVisible();
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Pendiente', exact: true }).click();
    const canceladas = app.getByRole('table', { name: 'Filas del lote' }).getByRole('row').nth(1);
    await expect(canceladas).toContainText('Cancelada');
    await paso(page, {
      titulo: 'Revisa lo que quedó sin invitar',
      explicacion:
        'El lote queda «Cancelado» (1) y las filas que no se alcanzaron a procesar figuran como «Cancelada» (2) en la tabla y en el informe. Los botones de pausar, reanudar y cancelar desaparecen: el estado es definitivo. ' +
        'El filtro «Estado» (3) separa las filas por su resultado; con «Pendiente» se ven justo las que no se alcanzaron a procesar.',
      resaltar: [estado('Cancelado'), canceladas, app.getByRole('button', { name: /^Estado/ })],
      avisos: true,
    });

    // 6. Un lote que terminó con filas fallidas por el límite del plan.
    const existentes = await vinculosDelGrupo(e);
    await fijarLimiteDeVinculos(e, existentes + 3);
    const conFallidas = await crearLote(e, 'Proveedores de mantenimiento', 8);
    expect((await esperarFinDelLote(e, conFallidas.id)).totales.fallidas).toBe(5);
    await app.getByRole('link', { name: 'Invitaciones por lote' }).click();
    const fila = app.getByRole('row', { name: 'Abrir el lote Proveedores de mantenimiento' });
    await expect(fila).toContainText('Con errores');
    await expect(fila).toContainText('3 invitadas · 5 fallidas');
    await paso(page, {
      titulo: 'Reconoce un lote que terminó con errores',
      explicacion:
        'La lista de lotes muestra cada uno con su estado, su avance y su resultado. El lote de mantenimiento terminó «Con errores» (1): se invitaron 3 filas y 5 fallaron. ' +
        'Una fila falla cuando la regla de negocio no la deja pasar al procesarla; aquí, porque el grupo llegó al límite de vínculos de su plan. Abre el lote para ver cuáles y por qué.',
      resaltar: fila,
      variantes: [{ cuando: 'El lote terminó sin fallas', entonces: 'Su estado es «Completado» y no aparece el botón de reintentar.' }],
    });

    // 7. Qué falló.
    await fila.click();
    await expect(app.getByRole('heading', { level: 1, name: 'Proveedores de mantenimiento' })).toBeVisible();
    await expect(estado('CompletadoConErrores')).toBeVisible();
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Fallida', exact: true }).click();
    const tabla = app.getByRole('table', { name: 'Filas del lote' });
    const motivo = tabla.getByText(/permite \d+ vínculos activos y el grupo ya los tiene/).first();
    await expect(motivo).toBeVisible();
    const reintentar = app.getByRole('button', { name: 'Reintentar 5 fallidas' });
    await expect(reintentar).toBeVisible();
    await paso(page, {
      titulo: 'Mira por qué fallaron las filas',
      explicacion:
        'Filtrando por «Fallida» (1), la columna «Detalle» (2) dice el motivo de cada fila: aquí, que el plan permite cierto número de vínculos activos y el grupo ya los tiene, y que hay que pedir a Plataforma ampliar el plan. ' +
        'Mientras el lote tenga filas fallidas aparece «Reintentar 5 fallidas» (3), que vuelve a procesar solo esas filas.',
      resaltar: [app.getByRole('button', { name: /^Estado/ }), motivo, reintentar],
      variantes: [
        { cuando: 'El vínculo del tercero está bloqueado o terminado', entonces: 'La fila falla con el motivo del conflicto: no se puede invitar a ese tercero hasta resolverlo.' },
        { cuando: 'Ocurre un error inesperado al procesar la fila', entonces: 'Queda fallida con «No se pudo procesar la fila; reinténtala.» y se puede reintentar.' },
      ],
      configuracion: { ajuste: 'Plan del grupo (límite de vínculos)', ruta: 'Configuración › Plan del grupo (/plan-grupo)', efecto: 'Lo fija Plataforma; con el bloqueo activo, las filas que pasan del límite fallan hasta que se amplíe.' },
    });

    // 8. Ampliar el plan y reintentar.
    await fijarLimiteDeVinculos(e, existentes + 20);
    await reintentar.click();
    await expect(app.getByText('Las filas fallidas se vuelven a procesar.')).toBeVisible();
    await expect(estado('Completado')).toBeVisible();
    await expect(app.getByRole('button', { name: /^Reintentar/ })).toHaveCount(0);
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Todos', exact: true }).click();
    await expect(app.getByText('8 de 8')).toBeVisible();
    await paso(page, {
      titulo: 'Reintenta solo las filas fallidas',
      explicacion:
        'Después de que Plataforma amplió el plan, «Reintentar fallidas» devolvió las 5 filas a «Pendiente» y las procesó otra vez; las 3 que ya estaban invitadas no se tocaron. ' +
        'El lote pasó a «Completado» (1) con las 8 filas procesadas (2) y el botón de reintentar desapareció.',
      resaltar: [estado('Completado'), app.getByText('8 de 8')],
      avisos: true,
    });

    // 9. El informe por fila.
    const informe = app.getByRole('button', { name: 'Descargar informe' });
    const bajada = page.waitForEvent('download');
    await informe.click();
    const archivo = await bajada;
    expect(archivo.suggestedFilename()).toMatch(/\.xlsx$/i);
    await paso(page, {
      titulo: 'Descarga el informe por fila',
      explicacion:
        '«Descargar informe» (1) baja un Excel con una línea por fila: identificación, razón social, correo, tipo de vínculo, estado, acción (invitado, reenviado o sin cambios), detalle del error, fila de la que es repetida, el vínculo creado o reutilizado y cuándo se procesó. ' +
        'Sirve para conciliar el lote con tu archivo original.',
      resaltar: informe,
    });

    // Los correos de invitación de las filas procesadas salen por una cola: se espera a que se asienten para no atrasar el correo de otros recorridos.
    await esperarQueSeAsienteElCorreo();
  },
);
