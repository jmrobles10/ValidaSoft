import { pdf } from '../../soporte/archivos';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Revisa tu bóveda de documentos',
    resumen:
      'Mi bóveda guarda cada documento una sola vez, aunque lo pidan varias empresas. En cada tarjeta ves la versión vigente, su vigencia y qué tiene cada empresa de ese documento.',
    precondiciones: [
      'Dos grupos o empresas que invitan al mismo tercero: Constructora Norte (RUT y Cámara de comercio) y Constructora Sur (RUT y certificado de seguridad y salud en el trabajo).',
      'Un tercero invitado por ambas, que ya aceptó las dos invitaciones y subió los documentos que cada una pide; el RUT se sube una sola vez.',
    ],
    relacionados: ['tercero/boveda-subir-un-documento', 'tercero/boveda-documento-compartido', 'tercero/subir-version-nueva'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería La Esquina S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    const enSur = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await completarInvitacion(page, enNorte, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.identificacion.numero}`) },
    ]);
    await entrarPorInvitacion(page, enSur);
    await aceptarInvitacion(page, enSur);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, SST.nombre, pdf(`SST ${tercero.identificacion.numero}`), {
      campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) },
    });
    await esperarEstadoDelTercero(page, 'EnRevision');

    await page.goto('/boveda');
    const tarjetaRut = page.getByRole('article').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
    const tarjetaSst = page.getByRole('article').filter({ has: page.getByRole('heading', { name: SST.nombre, exact: true }) });
    await expect(tarjetaRut).toHaveCount(1);
    await expect(tarjetaRut.getByText('Versión 1 · cargada')).toBeVisible();

    // 1. Cada documento una sola vez.
    await paso(page, {
      titulo: 'Mira cada documento una sola vez',
      explicacion:
        'En «Mi bóveda» el RUT aparece una sola tarjeta aunque lo pidan Norte y Sur. Arriba ves la versión vigente y la fecha en que la subiste. ' +
        'Por eso no tienes que volver a cargar el mismo archivo para cada empresa.',
      resaltar: tarjetaRut,
    });

    // 2. Qué tiene cada empresa de ese documento.
    await expect(tarjetaRut.getByRole('list', { name: 'Con tus empresas' })).toBeVisible();
    await expect(tarjetaRut).toContainText(e.norte.nombre);
    await expect(tarjetaRut).toContainText(e.sur.nombre);
    await expect(tarjetaRut).toContainText('Revisando la versión 1.');
    await paso(page, {
      titulo: 'Revisa qué tiene cada empresa del documento',
      explicacion:
        'En «Con tus empresas» cada empresa aparece con lo que tiene de ese documento. Aquí ambas están revisando la versión 1, la vigente. ' +
        'Cuando una empresa ya la aprobó, la línea dice «Aprobó la versión»; si te pidió corregirla, dice «Te pidió corregir la versión».',
      resaltar: tarjetaRut.getByRole('list', { name: 'Con tus empresas' }),
      variantes: [
        { cuando: 'Retiraste la versión vigente y no tienes otra', entonces: 'La empresa aparece como «Tenía la versión…, que retiraste»: ya no la recibe hasta que subas una nueva.' },
      ],
    });

    // 3. Un documento que solo pide una empresa.
    await expect(tarjetaSst).toContainText(e.sur.nombre);
    await paso(page, {
      titulo: 'Identifica los documentos que pide una sola empresa',
      explicacion:
        'El certificado de seguridad y salud solo lo pide Sur, así que su tarjeta muestra solo esa empresa. Cada tarjeta muestra la vigencia de ese documento con su fecha de vencimiento.',
      resaltar: tarjetaSst,
    });
  },
);
