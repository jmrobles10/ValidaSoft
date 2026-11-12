import { Button, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import { CALIDADES_SOCIEDAD, type CalidadSociedad, type SociedadPep } from '../tipos';
import { FilaDeclaracion, type MensajeDeCampo } from './CamposPep';
import { MAXIMO, MAXIMO_FILAS } from './declaracion';

interface Props {
  filas: readonly SociedadPep[];
  onCambiar: (filas: SociedadPep[]) => void;
  mensaje: MensajeDeCampo;
}

const esCalidad = (valor: string): valor is CalidadSociedad => CALIDADES_SOCIEDAD.some((c) => c === valor);

/** Sociedades, fiducias o patrimonios autónomos: filas repetibles, cada una todo o nada (máximo 30); «Otra» pide «¿Cuál?». */
export function FilasSociedades({ filas, onCambiar, mensaje }: Props) {
  const { t } = useTranslation();
  const cambiarFila = (i: number, cambio: SociedadPep) => onCambiar(filas.map((s, j) => (j === i ? { ...s, ...cambio } : s)));
  const lleno = filas.length >= MAXIMO_FILAS;
  return (
    <Stack spacing={1.5}>
      <Typography component="h3" sx={{ fontWeight: 500 }}>
        {t('ficha.pep.sociedades.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 14, mt: -1 }}>{t('ficha.pep.sociedades.ayuda')}</Typography>
      {mensaje('sociedades') && <Typography sx={{ color: 'error.main', fontSize: 14 }}>{mensaje('sociedades')}</Typography>}
      {filas.length === 0 ? (
        <Typography sx={{ color: 'text.secondary' }}>{t('ficha.pep.sociedades.vacio')}</Typography>
      ) : (
        <Stack component="ul" spacing={1.5} aria-label={t('ficha.pep.sociedades.lista')} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {filas.map((s, i) => {
            const ruta = (campo: string) => mensaje(`sociedades[${String(i)}].${campo}`);
            return (
              <FilaDeclaracion key={`sociedad-${String(i)}`} nombre={t('ficha.pep.sociedades.fila', { n: String(i + 1) })}>
                <Rejilla>
                  <CampoTexto etiqueta={t('ficha.pep.sociedades.nombre')} valor={s.nombre} onCambiar={(v) => cambiarFila(i, { nombre: v })} mensaje={ruta('nombre')} maxLength={MAXIMO.nombre} />
                  <CampoTexto etiqueta={t('ficha.pep.sociedades.identificacion')} valor={s.identificacion} onCambiar={(v) => cambiarFila(i, { identificacion: v })} mensaje={ruta('identificacion')} maxLength={MAXIMO.numero} />
                  <TextField
                    select
                    label={t('ficha.pep.sociedades.calidad')}
                    value={s.calidad ?? ''}
                    onChange={(e) => cambiarFila(i, { calidad: esCalidad(e.target.value) ? e.target.value : null })}
                    error={Boolean(ruta('calidad'))}
                    helperText={ruta('calidad')}
                    fullWidth
                  >
                    {CALIDADES_SOCIEDAD.map((c) => (
                      <MenuItem key={c} value={c}>
                        {t(`ficha.pep.sociedades.calidades.${c}`)}
                      </MenuItem>
                    ))}
                  </TextField>
                  {s.calidad === 'Otra' && <CampoTexto etiqueta={t('ficha.pep.sociedades.calidadOtra')} valor={s.calidad_otra} onCambiar={(v) => cambiarFila(i, { calidad_otra: v })} mensaje={ruta('calidad_otra')} maxLength={MAXIMO.otra} />}
                </Rejilla>
                <Button variant="text" color="inherit" startIcon={<Trash2 size={16} aria-hidden />} onClick={() => onCambiar(filas.filter((_, j) => j !== i))} aria-label={t('ficha.pep.quitarSociedad', { n: String(i + 1) })} sx={{ alignSelf: 'flex-end' }}>
                  {t('ficha.quitar')}
                </Button>
              </FilaDeclaracion>
            );
          })}
        </Stack>
      )}
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...filas, {}])} disabled={lleno}>
          {t('ficha.pep.agregarSociedad')}
        </Button>
        {lleno && <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.sociedades.maximo', { max: String(MAXIMO_FILAS) })}</Typography>}
      </Stack>
    </Stack>
  );
}
