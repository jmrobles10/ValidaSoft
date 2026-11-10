import { Alert, Chip, InputAdornment, Paper, Skeleton, Stack, TextField, Typography } from '@mui/material';
import { Search, X } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../../shared/componentes';
import { useCatalogoCompleto } from '../api';
import { Subtitulo } from '../campos';
import type { DatosEspecialidades, PropsFormulario } from '../tipos';
import { ArbolEspecialidades } from './ArbolEspecialidades';
import { armarArbol, filtrarArbol } from './arbol';

/**
 * Especialidades (`FICHA_ESPECIALIDADES`): lo elegido arriba (chips que se quitan) y el catálogo de plataforma en árbol
 * de 3 niveles con búsqueda; solo las especialidades (nivel 3) se pueden marcar.
 */
export function FormularioEspecialidades({ datos, onCambiar, mensaje, deshabilitado }: PropsFormulario<DatosEspecialidades>) {
  const { t } = useTranslation();
  const catalogo = useCatalogoCompleto('especialidades');
  const [texto, setTexto] = useState('');
  const elegidas = datos.especialidades ?? [];
  const nombreDe = (codigo: string) => catalogo.data?.find((e) => e.codigo === codigo)?.nombre ?? codigo;

  const alternar = (codigo: string, marcado: boolean) => {
    const lista = marcado ? [...elegidas, codigo] : elegidas.filter((c) => c !== codigo);
    onCambiar({ especialidades: lista.length > 0 ? lista : null });
  };
  const errorDeLista = mensaje('especialidades') ?? elegidas.map((_, i) => mensaje(`especialidades[${String(i)}]`)).find(Boolean) ?? null;
  const arbol = catalogo.data ? filtrarArbol(armarArbol(catalogo.data), texto) : [];

  return (
    <Stack spacing={2}>
      <Stack spacing={1} component="section" aria-label={t('ficha.especialidades.elegidas')}>
        <Subtitulo>{t('ficha.especialidades.elegidas')}</Subtitulo>
        {elegidas.length === 0 ? (
          <Typography sx={{ color: 'text.secondary' }}>{t('ficha.especialidades.sinElegidas')}</Typography>
        ) : (
          <Stack direction="row" useFlexGap spacing={1} sx={{ flexWrap: 'wrap' }}>
            {elegidas.map((codigo) => (
              <Chip
                key={codigo}
                label={nombreDe(codigo)}
                onDelete={deshabilitado ? undefined : () => alternar(codigo, false)}
                deleteIcon={<X size={16} aria-label={t('ficha.especialidades.quitarAccesible', { nombre: nombreDe(codigo) })} />}
              />
            ))}
          </Stack>
        )}
        {errorDeLista && <Alert severity="error">{errorDeLista}</Alert>}
      </Stack>
      <TextField
        label={t('ficha.especialidades.buscar')}
        helperText={t('ficha.especialidades.buscarAyuda')}
        value={texto}
        onChange={(e) => setTexto(e.target.value)}
        type="search"
        fullWidth
        slotProps={{ input: { startAdornment: <InputAdornment position="start"><Search size={18} aria-hidden /></InputAdornment> } }}
      />
      <Paper variant="outlined" sx={{ maxHeight: 480, overflowY: 'auto' }}>
        {catalogo.isPending ? (
          <Skeleton variant="rounded" height={160} />
        ) : catalogo.isError ? (
          <ErrorRecuperable error={catalogo.error} onReintentar={() => void catalogo.refetch()} />
        ) : arbol.length === 0 ? (
          <Typography sx={{ p: 2, color: 'text.secondary' }}>{t('ficha.especialidades.sinResultados', { texto })}</Typography>
        ) : (
          <ArbolEspecialidades nodos={arbol} elegidas={elegidas} abiertoTodo={texto.trim().length > 0} onAlternar={alternar} deshabilitado={deshabilitado} />
        )}
      </Paper>
    </Stack>
  );
}
