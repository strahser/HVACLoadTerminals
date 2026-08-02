using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace HVACLoadTerminals.Infrastructure.Visualization
{
    /// <summary>
    /// Exports a placement scene (as produced by <see cref="PlacementSceneSerializer.ToJson"/>)
    /// to a single self-contained HTML5 document: dark UI, room sidebar with per-system
    /// summary, an interactive Canvas-2D viewer (pan / zoom / hover tooltip / room focus)
    /// and an optional Three.js 3D view loaded on demand from a CDN.
    /// </summary>
    public static class HtmlSceneExporter
    {
        /// <summary>Builds the full HTML document. <paramref name="sceneJson"/> is injected raw (trusted).</summary>
        public static string BuildHtml(string title, string sceneJson)
        {
            var json = string.IsNullOrWhiteSpace(sceneJson)
                ? "{\"Title\":\"\",\"Rooms\":[]}"
                : sceneJson;

            // Prevent premature </script> termination inside JSON string values.
            var safeJson = json.Replace("</", "<\\/");

            var effectiveTitle = string.IsNullOrWhiteSpace(title) ? "Terminal Placement Scene" : title;
            var titleHtml = System.Net.WebUtility.HtmlEncode(effectiveTitle);
            var titleJs = JsonConvert.SerializeObject(effectiveTitle).Replace("</", "<\\/");

            return HeadTemplate.Replace("{TITLE}", titleHtml)
                + safeJson
                + TailTemplate.Replace("{TITLE_JS}", titleJs);
        }

        /// <summary>Writes <c>index.html</c> into <paramref name="directory"/> (UTF-8, no BOM) and returns its full path.</summary>
        public static string SaveToFile(string directory, string title, string sceneJson)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "index.html");
            File.WriteAllText(path, BuildHtml(title, sceneJson), new UTF8Encoding(false));
            return path;
        }

        private const string HeadTemplate = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{TITLE}</title>
<style>
* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; height: 100%; overflow: hidden;
  background: #0d1117; color: #e6edf3;
  font-family: 'Segoe UI', Tahoma, Arial, sans-serif; }
#layout { display: flex; height: 100vh; }
#sidebar { width: 330px; min-width: 330px; background: #161b22;
  border-right: 1px solid #30363d; overflow-y: auto; padding: 12px; }
#main { position: relative; flex: 1; overflow: hidden; }
#cv { display: block; width: 100%; height: 100%; cursor: grab; }
#cv.dragging { cursor: grabbing; }
#toolbar { position: absolute; top: 8px; left: 8px; right: 8px; z-index: 5;
  display: flex; align-items: center; gap: 8px; pointer-events: none; }
#toolbar button { pointer-events: auto; cursor: pointer; background: #21262d;
  color: #e6edf3; border: 1px solid #30363d; border-radius: 6px;
  padding: 6px 12px; font-size: 12px; }
#toolbar button:hover { background: #30363d; }
#toolbar button:disabled { opacity: 0.5; cursor: default; }
#hint { color: #8b949e; font-size: 11px; margin-left: auto; }
#tooltip { position: absolute; display: none; background: rgba(13,17,23,0.95);
  border: 1px solid #30363d; border-radius: 6px; padding: 6px 8px;
  font-size: 12px; z-index: 10; pointer-events: none; max-width: 260px; }
