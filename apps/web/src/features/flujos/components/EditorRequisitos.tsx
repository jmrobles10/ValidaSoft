import { Box, Button, FormControlLabel, IconButton, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material';
import type { AreaDto, TipoDocumentoTenantDto } from '@validasoft/api-client';
import { AlineadoConCampo } from '@validasoft/ui';
import { ClipboardList, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { fichaEmpresa } from '../../../i18n/es-CO-ficha-empresa';
import { resumenTipo } from '../../configuracion/tiposDocumento/resumen';
import { nombreSeccion, seccionFicha } from '../../terceros/ficha/secciones';
import { atajoDe, condicionDe, describirCondicion, type AtajoCondicion } from '../condiciones';
import { MAXIMO_APROBADORES, requisitoDeSeccionNuevo, requisitoNuevo, sePuedePedir, type ErrorFlujo, type EtapaEnEditor, type RequisitoEnEditor } from '../editor';
import { seccionesQueRespalda } from '../requisitosDeSeccion';
import { OpcionesSeccion, SelectorSeccion } from './CamposRequisitoSeccion';
import { SelectorAreasRequisito } from './SelectorAreasRequisito';

export interface EditorRequisitosProps {
  requisitos: RequisitoEnEditor[];
  etapas: readonly EtapaEnEditor[];
  areas: readonly AreaDto[];
  tiposDocumento: readonly TipoDocumentoTenantDto[];
  onCambiar: (requisitos: RequisitoEnEditor[]) => void;
  errores: readonly ErrorFlujo[];
  soloLectura: boolean;
}

type CodigoErrorSeccion = keyof typeof fichaEmpresa.editor.errores;
const esErrorDeSeccion = (codigo: string): codigo is CodigoErrorSeccion => codigo in fichaEmpresa.editor.errores;

/**
 * Lo que pide el flujo (RQ-1): documentos o secciones de la información del tercero (F2, FM-25). Cada uno con
 * obligatorio, qué áreas lo revisan (ninguna elegida = todas las del flujo), cuántas personas deben aprobarlo (vacío =
 * las del área) y a quién se le pide (RQ-12: todos, solo personas jurídicas o naturales; una condición armada por la
 * API se describe y se conserva). Una sección suma periodos (la financiera) y «Exige soporte». Sin requisitos, el flujo
 * decide por área (FL-1).
 */
export function EditorRequisitos({ requisitos, etapas, areas, tiposDocumento, onCambiar, errores, soloLectura }: EditorRequisitosProps) {
  const { t } = useTranslation();
  const areasDelFlujo = etapas.flatMap((e) => e.areas.map((a) => a.areaId)).filter(Boolean);
  const nombreArea = (id: string) => areas.find((a) => a.id === id)?.nombre ?? id;
  const elegibles = tiposDocumento.filter(sePuedePedir);
  const cambiar = (k: number, cambio: Partial<RequisitoEnEditor>) => onCambiar(requisitos.map((r, i) => (i === k ? { ...r, ...cambio } : r)));
  const error = (campo: string) => errores.find((e) => e.campo === campo);
  const textoError = (e: ErrorFlujo, esSeccion: boolean) =>
    esSeccion && esErrorDeSeccion(e.codigo) ? t(`fichaEmpresa.editor.errores.${e.codigo}`) : t(`flujos.editor.errores.${e.codigo}`);

  return (
    <Stack spacing={1.5}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
        {requisitos.length === 0 ? t('flujos.editor.requisitos.vacio') : t('flujos.editor.requisitos.ayuda')}
      </Typography>
      {requisitos.map((r, k) => {
        const e = error(`requisito.${String(k)}`);
        const ePeriodos = error(`requisito.${String(k)}.periodos`);
        const eVigencia = error(`requisito.${String(k)}.vigencia`);
        const eRevalidacion = error(`requisito.${String(k)}.revalidacion`);
        const esSeccion = r.seccion !== null;
        const tipo = esSeccion ? undefined : tiposDocumento.find((x) => x.id === r.tipoDocumentoTenantId);
        const seccion = seccionFicha(r.seccion);
        const nombre = esSeccion
          ? seccion ? nombreSeccion(seccion.codigo, t) : t('fichaEmpresa.editor.nueva')
          : (tipo?.nombre ?? t('flujos.editor.requisitos.nuevo'));
        const quitar = esSeccion ? t('fichaEmpresa.editor.quitar') : t('flujos.editor.requisitos.quitar');
        return (
          <Box
            key={r.clave}
            component="section"
            aria-label={nombre}
            sx={{ border: 1, borderColor: e || ePeriodos || eVigencia || eRevalidacion ? 'error.main' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}
          >
            <Box
              sx={{
                display: 'grid',
                gridTemplateColumns: {
                  xs: '1fr',
                  sm: 'minmax(180px,1fr) minmax(180px,1fr)',
                  lg: 'minmax(180px,1.2fr) minmax(180px,1.2fr) minmax(170px,1fr) 120px auto',
                },
                columnGap: 1.5,
                rowGap: 1,
                alignItems: 'start',
              }}
            >
              {esSeccion ? (
                <SelectorSeccion requisito={r} onCambiar={(cambio) => cambiar(k, cambio)} deshabilitado={soloLectura} />
              ) : (
                <TextField
                  select
                  size="small"
                  label={t('flujos.editor.requisitos.tipo')}
                  value={r.tipoDocumentoTenantId}
                  onChange={(ev) => cambiar(k, { tipoDocumentoTenantId: ev.target.value })}
                  disabled={soloLectura}
                >
                  {(tipo && !elegibles.includes(tipo) ? [tipo, ...elegibles] : elegibles).map((x) => (
                    <MenuItem key={x.id} value={x.id}>
                      {x.nombre}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              <SelectorAreasRequisito
                areasDelFlujo={areasDelFlujo}
                elegidas={r.areasQueRevisan}
                nombreArea={nombreArea}
                onCambiar={(areasQueRevisan) => cambiar(k, { areasQueRevisan })}
                deshabilitado={soloLectura}
              />
              <TextField
                select
                size="small"
                label={t('flujos.editor.requisitos.sePideA')}
                value={atajoDe(r.condicion)}
                onChange={(ev) => cambiar(k, { condicion: condicionDe(ev.target.value as AtajoCondicion, r.condicion) })}
                disabled={soloLectura}
              >
                {(['siempre', 'juridica', 'natural'] as const).map((a) => (
                  <MenuItem key={a} value={a}>
                    {t(`flujos.editor.requisitos.atajos.${a}`)}
                  </MenuItem>
                ))}
                {atajoDe(r.condicion) === 'personalizada' && <MenuItem value="personalizada">{t('flujos.editor.requisitos.atajos.personalizada')}</MenuItem>}
              </TextField>
              <TextField
                size="small"
                type="number"
                label={t('flujos.editor.requisitos.aprobadores')}
                value={r.aprobadoresRequeridos ?? ''}
                placeholder={t('flujos.editor.requisitos.losDelArea')}
                onChange={(ev) => cambiar(k, { aprobadoresRequeridos: ev.target.value === '' ? null : Number(ev.target.value) })}
                slotProps={{ htmlInput: { min: 1, max: MAXIMO_APROBADORES } }}
                disabled={soloLectura}
              />
              <AlineadoConCampo desde="sm">
                <FormControlLabel
                  control={<Switch checked={r.obligatorio} onChange={(ev) => cambiar(k, { obligatorio: ev.target.checked })} disabled={soloLectura} />}
                  label={t('flujos.editor.requisitos.obligatorio')}
                  sx={{ mx: 0, flex: 'none' }}
                />
                {!soloLectura && (
                  <Tooltip title={quitar}>
                    <IconButton size="small" aria-label={quitar} onClick={() => onCambiar(requisitos.filter((_, i) => i !== k))} sx={{ ml: 'auto' }}>
                      <Trash2 size={16} strokeWidth={1.75} />
                    </IconButton>
                  </Tooltip>
                )}
              </AlineadoConCampo>
            </Box>
            {esSeccion && (
              <OpcionesSeccion
                requisito={r}
                onCambiar={(cambio) => cambiar(k, cambio)}
                deshabilitado={soloLectura}
                errorPeriodos={ePeriodos ? textoError(ePeriodos, true) : null}
                errorVigencia={eVigencia ? textoError(eVigencia, true) : null}
                errorRevalidacion={eRevalidacion ? textoError(eRevalidacion, true) : null}
              />
            )}
            {tipo && seccionesQueRespalda(tipo.tipoCanonico).length > 0 && (
              <Box sx={{ mt: 0.5 }}>
                <FormControlLabel
                  control={<Switch checked={r.usarComoSoporte !== false} onChange={(ev) => cambiar(k, { usarComoSoporte: ev.target.checked })} disabled={soloLectura} />}
                  label={t('cruce.editor.usarComoSoporte')}
                  sx={{ mx: 0 }}
                />
                <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12 }}>
                  {t('cruce.editor.ayuda', { secciones: seccionesQueRespalda(tipo.tipoCanonico).map((c) => nombreSeccion(c, t)).join(', ') })}
                </Typography>
              </Box>
            )}
            {tipo && (
              <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12, mt: 0.75 }} data-resumen-tipo="">
                {resumenTipo(tipo, t)}
              </Typography>
            )}
            {r.condicion && atajoDe(r.condicion) === 'personalizada' && (
              <Typography variant="body2" sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>
                {t('flujos.editor.requisitos.personalizada', { condicion: describirCondicion(r.condicion, t) })}
              </Typography>
            )}
            {e && (
              <Typography color="error" sx={{ fontSize: 12, mt: 1 }}>
                {textoError(e, esSeccion)}
              </Typography>
            )}
          </Box>
        );
      })}
      {!soloLectura && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
          <Button variant="outlined" size="small" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...requisitos, requisitoNuevo()])}>
            {t('flujos.editor.requisitos.agregar')}
          </Button>
          <Button variant="outlined" size="small" startIcon={<ClipboardList size={16} aria-hidden />} onClick={() => onCambiar([...requisitos, requisitoDeSeccionNuevo()])}>
            {t('fichaEmpresa.editor.agregarSeccion')}
          </Button>
        </Stack>
      )}
    </Stack>
  );
}
