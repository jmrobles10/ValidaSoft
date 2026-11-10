import { Autocomplete, TextField } from '@mui/material';
import type { EntradaCatalogoDto } from '@validasoft/api-client';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { etiquetaEntrada, useBusquedaCatalogo, useEntradasCatalogo, type Catalogo } from './api';
import { filtrarOpciones, type Opcion } from './paises';

interface PropsBase {
  etiqueta: string;
  mensaje?: string | null;
  ayuda?: string;
  deshabilitado?: boolean;
}

/** Espera a que el tercero deje de escribir para buscar (en el celular cada letra sería una petición). */
function useRetrasado(valor: string, ms = 250) {
  const [retrasado, setRetrasado] = useState(valor);
  useEffect(() => {
    const id = setTimeout(() => setRetrasado(valor), ms);
    return () => clearTimeout(id);
  }, [valor, ms]);
  return retrasado;
}

interface PropsCampoCatalogo extends PropsBase {
  catalogo: Catalogo;
  valor: string | null | undefined;
  onCambiar: (codigo: string | null) => void;
}

/** Autocompletar contra un catálogo grande de plataforma (ciudades, CIIU): busca en el servidor mientras se escribe. */
export function CampoCatalogo({ catalogo, valor, onCambiar, etiqueta, mensaje, ayuda, deshabilitado = false }: PropsCampoCatalogo) {
  const { t } = useTranslation();
  const [texto, setTexto] = useState('');
  const buscado = useRetrasado(texto);
  const busqueda = useBusquedaCatalogo(catalogo, buscado);
  const conocidas = useEntradasCatalogo(catalogo, valor ? [valor] : []);
  const elegida: EntradaCatalogoDto | null = valor ? (conocidas.get(valor) ?? { codigo: valor, nombre: valor }) : null;

  return (
    <Autocomplete
      options={busqueda.data?.datos ?? []}
      value={elegida}
      onChange={(_e, entrada) => onCambiar(entrada?.codigo ?? null)}
      onInputChange={(_e, nuevo, razon) => {
        if (razon === 'input') setTexto(nuevo);
      }}
      filterOptions={(opciones) => opciones}
      getOptionLabel={(e) => etiquetaEntrada(catalogo, e)}
      isOptionEqualToValue={(a, b) => a.codigo === b.codigo}
      loading={busqueda.isFetching}
      loadingText={t('ficha.buscando')}
      noOptionsText={texto ? t('ficha.sinOpciones') : t('ficha.escribeParaBuscar')}
      disabled={deshabilitado}
      fullWidth
      renderInput={(params) => <TextField {...params} label={etiqueta} error={Boolean(mensaje)} helperText={mensaje ?? ayuda} />}
    />
  );
}

interface PropsSelectorUnico extends PropsBase {
  opciones: readonly Opcion[];
  valor: string | null | undefined;
  onCambiar: (codigo: string | null) => void;
}

/** Autocompletar sobre una lista conocida (países), filtrando sin tildes. */
export function SelectorUnico({ opciones, valor, onCambiar, etiqueta, mensaje, ayuda, deshabilitado = false }: PropsSelectorUnico) {
  const { t } = useTranslation();
  const elegida = valor ? (opciones.find((o) => o.codigo === valor) ?? { codigo: valor, nombre: valor }) : null;
  return (
    <Autocomplete
      options={opciones}
      value={elegida}
      onChange={(_e, opcion) => onCambiar(opcion?.codigo ?? null)}
      filterOptions={(lista, estado) => filtrarOpciones(lista, estado.inputValue)}
      getOptionLabel={(o) => o.nombre}
      isOptionEqualToValue={(a, b) => a.codigo === b.codigo}
      noOptionsText={t('ficha.sinOpciones')}
      disabled={deshabilitado}
      fullWidth
      renderInput={(params) => <TextField {...params} label={etiqueta} error={Boolean(mensaje)} helperText={mensaje ?? ayuda} />}
    />
  );
}

interface PropsSelectorMultiple extends PropsBase {
  opciones: readonly Opcion[];
  valor: readonly string[] | null | undefined;
  onCambiar: (codigos: string[] | null) => void;
  cargando?: boolean;
  /** Cómo se lee cada opción en la lista y en su chip (por omisión, «código · nombre»). */
  etiquetaOpcion?: (o: Opcion) => string;
}

/** Selección múltiple con chips (responsabilidades DIAN, países); sin ninguna elegida es `null`. */
export function SelectorMultiple({ opciones, valor, onCambiar, etiqueta, mensaje, ayuda, cargando = false, etiquetaOpcion, deshabilitado = false }: PropsSelectorMultiple) {
  const { t } = useTranslation();
  const elegidas = (valor ?? []).map((codigo) => opciones.find((o) => o.codigo === codigo) ?? { codigo, nombre: codigo });
  return (
    <Autocomplete
      multiple
      options={opciones}
      value={elegidas}
      onChange={(_e, lista) => onCambiar(lista.length > 0 ? lista.map((o) => o.codigo) : null)}
      filterOptions={(lista, estado) => filtrarOpciones(lista, estado.inputValue, 100)}
      getOptionLabel={etiquetaOpcion ?? ((o) => `${o.codigo} · ${o.nombre}`)}
      isOptionEqualToValue={(a, b) => a.codigo === b.codigo}
      loading={cargando}
      loadingText={t('comun.cargando')}
      noOptionsText={t('ficha.sinOpciones')}
      disableCloseOnSelect
      disabled={deshabilitado}
      fullWidth
      renderInput={(params) => <TextField {...params} label={etiqueta} error={Boolean(mensaje)} helperText={mensaje ?? ayuda} />}
    />
  );
}
