import { Alert, Box, Button, Chip, Stack, Typography } from '@mui/material';
import { SCOPES, type AreaDeRequisitoDto, type AreaExpedienteDto, type DocumentoVistaEmpresaDto, type ExpedienteDto, type HechosDto, type RequisitoExpedienteDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { ClipboardList, Clock, FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { hashCorto } from '../../documentos/api';
import { RevisionesAutomaticasDelRequisito } from '../../revisionAutomatica/components/RevisionesAutomaticasDelRequisito';
import { etiquetaDeRuta } from '../../terceros/ficha/secciones';
import { ResumenVersion } from '../../documentos/ResumenVersion';
import type { DocumentoEnVisor } from '../../documentos/VisorDocumento';
import type { ContextoRevisor, DialogoExpediente } from '../reglas';
import {
  bloqueoRequisito,
  documentoEnVisor,
  puedeEximir,
  veredictosInvalidados,
  veredictosRevertidos,
  veredictosVigentesDeRequisito,
  type BloqueoRequisito,
} from '../requisitos';
import { BotonVerVersion } from './BotonVerVersion';
import { DetalleSeccionRequisito } from './DetalleSeccionRequisito';
import { SoporteDeSecciones } from './SoporteDeSecciones';

/** Bloqueos que se explican a quien revisa (los demás no dicen nada útil: sin permiso, cerrado). */
const BLOQUEOS_EXPLICADOS = ['expediente-en-carga', 'expediente-observado', 'vinculo-bloqueado', 'sin-version', 'area-esperando', 'no-es-miembro', 'ya-voto'] as const;
type BloqueoExplicado = (typeof BLOQUEOS_EXPLICADOS)[number];

function seExplica(bloqueo: BloqueoRequisito | null): bloqueo is BloqueoExplicado {
  return (BLOQUEOS_EXPLICADOS as readonly string[]).includes(bloqueo ?? '');
}

export interface TarjetaRequisitoProps {
  expediente: ExpedienteDto;
  requisito: RequisitoExpedienteDto;
  areas: ReadonlyMap<string, AreaExpedienteDto>;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  nombres: ReadonlyMap<string, string>;
  puedeVer: boolean;
  onVer: (documento: DocumentoEnVisor) => void;
  onAccion: (dialogo: DialogoExpediente) => void;
  /** Muestra «Aprobar u observar» por documento (acción secundaria; la principal es la decisión del área). */
  decidirPorDocumento?: boolean;
  /** Hechos de la versión vigente (de los documentos del vínculo): la revisión automática (IA-11). */
  hechos?: HechosDto | null;
  /** Documentos compartidos con el vínculo: abren en el visor los soportes de una sección. */
  documentos?: readonly DocumentoVistaEmpresaDto[];
}

/**
 * Un documento (o sección de la información del tercero, F2) que pide el flujo: estado, versión vigente (con «Ver» en el visor si trae archivo), su vigencia, los
 * datos que declaró el tercero, la revisión automática y, por cada área que lo revisa, su
 * estado, sus veredictos sobre la versión y «Aprobar u observar» cuando le toca a quien mira (RQ-4). Una sección muestra
 * en su lugar la completitud para la empresa, los cambios, sus datos y sus soportes (DetalleSeccionRequisito).
 */
export function TarjetaRequisito({ expediente, requisito, areas, contexto, nombres, puedeVer, onVer, onAccion, decidirPorDocumento = false, hechos, documentos = [] }: TarjetaRequisitoProps) {
  const { t } = useTranslation();
  const version = requisito.versionActual;
  const seccion = requisito.seccion ?? null;
  const visor = seccion ? null : documentoEnVisor(requisito, hechos);
  const Icono = seccion ? ClipboardList : FileText;
  const documento = requisito.tipoDocumento.nombre;
  const eximir = puedeEximir(expediente, requisito, contexto(null).scopes);

  return (
    <Box component="li" sx={{ listStyle: 'none', border: 1, borderColor: requisito.estado === 'Observado' ? 'estado.advertencia.borde' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5 }}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'flex-start', flexWrap: 'wrap' }}>
        <Icono size={18} strokeWidth={1.75} aria-hidden style={{ flex: 'none', marginTop: 2, opacity: 0.7 }} />
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px' }}>
            {documento}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {requisito.obligatorio ? t('expedientes.requisitos.obligatorio') : t('expedientes.requisitos.opcional')}
            {version && (
              <>
                {' · '}
                {t('expedientes.requisitos.version', { n: String(version.n), fecha: formatearFecha(version.cargadaEn) })}
                {visor?.sinArchivo && ` · ${t('expedientes.datosDeclarados.formulario')}`}
                {' · '}
                <Typography component="span" variant="mono" title={version.sha256} sx={{ fontSize: 12 }}>
                  {t('expedientes.requisitos.hash', { hash: hashCorto(version.sha256) })}
                </Typography>
              </>
            )}
          </Typography>
        </Box>
        <ChipEstado estado={requisito.estado} />
        {/* Una versión de solo formulario no tiene archivo que ver: sus datos van abajo, en la misma tarjeta. */}
        {visor && !visor.sinArchivo && <BotonVerVersion documento={visor} puedeVerArchivo={puedeVer} onVer={onVer} />}
        {eximir && (
          <Button size="small" variant="text" onClick={() => onAccion({ tipo: 'eximir', requisito })} sx={{ color: 'text.secondary' }}>
            {t('expedientes.requisitos.eximirAccion')}
          </Button>
        )}
      </Stack>

      {/* El tercero retiró la versión que este documento reemplaza: por qué cambió la versión y qué hacer (RT-3). */}
      {requisito.retiro && version && (
        <Alert severity="warning" sx={{ mt: 1 }}>
          {t('expedientes.requisitos.retiro', { retirada: String(requisito.retiro.numero), motivo: requisito.retiro.motivo, n: String(version.n) })}
        </Alert>
      )}
      {visor && (
        <Box sx={{ mt: 1 }}>
          <ResumenVersion datos={visor.datosDeclarados ?? []} vigencia={visor.vigencia} limite={6} hechos={visor.sinArchivo ? null : visor.hechos} tipoEsperado={visor.tipoEsperado} />
        </Box>
      )}
      {/* Lo que decidió la revisión automática en cada área (ADR-0012): sugerencia, aprobación del sistema o pase a una persona. */}
      {!seccion && <RevisionesAutomaticasDelRequisito expediente={expediente} requisito={requisito} areas={areas} contexto={contexto} nombres={nombres} />}
      {seccion && <DetalleSeccionRequisito requisito={requisito} seccion={seccion} documentos={documentos} puedeVer={puedeVer} onVer={onVer} empresaId={expediente.empresaId} terceroId={expediente.tercero.id} />}
      {/* Un documento que respalda secciones de la ficha («Usar como soporte», F5): qué respalda y el resultado de su cruce. */}
      {!seccion && requisito.soporteDeSecciones && (
        <Box sx={{ mt: 1 }}>
          <SoporteDeSecciones soporte={requisito.soporteDeSecciones} />
        </Box>
      )}
      {!seccion && !version && requisito.estado === 'Pendiente' && (
        <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.sinVersion')}
        </Typography>
      )}
      {requisito.reenvios > 0 && (
        <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.versionesNuevas', { count: requisito.reenvios })}
        </Typography>
      )}
      {requisito.exencion && (
        <Typography sx={{ fontSize: 13, mt: 1 }}>
          {t('expedientes.requisitos.eximido', {
            persona: nombres.get(requisito.exencion.por) ?? t('expedientes.requisitos.persona'),
            fecha: formatearFecha(requisito.exencion.fecha),
            justificacion: requisito.exencion.justificacion,
          })}
        </Typography>
      )}

      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, mt: 1.25 }}>
        {requisito.areas.map((areaRequisito) => (
          <AreaDelRequisito
            key={areaRequisito.areaId}
            expediente={expediente}
            requisito={requisito}
            areaRequisito={areaRequisito}
            area={areas.get(areaRequisito.areaId) ?? null}
            contexto={contexto}
            nombres={nombres}
            onAccion={onAccion}
            decidirPorDocumento={decidirPorDocumento}
          />
        ))}
      </Stack>
    </Box>
  );
}

