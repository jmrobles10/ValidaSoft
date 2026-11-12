import { Button, Stack, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import type { FamiliarPep } from '../tipos';
import { CampoTipoDocumento, FilaDeclaracion, type MensajeDeCampo } from './CamposPep';
import { MAXIMO, MAXIMO_FILAS } from './declaracion';

interface Props {
  filas: readonly FamiliarPep[];
  onCambiar: (filas: FamiliarPep[]) => void;
  mensaje: MensajeDeCampo;
}

/** Familiares hasta el 2.° grado: filas repetibles, cada una todo o nada (máximo 30). */
export function FilasFamiliares({ filas, onCambiar, mensaje }: Props) {
  const { t } = useTranslation();
  const cambiarFila = (i: number, cambio: FamiliarPep) => onCambiar(filas.map((f, j) => (j === i ? { ...f, ...cambio } : f)));
  const lleno = filas.length >= MAXIMO_FILAS;
  return (
    <Stack spacing={1.5}>
      <Typography component="h3" sx={{ fontWeight: 500 }}>
        {t('ficha.pep.familiares.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 14, mt: -1 }}>{t('ficha.pep.familiares.ayuda')}</Typography>
      {mensaje('familiares') && <Typography sx={{ color: 'error.main', fontSize: 14 }}>{mensaje('familiares')}</Typography>}
      {filas.length === 0 ? (
        <Typography sx={{ color: 'text.secondary' }}>{t('ficha.pep.familiares.vacio')}</Typography>
      ) : (
        <Stack component="ul" spacing={1.5} aria-label={t('ficha.pep.familiares.lista')} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {filas.map((f, i) => {
            const ruta = (campo: string) => mensaje(`familiares[${String(i)}].${campo}`);
            return (
              <FilaDeclaracion key={`familiar-${String(i)}`} nombre={t('ficha.pep.familiares.fila', { n: String(i + 1) })}>
                <Rejilla>
                  <CampoTexto etiqueta={t('ficha.pep.familiares.nombre')} valor={f.nombre} onCambiar={(v) => cambiarFila(i, { nombre: v })} mensaje={ruta('nombre')} maxLength={MAXIMO.nombre} />
                  <CampoTexto etiqueta={t('ficha.pep.familiares.parentesco')} valor={f.parentesco} onCambiar={(v) => cambiarFila(i, { parentesco: v })} mensaje={ruta('parentesco')} maxLength={MAXIMO.parentesco} />
                  <CampoTipoDocumento etiqueta={t('ficha.pep.familiares.tipoDocumento')} valor={f.tipo_identificacion} onCambiar={(v) => cambiarFila(i, { tipo_identificacion: v })} mensaje={ruta('tipo_identificacion')} />
                  <CampoTexto etiqueta={t('ficha.pep.familiares.numero')} valor={f.numero} onCambiar={(v) => cambiarFila(i, { numero: v })} mensaje={ruta('numero')} maxLength={MAXIMO.numero} />
                </Rejilla>
                <Button variant="text" color="inherit" startIcon={<Trash2 size={16} aria-hidden />} onClick={() => onCambiar(filas.filter((_, j) => j !== i))} aria-label={t('ficha.pep.quitarFamiliar', { n: String(i + 1) })} sx={{ alignSelf: 'flex-end' }}>
                  {t('ficha.quitar')}
                </Button>
              </FilaDeclaracion>
            );
          })}
        </Stack>
      )}
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...filas, {}])} disabled={lleno}>
          {t('ficha.pep.agregarFamiliar')}
        </Button>
        {lleno && <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.familiares.maximo', { max: String(MAXIMO_FILAS) })}</Typography>}
      </Stack>
    </Stack>
  );
}
