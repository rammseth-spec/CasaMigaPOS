// Arranque que nunca queda en blanco (archivo aparte porque la política de seguridad no deja scripts dentro del HTML): mientras baja la
// web se ve «Cargando Casa Miga…» (#arranque en index.html) y app.js lo quita al mostrar la entrada o la app. Si tarda o no baja un
// archivo, se dice y se ofrece «Volver a cargar», que sólo recarga la página: no borra datos ni la sesión.
(function () {
  var box = document.getElementById('arranque');
  if (!box) return;
  var text = document.getElementById('arranque-texto'), retry = document.getElementById('arranque-reintentar');
  function avisar(message) {
    if (box.hidden) return; // la web ya arrancó
    text.textContent = message; retry.hidden = false;
  }
  retry.addEventListener('click', function () { location.reload(); });
  setTimeout(function () { avisar('Está tardando: el internet va lento. Puedes esperar o volver a cargar.'); }, 15000);
  addEventListener('error', function (e) {
    var el = e.target;
    if (el && (el.tagName === 'IMG' || el.tagName === 'VIDEO' || el.tagName === 'AUDIO')) return; // una imagen rota no impide arrancar
    if (el && (el.tagName === 'SCRIPT' || el.tagName === 'LINK')) avisar('No se pudo bajar Casa Miga. Revisa el internet y vuelve a cargar.');
    else avisar('Casa Miga no pudo arrancar. Vuelve a cargar; tus datos siguen guardados.');
  }, true);
})();