h2 { font-size: 15px; margin: 4px 0 10px; color: #58a6ff; }
.roomBlock { margin-bottom: 10px; border: 1px solid #30363d; border-radius: 6px;
  padding: 8px; cursor: pointer; background: #0d1117; }
.roomBlock.active { border-color: #58a6ff; box-shadow: 0 0 0 1px #58a6ff; }
.roomHead { font-weight: 600; margin-bottom: 6px; }
.count { float: right; background: #30363d; border-radius: 10px;
  padding: 0 8px; font-size: 11px; line-height: 16px; }
.sumTable { width: 100%; border-collapse: collapse; font-size: 12px; }
.sumTable td { padding: 2px 4px; border-top: 1px solid #21262d; }
.chip { display: inline-block; width: 12px; height: 12px; border-radius: 3px;
  vertical-align: middle; }
.none { color: #8b949e; font-style: italic; }
</style>
</head>
<body>
<div id="layout">
  <aside id="sidebar">
    <h2 id="appTitle">Terminal Placement Scene</h2>
    <div id="roomList"></div>
  </aside>
  <main id="main">
    <div id="toolbar">
      <button id="btn3d" type="button">3D (Three.js)</button>
      <button id="btn2d" type="button" style="display:none">Back to 2D</button>
      <span id="hint">Drag: pan &middot; Wheel: zoom &middot; Dbl-click: reset &middot; Click room: focus</span>
    </div>
    <canvas id="cv"></canvas>
    <div id="tooltip"></div>
  </main>
</div>
<script>const SCENE = 
""";

        private const string TailTemplate = """
;
</script>
<script>
(function () {
  'use strict';

  var SC = SCENE || { Rooms: [] };
  var APP_TITLE = {TITLE_JS};

  var cv = document.getElementById('cv');
  var ctx = cv.getContext('2d');
  var tooltip = document.getElementById('tooltip');
  var sidebar = document.getElementById('roomList');
  var btn3d = document.getElementById('btn3d');
  var btn2d = document.getElementById('btn2d');

  var view = { scale: 1, tx: 0, ty: 0, w: 0, h: 0 };
  var selectedRoom = null;
  var dragging = false;
  var lastX = 0, lastY = 0;
  var mode3d = false;
  var rooms = [];
  var placementsFlat = [];
  var threeLoaded = false, threeFailed = false, threeState = null, animStarted = false;

  document.title = APP_TITLE;
  var titleEl = document.getElementById('appTitle');
  if (titleEl) titleEl.textContent = APP_TITLE;

  function esc(s) {
    return String(s == null ? '' : s)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }

  function fmtFlow(v) {
    if (v == null || isNaN(v)) return '0';
    var n = Number(v);
    return (n % 1 === 0) ? String(Math.round(n)) : n.toFixed(1);
  }

  function buildData() {
    (SC.Rooms || []).forEach(function (r) {
      var bnd = (r.Boundary || []).map(function (p) { return [p.X, p.Y]; });
      var off = (r.OffsetPolygon || []).map(function (p) { return [p.X, p.Y]; });
      var room = { id: r.RoomId, name: r.RoomName, bnd: bnd, off: off, systems: r.Systems || [] };
      rooms.push(room);
      room.systems.forEach(function (s) {
        (s.Placements || []).forEach(function (pl) {
          placementsFlat.push({
            x: pl.Position.X, y: pl.Position.Y, rot: pl.RotationDegrees,
            family: pl.FamilyName, type: pl.TypeName, flow: pl.Flow,
            color: s.Color, roomId: r.RoomId
          });
        });
      });
    });
  }

  function bounds() {
    var minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    rooms.forEach(function (r) {
      r.bnd.forEach(function (p) {
        if (p[0] < minX) minX = p[0];
        if (p[0] > maxX) maxX = p[0];
        if (p[1] < minY) minY = p[1];
        if (p[1] > maxY) maxY = p[1];
      });
    });
    if (!isFinite(minX)) { minX = -5; minY = -5; maxX = 5; maxY = 5; }
    return { minX: minX, minY: minY, maxX: maxX, maxY: maxY,
             cx: (minX + maxX) / 2, cy: (minY + maxY) / 2 };
  }

  function toScreen(x, y) {
    return { x: view.scale * x + view.tx, y: -view.scale * y + view.ty };
  }

  function fitView() {
    var b = bounds();
    var pad = 40;
    var sx = (view.w - pad * 2) / Math.max(1e-6, b.maxX - b.minX);
    var sy = (view.h - pad * 2) / Math.max(1e-6, b.maxY - b.minY);
    view.scale = Math.max(1e-9, Math.min(sx, sy));
    view.tx = view.w / 2 - view.scale * b.cx;
    view.ty = view.h / 2 + view.scale * b.cy;
  }

  function resize() {
    if (mode3d) return;
    var rect = cv.parentElement.getBoundingClientRect();
    view.w = Math.max(50, rect.width);
    view.h = Math.max(50, rect.height);
    var dpr = window.devicePixelRatio || 1;
    cv.width = Math.round(view.w * dpr);
    cv.height = Math.round(view.h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    draw();
  }

  function draw() {
    ctx.fillStyle = '#15171a';
    ctx.fillRect(0, 0, view.w, view.h);
    drawRooms();
    drawPlacements();
  }

  function drawRooms() {
    rooms.forEach(function (r) {
      if (r.bnd.length < 2) return;
      var sel = (r.id === selectedRoom);
      ctx.beginPath();
      var s0 = toScreen(r.bnd[0][0], r.bnd[0][1]);
      ctx.moveTo(s0.x, s0.y);
      for (var i = 1; i < r.bnd.length; i++) {
        var s = toScreen(r.bnd[i][0], r.bnd[i][1]);
        ctx.lineTo(s.x, s.y);
      }
      ctx.closePath();
      ctx.fillStyle = sel ? 'rgba(255,255,255,0.12)' : 'rgba(255,255,255,0.05)';
      ctx.fill();
      ctx.strokeStyle = sel ? '#ffffff' : 'rgba(255,255,255,0.75)';
      ctx.lineWidth = sel ? 3 : 1.5;
      ctx.stroke();

      if (r.off.length >= 2) {
        ctx.beginPath();
        var o0 = toScreen(r.off[0][0], r.off[0][1]);
        ctx.moveTo(o0.x, o0.y);
        for (var j = 1; j < r.off.length; j++) {
          var oj = toScreen(r.off[j][0], r.off[j][1]);
          ctx.lineTo(oj.x, oj.y);
        }
        ctx.closePath();
        ctx.setLineDash([6, 4]);
        ctx.strokeStyle = '#ffb000';
        ctx.lineWidth = 1.5;
        ctx.stroke();
        ctx.setLineDash([]);
      }

      var c0 = toScreen(r.bnd[0][0], r.bnd[0][1]);
      ctx.font = '12px Consolas, monospace';
      ctx.fillStyle = '#c9d1d9';
      ctx.fillText(r.name || r.id || '', c0.x + 8, c0.y - 8);
    });
  }

  function drawPlacements() {
    placementsFlat.forEach(function (p) {
      if (selectedRoom != null && p.roomId !== selectedRoom) return;
      var rot = (p.rot || 0) * Math.PI / 180;
      var cos = Math.cos(rot), sin = Math.sin(rot);
      var hw = 1.0, hh = 0.5;
      var corners = [[-hw, -hh], [hw, -hh], [hw, hh], [-hw, hh]];
      ctx.beginPath();
      for (var i = 0; i < 4; i++) {
        var wx = p.x + corners[i][0] * cos - corners[i][1] * sin;
        var wy = p.y + corners[i][0] * sin + corners[i][1] * cos;
        var s = toScreen(wx, wy);
        if (i === 0) ctx.moveTo(s.x, s.y); else ctx.lineTo(s.x, s.y);
      }
      ctx.closePath();
      ctx.fillStyle = p.color || '#e6194b';
      ctx.globalAlpha = 0.85;
      ctx.fill();
      ctx.globalAlpha = 1;
      ctx.strokeStyle = 'rgba(255,255,255,0.9)';
      ctx.lineWidth = 1;
      ctx.stroke();

      var c = toScreen(p.x, p.y);
      var e = toScreen(p.x + 0.8 * cos, p.y + 0.8 * sin);
      ctx.beginPath();
      ctx.moveTo(c.x, c.y);
      ctx.lineTo(e.x, e.y);
      ctx.strokeStyle = '#ffffff';
      ctx.lineWidth = 1.5;
      ctx.stroke();

      ctx.font = '10px Consolas, monospace';
      ctx.fillStyle = '#e6edf3';
      ctx.fillText(p.family + ' ' + p.type + ' (' + fmtFlow(p.flow) + ')', c.x + 6, c.y - 6);
    });
  }

  function buildSidebar() {
    var html = '';
    rooms.forEach(function (r) {
      var total = 0;
      r.systems.forEach(function (s) { total += (s.Placements || []).length; });
      html += '<div class="roomBlock' + (selectedRoom === r.id ? ' active' : '') + '" data-id="' + esc(r.id) + '">';
      html += '<div class="roomHead">' + esc(r.name || r.id || 'Room') +
              ' <span class="count">' + total + '</span></div>';
      html += '<table class="sumTable">';
      r.systems.forEach(function (s) {
        var n = (s.Placements || []).length;
        var flow = 0;
        (s.Placements || []).forEach(function (pl) { flow += (pl.Flow || 0); });
        html += '<tr><td><span class="chip" style="background:' + esc(s.Color || '#888') + '"></span></td>' +
                '<td>' + esc(s.SystemName) + '</td>' +
                '<td>' + n + '</td>' +
                '<td>' + fmtFlow(flow) + '</td></tr>';
      });
      html += '</table></div>';
    });
    sidebar.innerHTML = html || '<div class="none">No rooms in scene</div>';
  }

  function updateTooltip(ev) {
    var rect = cv.getBoundingClientRect();
    var mx = ev.clientX - rect.left;
    var my = ev.clientY - rect.top;
    var best = null, bestD = 25;
    placementsFlat.forEach(function (p) {
      if (selectedRoom != null && p.roomId !== selectedRoom) return;
      var c = toScreen(p.x, p.y);
      var dx = c.x - mx, dy = c.y - my;
      var d = dx * dx + dy * dy;
      if (d < bestD) { bestD = d; best = p; }
    });
    if (best) {
      tooltip.style.display = 'block';
      tooltip.style.left = (mx + 14) + 'px';
      tooltip.style.top = (my + 14) + 'px';
      tooltip.innerHTML = '<b>' + esc(best.family) + ' ' + esc(best.type) + '</b><br>' +
        'Flow: ' + fmtFlow(best.flow) + ' m&sup3;/h<br>' +
        'Pos: (' + best.x.toFixed(2) + ', ' + best.y.toFixed(2) + ') ft';
    } else {
      tooltip.style.display = 'none';
    }
  }

  cv.addEventListener('wheel', function (ev) {
    if (mode3d) return;
    ev.preventDefault();
    var rect = cv.getBoundingClientRect();
    var mx = ev.clientX - rect.left;
    var my = ev.clientY - rect.top;
    var factor = ev.deltaY < 0 ? 1.15 : 1 / 1.15;
    var wx = (mx - view.tx) / view.scale;
    var wy = (view.ty - my) / view.scale;
    view.scale *= factor;
    view.tx = mx - view.scale * wx;
    view.ty = my + view.scale * wy;
    draw();
  }, { passive: false });

  cv.addEventListener('mousedown', function (ev) {
    if (mode3d || ev.button !== 0) return;
    dragging = true;
    lastX = ev.clientX; lastY = ev.clientY;
    cv.classList.add('dragging');
  });

  window.addEventListener('mousemove', function (ev) {
    if (dragging) {
      view.tx += ev.clientX - lastX;
      view.ty += ev.clientY - lastY;
      lastX = ev.clientX; lastY = ev.clientY;
      draw();
    } else if (!mode3d) {
      updateTooltip(ev);
    }
  });

  window.addEventListener('mouseup', function () {
    if (dragging) {
      dragging = false;
      cv.classList.remove('dragging');
    }
  });

  cv.addEventListener('dblclick', function () {
    if (mode3d) return;
    fitView();
    draw();
  });

  window.addEventListener('resize', function () {
    if (mode3d) { if (threeState) resize3D(threeState); }
    else resize();
  });

  sidebar.addEventListener('click', function (ev) {
    var t = ev.target;
    while (t && t !== sidebar && !(t.classList && t.classList.contains('roomBlock'))) {
      t = t.parentNode;
    }
    if (!t || t === sidebar) return;
    var id = t.getAttribute('data-id');
    selectedRoom = (selectedRoom === id) ? null : id;
    buildSidebar();
    draw();
  });

  // ---------- 3D (Three.js) ----------

  function loadThree(cb) {
    var done = false;
    var s = document.createElement('script');
    s.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js';
    s.onload = function () { if (!done) { done = true; cb(!!window.THREE); } };
    s.onerror = function () { if (!done) { done = true; cb(false); } };
    document.head.appendChild(s);
    setTimeout(function () { if (!done) { done = true; cb(!!window.THREE); } }, 3000);
  }

  btn3d.addEventListener('click', function () {
    if (threeFailed) { alert('3D unavailable (no network)'); return; }
    if (threeLoaded) { start3D(); return; }
    btn3d.disabled = true;
    btn3d.textContent = 'Loading 3D...';
    loadThree(function (ok) {
      if (ok) {
        threeLoaded = true;
        btn3d.disabled = false;
        start3D();
      } else {
        threeFailed = true;
        btn3d.disabled = false;
        btn3d.textContent = '3D (Three.js)';
        alert('3D unavailable (no network)');
      }
    });
  });

  function start3D() {
    if (!threeState) threeState = buildThree();
    threeState.container.style.display = 'block';
    resize3D(threeState);
    cv.style.display = 'none';
    tooltip.style.display = 'none';
    btn3d.style.display = 'none';
    btn2d.style.display = 'inline-block';
    mode3d = true;
    ensureAnim();
  }

  function stop3D() {
    if (!threeState) return;
    mode3d = false;
    threeState.container.style.display = 'none';
    cv.style.display = 'block';
    btn3d.style.display = 'inline-block';
    btn2d.style.display = 'none';
    draw();
  }

  btn2d.addEventListener('click', stop3D);

  function buildThree() {
    var container = document.createElement('div');
    container.style.cssText = 'position:absolute;inset:0;display:none;';
    document.getElementById('main').appendChild(container);

    var renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio || 1);
    container.appendChild(renderer.domElement);

    var scene = new THREE.Scene();
    scene.background = new THREE.Color(0x15171a);

    var camera = new THREE.PerspectiveCamera(50, 1, 0.1, 100000);
    var b = bounds();
    var span = Math.max(b.maxX - b.minX, b.maxY - b.minY, 1);
    var center = new THREE.Vector3(b.cx, b.cy, 0);

    rooms.forEach(function (r) {
      if (r.bnd.length < 2) return;
      var pts = [];
      r.bnd.forEach(function (p) { pts.push(p[0], p[1], 0); });
      pts.push(r.bnd[0][0], r.bnd[0][1], 0);
      var geo = new THREE.BufferGeometry();
      geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
      scene.add(new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0x9aa5b1 })));

      if (r.off.length >= 2) {
        var o = [];
        r.off.forEach(function (p) { o.push(p[0], p[1], 0); });
        o.push(r.off[0][0], r.off[0][1], 0);
        var geo2 = new THREE.BufferGeometry();
        geo2.setAttribute('position', new THREE.Float32BufferAttribute(o, 3));
        scene.add(new THREE.Line(geo2, new THREE.LineBasicMaterial({ color: 0xffb000 })));
      }
    });

    placementsFlat.forEach(function (p) {
      var geo = new THREE.BoxGeometry(2, 0.3, 1);
      var mat = new THREE.MeshBasicMaterial({ color: new THREE.Color(p.color || '#e6194b') });
      var mesh = new THREE.Mesh(geo, mat);
      mesh.position.set(p.x, p.y, 0);
      mesh.rotation.z = (p.rot || 0) * Math.PI / 180;
      scene.add(mesh);
    });

    var grid = new THREE.GridHelper(span * 2, 10, 0x404040, 0x26292e);
    grid.rotation.x = Math.PI / 2;
    grid.position.set(center.x, center.y, 0);
    scene.add(grid);

    var st = {
      container: container, renderer: renderer, scene: scene, camera: camera,
      center: center, radius: span * 1.6,
      theta: Math.PI / 4, phi: Math.PI / 3.2
    };
    updateCamera(st);
    bind3DControls(st);
    return st;
  }

  function updateCamera(st) {
    var r = st.radius;
    var x = st.center.x + r * Math.sin(st.phi) * Math.sin(st.theta);
    var y = st.center.y + r * Math.sin(st.phi) * Math.cos(st.theta);
    var z = st.center.z + r * Math.cos(st.phi);
    st.camera.position.set(x, y, z);
    st.camera.lookAt(st.center);
  }

  function resize3D(st) {
    var w = st.container.clientWidth || 800;
    var h = st.container.clientHeight || 600;
    st.camera.aspect = w / h;
    st.camera.updateProjectionMatrix();
    st.renderer.setSize(w, h);
  }

  function bind3DControls(st) {
    var el = st.container;
    var down = false, lx = 0, ly = 0;
    el.addEventListener('mousedown', function (ev) {
      if (!mode3d || ev.button !== 0) return;
      down = true; lx = ev.clientX; ly = ev.clientY;
    });
    window.addEventListener('mousemove', function (ev) {
      if (!down || !mode3d) return;
      var dx = ev.clientX - lx, dy = ev.clientY - ly;
      lx = ev.clientX; ly = ev.clientY;
      st.theta += dx * 0.01;
      st.phi -= dy * 0.01;
      st.phi = Math.max(0.15, Math.min(Math.PI / 2 - 0.05, st.phi));
      updateCamera(st);
    });
    window.addEventListener('mouseup', function () { down = false; });
    el.addEventListener('wheel', function (ev) {
      if (!mode3d) return;
      ev.preventDefault();
      st.radius *= (ev.deltaY > 0 ? 1.1 : 1 / 1.1);
      st.radius = Math.max(1, Math.min(st.radius, 100000));
      updateCamera(st);
    }, { passive: false });
  }

  function ensureAnim() {
    if (animStarted || !threeState) return;
    animStarted = true;
    (function loop() {
      requestAnimationFrame(loop);
      if (threeState) threeState.renderer.render(threeState.scene, threeState.camera);
    })();
  }

  // ---------- init ----------
  buildData();
  buildSidebar();
  resize();
  fitView();
  draw();
})();
</script>
</body>
</html>
""";
    }
}
