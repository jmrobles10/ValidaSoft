import { Alert, Button, Paper, Stack, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoCifra, Subtitulo } from '../campos';
import { CampoCatalogo } from '../selectores';
import type { FilaIca } from '../tipos';

interface Props {
  filas: readonly FilaIca[];
  onCambiar: (filas: FilaIca[]) => void;
  mensaje: (ruta: string) => string | null;
  deshabilitado: boolean;
}

/** Municipios donde declara ICA: municipio (DANE), actividad (CIIU) y tarifa opcional por mil; una tarjeta por fila. */
export function FilasIca({ filas, onCambiar, mensaje, deshabilitado }: Props) {
  const { t } = useTranslation();
  // Llave estable por fila para que quitar una no le pase el texto escrito a la siguiente.
  const [llaves, setLlaves] = useState(() => filas.map((_, i) => i));
  const [siguiente, setSiguiente] = useState(filas.length);
  const llavesActuales = filas.map((_, i) => llaves[i] ?? -1 - i);

  const cambiar = (i: number, cambio: FilaIca) => onCambiar(filas.map((f, j) => (j === i ? { ...f, ...cambio } : f)));
  const agregar = () => {
    setLlaves([...llavesActuales, siguiente]);
    setSiguiente(siguiente + 1);
    onCambiar([...filas, {}]);
  };
  const quitar = (i: number) => {
    setLlaves(llavesActuales.filter((_, j) => j !== i));
    onCambiar(filas.filter((_, j) => j !== i));
  };

  return (
    <Stack spacing={1.5}>
      <Subtitulo>{t('ficha.tributaria.icaTitulo')}</Subtitulo>
      {mensaje('ica') && <Alert severity="error">{mensaje('ica')}</Alert>}
      {filas.map((fila, i) => {
        const ruta = `ica[${String(i)}]`;
        const n = String(i + 1);
        return (
          <Paper key={llavesActuales[i]} variant="outlined" component="section" aria-label={t('ficha.tributaria.filaIca', { n })} sx={{ p: 1.5 }}>
            <Stack spacing={1.5}>
              <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
                <Typography sx={{ fontWeight: 500 }}>{t('ficha.tributaria.filaIca', { n })}</Typography>
                <Button
                  variant="text"
                  color="inherit"
                  startIcon={<Trash2 size={16} aria-hidden />}
                  onClick={() => quitar(i)}
                  disabled={deshabilitado}
                  aria-label={t('ficha.tributaria.quitarIca', { n })}
                >
                  {t('ficha.quitar')}
                </Button>
              </Stack>
              {mensaje(ruta) && <Alert severity="error">{mensaje(ruta)}</Alert>}
              <CampoCatalogo catalogo="ciudades" etiqueta={t('ficha.tributaria.municipio')} valor={fila.municipio} onCambiar={(municipio) => cambiar(i, { municipio })} mensaje={mensaje(`${ruta}.municipio`)} deshabilitado={deshabilitado} />
              <CampoCatalogo catalogo="actividades-economicas" etiqueta={t('ficha.tributaria.ciiu')} valor={fila.ciiu} onCambiar={(ciiu) => cambiar(i, { ciiu })} mensaje={mensaje(`${ruta}.ciiu`)} deshabilitado={deshabilitado} />
              <CampoCifra etiqueta={t('ficha.tributaria.tarifa')} valor={fila.tarifa_por_mil} onCambiar={(tarifa) => cambiar(i, { tarifa_por_mil: tarifa })} mensaje={mensaje(`${ruta}.tarifa_por_mil`)} deshabilitado={deshabilitado} />
            </Stack>
          </Paper>
        );
      })}
      <Button variant="outlined" startIcon={<Plus size={18} aria-hidden />} onClick={agregar} disabled={deshabilitado} sx={{ alignSelf: { xs: 'stretch', sm: 'flex-start' } }}>
        {t('ficha.tributaria.agregarIca')}
      </Button>
    </Stack>
  );
}
