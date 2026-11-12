import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import { Dato, RejillaDatos } from '@validasoft/ui';
import { FileDown } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { guardarArchivo } from '../../vencimientos/api';
import { useDescargarDeclaracionPep, type DescargaDeclaracionPep } from './descargaDeclaracionPep';
import { esObjeto, formatearValor } from './formato';

type Campos = Record<string, unknown>;

const texto = (d: Campos, clave: string): string | null => (typeof d[clave] === 'string' && d[clave].length > 0 ? d[clave] : null);
const filas = (d: Campos, clave: string): Campos[] => (Array.isArray(d[clave]) ? d[clave].filter(esObjeto) : []);
const RELACIONES = ['Proveedor', 'Socio', 'Otro'] as const;
const esRelacion = (v: unknown): v is (typeof RELACIONES)[number] => RELACIONES.some((r) => r === v);
const CALIDADES = ['Socio', 'Administrador', 'Fideicomitente', 'Otra'] as const;
const esCalidad = (v: unknown): v is (typeof CALIDADES)[number] => CALIDADES.some((c) => c === v);

/** Texto vacío ⇒ guion largo (dato sin diligenciar). */
const o = (v: string) => (v === '' ? '—' : v);
const unir = (partes: (string | null | undefined)[], separador = ' · ') => partes.filter(Boolean).join(separador);

interface Props {
  /** `declaracion_pep` de la persona (o del propio tercero en la General). */
  declaracion: Campos;
  /** Posición de la persona en `personas[]`; `null` en la General. */
  indice: number | null;
  /** Sin esto no se ofrece el PDF (la pantalla no sabe de qué tercero ni versión es). */
  descarga: DescargaDeclaracionPep | null;
}

/**
 * La declaración jurada PEP de una persona en solo lectura (HU 49093): las cuatro secciones del formulario resumidas, quién y
 * cuándo la firmó (con la huella de la imagen) y «Descargar PDF». La imagen de la firma no se muestra aquí: va en el PDF.
 */
