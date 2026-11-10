import { Alert, AlertTitle, Button, Stack } from '@mui/material';
import { SCOPES, type AreaExpedienteDto, type ExpedienteDto } from '@validasoft/api-client';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { claveVersion, hechosPorVersion, useDocumentosVinculo } from '../../documentos/api';
import { VisorDocumento, type DocumentoEnVisor } from '../../documentos/VisorDocumento';
import type { ContextoRevisor, DialogoExpediente } from '../reglas';
import { enFaseDeCarga, requisitosDe, requisitosFaltantes } from '../requisitos';
import { TarjetaRequisito } from './TarjetaRequisito';

/**
 * Documentos (y secciones de la información del tercero, F2) que pide el flujo (docs/05 §2.4.2): la fase de carga mientras falten obligatorios y, por documento, su
 * versión vigente, el visor, la revisión automática y las decisiones de cada área. No se muestra en un flujo sin
 * requisitos (FL-1). Los hechos de cada versión (IA-11) vienen de los documentos del vínculo (`documentos:leer`).
 */
export function PanelRequisitos({
  expediente,
  contexto,
  nombres,
  onAccion,
}: {
  expediente: ExpedienteDto;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  nombres: ReadonlyMap<string, string>;
  onAccion: (dialogo: DialogoExpediente) => void;
}) {
  const { t } = useTranslation();
  const [abierto, setAbierto] = useState<DocumentoEnVisor | null>(null);
  // La decisión principal es por área (PanelDecisionArea); documento por documento queda como acción secundaria.
  const [porDocumento, setPorDocumento] = useState(false);
  const areas = useMemo(() => new Map(expediente.etapas.flatMap((e) => e.areas.map((a) => [a.areaId, a] as const))), [expediente.etapas]);
  const requisitos = requisitosDe(expediente);
  const faltantes = requisitosFaltantes(expediente);
  const puedeVer = contexto(null).scopes.has(SCOPES.documentosDescargar);
  const documentos = useDocumentosVinculo(expediente.empresaId, expediente.vinculoId, contexto(null).scopes.has(SCOPES.documentosLeer));
  const hechos = useMemo(() => hechosPorVersion(documentos.data ?? []), [documentos.data]);
  if (requisitos.length === 0) return null;

  return (
    <Tarjeta
      titulo={t('expedientes.requisitos.titulo')}
      acciones={
        contexto(null).scopes.has(SCOPES.expedientesRevisar) && expediente.estado === 'EnRevision' ? (
          <Button size="small" variant="text" onClick={() => setPorDocumento((v) => !v)} aria-pressed={porDocumento}>
            {porDocumento ? t('expedientes.requisitos.ocultarPorDocumento') : t('expedientes.requisitos.decidirPorDocumento')}
          </Button>
        ) : undefined
      }
    >
      {enFaseDeCarga(expediente) && faltantes.length > 0 && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          <AlertTitle>{t('expedientes.requisitos.faseCargaTitulo')}</AlertTitle>
          {t('expedientes.requisitos.faseCarga', { count: faltantes.length, documentos: faltantes.map((r) => r.tipoDocumento.nombre).join(', ') })}
        </Alert>
      )}
      <Stack component="ul" spacing={1.25} sx={{ p: 0, m: 0 }}>
        {requisitos.map((requisito) => (
          <TarjetaRequisito
            key={requisito.id}
            expediente={expediente}
            requisito={requisito}
            areas={areas}
            contexto={contexto}
            nombres={nombres}
            puedeVer={puedeVer}
            onVer={setAbierto}
            onAccion={onAccion}
            decidirPorDocumento={porDocumento}
            hechos={requisito.versionActual ? hechos.get(claveVersion(requisito.versionActual.documentoId, requisito.versionActual.n)) : null}
            documentos={documentos.data}
          />
        ))}
      </Stack>
      {abierto && <VisorDocumento empresaId={expediente.empresaId} vinculoId={expediente.vinculoId} documento={abierto} onCerrar={() => setAbierto(null)} />}
    </Tarjeta>
  );
}