function AreaDelRequisito({
  expediente,
  requisito,
  areaRequisito,
  area,
  contexto,
  nombres,
  onAccion,
  decidirPorDocumento,
}: Pick<TarjetaRequisitoProps, 'expediente' | 'requisito' | 'contexto' | 'nombres' | 'onAccion' | 'decidirPorDocumento'> & {
  areaRequisito: AreaDeRequisitoDto;
  area: AreaExpedienteDto | null;
}) {
  const { t } = useTranslation();
  const ctx = contexto(area);
  const nombreArea = area?.nombre ?? areaRequisito.codigo;
  const bloqueo = bloqueoRequisito(expediente, requisito, areaRequisito, ctx);
  const explicar = decidirPorDocumento && seExplica(bloqueo) && ctx.scopes.has(SCOPES.expedientesRevisar) ? bloqueo : null;
  const nombre = (id: string | null | undefined) =>
    id && id === ctx.usuarioId ? t('expedientes.veredictos.tu') : ((id && nombres.get(id)) ?? t('expedientes.requisitos.persona'));
  // Quién decidió: el sistema (área automática o aprobación que la empresa ya dio en otro expediente, RQ-11), el
  // administrador en nombre del área o la persona. Antes un veredicto del sistema aparecía como «Persona del área».
  const autor = (v: { origen: string; usuario?: string | null }) =>
    v.origen === 'Sistema'
      ? t('expedientes.veredictos.sistema')
      : !v.usuario && v.origen === 'Administrador'
        ? t('expedientes.veredictos.administrador')
        : nombre(v.usuario);

  return (
    <Box component="li" sx={{ listStyle: 'none', pl: 1.5, borderLeft: 2, borderColor: 'divider' }}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography sx={{ fontWeight: 500, fontSize: 13, flex: '1 1 auto' }}>{nombreArea}</Typography>
        {areaRequisito.estado === 'Esperando' ? (
          <Chip size="small" variant="outlined" icon={<Clock size={14} aria-hidden />} label={t('expedientes.requisitos.areaEsperando')} sx={{ color: 'text.secondary' }} />
        ) : (
          <ChipEstado estado={areaRequisito.estado} />
        )}
        <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary' }}>
          {t('bandeja.aprobaciones', { aprobaciones: String(areaRequisito.aprobaciones), requeridas: String(areaRequisito.aprobadoresRequeridos) })}
        </Typography>
        {bloqueo === null && decidirPorDocumento && (
          <Button
            size="small"
            variant="contained"
            onClick={() => onAccion({ tipo: 'veredictoRequisito', requisito, areaRequisito, nombreArea })}
            aria-label={t('expedientes.requisitos.decidirAccesible', { documento: requisito.tipoDocumento.nombre, area: nombreArea })}
          >
            {t('expedientes.requisitos.decidir')}
          </Button>
        )}
      </Stack>
      {veredictosVigentesDeRequisito(areaRequisito).map((v) => (
        <Typography key={v.id} sx={{ fontSize: 13, mt: 0.5 }}>
          {autor(v)} · {t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()} · {t('expedientes.requisitos.sobreVersion', { n: String(v.sobreVersion ?? '—') })}
          {v.observacion ? ` · “${v.observacion}”` : ''}
          {v.justificacion ? ` · ${v.justificacion}` : ''}
          {v.campos?.length ? ` · ${t('fichaEmpresa.requisito.campos', { campos: v.campos.map((c) => etiquetaDeRuta(c, t)).join(', ') })}` : ''}
        </Typography>
      ))}
      {veredictosInvalidados(areaRequisito).map((v) => (
        <Typography key={v.id} sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5, textDecoration: 'line-through' }}>
          {autor(v)} · {t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()} · {t('expedientes.requisitos.invalidado', { n: String(v.sobreVersion ?? '—') })}
        </Typography>
      ))}
      {veredictosRevertidos(areaRequisito).map((v) => (
        <Typography key={v.id} sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5, textDecoration: 'line-through' }}>
          {autor(v)} · {t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()} · {t('expedientes.requisitos.revertido')}
        </Typography>
      ))}
      {explicar && (
        <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>
          {t(`expedientes.requisitos.bloqueos.${explicar}`)}
        </Typography>
      )}
    </Box>
  );
}
