// Photo lightbox: zoom (chuột) + pinch + kéo khi đã zoom
(function () {
  function initPhotoLightboxZoom() {
    const modal = document.getElementById('photoLightbox');
    const zoomport = document.getElementById('photoLightboxZoomport');
    const img = document.getElementById('photoLightboxImg');
    if (!modal || !zoomport || !img) return;

    let scale = 1;
    let tx = 0;
    let ty = 0;
    const minScale = 1;
    const maxScale = 5;

    const pointers = new Map();
    let pinchBaseDist = 0;
    let pinchBaseScale = 1;
    let panStart = null;

    function applyTransform() {
      if (scale <= 1) {
        scale = 1;
        tx = 0;
        ty = 0;
      }
      img.style.transform = 'translate(' + tx + 'px,' + ty + 'px) scale(' + scale + ')';
    }

    function resetTransform() {
      scale = 1;
      tx = 0;
      ty = 0;
      pinchBaseDist = 0;
      pinchBaseScale = 1;
      panStart = null;
      pointers.clear();
      zoomport.classList.remove('is-panning');
      applyTransform();
    }

    function dist(a, b) {
      return Math.hypot(a.x - b.x, a.y - b.y);
    }

    modal.addEventListener('hidden.bs.modal', resetTransform);
    img.addEventListener('load', resetTransform);

    img.addEventListener('dblclick', function (e) {
      e.preventDefault();
      resetTransform();
    });

    zoomport.addEventListener(
      'wheel',
      function (e) {
        if (!img.getAttribute('src')) return;
        e.preventDefault();
        const factor = e.deltaY > 0 ? 0.9 : 1.1;
        scale = Math.min(maxScale, Math.max(minScale, scale * factor));
        applyTransform();
      },
      { passive: false }
    );

    zoomport.addEventListener('pointerdown', function (e) {
      if (!img.getAttribute('src')) return;
      if (e.button !== 0) return;
      try {
        zoomport.setPointerCapture(e.pointerId);
      } catch (_) {}
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

      if (pointers.size === 2) {
        const arr = Array.from(pointers.values());
        pinchBaseDist = dist(arr[0], arr[1]);
        pinchBaseScale = scale;
        panStart = null;
      } else if (pointers.size === 1 && scale > 1) {
        panStart = { cx: e.clientX, cy: e.clientY, tx: tx, ty: ty };
      }
    });

    zoomport.addEventListener('pointermove', function (e) {
      if (!pointers.has(e.pointerId)) return;
      pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });

      if (pointers.size === 2) {
        const arr = Array.from(pointers.values());
        const d = dist(arr[0], arr[1]);
        if (pinchBaseDist > 0) {
          scale = Math.min(maxScale, Math.max(minScale, pinchBaseScale * (d / pinchBaseDist)));
          applyTransform();
        }
      } else if (pointers.size === 1 && panStart && scale > 1) {
        zoomport.classList.add('is-panning');
        tx = panStart.tx + (e.clientX - panStart.cx);
        ty = panStart.ty + (e.clientY - panStart.cy);
        applyTransform();
      }
    });

    function pointerEnd(e) {
      pointers.delete(e.pointerId);
      try {
        zoomport.releasePointerCapture(e.pointerId);
      } catch (_) {}

      if (pointers.size < 2) pinchBaseDist = 0;

      if (pointers.size === 0) {
        panStart = null;
        zoomport.classList.remove('is-panning');
      } else if (pointers.size === 1 && scale > 1) {
        const pt = pointers.values().next().value;
        panStart = { cx: pt.x, cy: pt.y, tx: tx, ty: ty };
      }
    }

    zoomport.addEventListener('pointerup', pointerEnd);
    zoomport.addEventListener('pointercancel', pointerEnd);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initPhotoLightboxZoom);
  } else {
    initPhotoLightboxZoom();
  }
})();
