import { Alert, AlertTitle, Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { DocumentoVistaEmpresaDto, RequisitoExpedienteDto, SeccionDeRequisitoDto } from '@validasoft/api-client';
import { Eye, FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { DocumentoEnVisor } from '../../documentos/VisorDocumento';
import { DatosDeSeccion } from '../../terceros/ficha/DatosDeSeccion';
import { esObjeto } from '../../terceros/ficha/formato';
import { SinSoporte } from '../../terceros/ficha/SinSoporte';
import { SeccionFicha } from '../../terceros/SeccionesVinculo';
import { soporteEnVisor } from '../secciones';
import { ComparacionSeccion } from './ComparacionSeccion';
import { CruceSeccion } from './CruceSeccion';

export interface DetalleSeccionRequisitoProps {
  requisito: RequisitoExpedienteDto;
  seccion: SeccionDeRequisitoDto;
  /** Documentos compartidos con el vínculo: un soporte que esté entre ellos se abre en el visor. */
  documentos: readonly DocumentoVistaEmpresaDto[];
  puedeVer: boolean;
  onVer: (documento: DocumentoEnVisor) => void;
  /** Empresa y tercero del expediente: con ellos se descarga el PDF de la declaración PEP de las personas de la sección. */
  empresaId?: string;
  terceroId?: string;
}

/**
 * Lo propio de un requisito de sección (F2): la completitud **para la empresa** con sus motivos legibles (FM-26), en una
 * actualización lo que cambió frente a la versión aceptada (FM-28), el cruce con los documentos que la respaldan (F5), los
 * datos de la versión en solo lectura y sus soportes. Nada se calcula aquí: todo viene del expediente.
 */
export function DetalleSeccionRequisito({ requisito, seccion, documentos, puedeVer, onVer, empresaId, terceroId }: DetalleSeccionRequisitoProps) {
  const { t } = useTranslation();
  const actual = requisito.versionActual?.n ?? null;
  const cubre = actual ?? seccion.versionEvaluada;
  const actualizacion = seccion.versionAceptada !== null && actual !== null && actual !== seccion.versionAceptada;

  return (
    <Stack spacing={1.5} sx={{ mt: 1 }}>
      <Completitud seccion={seccion} actual={actual} />
      {seccion.sinSoporte && <SinSoporte sinSoporte={seccion.sinSoporte} />}
      {actualizacion && seccion.versionAceptada !== null && (
        <SeccionFicha titulo={t('fichaEmpresa.comparacion.titulo', { n: String(seccion.versionAceptada) })}>
          <ComparacionSeccion cambios={seccion.cambios} versionAceptada={seccion.versionAceptada} />
        </SeccionFicha>
      )}
      {seccion.cruce && <CruceSeccion cruce={seccion.cruce} />}
      {esObjeto(seccion.datos) && cubre !== null && (
        <SeccionFicha titulo={t('fichaEmpresa.requisito.datos', { n: String(cubre) })}>
          <DatosDeSeccion datos={seccion.datos} empresaId={empresaId} terceroId={terceroId} seccion={seccion.seccion} version={cubre} />
        </SeccionFicha>
      )}
      {(seccion.soportes.length > 0 || seccion.exigeSoporte) && (
        <SeccionFicha titulo={t('fichaEmpresa.requisito.soportes')} chip={seccion.exigeSoporte ? <Chip size="small" variant="outlined" label={t('fichaEmpresa.requisito.exigeSoporte')} /> : undefined}>
          {seccion.soportes.length === 0 ? (
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('fichaEmpresa.requisito.sinSoportes')}
            </Typography>
          ) : (
            <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
              {seccion.soportes.map((s) => {
                const visor = soporteEnVisor(s, documentos);
                return (
                  <Stack component="li" key={`${s.documentoId}-${String(s.version)}`} direction="row" spacing={1} sx={{ alignItems: 'center', minHeight: 30 }}>
                    <FileText size={16} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
                    <Typography variant="body2" sx={{ flex: 1, minWidth: 0 }}>
                      {visor ? t('fichaEmpresa.requisito.soporte', { tipo: visor.tipo, n: String(s.version) }) : t('fichaEmpresa.requisito.soporteNoCompartido', { n: String(s.version) })}
                    </Typography>
                    {visor && (puedeVer || visor.sinArchivo) && (
                      <Button
                        size="small"
                        variant="text"
                        startIcon={<Eye size={14} aria-hidden />}
                        onClick={() => onVer(visor)}
                        aria-label={t('fichaEmpresa.requisito.verSoporteAccesible', { tipo: visor.tipo, n: String(s.version) })}
                      >
                        {t('fichaEmpresa.requisito.verSoporte')}
                      </Button>
                    )}
                  </Stack>
                );
              })}
            </Stack>
          )}
        </SeccionFicha>
      )}
    </Stack>
  );
}

/** Completa para la empresa, lo que le falta (con su motivo) o que aún no se evalúa (antes de aceptar la invitación). */
function Completitud({ seccion, actual }: { seccion: SeccionDeRequisitoDto; actual: number | null }) {
  const { t } = useTranslation();
  const completitud = seccion.completitud;
  if (!completitud) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('fichaEmpresa.requisito.sinEvaluar')}
      </Typography>
    );
  }
  const evaluada = seccion.versionEvaluada;
  // La versión nueva incompleta no se adjunta: queda la anterior y la completitud dice qué le falta a la nueva (FM-26).
  const otra = evaluada !== null && actual !== null && evaluada !== actual;
  if (completitud.completa) {
    return (
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Chip size="small" color="success" variant="outlined" label={t('fichaEmpresa.requisito.completa')} />
        {evaluada !== null && (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('fichaEmpresa.requisito.evaluada', { n: String(evaluada) })}
          </Typography>
        )}
      </Stack>
    );
  }
  return (
    <Alert severity="warning" aria-label={t('fichaEmpresa.requisito.incompleta', { count: completitud.faltantes.length })}>
      <AlertTitle>{t('fichaEmpresa.requisito.incompleta', { count: completitud.faltantes.length })}</AlertTitle>
      <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
        {completitud.faltantes.map((f, i) => (
          <li key={`${f.campo ?? f.regla ?? ''}-${String(i)}`}>{f.mensaje}</li>
        ))}
      </Box>
      {otra && <Box sx={{ mt: 0.5 }}>{t('fichaEmpresa.requisito.otraIncompleta', { n: String(evaluada), actual: String(actual) })}</Box>}
    </Alert>
  );
}
