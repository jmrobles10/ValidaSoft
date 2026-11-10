import { Alert, AlertTitle, Box, Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import type { PropuestaDeSeccionDto, SoporteSeccionDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { FileSearch } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePropuestasDeSeccion } from './api';
import { clavePropuesta, datosDePropuesta, mismoSoporte, nombreCortoDelDocumento, soporteDePropuesta } from './propuestas';

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
 * «Actualizar desde mi RUT / Cámara…» (F5, FM-14): en la sección, lo que un documento más nuevo propone campo por campo. El tercero
 * lo confirma («Usar estos valores» solo llena el formulario) o lo corrige a mano, y guarda como siempre: la propuesta nunca se
 * guarda sola y el documento queda como soporte de la versión. Sin lectura automática (sin plan de IA, sin cupo o fallida) no hay
 * valores: se avisa y el tercero diligencia con el documento a la vista.
 */
export function PropuestaDesdeDocumento({ codigo, periodo, soportes, onUsarValores, onCambiarSoportes, deshabilitado }: Props) {
  const consulta = usePropuestasDeSeccion(codigo, periodo);
  const [descartadas, setDescartadas] = useState<ReadonlySet<string>>(new Set());
  const [aplicadas, setAplicadas] = useState<ReadonlySet<string>>(new Set());
  const marcar = (conjunto: ReadonlySet<string>, clave: string) => new Set([...conjunto, clave]);

  // Si la consulta falla no se interrumpe el formulario: la propuesta es una ayuda, no un requisito.
  const propuestas = (consulta.data ?? []).filter((p) => !descartadas.has(clavePropuesta(p)));
  if (propuestas.length === 0) return null;

  const agregarSoporte = (propuesta: PropuestaDeSeccionDto) => {
    const soporte = soporteDePropuesta(propuesta);
    if (!soportes.some((s) => mismoSoporte(s, soporte))) onCambiarSoportes([...soportes, soporte]);
  };

  return (
    <Stack spacing={1.5}>
      {propuestas.map((propuesta) => {
        const clave = clavePropuesta(propuesta);
        return (
          <TarjetaDePropuesta
            key={clave}
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
  );
}

interface TarjetaProps {
  propuesta: PropuestaDeSeccionDto;
  aplicada: boolean;
  deshabilitado: boolean;
  onUsar: () => void;
  onSoloSoporte: () => void;
  onAhoraNo: () => void;
}

function TarjetaDePropuesta({ propuesta, aplicada, deshabilitado, onUsar, onSoloSoporte, onAhoraNo }: TarjetaProps) {
  const { t } = useTranslation();
  const documento = nombreCortoDelDocumento(propuesta);
  const titulo = t('ficha.propuesta.titulo', { documento });
  const vacio = t('ficha.propuesta.vacio');
  const referencia = { documento, n: String(propuesta.documento.version), fecha: formatearFecha(propuesta.documento.cargadaEn) };

  if (!propuesta.disponible) {
    return (
      <Alert severity="info" icon={<FileSearch size={20} aria-hidden />} aria-label={titulo}>
        <AlertTitle>{titulo}</AlertTitle>
        <Typography sx={{ fontSize: 14 }}>{t('ficha.propuesta.sinLectura', referencia)}</Typography>
        {aplicada ? (
          <Typography sx={{ fontSize: 14, mt: 1 }}>{t('ficha.propuesta.soporteAgregado')}</Typography>
        ) : (
          <Stack direction="row" spacing={1} useFlexGap sx={{ mt: 1, flexWrap: 'wrap' }}>
            <Button size="small" variant="outlined" onClick={onSoloSoporte} disabled={deshabilitado} sx={{ minHeight: 44 }}>
              {t('ficha.propuesta.usarSoporte')}
            </Button>
            <Button size="small" variant="text" onClick={onAhoraNo} sx={{ minHeight: 44 }}>
              {t('ficha.propuesta.ahoraNo')}
            </Button>
          </Stack>
        )}
      </Alert>
    );
  }

  return (
    <Alert severity="info" icon={<FileSearch size={20} aria-hidden />} aria-label={titulo} sx={{ '& .MuiAlert-message': { minWidth: 0, flex: 1 } }}>
      <AlertTitle>{titulo}</AlertTitle>
      <Typography sx={{ fontSize: 14 }}>{t('ficha.propuesta.texto', referencia)}</Typography>
      <TableContainer sx={{ mt: 1, bgcolor: 'background.paper', border: 1, borderColor: 'divider', borderRadius: 1 }}>
        <Table size="small" aria-label={t('ficha.propuesta.tabla', { documento })}>
          <TableHead>
            <TableRow>
              <TableCell>{t('ficha.propuesta.dato')}</TableCell>
              <TableCell>{t('ficha.propuesta.actual')}</TableCell>
              <TableCell>{t('ficha.propuesta.enElDocumento', { documento })}</TableCell>
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
      {aplicada ? (
        <Typography role="status" sx={{ fontSize: 14, mt: 1 }}>
          {t('ficha.propuesta.aplicada')}
        </Typography>
      ) : (
        <Box sx={{ mt: 1 }}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Button size="small" variant="contained" onClick={onUsar} disabled={deshabilitado} sx={{ minHeight: 44 }}>
              {t('ficha.propuesta.usar')}
            </Button>
            <Button size="small" variant="text" onClick={onAhoraNo} sx={{ minHeight: 44 }}>
              {t('ficha.propuesta.ahoraNo')}
            </Button>
          </Stack>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, mt: 0.5 }}>{t('ficha.propuesta.nadaSeGuarda')}</Typography>
        </Box>
      )}
    </Alert>
  );
}
