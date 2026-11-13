import { Button, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import { PAISES } from '../paises';
import { SelectorUnico } from '../selectores';
import { RELACIONES_PEP, TIPOS_VINCULO_PEP, type PepRelacionada, type TipoVinculoPep } from '../tipos';
import { CampoPeriodo, FilaDeclaracion, type MensajeDeCampo } from './CamposPep';
import { MAXIMO, MAXIMO_FILAS } from './declaracion';

interface Props {
  filas: readonly PepRelacionada[];
  onCambiar: (filas: PepRelacionada[]) => void;
  mensaje: MensajeDeCampo;
  hoy: string;
}

const esTipo = (valor: string): valor is TipoVinculoPep => TIPOS_VINCULO_PEP.some((t) => t === valor);

/** Las PEP con las que el declarante tiene un vínculo familiar o de asociado cercano: filas repetibles y completas (máximo 30). */
export function FilasPepRelacionadas({ filas, onCambiar, mensaje, hoy }: Props) {
  const { t } = useTranslation();
  const cambiarFila = (i: number, cambio: PepRelacionada) => onCambiar(filas.map((f, j) => (j === i ? { ...f, ...cambio } : f)));
  const lleno = filas.length >= MAXIMO_FILAS;
  return (
    <Stack spacing={1.5}>
      <Typography component="h3" sx={{ fontWeight: 500 }}>
        {t('ficha.pep.vinculo.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 14, mt: -1 }}>{t('ficha.pep.vinculo.ayuda')}</Typography>
      {mensaje('vinculo_con_pep.vinculos') && <Typography sx={{ color: 'error.main', fontSize: 14 }}>{mensaje('vinculo_con_pep.vinculos')}</Typography>}
      {filas.length === 0 ? (
        <Typography sx={{ color: 'text.secondary' }}>{t('ficha.pep.vinculo.vacio')}</Typography>
      ) : (
        <Stack component="ul" spacing={1.5} aria-label={t('ficha.pep.vinculo.lista')} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {filas.map((p, i) => {
            const ruta = (campo: string) => mensaje(`vinculo_con_pep.vinculos[${String(i)}].${campo}`);
            const relaciones = p.tipo ? RELACIONES_PEP[p.tipo] : [];
            return (
              <FilaDeclaracion key={`pep-relacionada-${String(i)}`} nombre={t('ficha.pep.vinculo.fila', { n: String(i + 1) })}>
                <Rejilla>
                  <TextField
                    select
                    label={t('ficha.pep.vinculo.tipo')}
                    value={p.tipo ?? ''}
                    onChange={(e) => cambiarFila(i, esTipo(e.target.value) ? { tipo: e.target.value, relacion: null } : {})}
                    error={Boolean(ruta('tipo'))}
                    helperText={ruta('tipo')}
                    fullWidth
                  >
                    {TIPOS_VINCULO_PEP.map((tipo) => (
                      <MenuItem key={tipo} value={tipo}>
                        {t(`ficha.pep.vinculo.tipos.${tipo}`)}
                      </MenuItem>
                    ))}
                  </TextField>
                  <TextField
                    select
                    label={t('ficha.pep.vinculo.relacion')}
                    value={p.relacion ?? ''}
                    onChange={(e) => cambiarFila(i, { relacion: e.target.value || null })}
                    disabled={!p.tipo}
                    error={Boolean(ruta('relacion'))}
                    helperText={ruta('relacion')}
                    fullWidth
                  >
                    {relaciones.map((relacion) => (
                      <MenuItem key={relacion} value={relacion}>
                        {relacion}
                      </MenuItem>
                    ))}
                  </TextField>
                  <CampoTexto etiqueta={t('ficha.pep.vinculo.nombre')} valor={p.nombre} onCambiar={(v) => cambiarFila(i, { nombre: v })} mensaje={ruta('nombre')} maxLength={MAXIMO.nombre} />
                  <CampoTexto etiqueta={t('ficha.pep.vinculo.cargo')} valor={p.cargo} onCambiar={(v) => cambiarFila(i, { cargo: v })} mensaje={ruta('cargo')} maxLength={MAXIMO.cargoPep} />
                  <CampoTexto etiqueta={t('ficha.pep.vinculo.entidad')} valor={p.entidad} onCambiar={(v) => cambiarFila(i, { entidad: v })} mensaje={ruta('entidad')} maxLength={MAXIMO.entidad} />
                  <SelectorUnico etiqueta={t('ficha.pep.vinculo.pais')} opciones={PAISES} valor={p.pais} onCambiar={(v) => cambiarFila(i, { pais: v })} mensaje={ruta('pais')} />
                </Rejilla>
                <CampoPeriodo valor={p} onCambiar={(cambio) => cambiarFila(i, cambio)} mensaje={(campo) => ruta(campo)} hoy={hoy} />
                <Button variant="text" color="inherit" startIcon={<Trash2 size={16} aria-hidden />} onClick={() => onCambiar(filas.filter((_, j) => j !== i))} aria-label={t('ficha.pep.vinculo.quitar', { n: String(i + 1) })} sx={{ alignSelf: 'flex-end' }}>
                  {t('ficha.quitar')}
                </Button>
              </FilaDeclaracion>
            );
          })}
        </Stack>
      )}
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Button variant="outlined" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...filas, {}])} disabled={lleno}>
          {t('ficha.pep.vinculo.agregar')}
        </Button>
        {lleno && <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('ficha.pep.vinculo.maximo', { max: String(MAXIMO_FILAS) })}</Typography>}
      </Stack>
    </Stack>
  );
}
