import { Alert, AlertTitle, Button, Collapse, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { PropuestaDeSeccionDto, SoporteSeccionDto } from '@validasoft/api-client';
import { FileSearch } from 'lucide-react';
import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePropuestasDeSeccion } from './api';
import { agruparPorDocumento, clavePropuesta, datosDePropuesta, mismoSoporte, soporteDePropuesta, type PropuestasDeDocumento } from './propuestas';
import { TarjetaDeDocumento } from './TarjetaDeDocumento';

interface Props {
  codigo: string;
  periodo: number | null;
  soportes: readonly SoporteSeccionDto[];
  /** Pasa los valores propuestos al formulario (sin guardar). */
  onUsarValores: (datos: Record<string, unknown>) => void;
  onCambiarSoportes: (soportes: SoporteSeccionDto[]) => void;
  deshabilitado: boolean;
}

/**
 * «Actualizar desde mi RUT / Cámara…» (F5, FM-14): en la sección, lo que los documentos más nuevos proponen, en **un solo aviso**
 * con un bloque con borde por documento (UI-6, UI-9). El tercero confirma («Usar estos valores» solo llena el formulario) o corrige a mano, y guarda
 * como siempre: la propuesta nunca se guarda sola y el documento queda como soporte de la versión. Sin lectura automática (sin plan
 * de IA, sin cupo o fallida) no hay valores: se dice en pocas palabras y el tercero diligencia con el documento a la vista.
 */
export function PropuestaDesdeDocumento({ codigo, periodo, soportes, onUsarValores, onCambiarSoportes, deshabilitado }: Props) {
  const { t } = useTranslation();
  const consulta = usePropuestasDeSeccion(codigo, periodo);
  const [descartadas, setDescartadas] = useState<ReadonlySet<string>>(new Set());
  const [aplicadas, setAplicadas] = useState<ReadonlySet<string>>(new Set());
  const marcar = (conjunto: ReadonlySet<string>, clave: string) => new Set([...conjunto, clave]);

  // Si la consulta falla no se interrumpe el formulario: la propuesta es una ayuda, no un requisito.
  const documentos = agruparPorDocumento((consulta.data ?? []).filter((p) => !descartadas.has(clavePropuesta(p))));
  if (documentos.length === 0) return null;

  const agregarSoporte = (propuesta: PropuestaDeSeccionDto) => {
    const soporte = soporteDePropuesta(propuesta);
    if (!soportes.some((s) => mismoSoporte(s, soporte))) onCambiarSoportes([...soportes, soporte]);
  };
  const hayValores = documentos.some((d) => d.propuestas.some((p) => p.disponible && !aplicadas.has(clavePropuesta(p))));

  return (
    <Alert severity="info" icon={<FileSearch size={20} aria-hidden />} aria-label={t('ficha.propuesta.titulo')} sx={{ '& .MuiAlert-message': { minWidth: 0, flex: 1 } }}>
      <AlertTitle>{t('ficha.propuesta.titulo')}</AlertTitle>
      <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, mt: 0.5, listStyle: 'none' }}>
        {documentos.map((documento) => {
          const propuesta = documento.propuestas[0];
          if (!propuesta) return null;
          const clave = clavePropuesta(propuesta);
          return (
            <LineaDeDocumento
              key={documento.clave}
              documento={documento}
              propuesta={propuesta}
              aplicada={aplicadas.has(clave)}
              deshabilitado={deshabilitado}
              onUsar={() => {
                const datos = datosDePropuesta(propuesta);
                if (datos) onUsarValores(datos);
                agregarSoporte(propuesta);
                setAplicadas((previas) => marcar(previas, clave));
              }}
              onSoloSoporte={() => {
                agregarSoporte(propuesta);
                setAplicadas((previas) => marcar(previas, clave));
              }}
              onAhoraNo={() => setDescartadas((previas) => marcar(previas, clave))}
            />
          );
        })}
      </Stack>
      {hayValores && <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 1 }}>{t('ficha.propuesta.nadaSeGuarda')}</Typography>}
    </Alert>
  );
}

