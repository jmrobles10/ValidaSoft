import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirExpedienteManual, aprobarPorApi, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { areaDe } from '../nucleo/semillas';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Revisa la lista de vínculos y filtra por estado y tipo',
    resumen:
      'La administradora abre «Vínculos» para ver a todos los terceros con relación con su empresa, filtra por estado y tipo de vínculo, busca un tercero y abre uno de la lista.',
    precondiciones: [
      'Un grupo empresarial con las empresas «Constructora Norte» y «Constructora Sur», y una administradora del grupo.',
      'Varios vínculos invitados por el sistema integrado: proveedores personas jurídicas, un cliente y tres aliados que ya están en validación, activos y bloqueados.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const aceros = await invitar(e, e.norte, nuevoTercero('Aceros del Norte S.A.S.'), 'PROVEEDOR');
    await invitar(e, e.norte, nuevoTercero('Ferretería Andina S.A.S.'), 'PROVEEDOR');
    const transportes = await invitar(e, e.norte, nuevoTercero('Transportes del Valle S.A.S.'), 'CLIENTE');
    // Tres «aliados» en otras situaciones (tipo de vínculo propio con un flujo sin documentos): en validación, activo y bloqueado.
    const compras = areaDe(e.norte, 'compras');
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: compras }]]);
    const enValidacion = await invitarVinculo(e, e.norte, nuevoTercero('Servicios en Validación S.A.S.'), 'ALIADO');
    await abrirExpedienteManual(e, enValidacion);
    const activo = await invitarVinculo(e, e.norte, nuevoTercero('Metalúrgica Activa S.A.S.'), 'ALIADO');
    await aprobarPorApi(e, activo, await abrirExpedienteManual(e, activo), compras, e.ruben);
    const bloqueado = await invitarVinculo(e, e.norte, nuevoTercero('Obras Bloqueadas S.A.S.'), 'ALIADO');
    await aprobarPorApi(e, bloqueado, await abrirExpedienteManual(e, bloqueado), compras, e.ruben);
    await llamar('POST', `/api/v1/vinculos/${bloqueado.vinculoId}/bloquear`, { llave: e.admin, empresa: e.norte.id, cuerpo: { motivo: 'Coincidencia en una lista restrictiva por revisar.', hasta: null } });

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${aceros.correo}` })).toBeVisible();

    // 1. La lista.
    await paso(page, {
      titulo: 'Abre la lista de vínculos',
      explicacion:
        'La lista reúne a todos los terceros que tienen un vínculo con tu empresa. Por cada fila ves el tercero, el tipo de vínculo, su estado (Invitado, En validación, Activo, Bloqueado…) y su fecha de creación, y en la columna «Referencias externas» el identificador que le dio cada sistema integrado. ' +
        'Para abrir uno, haz clic en su fila.',
      resaltar: app.getByRole('table', { name: 'Vínculos' }),
      variantes: [
        { cuando: 'Eres administrador del grupo', entonces: 'Ves los vínculos de la empresa activa; cambiar de empresa en el selector de la parte superior cambia la lista.' },
        { cuando: 'Tu rol no incluye la sección Vínculos', entonces: 'La sección no aparece en la barra y no puedes abrirla.' },
      ],
      configuracion: {
        ajuste: 'Usuarios y roles',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'El rol de cada persona decide si ve la sección Vínculos y qué puede hacer en ella.',
      },
    });

    // 2. El menú de estados.
    await app.getByRole('button', { name: /^Estado/ }).click();
    const menu = app.getByRole('menu');
    await expect(menu.getByRole('menuitem', { name: 'Activo', exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Elige un estado para filtrar',
      explicacion:
        'El filtro «Estado» (1) ofrece cada situación en que puede estar un vínculo: Invitado (esperan que el tercero acepte la invitación), En validación (su expediente está abierto), Activo, Por vencer, Bloqueado y los demás. ' +
        '«En revisión» no es un estado propio: es un vínculo activo cuya última actualización espera una decisión de las áreas. Los filtros se guardan en la dirección de la página, así que se mantienen al volver.',
      resaltar: menu,
    });

    // 3. Solo los activos.
    await menu.getByRole('menuitem', { name: 'Activo', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${activo.correo}` })).toBeVisible();
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${aceros.correo}` })).toHaveCount(0);
    await paso(page, {
      titulo: 'Mira solo los vínculos activos',
      explicacion:
        'Con «Activo» (1) la lista deja solo a los terceros que ya aprobaron todas las áreas y pueden operar con la empresa: aquí, la metalúrgica. Los demás estados desaparecen hasta que cambies el filtro.',
      resaltar: [app.getByRole('button', { name: /^Estado/ }), app.getByRole('row', { name: `Abrir vínculo de ${activo.correo}` })],
    });

    // 3b. Bloqueados.
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Bloqueado', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${bloqueado.correo}` })).toBeVisible();
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${activo.correo}` })).toHaveCount(0);
    await paso(page, {
      titulo: 'Encuentra los vínculos bloqueados',
      explicacion:
        'Con «Bloqueado» (1) ves a los terceros a los que la empresa suspendió, por ejemplo por una coincidencia en una lista restrictiva. Un vínculo bloqueado no recibe documentos nuevos ni puede enviar a revisión hasta que se desbloquee.',
      resaltar: [app.getByRole('button', { name: /^Estado/ }), app.getByRole('row', { name: `Abrir vínculo de ${bloqueado.correo}` })],
      variantes: [{ cuando: 'El vínculo está en validación', entonces: 'Aparece con el estado «En validación» mientras las áreas deciden; filtra por ese estado para verlos juntos.' }],
    });

    // 3. Filtro por tipo y limpiar.
    await app.getByRole('button', { name: /^Limpiar filtros/ }).click();
    await app.getByRole('button', { name: /^Tipo/ }).click();
    await app.getByRole('menuitem', { name: 'Cliente', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${transportes.correo}` })).toBeVisible();
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${aceros.correo}` })).toHaveCount(0);
    await paso(page, {
      titulo: 'Filtra por tipo de vínculo',
      explicacion:
        'El filtro «Tipo» separa los vínculos según el tipo que tiene cada tercero con la empresa (Proveedor, Cliente u otro que hayas configurado). ' +
        'El mismo tercero puede tener dos vínculos distintos con la empresa, uno por tipo; cada uno aparece en su fila. «Limpiar filtros» quita todos a la vez.',
      resaltar: app.getByRole('button', { name: /^Tipo/ }),
      configuracion: {
        ajuste: 'Tipos de vínculo',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Los tipos que existen en el grupo son las opciones de este filtro.',
      },
    });

    // 4. Buscar por referencia.
    await app.getByRole('button', { name: /^Limpiar filtros/ }).click();
    await paso(page, {
      titulo: 'Busca un vínculo por su referencia externa',
      explicacion:
        'El buscador encuentra un vínculo por el identificador que le dio un sistema integrado. Escribe el sistema, un punto medio y la referencia (por ejemplo «ERP · PRV-002») y pulsa Enter. ' +
        'Si no existe, la pantalla dice que ninguna referencia coincide; si escribes solo la referencia sin el sistema, te pide escribirla con el sistema delante.',
      resaltar: app.getByRole('searchbox'),
      variantes: [{ cuando: 'El sistema integrado no tiene referencias registradas', entonces: 'La búsqueda solo devuelve coincidencias de los sistemas que ya crearon vínculos con la empresa.' }],
    });

    // 5. Abrir uno.
    await app.getByRole('row', { name: `Abrir vínculo de ${aceros.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1 })).toBeVisible();
    await paso(page, {
      titulo: 'Abre un vínculo de la lista',
      explicacion:
        'Al hacer clic en la fila se abre el detalle del vínculo, con los datos del tercero, su estado, la invitación enviada y las acciones que el estado permite (reenviar, bloquear, desvincular, anular…). ' +
        'Desde el detalle se vuelve a la lista con la flecha de la parte superior.',
      resaltar: app.getByRole('heading', { level: 1 }),
    });
  },
);
