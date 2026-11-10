import { Stack } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useCatalogoCompleto } from '../api';
import { CampoSiNo, CampoTexto, Rejilla, Subtitulo } from '../campos';
import { SelectorMultiple } from '../selectores';
import type { DatosTributarios, PropsFormulario, Resolucion } from '../tipos';
import { FilasIca } from './FilasIca';

const GRAN_CONTRIBUYENTE = '13';
const AUTORRETENEDOR = '15';

/**
 * Información tributaria (`FICHA_TRIBUTARIA`): responsabilidades de la casilla 53 del RUT; las resoluciones de gran
 * contribuyente (13) y autorretenedor (15) aparecen solo si aplican; ICA es municipal y va aparte (FM-19).
 */
export function FormularioTributaria({ datos, onCambiar, mensaje, deshabilitado }: PropsFormulario<DatosTributarios>) {
  const { t } = useTranslation();
  const catalogo = useCatalogoCompleto('responsabilidades-dian');
  const responsabilidades = datos.responsabilidades ?? [];

  const resolucion = (clave: 'resolucion_gran_contribuyente' | 'resolucion_autorretenedor', titulo: string) => {
    const actual: Resolucion = datos[clave] ?? {};
    const cambiar = (cambio: Resolucion) => {
      const nueva = { ...actual, ...cambio };
      const valor = nueva.numero || nueva.fecha ? nueva : null;
      onCambiar(clave === 'resolucion_gran_contribuyente' ? { resolucion_gran_contribuyente: valor } : { resolucion_autorretenedor: valor });
    };
    return (
      <Stack spacing={1.5}>
        <Subtitulo>{titulo}</Subtitulo>
        <Rejilla>
          <CampoTexto etiqueta={t('ficha.tributaria.numeroResolucion')} valor={actual.numero} onCambiar={(numero) => cambiar({ numero })} mensaje={mensaje(`${clave}.numero`)} maxLength={50} deshabilitado={deshabilitado} />
          <CampoTexto etiqueta={t('ficha.tributaria.fechaResolucion')} valor={actual.fecha} onCambiar={(fecha) => cambiar({ fecha })} mensaje={mensaje(`${clave}.fecha`)} tipo="date" deshabilitado={deshabilitado} />
        </Rejilla>
      </Stack>
    );
  };

  return (
    <Stack spacing={2.5}>
      <SelectorMultiple
        etiqueta={t('ficha.tributaria.responsabilidades')}
        ayuda={t('ficha.tributaria.responsabilidadesAyuda')}
        opciones={catalogo.data ?? []}
        cargando={catalogo.isPending}
        valor={datos.responsabilidades}
        onCambiar={(lista) => onCambiar({ responsabilidades: lista })}
        mensaje={mensaje('responsabilidades') ?? responsabilidades.map((_, i) => mensaje(`responsabilidades[${String(i)}]`)).find(Boolean) ?? null}
        deshabilitado={deshabilitado}
      />
      {(responsabilidades.includes(GRAN_CONTRIBUYENTE) || datos.resolucion_gran_contribuyente) &&
        resolucion('resolucion_gran_contribuyente', t('ficha.tributaria.granContribuyente'))}
      {(responsabilidades.includes(AUTORRETENEDOR) || datos.resolucion_autorretenedor) && resolucion('resolucion_autorretenedor', t('ficha.tributaria.autorretenedor'))}
      <CampoSiNo etiqueta={t('ficha.tributaria.responsableIca')} valor={datos.responsable_ica} onCambiar={(v) => onCambiar({ responsable_ica: v })} mensaje={mensaje('responsable_ica')} deshabilitado={deshabilitado} />
      <CampoSiNo etiqueta={t('ficha.tributaria.autorretenedorIca')} valor={datos.autorretenedor_ica} onCambiar={(v) => onCambiar({ autorretenedor_ica: v })} mensaje={mensaje('autorretenedor_ica')} deshabilitado={deshabilitado} />
      {(datos.responsable_ica === true || (datos.ica ?? []).length > 0) && (
        <FilasIca filas={datos.ica ?? []} onCambiar={(ica) => onCambiar({ ica: ica.length > 0 ? ica : null })} mensaje={mensaje} deshabilitado={deshabilitado} />
      )}
    </Stack>
  );
}