export function DeclaracionPep({ declaracion: d, indice, descarga }: Props) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const pdf = useDescargarDeclaracionPep(descarga?.empresaId ?? '');
  const nombre = texto(d, 'nombre') ?? '';
  const relacion = esRelacion(d.relacion_empresa) ? d.relacion_empresa : null;
  const documento = (f: Campos) => unir([texto(f, 'tipo_identificacion'), texto(f, 'numero')], ' ');
  const si = (v: unknown) => (v === true ? t('fichaMaestra.si') : t('fichaMaestra.no'));
  const fecha = (clave: string, v: unknown) => formatearValor(clave, v, t);
  const periodo = (inicio: unknown, fin: unknown, vigente: unknown) =>
    unir([typeof inicio === 'string' ? fecha('fecha', inicio) : null, vigente === true ? t('fichaMaestra.pep.vigente') : typeof fin === 'string' ? fecha('fecha', fin) : null], ' – ');

  const cuentas = d.cuentas_exterior === true ? unir([t('fichaMaestra.si'), formatearValor('paises', d.paises_cuentas_exterior, t)]) : si(d.cuentas_exterior);
  const conyuge = esObjeto(d.conyuge) ? unir([texto(d.conyuge, 'nombre'), documento(d.conyuge)]) : null;
  const vinculo = esObjeto(d.vinculo_con_pep) ? d.vinculo_con_pep : null;
  const vinculoTexto =
    vinculo?.tiene === true
      ? unir([
          t('fichaMaestra.si'),
          texto(vinculo, 'nombre'),
          texto(vinculo, 'parentesco'),
          texto(vinculo, 'cargo_institucion'),
          periodo(vinculo.fecha_vinculacion, vinculo.fecha_desvinculacion, vinculo.vigente),
        ])
      : si(false);
  const firmante =
    d.firma_en_calidad === 'RepresentanteLegal'
      ? t('fichaMaestra.pep.firmaRepresentante', { nombre: texto(d, 'firmante_nombre') ?? '', documento: unir([texto(d, 'firmante_tipo_identificacion'), texto(d, 'firmante_numero')], ' ') })
      : t('fichaMaestra.pep.firmaNombrePropio');

  const descargar = () => {
    if (!descarga) return;
    pdf.mutate(
      { descarga, persona: indice },
      {
        onSuccess: (archivo) => guardarArchivo(archivo, t('fichaMaestra.pep.archivo')),
        onError: (error) => mostrar(`${t('fichaMaestra.pep.errorDescarga')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );
  };

  const lista = (items: Campos[], linea: (f: Campos) => string): ReactNode =>
    items.length === 0 ? (
      '—'
    ) : (
      <Stack component="ul" spacing={0.25} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {items.map((f, i) => (
          <li key={`${linea(f)}-${String(i)}`}>{linea(f)}</li>
        ))}
      </Stack>
    );

  return (
    <Box
      component="section"
      aria-label={t('fichaMaestra.pep.titulo')}
      sx={(th) => ({ mt: 1, p: 1.5, border: 1, borderColor: 'divider', borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2' })}
    >
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', mb: 1 }}>
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography variant="overline" component="h6" sx={{ color: 'text.secondary' }}>
            {t('fichaMaestra.pep.titulo')}
          </Typography>
          {texto(d, 'fecha_firma') && <Chip size="small" color="success" variant="outlined" label={t('fichaMaestra.pep.firmada', { fecha: formatearValor('fecha_firma', d.fecha_firma, t) })} />}
        </Stack>
        {descarga && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<FileDown size={14} aria-hidden />}
            onClick={descargar}
            disabled={pdf.isPending}
            aria-label={t('fichaMaestra.pep.descargarAccesible', { nombre })}
          >
            {t('fichaMaestra.pep.descargar')}
          </Button>
        )}
      </Stack>
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('fichaMaestra.pep.relacion')}>{o(unir([relacion ? t(`fichaMaestra.pep.relaciones.${relacion}`) : null, texto(d, 'relacion_otra')], ': '))}</Dato>
        <Dato etiqueta={t('fichaMaestra.campos.nacionalidad')}>{formatearValor('nacionalidad', d.nacionalidad, t)}</Dato>
        <Dato etiqueta={t('fichaMaestra.campos.direccion')}>{texto(d, 'direccion') ?? '—'}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.cargo')}>{texto(d, 'cargo') ?? '—'}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.institucion')}>{texto(d, 'institucion') ?? '—'}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.pais')}>{formatearValor('pais', d.pais, t)}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.periodo')}>{o(periodo(d.fecha_vinculacion, d.fecha_desvinculacion, d.vigente))}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.cuentas')}>{cuentas}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.conyuge')}>{o(conyuge ?? '')}</Dato>
        <Dato etiqueta={t('fichaMaestra.pep.familiares')} ancho>
          {lista(filas(d, 'familiares'), (f) => unir([texto(f, 'nombre'), texto(f, 'parentesco'), documento(f)]))}
        </Dato>
        <Dato etiqueta={t('fichaMaestra.pep.sociedades')} ancho>
          {lista(filas(d, 'sociedades'), (s) =>
            unir([texto(s, 'nombre'), texto(s, 'identificacion'), s.calidad === 'Otra' ? (texto(s, 'calidad_otra') ?? t('fichaMaestra.pep.calidades.Otra')) : esCalidad(s.calidad) ? t(`fichaMaestra.pep.calidades.${s.calidad}`) : null]),
          )}
        </Dato>
        <Dato etiqueta={t('fichaMaestra.pep.vinculo')} ancho>
          {vinculoTexto}
        </Dato>
        <Dato etiqueta={t('fichaMaestra.pep.firma')} ancho>
          {unir([firmante, texto(d, 'firma_huella') ? t('fichaMaestra.pep.huella', { huella: (texto(d, 'firma_huella') ?? '').slice(0, 12) }) : null])}
        </Dato>
      </RejillaDatos>
    </Box>
  );
}
