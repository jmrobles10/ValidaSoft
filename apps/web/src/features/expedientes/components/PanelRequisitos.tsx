import { Alert, AlertTitle, Stack } from '@mui/material';
import { SCOPES, type AreaExpedienteDto, type ExpedienteDto } from '@validasoft/api-client';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { VisorDocumento, type DocumentoEnVisor } from '../../documentos/VisorDocumento';
import type { ContextoRevisor, DialogoExpediente } from '../reglas';
import { enFaseDeCarga, requisitosDe, requisitosFaltantes } from '../requisitos';
import { TarjetaRequisito } from './TarjetaRequisito';

/**
 * Documentos que pide el flujo (docs/05 §2.4.2): la fase de carga mientras falten obligatorios y, por documento, su
 * versión vigente, el visor y las decisiones de cada área. No se muestra en un flujo sin requisitos (FL-1).
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
  const areas = useMemo(() => new Map(expediente.etapas.flatMap((e) => e.areas.map((a) => [a.areaId, a] as const))), [expediente.etapas]);
  const requisitos = requisitosDe(expediente);
  const faltantes = requisitosFaltantes(expediente);
  const puedeVer = contexto(null).scopes.has(SCOPES.documentosDescargar);
  if (requisitos.length === 0) return null;

  return (
    <Tarjeta titulo={t('expedientes.requisitos.titulo')}>
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
          />
        ))}
      </Stack>
      {abierto && <VisorDocumento empresaId={expediente.empresaId} vinculoId={expediente.vinculoId} documento={abierto} onCerrar={() => setAbierto(null)} />}
    </Tarjeta>
  );
}
