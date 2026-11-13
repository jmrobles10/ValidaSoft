import { pdf } from '../../soporte/archivos';
import { completarInvitacion, esperarTarjeta, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { invitar, nuevoTercero, SST, crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

/** Fecha `YYYY-MM-DD` de hoy en Bogotá más `dias` (negativo: en el pasado). */
function fecha(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Atiende un documento que está por vencer',
    resumen:
      'El certificado de seguridad y salud vence 30 días después de su expedición. Cuando faltan pocos días, la tarjeta lo marca como «Por vencer»; cuando vence, pasa por un periodo de gracia. Subes uno nuevo y el documento vuelve a estar vigente.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo, con vigencia de 30 días, 10 de aviso y 5 de gracia).',
      'Un tercero cuyo certificado de seguridad fue expedido hace 25 días, así que queda a 5 días de vencer.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/vinculo-por-vencer-renovar'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Constructora Vigencia S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero);
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${tercero.identificacion.numero}`), campos: { 'Fecha de expedición': fecha(-25) } },
    ]);
    await esperarTarjeta(page, SST.nombre, 'Por vencer');
    const vence = fecha(5).split('-').reverse().join('/');

    // 1. La tarjeta avisa que vence.
    await expect(tarjeta(page, SST.nombre)).toContainText(`Vence el ${vence}`);
    await expect(tarjeta(page, SST.nombre)).toContainText('Vence pronto');
    await paso(page, {
      titulo: 'Revisa qué documento está por vencer',
      explicacion:
        'La tarjeta del certificado dice «Por vencer» y la fecha en que vence. El aviso «Vence pronto» te recuerda que debes subir uno nuevo antes de esa fecha. ' +
        'Si el documento vence y pasa su plazo de gracia, la tarjeta pasa a «Vencido» y la empresa lo ve como vencido.',
      resaltar: tarjeta(page, SST.nombre),
      variantes: [
        { cuando: 'El documento venció pero aún está dentro del plazo de gracia', entonces: 'La tarjeta dice «En periodo de gracia» y la fecha hasta la que puedes subir uno nuevo.' },
      ],
    });

    // 2. Subir uno nuevo lo deja vigente otra vez.
    await subirDocumento(page, SST.nombre, pdf(`SST nuevo ${tercero.identificacion.numero}`), {
      version: 2,
      campos: { 'Fecha de expedición': fecha(-2) },
    });
    await expect(tarjeta(page, SST.nombre)).toContainText('Versión 2');
    await paso(page, {
      titulo: 'Sube un certificado nuevo',
      explicacion:
        'Sube el certificado nuevo con su fecha de expedición reciente. Queda como la versión 2 y el vencimiento se cuenta desde esa fecha. ' +
        'La empresa lo revisa de nuevo, como cualquier versión nueva.',
      resaltar: tarjeta(page, SST.nombre),
    });
  },
);
