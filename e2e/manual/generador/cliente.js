/* Comportamiento del manual en el navegador: tema, menú, buscador, visor de pantallazos y botones de copiar. Sin dependencias. */
(function () {
  'use strict';

  var raiz = document.body.getAttribute('data-raiz') || './';

  function guardar(clave, valor) {
    try {
      localStorage.setItem(clave, valor);
    } catch {
      /* sin almacenamiento: el tema solo dura esta página */
    }
  }

  function leer(clave) {
    try {
      return localStorage.getItem(clave);
    } catch {
      return null;
    }
  }

  // ---------- Tema ----------
  var temaGuardado = leer('manual-tema');
  if (temaGuardado === 'claro' || temaGuardado === 'oscuro') document.documentElement.setAttribute('data-tema', temaGuardado);

  var botonTema = document.getElementById('cambiar-tema');
  if (botonTema) {
    botonTema.addEventListener('click', function () {
      var actual = document.documentElement.getAttribute('data-tema');
      var oscuroPorSistema = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
      var esOscuro = actual ? actual === 'oscuro' : oscuroPorSistema;
      var siguiente = esOscuro ? 'claro' : 'oscuro';
      document.documentElement.setAttribute('data-tema', siguiente);
      guardar('manual-tema', siguiente);
    });
  }

  // ---------- Menú lateral en el celular ----------
  var botonMenu = document.getElementById('abrir-menu');
  if (botonMenu) {
    botonMenu.addEventListener('click', function () {
      var abierto = document.body.classList.toggle('menu-abierto');
      botonMenu.setAttribute('aria-expanded', abierto ? 'true' : 'false');
    });
  }

  // ---------- Buscador ----------
  function normalizar(texto) {
    return String(texto)
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .toLowerCase();
  }

  var entrada = document.getElementById('buscar');
  var panel = document.getElementById('resultados');
  var indice = null;

  function prepararIndice() {
    if (indice || !window.MANUAL_INDICE) return;
    indice = window.MANUAL_INDICE.map(function (e) {
      return { e: e, titulo: normalizar(e.t), contexto: normalizar(e.c || ''), texto: normalizar(e.x || '') };
    });
  }

  function buscar(consulta) {
    prepararIndice();
    if (!indice) return [];
    var palabras = normalizar(consulta).split(/\s+/).filter(Boolean);
    if (palabras.length === 0) return [];
    var encontrados = [];
    indice.forEach(function (item) {
      var puntos = 0;
      for (const p of palabras) {
        if (item.titulo.indexOf(p) >= 0) puntos += item.titulo.indexOf(p) === 0 ? 6 : 4;
        else if (item.contexto.indexOf(p) >= 0) puntos += 2;
        else if (item.texto.indexOf(p) >= 0) puntos += 1;
        else return;
      }
      encontrados.push({ e: item.e, puntos: puntos });
    });
    encontrados.sort(function (a, b) {
      return b.puntos - a.puntos;
    });
    return encontrados.slice(0, 12);
  }

  var seleccion = -1;

  function pintarResultados(lista, consulta) {
    panel.innerHTML = '';
    seleccion = -1;
    if (!consulta) {
      panel.hidden = true;
      return;
    }
    if (lista.length === 0) {
      var vacio = document.createElement('div');
      vacio.className = 'resultados-vacio';
      vacio.textContent = 'Sin resultados para «' + consulta + '».';
      panel.appendChild(vacio);
    }
    lista.forEach(function (r, i) {
      var enlace = document.createElement('a');
      enlace.className = 'resultado';
      enlace.href = raiz + r.e.u;
      enlace.setAttribute('role', 'option');
      enlace.setAttribute('aria-selected', 'false');
      enlace.id = 'resultado-' + i;
      var titulo = document.createElement('b');
      titulo.textContent = r.e.t;
      var contexto = document.createElement('span');
      contexto.textContent = r.e.c || '';
      enlace.appendChild(titulo);
      enlace.appendChild(contexto);
      panel.appendChild(enlace);
    });
    panel.hidden = false;
  }

  function marcar(nuevo) {
    var items = panel.querySelectorAll('.resultado');
    if (items.length === 0) return;
    seleccion = (nuevo + items.length) % items.length;
    items.forEach(function (it, i) {
      it.setAttribute('aria-selected', i === seleccion ? 'true' : 'false');
    });
    items[seleccion].scrollIntoView({ block: 'nearest' });
  }

  if (entrada && panel) {
    entrada.addEventListener('input', function () {
      pintarResultados(buscar(entrada.value), entrada.value.trim());
    });
    entrada.addEventListener('focus', function () {
      if (entrada.value.trim()) pintarResultados(buscar(entrada.value), entrada.value.trim());
    });
    entrada.addEventListener('keydown', function (ev) {
      if (ev.key === 'ArrowDown') {
        ev.preventDefault();
        marcar(seleccion + 1);
      } else if (ev.key === 'ArrowUp') {
        ev.preventDefault();
        marcar(seleccion - 1);
      } else if (ev.key === 'Enter') {
        var items = panel.querySelectorAll('.resultado');
        var destino = items[seleccion >= 0 ? seleccion : 0];
        if (destino) window.location.href = destino.href;
      } else if (ev.key === 'Escape') {
        panel.hidden = true;
        entrada.blur();
      }
    });
    document.addEventListener('click', function (ev) {
      if (!panel.contains(ev.target) && ev.target !== entrada) panel.hidden = true;
    });
    document.addEventListener('keydown', function (ev) {
      var enCampo = /^(INPUT|TEXTAREA|SELECT)$/.test((document.activeElement || {}).tagName || '');
      if (ev.key === '/' && !enCampo) {
        ev.preventDefault();
        entrada.focus();
      }
    });
  }

  // ---------- Visor de pantallazos ----------
  var visor = document.getElementById('visor');
  if (visor && typeof visor.showModal === 'function') {
    var imagenVisor = visor.querySelector('img');
    var pie = visor.querySelector('.visor-barra span');
    document.addEventListener('click', function (ev) {
      var origen = ev.target instanceof Element ? ev.target.closest('[data-zoom]') : null;
      if (!origen) return;
      ev.preventDefault();
      // Con el sitio incrustado el pantallazo vive en la propia imagen de la página (data-zoom va vacío para no duplicar los bytes).
      var imagenOrigen = origen.querySelector('img');
      imagenVisor.src = origen.getAttribute('data-zoom') || (imagenOrigen ? imagenOrigen.currentSrc || imagenOrigen.src : '');
      imagenVisor.alt = origen.getAttribute('data-alt') || '';
      pie.textContent = origen.getAttribute('data-alt') || '';
      visor.showModal();
    });
    visor.addEventListener('click', function (ev) {
      if (ev.target === visor || (ev.target instanceof Element && ev.target.closest('[data-cerrar]'))) visor.close();
    });
  }

  // ---------- Copiar bloques de código ----------
  document.querySelectorAll('pre[data-copiable]').forEach(function (pre) {
    var boton = document.createElement('button');
    boton.type = 'button';
    boton.className = 'copiar';
    boton.textContent = 'Copiar';
    boton.addEventListener('click', function () {
      var texto = pre.querySelector('code') ? pre.querySelector('code').textContent : pre.textContent;
      if (navigator.clipboard) {
        navigator.clipboard.writeText(texto).then(function () {
          boton.textContent = 'Copiado';
          setTimeout(function () {
            boton.textContent = 'Copiar';
          }, 1500);
        });
      }
    });
    pre.appendChild(boton);
  });
})();
