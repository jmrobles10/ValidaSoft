import { pdf } from '../../soporte/archivos';
import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, subirDocumento } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Respalda tu información con un documento de tu bóveda',
    resumen:
      'En tu «Mi información», cada sección puede llevar un documento que la respalde. Eliges el documento de tu bóveda, como el RUT, para que la empresa pueda comprobar los datos que escribiste.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Proveedor que pide RUT, Cámara de comercio y las seis secciones de la ficha.',
      'Un tercero jurídico invitado por Sur, que aceptó la invitación y ya tiene su RUT en la bóveda.',
    ],
    relacionados: ['tercero/ficha-actualizar-desde-mi-rut', 'tercero/ficha-barra-y-pendientes'],
  },
  async ({ page }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Aceros Ficha Soporte S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));

    // 1. La sección General ofrece los documentos de la bóveda que la respaldan.
    await abrirSeccion(page, 'general', 'Información general');
    await llenarContactoYDomicilio(page, '601 555 1234', 'Calle 45 # 12-08');
    const soporte = page.getByRole('group', { name: 'Documento que respalda esta información' });
    await expect(soporte).toBeVisible();
    await expect(soporte.getByRole('checkbox', { name: new RegExp(`^${RUT} · versión 1`) })).toBeVisible();
    await paso(page, {
      titulo: 'Elige el documento que respalda la sección',
      explicacion:
        'En «Documento que respalda esta información» aparecen los documentos de tu bóveda que sirven para comprobar esta sección. Aquí el RUT, versión 1. ' +
        'Marca el que tu empresa pueda usar para verificar estos datos: la empresa cruza lo que escribiste con ese documento al revisarlo.',
      resaltar: soporte,
      variantes: [
        { cuando: 'Tu bóveda no tiene ningún documento que sirva de soporte', entonces: 'El recuadro dice que aún no tienes un documento de soporte y te ofrece subir uno desde ahí mismo.' },
      ],
    });

    // 2. Marca el soporte y guarda la sección.
    await soporte.getByRole('checkbox', { name: new RegExp(`^${RUT} · versión 1`) }).check();
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
    await paso(page, {
      titulo: 'Guarda la sección con su soporte',
      explicacion:
        'Al guardar, la sección queda completa con el documento marcado. Si después retiras ese documento de tu bóveda, la sección conserva tus datos, pero el soporte deja de valer y tendrás que elegir otro.',
      resaltar: page.getByRole('group', { name: 'Guardar la sección' }),
    });

    // 3. Si no tienes el soporte, lo subes desde la misma sección.
    await abrirSeccion(page, 'general', 'Información general');
    const subir = page.getByRole('link', { name: 'Subir un soporte' });
    await expect(subir).toBeVisible();
    await paso(page, {
      titulo: 'Sube un soporte si no lo tienes',
      explicacion:
        'Si el documento que necesitas no está en tu bóveda, usa «Subir» dentro del mismo recuadro. Antes de salir, guarda tus cambios: al volver a la sección los encuentras guardados y ya puedes elegir el archivo que subiste.',
      resaltar: subir,
    });
  },
);
