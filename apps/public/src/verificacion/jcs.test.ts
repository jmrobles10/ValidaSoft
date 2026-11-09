// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { bytesCanonicos, canonicalizar } from './jcs';

/** Doble IEEE 754 a partir de sus 64 bits en hexadecimal (como los da el apéndice B de RFC 8785). */
function doble(hex: string): number {
  const vista = new DataView(new ArrayBuffer(8));
  vista.setBigUint64(0, BigInt(`0x${hex}`));
  return vista.getFloat64(0);
}

// Los escapes se arman en tiempo de ejecución para que el archivo diga exactamente lo que dice el RFC.
const BARRA = '\\';
/** Escape JSON de una unidad UTF-16 escrito como texto: barra, «u» y cuatro hexadecimales. */
const esc = (hex: string) => `${BARRA}u${hex}`;
/** El carácter de una unidad de código UTF-16. */
const car = (hex: string) => String.fromCharCode(Number.parseInt(hex, 16));

describe('JCS (RFC 8785)', () => {
  it('§3.2.2: ejemplo completo (literales, números y escape de cadenas)', () => {
    // "string": "<20ac>$<000F><000a>A'<0042><0022><005c>\\\"\/" con los escapes del RFC.
    const cadena = `${esc('20ac')}$${esc('000F')}${esc('000a')}A'${esc('0042')}${esc('0022')}${esc('005c')}${BARRA}${BARRA}${BARRA}"${BARRA}/`;
    const entrada = `{
      "numbers": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001],
      "string": "${cadena}",
      "literals": [null, true, false]
    }`;
    // Salida del RFC: "€$<esc 000f>\nA'B\"\\\\\"/"
    const salida = `${car('20ac')}$${esc('000f')}${BARRA}nA'B${BARRA}"${BARRA}${BARRA}${BARRA}${BARRA}${BARRA}"/`;
    expect(canonicalizar(JSON.parse(entrada))).toBe(`{"literals":[null,true,false],"numbers":[333333333.3333333,1e+30,4.5,0.002,1e-27],"string":"${salida}"}`);
  });

  it('§3.2.3: ordena las claves por unidades de código UTF-16, no por punto de código', () => {
    const entrada = `{
      "${esc('20ac')}": "Euro Sign",
      "${esc('000d')}": "Carriage Return",
      "${esc('fb33')}": "Hebrew Letter Dalet With Dagesh",
      "1": "One",
      "${esc('d83d')}${esc('de00')}": "Emoji: Grinning Face",
      "${esc('0080')}": "Control",
      "${esc('00f6')}": "Latin Small Letter O With Diaeresis"
    }`;
    // El emoji (sustitutos D83D DE00) va antes que U+FB33 aunque su punto de código (U+1F600) sea mayor.
    const esperado: [string, string][] = [
      [`${BARRA}r`, 'Carriage Return'],
      ['1', 'One'],
      [car('0080'), 'Control'],
      [car('00f6'), 'Latin Small Letter O With Diaeresis'],
      [car('20ac'), 'Euro Sign'],
      [car('d83d') + car('de00'), 'Emoji: Grinning Face'],
      [car('fb33'), 'Hebrew Letter Dalet With Dagesh'],
    ];
    expect(canonicalizar(JSON.parse(entrada))).toBe(`{${esperado.map(([clave, valor]) => `"${clave}":"${valor}"`).join(',')}}`);
  });

  it('ordena también los objetos anidados y respeta el orden de los arreglos', () => {
    expect(canonicalizar({ b: [3, 1, { z: 1, a: 2 }], a: { y: null, x: true } })).toBe('{"a":{"x":true,"y":null},"b":[3,1,{"a":2,"z":1}]}');
  });

  it('Unicode: lo no ASCII va literal en UTF-8; solo se escapan comillas, barra invertida y controles', () => {
    // U+2028 y U+2029 van literales (JSON.stringify de ES2019 ya no los escapa).
    const texto = `Ñandú € 😀 ${car('2028')}${car('2029')}`;
    expect(canonicalizar(texto)).toBe(`"${texto}"`);
    const controles = `\b\t\n\f\r${car('0001')}${car('001f')}"${BARRA}/`;
    expect(canonicalizar(controles)).toBe(`"${BARRA}b${BARRA}t${BARRA}n${BARRA}f${BARRA}r${esc('0001')}${esc('001f')}${BARRA}"${BARRA}${BARRA}/"`);
    // UTF-8: «é» son 2 bytes, «€» 3 y el emoji 4.
    expect(Array.from(bytesCanonicos('é€😀'))).toEqual([0x22, 0xc3, 0xa9, 0xe2, 0x82, 0xac, 0xf0, 0x9f, 0x98, 0x80, 0x22]);
  });

  it('rechaza sustitutos UTF-16 sueltos (no son Unicode válido)', () => {
    expect(() => canonicalizar(car('d83d'))).toThrow(RangeError);
    expect(() => canonicalizar(`a${car('de00')}`)).toThrow(RangeError);
    expect(() => canonicalizar({ [car('de00')]: 1 })).toThrow(RangeError);
  });

  it('apéndice B: números con la serialización de ECMAScript', () => {
    const vectores: [string, string][] = [
      ['0000000000000000', '0'],
      ['8000000000000000', '0'],
      ['0000000000000001', '5e-324'],
      ['8000000000000001', '-5e-324'],
      ['7fefffffffffffff', '1.7976931348623157e+308'],
      ['ffefffffffffffff', '-1.7976931348623157e+308'],
      ['4340000000000000', '9007199254740992'],
      ['c340000000000000', '-9007199254740992'],
      ['4430000000000000', '295147905179352830000'],
      ['44b52d02c7e14af5', '9.999999999999997e+22'],
      ['44b52d02c7e14af6', '1e+23'],
      ['44b52d02c7e14af7', '1.0000000000000001e+23'],
      ['444b1ae4d6e2ef4e', '999999999999999700000'],
      ['444b1ae4d6e2ef4f', '999999999999999900000'],
      ['444b1ae4d6e2ef50', '1e+21'],
      ['3eb0c6f7a0b5ed8c', '9.999999999999997e-7'],
      ['3eb0c6f7a0b5ed8d', '0.000001'],
      ['41b3de4355555553', '333333333.3333332'],
      ['41b3de4355555554', '333333333.33333325'],
      ['41b3de4355555555', '333333333.3333333'],
      ['41b3de4355555556', '333333333.3333334'],
      ['41b3de4355555557', '333333333.33333343'],
      ['becbf647612f3696', '-0.0000033333333333333333'],
      ['43143ff3c1cb0959', '1424953923781206.2'],
    ];
    for (const [bits, esperado] of vectores) expect(canonicalizar(doble(bits)), bits).toBe(esperado);
  });

  it('rechaza NaN, infinitos y valores que no son JSON', () => {
    expect(() => canonicalizar(doble('7fffffffffffffff'))).toThrow(RangeError);
    expect(() => canonicalizar(doble('7ff0000000000000'))).toThrow(RangeError);
    expect(() => canonicalizar(doble('fff0000000000000'))).toThrow(RangeError);
    expect(() => canonicalizar({ a: undefined })).toThrow(TypeError);
    expect(() => canonicalizar(() => 1)).toThrow(TypeError);
  });
});