interface LineaProps {
  documento: PropuestasDeDocumento;
  propuesta: PropuestaDeSeccionDto;
  aplicada: boolean;
  deshabilitado: boolean;
  onUsar: () => void;
  onSoloSoporte: () => void;
  onAhoraNo: () => void;
}

/** Un bloque por documento: qué dice (en chips), la acción pequeña a la derecha y los cambios que se despliegan debajo, campo por campo. */
function LineaDeDocumento({ documento, propuesta, aplicada, deshabilitado, onUsar, onSoloSoporte, onAhoraNo }: LineaProps) {
  const { t } = useTranslation();
  const [abierta, setAbierta] = useState(false);
  const idTabla = useId();
  const nombre = documento.documento;
  const vacio = t('ficha.propuesta.vacio');

  return (
    <TarjetaDeDocumento
      documento={nombre}
      version={documento.version}
      datosDistintos={propuesta.disponible ? propuesta.campos.length : 0}
      noLeido={!propuesta.disponible}
      acciones={
        aplicada ? null : propuesta.disponible ? (
          <>
            <Button size="small" variant="text" aria-expanded={abierta} aria-controls={idTabla} aria-label={t('ficha.propuesta.verCambiosAccesible', { documento: nombre })} onClick={() => setAbierta((v) => !v)}>
              {abierta ? t('ficha.propuesta.ocultarCambios') : t('ficha.propuesta.verCambios')}
            </Button>
            <Button size="small" variant="contained" aria-label={t('ficha.propuesta.usarAccesible', { documento: nombre })} onClick={onUsar} disabled={deshabilitado}>
              {t('ficha.propuesta.usar')}
            </Button>
            <Button size="small" variant="text" aria-label={t('ficha.propuesta.ahoraNoAccesible', { documento: nombre })} onClick={onAhoraNo}>
              {t('ficha.propuesta.ahoraNo')}
            </Button>
          </>
        ) : (
          <>
            <Button size="small" variant="outlined" aria-label={t('ficha.propuesta.usarSoporteAccesible', { documento: nombre })} onClick={onSoloSoporte} disabled={deshabilitado}>
              {t('ficha.propuesta.usarSoporte')}
            </Button>
            <Button size="small" variant="text" aria-label={t('ficha.propuesta.ahoraNoAccesible', { documento: nombre })} onClick={onAhoraNo}>
              {t('ficha.propuesta.ahoraNo')}
            </Button>
          </>
        )
      }
    >
      {aplicada && (
        <Typography role="status" sx={{ fontSize: 13, color: 'text.secondary' }}>
          {propuesta.disponible ? t('ficha.propuesta.aplicada') : t('ficha.propuesta.soporteAgregado')}
        </Typography>
      )}
      {propuesta.disponible && !aplicada && (
        <Collapse in={abierta} unmountOnExit>
          <TableContainer id={idTabla} sx={{ bgcolor: 'background.paper', border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
            <Table size="small" aria-label={t('ficha.propuesta.tabla', { documento: nombre })}>
              <TableHead>
                <TableRow>
                  <TableCell>{t('ficha.propuesta.dato')}</TableCell>
                  <TableCell>{t('ficha.propuesta.actual')}</TableCell>
                  <TableCell>{t('ficha.propuesta.enElDocumento', { documento: nombre })}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {propuesta.campos.map((campo) => (
                  <TableRow key={campo.campo}>
                    <TableCell sx={{ fontWeight: 500 }}>{campo.etiqueta}</TableCell>
                    <TableCell sx={{ color: 'text.secondary' }}>{campo.actual ?? vacio}</TableCell>
                    <TableCell>{campo.propuesto ?? vacio}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Collapse>
      )}
    </TarjetaDeDocumento>
  );
}
