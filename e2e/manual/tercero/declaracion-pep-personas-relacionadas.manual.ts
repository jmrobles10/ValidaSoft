import { areaDe } from '../nucleo/semillas';
import { abrirSeccion, guardarSeccionCompleta, publicarFlujoDeFicha, REPRESENTANTE, SOCIO } from '../../soporte/ficha';
import { dialogoDeclaracion, diligenciarDeclaracion } from '../../soporte/declaracion-pep';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import type { Locator, Page } from '@playwright/test';

const PREGUNTA_PERSONA = '¿Es persona expuesta políticamente (PEP)?';
const PREGUNTA_VINCULO = '¿Tiene vínculo con una persona expuesta políticamente?';

/** Agrega una persona desde «Agregar persona» y responde su PEP; el diálogo queda abierto hasta que se pulse «Listo». */
async function agregar(page: Page, persona: { rol: string; numero: string; nombres: string; apellidos: string; porcentaje?: string }, pep: 'Sí' | 'No'): Promise<Locator> {
  await page.getByRole('button', { name: 'Agregar persona' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Agregar persona' });
  await dialogo.getByRole('combobox', { name: 'Rol' }).click();
  await page.getByRole('option', { name: persona.rol, exact: true }).click();
  await dialogo.getByRole('combobox', { name: 'Tipo de identificación' }).click();
  await page.getByRole('option', { name: 'CC', exact: true }).click();
  await dialogo.getByRole('textbox', { name: 'Número de identificación' }).fill(persona.numero);
  await dialogo.getByRole('textbox', { name: 'Nombres', exact: true }).fill(persona.nombres);
  await dialogo.getByRole('textbox', { name: 'Apellidos', exact: true }).fill(persona.apellidos);
  if (persona.porcentaje) await dialogo.getByRole('textbox', { name: 'Participación (%)' }).fill(persona.porcentaje);
  await dialogo.getByRole('radiogroup', { name: PREGUNTA_VINCULO }).getByRole('radio', { name: 'No' }).check();
  await dialogo.getByRole('radiogroup', { name: PREGUNTA_PERSONA }).getByRole('radio', { name: pep }).check();
  return dialogo;
}

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Declara la PEP de un socio y de sus personas relacionadas',
    resumen:
      'En la información legal de tu empresa, cada representante y socio tiene su propia respuesta de PEP. Si un socio responde que sí, declara en su fila, indica sus personas relacionadas y puede firmar un apoderado.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Cliente que pide la información legal de la persona jurídica.',
      'Una persona jurídica invitada como cliente de Sur, que aceptó la invitación, con un representante legal que no es PEP y un socio del 100 % que sí lo es.',
    ],
    relacionados: ['tercero/declaracion-pep-persona-natural', 'tercero/descargar-mi-declaracion-pep'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await publicarFlujoDeFicha(e, e.sur, 'PEP_JURIDICA_MANUAL', areaDe(e.sur, 'revision'), 'CLIENTE', [{ seccion: 'FICHA_LEGAL' }]);
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await abrirSeccion(page, 'legal', 'Información legal');

    // 1. Representante (no PEP) y socio (PEP): al responder «Sí» se abre su declaración.
    const representante = await agregar(page, REPRESENTANTE, 'No');
    await representante.getByRole('button', { name: 'Listo' }).click();
    const socio = await agregar(page, { ...SOCIO, porcentaje: '100' }, 'Sí');
    const dialogo = dialogoDeclaracion(page);
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Responde «Sí» a la PEP del socio',
      explicacion:
        'Al agregar a un socio o accionista, la pregunta «¿Es persona expuesta políticamente (PEP)?» pide su respuesta. Si respondes que sí, se abre la declaración jurada PEP de esa persona, en su propia fila. ' +
        'El nombre y la relación salen de lo que ya escribiste sobre el socio; la dirección la escribe quien declara.',
      resaltar: dialogo,
      variantes: [{ cuando: 'La persona responde «No»', entonces: 'No se abre declaración y la persona queda lista con solo sus datos.' }],
    });

    // 2. Firma por apoderado y personas PEP relacionadas.
    await diligenciarDeclaracion(page, {
      firma: 'dibujar',
      direccion: 'Calle 80 # 12-34',
      pepRelacionadas: true,
      firmante: { nombre: 'Pedro Apoderado Gil', tipo: 'CC', numero: '80123456', calidad: 'Apoderado' },
    });
    await expect(socio.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
    await paso(page, {
      titulo: 'Registra las personas PEP relacionadas y el firmante',
      explicacion:
        'Si respondes «Sí» a «¿Tiene vínculo con una persona expuesta políticamente?», registras a las personas PEP relacionadas: en este ejemplo, un familiar y un asociado cercano, cada uno con su cargo y entidad. ' +
        'El firmante aquí es un apoderado, así que la declaración queda firmada «por apoderado» con su nombre y documento. Tampoco se guarda nada hasta que guardes la sección.',
      resaltar: socio,
    });

    // 3. Vuelve a la lista de personas relacionadas y guarda la sección.
    await socio.getByRole('button', { name: 'Listo' }).click();
    const lista = page.getByRole('list', { name: 'Personas relacionadas' });
    await expect(lista.getByRole('listitem', { name: /Jorge Iván Castro Pinto/ }).getByText('Falta la declaración PEP')).toHaveCount(0);
    await guardarSeccionCompleta(page, 'Guardaste Información legal: está completa.');
    await paso(page, {
      titulo: 'Guarda la información legal',
      explicacion:
        'Al guardar, la sección queda completa y la declaración del socio queda guardada con ella. Quien respondió «No» no tiene declaración. ' +
        'Mientras una persona que es PEP no tenga su declaración firmada, aparece «Falta la declaración PEP» en su fila.',
      resaltar: lista,
    });
  },
);
