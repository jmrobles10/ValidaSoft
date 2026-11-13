import { abrirEmbebido } from '../../soporte/embebido';
import { aprobarPorApi, crearTipoDeVinculo, invitarVinculo, abrirExpedienteManual, leerCertificado, leerVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Desvincula un tercero y lo reintegra sin revalidar',
    resumen:
      'La administradora desvincula a un aliado que ya estaba aprobado y, mientras lo aprobado siga vigente, lo reintegra sin pedirle documentos nuevos. El certificado se revoca al desvincular y se emite de nuevo al reintegrar.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y una administradora.',
      'Un tipo de vínculo Aliado con un flujo de una etapa (Compras) y un vínculo ya aprobado y activo, con su certificado vigente.',
    ],
    relacionados: ['vinculos/reintegrar-revalidando', 'vinculos/detalle-del-vinculo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: e.norte.areas.compras! }]]);
    const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
    const expedienteId = await abrirExpedienteManual(e, invitacion);
    await aprobarPorApi(e, invitacion, expedienteId, e.norte.areas.compras!, e.ruben);
    expect((await leerVinculo(e, invitacion)).estado).toBe('Activo');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    await expect(app.getByText('Activo', { exact: true }).first()).toBeVisible();

    await app.getByRole('button', { name: 'Más acciones' }).click();
    await paso(page, {
      titulo: 'Abre las acciones del vínculo activo',
      explicacion:
        'Un vínculo activo tiene la acción «Desvincular» en «Más acciones». Desvincular corta la relación con el tercero: el vínculo deja de estar activo, ' +
        'su expediente abierto se anula y el certificado vigente se revoca. Lo aprobado no se borra y puedes reintegrarlo después.',
      resaltar: app.getByRole('menuitem', { name: /^Desvincular/ }),
      configuracion: {
        ajuste: 'Usuarios y roles',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Quién puede desvincular y reintegrar lo decide el permiso de cada rol.',
      },
      mantenerPuntero: true,
    });

    await app.getByRole('menuitem', { name: /^Desvincular/ }).click();
    const desvincular = app.getByRole('dialog', { name: /^Desvincular tercero/ });
    await desvincular.getByRole('textbox', { name: /Motivo|Justificación/ }).first().fill('Terminó el contrato marco.');
    await paso(page, {
      titulo: 'Escribe el motivo de la desvinculación',
      explicacion:
        'El motivo queda en el historial del vínculo y en la auditoría de la empresa. El tercero no ve el motivo.',
      resaltar: desvincular,
    });

    await desvincular.getByRole('button', { name: /^Desvincular/ }).click();
    await expect(desvincular).toBeHidden();
    await expect(app.getByText('Tercero desvinculado.')).toBeVisible();
    await expect(app.getByText('Desvinculado', { exact: true }).first()).toBeVisible();
    expect((await leerCertificado(e, invitacion)).actual?.estado).toBe('Revocado');
    await paso(page, {
      titulo: 'Comprueba que el vínculo quedó desvinculado',
      explicacion:
        'El estado pasa a «Desvinculado» y el certificado del tercero queda revocado: sus sistemas integrados lo ven como no vigente. ' +
        'El historial del vínculo registra la desvinculación con su fecha.',
      resaltar: app.getByText('Desvinculado', { exact: true }).first(),
      avisos: true,
    });

    await app.getByRole('button', { name: 'Reintegrar', exact: true }).click();
    const reintegrar = app.getByRole('dialog', { name: /^Reintegrar vínculo/ });
    await reintegrar.getByRole('radio', { name: /Reintegrar sin revalidar/ }).click();
    await reintegrar.getByRole('textbox', { name: 'Justificación' }).fill('Se renovó el contrato marco.');
    await paso(page, {
      titulo: 'Reintegra sin revalidar',
      explicacion:
        'La opción «Reintegrar sin revalidar» restituye el estado que tenía el vínculo antes de desvincularlo y vuelve a emitir el certificado con lo que ya se aprobó. ' +
        'Solo está disponible si el vínculo fue aprobado y su aprobación sigue vigente. Si no, el sistema pide revalidar (ver el recorrido de reintegrar revalidando).',
      resaltar: reintegrar.getByRole('radio', { name: /Reintegrar sin revalidar/ }),
      variantes: [
        {
          cuando: 'Algún documento del flujo venció',
          entonces: 'La opción sin revalidar no se puede elegir y el diálogo explica por qué: hay que renovar los documentos.',
        },
      ],
    });

    await reintegrar.getByRole('button', { name: 'Reintegrar vínculo' }).click();
    await expect(app.getByText('Vínculo reintegrado.')).toBeVisible();
    await expect(app.getByText('Activo', { exact: true }).first()).toBeVisible();
    const certificados = await leerCertificado(e, invitacion);
    expect(certificados.actual).toMatchObject({ emision: 2, estado: 'Vigente', expedienteId });
    await paso(page, {
      titulo: 'Comprueba que el vínculo volvió a estar activo',
      explicacion:
        'El vínculo vuelve a «Activo» sin abrir un expediente nuevo y se emite un certificado nuevo con lo aprobado. El tercero recibe un aviso por correo que dice que quedó como estaba, sin documentos nuevos.',
      resaltar: app.getByText('Activo', { exact: true }).first(),
      avisos: true,
    });
  },
);
