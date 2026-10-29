/* FreeSlate slide show player (catalog: interactivity.player, transition.*, animation.*).
 * Reads the presentation data (#freeslate-data) and plays the slides of this document: navigation, transitions
 * and the slides' timing trees (PowerPoint's par/seq time containers and animation behaviours). */
(() => {
  'use strict';
  // A picture without its picture shows PowerPoint's sign; Office words it in its own language, here the viewer's.
  if (/^de\b/i.test(navigator.language || ''))
    document.querySelectorAll('.fs-nopic span').forEach(span => { span.textContent = 'Das Bild kann nicht angezeigt werden.'; });

  const dataElement = document.getElementById('freeslate-data');
  const slides = [...document.querySelectorAll('section.slide')];
  if (!dataElement || slides.length === 0) return;
  const deck = JSON.parse(dataElement.textContent);
  const W = parseFloat(slides[0].style.width) || slides[0].offsetWidth;
  const H = parseFloat(slides[0].style.height) || slides[0].offsetHeight;
  const info = i => deck.slides[i] || {};

  /* ---------------------------------------------------------------- formulas (tav values, fmla) */

  const formulaCache = new Map();
  /** Compiles a PowerPoint animation formula (#ppt_x, $, sin(), ^ …) into a function of its variables. */
  function formula(text) {
    if (typeof text === 'number') return () => text;
    let compiled = formulaCache.get(text);
    if (compiled) return compiled;
    const tokens = String(text).match(/#?ppt_[xywh]|\$|[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?|[a-z]+|[-+*/^(),]/gi) || [];
    let i = 0;
    const peek = () => tokens[i], next = () => tokens[i++];
    const functions = {
      abs: Math.abs, acos: Math.acos, asin: Math.asin, atan: Math.atan, ceil: Math.ceil, cos: Math.cos,
      cosh: Math.cosh, deg: x => x * 180 / Math.PI, exp: Math.exp, floor: Math.floor, ln: Math.log, max: Math.max,
      min: Math.min, rad: x => x * Math.PI / 180, rand: x => Math.random() * x, sin: Math.sin, sinh: Math.sinh,
      sqrt: Math.sqrt, tan: Math.tan, tanh: Math.tanh,
    };
    function primary() {
      const t = next();
      if (t === '(') { const v = expression(); next(); return v; }
      if (t === '-') { const v = power(); return s => -v(s); }
      if (t === '+') return power();
      if (t === '$') return s => s.$;
      // #ppt_x is the value before the animation, ppt_x the current one (exit effects start from it).
      if (t && t[0] === '#') { const key = t.slice(5).toLowerCase(); return s => s[key]; }
      if (t && /^ppt_[xywh]$/i.test(t)) { const key = t.toLowerCase(); return s => (s[key] !== undefined ? s[key] : s[key.slice(4)]); }
      if (t === 'pi') return () => Math.PI;
      if (t === 'e') return () => Math.E;
      if (t && functions[t.toLowerCase()]) {
        const f = functions[t.toLowerCase()];
        const args = [];
        if (peek() === '(') {
          next();
          while (peek() !== undefined && peek() !== ')') { args.push(expression()); if (peek() === ',') next(); }
          next();
        }
        return s => f(...args.map(a => a(s)));
      }
      const n = parseFloat(t);
      return () => (Number.isFinite(n) ? n : 0);
    }
    function power() {
      const base = primary();
      if (peek() === '^') { next(); const exponent = power(); return s => Math.pow(base(s), exponent(s)); }
      return base;
    }
    function term() {
      let left = power();
      while (peek() === '*' || peek() === '/') {
        const op = next(), right = power(), l = left;
        left = op === '*' ? s => l(s) * right(s) : s => l(s) / right(s);
      }
      return left;
    }
    function expression() {
      let left = term();
      while (peek() === '+' || peek() === '-') {
        const op = next(), right = term(), l = left;
        left = op === '+' ? s => l(s) + right(s) : s => l(s) - right(s);
      }
      return left;
    }
    try { compiled = expression(); } catch { compiled = () => 0; }
    formulaCache.set(text, compiled);
    return compiled;
  }

  /* ---------------------------------------------------------------- geometry helpers */

  const IDENTITY = [1, 0, 0, 1, 0, 0];
  function parseMatrix(transform) {
    const m = /matrix\(([^)]+)\)/.exec(transform || '');
    return m ? m[1].split(/[ ,]+/).map(Number) : IDENTITY.slice();
  }
  const multiply = (a, b) => [
    a[0] * b[0] + a[2] * b[1], a[1] * b[0] + a[3] * b[1],
    a[0] * b[2] + a[2] * b[3], a[1] * b[2] + a[3] * b[3],
    a[0] * b[4] + a[2] * b[5] + a[4], a[1] * b[4] + a[3] * b[5] + a[5]];
  const apply = (m, x, y) => [m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5]];
  const css = m => `matrix(${m.map(v => +v.toFixed(5)).join(',')})`;
  const translate = (x, y) => [1, 0, 0, 1, x, y];
  const rotate = deg => { const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r); return [c, s, -s, c, 0, 0]; };
  const scale = (x, y) => [x, 0, 0, y, 0, 0];

  /* ---------------------------------------------------------------- animation targets */

  /**
   * A target of behaviours: a shape (its div), its background, a paragraph (SVG groups inside the shape) or a chart's
   * part (several SVG elements acting as one: a series, a category, the background). Base values are PowerPoint's
   * ppt_x/ppt_y (center) and ppt_w/ppt_h, as fractions of the slide size.
   */
  class Target {
    constructor(element, svg) {
      this.els = Array.isArray(element) ? element : [element];
      this.el = this.els[0];
      this.svg = svg;   // true for SVG elements inside a shape
      if (svg) {
        const shape = this.el.closest('.sp');
        this.shapeMatrix = parseMatrix(shape ? shape.style.transform : '');
        this.matrix = IDENTITY.slice();
        // Several elements share the box around them all (PowerPoint wipes a series as one).
        let box = null;
        for (const e of this.els) {
          let b;
          try { b = e.getBBox(); } catch { continue; }
          if (b.width === 0 && b.height === 0) continue;
          box = box ? {
            x: Math.min(box.x, b.x), y: Math.min(box.y, b.y),
            width: Math.max(box.x + box.width, b.x + b.width) - Math.min(box.x, b.x),
            height: Math.max(box.y + box.height, b.y + b.height) - Math.min(box.y, b.y),
          } : { x: b.x, y: b.y, width: b.width, height: b.height };
        }
        box = box || { x: 0, y: 0, width: 0, height: 0 };
        this.local = box;
        const [cx, cy] = apply(this.shapeMatrix, box.x + box.width / 2, box.y + box.height / 2);
        this.base = { x: cx / W, y: cy / H, w: box.width / W, h: box.height / H };
      } else {
        // A shape inside a SmartArt frame is placed in the frame's coordinates.
        const host = element.parentElement && element.parentElement.closest('.sp');
        this.host = host ? parseMatrix(host.style.transform) : null;
        const own = parseMatrix(element.style.transform);
        this.matrix = this.host ? multiply(this.host, own) : own;
        this.originalTransform = element.style.transform;
        const w = parseFloat(element.style.width) || element.offsetWidth || 0;
        const h = parseFloat(element.style.height) || element.offsetHeight || 0;
        this.local = { x: 0, y: 0, width: w, height: h };
        const [cx, cy] = apply(this.matrix, w / 2, h / 2);
        this.base = { x: cx / W, y: cy / H, w: w * Math.hypot(this.matrix[0], this.matrix[1]) / W, h: h * Math.hypot(this.matrix[2], this.matrix[3]) / H };
      }
      this.applied = false;
    }

    fresh(hidden) {
      return {
        visible: !hidden, opacity: 1, x: this.base.x, y: this.base.y, w: this.base.w, h: this.base.h,
        rot: 0, sx: 1, sy: 1, mx: 0, my: 0, shx: 0, shy: 0, clip: null, fill: null, color: null, stroke: null, fontSize: 1,
        weight: null, fontStyle: null, underline: null, family: null, fillOn: false, strokeOn: true, fillOpacity: 1,
        strokeOpacity: 1, strokeWidth: null, gray: false, model: null,
      };
    }

    /** Writes a state to the element (only what differs from the original). */
    render(s) {
      const el = this.el;
      for (const e of this.els) {
        e.style.visibility = s.visible ? '' : 'hidden';
        e.style.opacity = s.opacity < 1 ? String(Math.max(0, s.opacity)) : '';
      }
      const b = this.base;
      const moved = Math.abs(s.x - b.x) > 1e-6 || Math.abs(s.y - b.y) > 1e-6 || Math.abs(s.w - b.w) > 1e-6 || Math.abs(s.h - b.h) > 1e-6
                    || s.rot !== 0 || s.sx !== 1 || s.sy !== 1 || s.mx !== 0 || s.my !== 0 || s.shx !== 0 || s.shy !== 0;
      // Shear (xshear, yshear: Light Speed leans the shape) about the center, before rotation.
      const shear = [1, s.shy || 0, s.shx || 0, 1, 0, 0];
      // Text in another formatting shows its drawing in it; a size it was not drawn in is scaled.
      const sized = this.renderFormatting(s);
      if (this.svg) {
        // A font size relative to the text's own (Grow with color: 1.5) scales text drawn on its own about its
        // middle; a whole shape keeps its size, and so does its background (an iterated effect's first pass).
        const font = this.el.closest('[data-tx]') && !sized ? s.fontSize || 1 : 1;
        if (moved || font !== 1) {
          // Slide-space change around the paragraph's center, expressed in the shape's coordinates.
          const [lx, ly] = [this.local.x + this.local.width / 2, this.local.y + this.local.height / 2];
          const kx = (b.w > 0 ? s.w / b.w : 1) * s.sx * font, ky = (b.h > 0 ? s.h / b.h : 1) * s.sy * font;
          const shape = this.shapeMatrix;
          const inverse = invert(shape);
          const slideCenter = apply(shape, lx, ly);
          const target = [(s.x + s.mx) * W, (s.y + s.my) * H];
          const change = multiply(translate(target[0], target[1]), multiply(rotate(s.rot), multiply(shear, multiply(scale(kx, ky), translate(-slideCenter[0], -slideCenter[1])))));
          const transform = css(multiply(inverse, multiply(change, shape)));
          for (const e of this.els) e.setAttribute('transform', transform);
        } else {
          for (const e of this.els) e.removeAttribute('transform');
        }
        this.renderMask(s.clip);
      } else {
        if (moved) {
          const w = this.local.width, h = this.local.height;
          const [cx, cy] = apply(this.matrix, w / 2, h / 2);
          const kx = (b.w > 0 ? s.w / b.w : 1) * s.sx, ky = (b.h > 0 ? s.h / b.h : 1) * s.sy;
          const change = multiply(translate((s.x + s.mx) * W, (s.y + s.my) * H), multiply(rotate(s.rot), multiply(shear, multiply(scale(kx, ky), translate(-cx, -cy)))));
          const placed = multiply(change, this.matrix);
          el.style.transform = css(this.host ? multiply(invert(this.host), placed) : placed);
        } else {
          el.style.transform = this.originalTransform;
        }
        this.renderMask(s.clip);
        this.renderTextScale(sized ? 1 : s.fontSize || 1);
      }
      this.renderPaint(s);
      this.renderModel(s.model);
    }

    /**
     * Shows a 3D model's embedded animation (catalog: animation.pictures) at its state, on the model's canvas over
     * PowerPoint's picture of it (the poster frame), which shows again without one.
     */
    renderModel(model) {
      const key = model ? `${model.anim}|${model.value}` : '';
      if (key === (this.modelKey || '')) return;
      this.modelKey = key;
      const canvas = this.svg ? null : this.el.querySelector('canvas.fs-model');
      if (!canvas) return;
      if (model) drawModel(canvas, model.anim, model.value);
      else { canvas.classList.remove('playing'); this.el.classList.remove('fs-modeled'); }
    }

    /**
     * Shows the target's text in the formatting its state asks for (catalog: animation.behaviors): bold, italic,
     * underlined, in another font or size, as the converter drew it for every paragraph or letter (data-v groups),
     * the nearest drawing when that one was not drawn. Returns whether the drawing has the state's font size.
     */
    renderFormatting(s) {
      const wanted = [s.weight, s.fontStyle, s.underline, s.family].map(v => v || '');
      const key = wanted.join('|') + '|' + (s.fontSize !== 1 ? s.fontSize : '');
      if (key === (this.formatting || '||||')) return this.formattingSized || false;
      this.formatting = key;
      if (!this.variants) this.variants = this.all('[data-v]');
      if (!this.variants.length) return (this.formattingSized = false);
      // The drawing that has the most of what is asked and nothing else.
      let chosen = '', best = 0, sized = false;
      for (const v of new Set(this.variants.map(g => g.dataset.v).filter(v => v))) {
        const parts = v.split('|');
        let score = 0, fits = true;
        for (let i = 0; i < 4 && fits; i++) {
          if (parts[i] === wanted[i]) score += parts[i] ? 1 : 0;
          else if (parts[i]) fits = false;
        }
        const size = parts[4] ? Number(parts[4]) : 1;
        if (size !== 1) { if (Math.abs(size - s.fontSize) < 1e-6) score += 1; else fits = false; }
        if (fits && score > best) { chosen = v; best = score; sized = size !== 1; }
      }
      for (const g of this.variants) {
        if (g.dataset.v !== chosen) { g.setAttribute('visibility', 'hidden'); continue; }
        g.removeAttribute('visibility');
        if (g.hasAttribute('data-align') && !g.dataset.aligned) alignVariant(g);
      }
      return (this.formattingSized = sized);
    }

    /** Scales a shape's text about its anchor (style.fontSize of a whole shape: Change Font Size). */
    renderTextScale(f) {
      if (f === (this.textScale || 1)) return;
      this.textScale = f;
      for (const g of this.all('g[data-tx]')) {
        if (g.dataset.transform === undefined) g.dataset.transform = g.getAttribute('transform') || '';
        if (f === 1) {
          if (g.dataset.transform) g.setAttribute('transform', g.dataset.transform); else g.removeAttribute('transform');
          continue;
        }
        let [x, y] = (g.dataset.anchor || '').split(' ').map(Number);
        if (!Number.isFinite(x) || !Number.isFinite(y)) {
          try { const b = g.getBBox(); [x, y] = [b.x + b.width / 2, b.y + b.height / 2]; } catch { [x, y] = [0, 0]; }
        }
        g.setAttribute('transform', `${g.dataset.transform} translate(${x} ${y}) scale(${f}) translate(${-x} ${-y})`.trim());
      }
    }

    /**
     * Shows a filter (catalog: animation.behaviors) as an SVG mask on the target's drawing, over the bounds it draws
     * with its outline (PowerPoint's blinds divide the line too, measured) and without cutting off what reaches past
     * its box; a target that has no drawing of its own (a group) gets it as a CSS mask image over its box.
     */
    renderMask(clip) {
      const key = clip ? `${clip.filter}|${clip.out}|${clip.p.toFixed(5)}` : '';
      if (key === (this.maskKey || '')) return;
      this.maskKey = key;
      if (this.maskHosts === undefined) this.maskHosts = this.findMaskHosts();
      const ns = 'http://www.w3.org/2000/svg';
      if (!this.maskHosts) {
        const box = { x: 0, y: 0, width: this.local.width, height: this.local.height };
        const markup = clip && effectMask(clip.filter, clip.p, clip.out, 'm', box);
        const image = markup && `<svg xmlns="${ns}" width="${box.width}" height="${box.height}" viewBox="0 0 ${box.width} ${box.height}" preserveAspectRatio="none">${markup}</svg>`;
        Object.assign(this.el.style, image
          ? { maskImage: `url("data:image/svg+xml,${encodeURIComponent(image)}")`, maskSize: '100% 100%', maskRepeat: 'no-repeat' }
          : { maskImage: '', maskSize: '', maskRepeat: '' });
        return;
      }
      for (const host of this.maskHosts) {
        const markup = clip && host.box.width > 0 && effectMask(clip.filter, clip.p, clip.out, host.id, host.box);
        if (!markup) { for (const e of host.els) e.removeAttribute('mask'); continue; }
        if (!host.mask) {
          host.mask = document.createElementNS(ns, 'mask');
          host.mask.id = host.id;
          // A pixel of room around the bounds keeps their antialiased edge.
          const { x, y, width, height } = host.box;
          for (const [k, v] of [['maskUnits', 'userSpaceOnUse'], ['x', x - 1], ['y', y - 1], ['width', width + 2], ['height', height + 2]]) host.mask.setAttribute(k, v);
          host.svg.appendChild(host.mask);
        }
        host.mask.innerHTML = markup;
        for (const e of host.els) e.setAttribute('mask', `url(#${host.id})`);
      }
    }

    /** The SVG elements a filter's mask goes on, by SVG element drawing them, with the bounds they draw (or null). */
    findMaskHosts() {
      const id = () => 'fs-mask-' + Math.random().toString(36).slice(2);
      if (this.svg) return this.el.ownerSVGElement ? [{ svg: this.el.ownerSVGElement, els: this.els, box: drawnBox(this.els), id: id() }] : null;
      const svgs = [...this.el.children];
      if (!svgs.length || svgs.some(e => e.tagName.toLowerCase() !== 'svg')) return null;
      return svgs.map(svg => {
        const els = [...svg.children].filter(e => !['defs', 'mask', 'clipPath', 'style', 'title'].includes(e.tagName));
        return { svg, els, box: drawnBox(els), id: id() };
      });
    }

    /** The elements matching a selector inside the target's elements. */
    all(selector) {
      return this.els.flatMap(e => [...e.querySelectorAll(selector)]);
    }

    renderPaint(s) {
      const fill = `${s.fill}|${s.fillOn}`;
      if (fill !== this.lastFill) {
        this.lastFill = fill;
        // A shape without fill gets one while an effect turns it on (fill.on; its fillable paths are marked).
        this.all(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => {
          if (p.getAttribute('fill') !== 'none') p.style.fill = s.fill || '';
          else if (p.hasAttribute('data-fillable')) p.style.fill = s.fillOn && s.fill ? s.fill : '';
        });
      }
      if (s.stroke !== this.lastStroke) {
        this.lastStroke = s.stroke;
        this.all(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => {
          if (p.getAttribute('stroke') && p.getAttribute('stroke') !== 'none') p.style.stroke = s.stroke || '';
        });
      }
      // The legacy paint attributes: fill and line opacity, line weight (points), the line off, a gray picture.
      const extra = `${s.fillOpacity}|${s.strokeOpacity}|${s.strokeWidth}|${s.strokeOn}|${s.gray}`;
      if (extra !== (this.lastExtra || '1|1|null|true|false')) {
        this.lastExtra = extra;
        this.all(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => {
          p.style.fillOpacity = s.fillOpacity !== undefined && s.fillOpacity !== 1 ? String(Math.max(0, s.fillOpacity)) : '';
          p.style.strokeOpacity = s.strokeOpacity !== undefined && s.strokeOpacity !== 1 ? String(Math.max(0, s.strokeOpacity)) : '';
          p.style.strokeWidth = typeof s.strokeWidth === 'number' && Number.isFinite(s.strokeWidth) ? `${s.strokeWidth * 96 / 72}px` : '';
          p.style.visibility = s.strokeOn === false && p.getAttribute('fill') === 'none' ? 'hidden' : '';
          if (s.strokeOn === false && p.getAttribute('fill') !== 'none') p.style.stroke = 'none';
          else if (p.style.stroke === 'none') p.style.stroke = s.stroke || '';
        });
        for (const e of this.els) if (e.style) e.style.filter = s.gray ? 'grayscale(1)' : '';
      }
      if (s.color !== this.lastColor) {
        this.lastColor = s.color;
        this.all('[data-tx] g[fill], [data-tx] text:not(.sel), [data-tx] rect').forEach(t => { t.style.fill = s.color || ''; });
      }
    }

    reset() {
      for (const el of this.els) {
        el.style.visibility = el.style.opacity = '';
        if (this.svg) el.removeAttribute('transform');
        else el.style.transform = this.originalTransform;
      }
      this.renderMask(null);
      this.renderPaint({ fill: null, stroke: null, color: null });
      this.renderModel(null);
    }
  }

  /**
   * The 3D models of the slides (catalog: picture.model3d), by canvas: read and loaded when first drawn, by the WebGL
   * renderer (model3d.js) that documents with models carry. A model's embedded animations play by their number
   * (attribute embeddedN: animation N - 1 of the glTF file) from 0 to 1 over its whole length (measured).
   */
  const models = new WeakMap();
  function drawModel(canvas, anim, value) {
    if (!window.FreeSlateModel3D) return;
    let entry = models.get(canvas);
    if (!entry) {
      entry = { model: null, pose: null };
      models.set(canvas, entry);
      try {
        const raw = atob(canvas.dataset.glb || ''), bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        const model = new window.FreeSlateModel3D.Model(canvas, bytes, JSON.parse(canvas.dataset.model || '{}'));
        model.load().then(() => { entry.model = model; if (entry.pose) paintModel(canvas, entry); }, () => {});
      } catch { return; }
    }
    entry.pose = { anim, value };
    if (entry.model) paintModel(canvas, entry);
  }
  function paintModel(canvas, entry) {
    const animations = entry.model.gltf.json.animations || [];
    const { anim, value } = entry.pose;
    if (anim < 0 || anim >= animations.length) return;
    // The canvas draws as many pixels as it shows (the show scales the slide).
    const box = canvas.getBoundingClientRect(), ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.min(4096, Math.round(box.width * ratio))), height = Math.max(1, Math.min(4096, Math.round(box.height * ratio)));
    if (Math.abs(canvas.width - width) > 2 || Math.abs(canvas.height - height) > 2) { canvas.width = width; canvas.height = height; }
    const clip = entry.model.clip(anim);
    entry.model.draw(clip.index, Math.min(1, Math.max(0, value)) * clip.length);
    // The model replaces PowerPoint's picture of it while it plays.
    canvas.classList.add('playing');
    canvas.parentElement.classList.add('fs-modeled');
  }

  function invert(m) {
    const det = m[0] * m[3] - m[1] * m[2] || 1;
    return [m[3] / det, -m[1] / det, -m[2] / det, m[0] / det,
      (m[2] * m[5] - m[3] * m[4]) / det, (m[1] * m[4] - m[0] * m[5]) / det];
  }

  /**
   * The letters or words of a shape's text (or of its paragraph range) in reading order, each a list of the
   * elements drawing it: the renderer draws text that is built letter by letter character by character (data-c,
   * data-w the word; blanks are no units, measured).
   */
  function textUnits(shape, target, type) {
    let scope = [shape];
    if (target.pRg) {
      scope = [];
      for (let p = target.pRg[0]; p <= target.pRg[1]; p++) { const g = shape.querySelector(`g[data-p="${p}"]`); if (g) scope.push(g); }
    }
    const letters = scope.flatMap(s => [...s.querySelectorAll('[data-c]')]);
    if (type === 'lt') return letters.map(e => [e]);
    const words = new Map();
    for (const e of letters) {
      if (e.dataset.w === '-1') continue;
      if (!words.has(e.dataset.w)) words.set(e.dataset.w, []);
      words.get(e.dataset.w).push(e);
    }
    return [...words.values()];
  }

  /** The targets of a behaviour's tgtEl on a slide (several for paragraph ranges); unit: an iterated effect's part. */
  function resolveTargets(slide, target, cache, unit) {
    if (!target || target.spid === undefined) return [];
    const shape = slide.querySelector(`[data-id="${target.spid}"]`);
    if (!shape) return [];
    const keys = [];
    if (unit && unit.type === 'bg') {
      keys.push([`${target.spid}:bg`, () => shape.querySelector('g[data-bg]'), true]);
    } else if (unit) {
      const range = target.pRg ? target.pRg.join('-') : '';
      keys.push([`${target.spid}:${range}:${unit.type}${unit.index}`, () => textUnits(shape, target, unit.type)[unit.index] || null, true]);
    } else if (target.charRg && shape.querySelector('[data-c]')) {
      // A character range (p:charRg; paragraph ends count as one character): the characters drawn on their own.
      const [first, end] = target.charRg;
      keys.push([`${target.spid}:c${first}-${end}`, () => {
        const found = [...shape.querySelectorAll('[data-c]')].filter(e => +e.dataset.c >= first && +e.dataset.c < end);
        return found.length ? found : null;
      }, true]);
    } else if (target.pRg) {
      for (let p = target.pRg[0]; p <= target.pRg[1]; p++) keys.push([`${target.spid}:p${p}`, () => shape.querySelector(`g[data-p="${p}"]`), true]);
    } else if (target.graphic && target.graphic.kind === 'dgm') {
      // A shape of a SmartArt by its model id, or the diagram's background and outline (bldStep bg).
      if (target.graphic.bldStep === 'bg') {
        shape.querySelectorAll(':scope > [data-dgm-bg]').forEach((el, i) => keys.push([`${target.spid}:bg${i}`, () => el, false]));
      } else {
        const id = String(target.graphic.id);
        keys.push([`${target.spid}:${id}`, () => Array.from(shape.querySelectorAll('[data-mid]')).find(e => e.dataset.mid === id), false]);
      }
    } else if (target.graphic && target.graphic.kind === 'chart') {
      // A chart's background (gridLegend), series, category or point (catalog: animation.build): the chart marks
      // its parts with data-ser and data-pt; everything else is background. Parts of a whole series (areas,
      // trendlines) build with the series' first category.
      const g = target.graphic, step = g.bldStep;
      const [ser, cat] = [String(g.seriesIdx), String(g.categoryIdx)];
      keys.push([`${target.spid}:chart:${step}:${ser}:${cat}`, () => {
        const svg = shape.querySelector(':scope > svg');
        if (!svg) return null;
        const parts = [...svg.querySelectorAll('[data-ser]')];
        const whole = e => e.dataset.pt === undefined && cat === '0';
        const found = step === 'gridLegend'
          ? [...svg.children].filter(e => !e.hasAttribute('data-ser') && !['defs', 'clipPath', 'mask'].includes(e.tagName))
          : step === 'series' ? parts.filter(e => e.dataset.ser === ser)
          : step === 'category' ? parts.filter(e => e.dataset.pt === cat || whole(e))
          : step === 'ptInSeries' || step === 'ptInCategory' ? parts.filter(e => e.dataset.ser === ser && (e.dataset.pt === cat || whole(e)))
          : parts;
        return found.length ? found : null;
      }, true]);
    } else if (target.bg) {
      keys.push([`${target.spid}:bg`, () => shape.querySelector('g[data-bg]'), true]);
    } else {
      keys.push([String(target.spid), () => shape, false]);
    }
    const result = [];
    for (const [key, find, svg] of keys) {
      if (!cache.has(key)) {
        const el = find();
        cache.set(key, el ? new Target(el, svg) : null);
      }
      const t = cache.get(key);
      if (t) result.push({ key, target: t });
    }
    return result;
  }

  /* ---------------------------------------------------------------- timing tree */

  const delayOf = node => {
    const conditions = node.begin || [];
    let delay = 0;
    for (const c of conditions) if (typeof c.delay === 'number' && c.evt === undefined) delay = c.delay;
    return delay;
  };
  const isIndefinite = node => (node.begin || []).some(c => c.delay === 'indefinite');
  const isAuto = node => (node.begin || []).some(c => c.evt === 'onBegin' || (c.evt === undefined && typeof c.delay === 'number'));

  /**
   * Schedules a time node: collects its behaviours with start times (ms, relative to the step) and returns the
   * node's end. par children start with the par, seq children one after another.
   */
  function schedule(node, start, out, context) {
    // PowerPoint's video starts time nodes on its frames only (context.tick, ms; measured).
    const begin = context.tick ? Math.ceil((start + delayOf(node)) / context.tick - 1e-6) * context.tick : start + delayOf(node);
    const speed = node.spd ? node.spd / 100000 : 1;
    const children = node.children || [];
    const first = out.length;
    let end = begin;
    if (node.t === 'par' || node.t === 'excl') {
      const inner = Object.assign({}, context, inherit(node, context));
      const plan = node.iterate && context.unitsOf ? context.unitsOf(node) : null;
      if (plan) {
        end = iterate(node, plan, begin, out, inner);
      } else {
        let previous = begin, firstEnd = Infinity;
        const played = [];
        for (const child of children) {
          // A group after the previous effect starts when the groups before it end, as PowerPoint plays it: the
          // delay it keeps is what the editor reckoned, which text built letter by letter outlasts (measured).
          const after = child.t === 'par' && (child.children || []).some(c => c.nodeType === 'afterEffect');
          const from = out.length;
          const childEnd = after
            ? schedule(Object.assign({}, child, { begin: [] }), Math.max(begin + delayOf(child), previous), out, inner)
            : schedule(child, begin, out, inner);
          played.push({ from, to: out.length, at: out.length > from ? Math.min(...out.slice(from).map(b => b.start)) : childEnd });
          previous = Math.max(previous, childEnd);
          end = Math.max(end, childEnd);
          firstEnd = Math.min(firstEnd, childEnd);
        }
        // An exclusive container (SMIL excl) plays one child at a time: one that begins stops the one playing.
        if (node.t === 'excl') {
          for (const a of played) {
            const next = Math.min(...played.filter(b => b.at > a.at).map(b => b.at));
            if (Number.isFinite(next)) for (let i = a.from; i < a.to; i++) out[i].until = Math.min(out[i].until ?? Infinity, next);
          }
        }
        // A container ends with its last child (endSync all, PowerPoint's), with endSync first with its first.
        if (node.endSync && node.endSync.rtn === 'first' && Number.isFinite(firstEnd)) end = firstEnd;
      }
      if (typeof node.dur === 'number') end = begin + node.dur / speed;
      end = repeatChildren(node, begin, end, first, out, inner);
      if (node.presetClass !== undefined && context.effects) context.effects.push({ node, begin, end, context });
      // An effect that rewinds when done (fill="remove", catalog: animation.timing) takes its behaviours back then.
      if (node.presetClass !== undefined && node.fill === 'remove') for (let i = first; i < out.length; i++) out[i].until = end;
    } else if (node.t === 'seq') {
      let t = begin;
      for (const child of children) t = schedule(child, t, out, Object.assign({}, context, inherit(node, context)));
      end = t;
    } else {
      const dur = typeof node.dur === 'number' ? node.dur / speed : (node.t === 'set' ? 1 : 0);
      const repeat = node.repeatCount === 'indefinite' ? 1 : (typeof node.repeatCount === 'number' ? node.repeatCount / 1000 : 1);
      const total = dur * repeat * (node.autoRev ? 2 : 1);
      out.push({ node, start: begin, dur: Math.max(dur, 1), repeat, total: Math.max(total, 1), context });
      end = begin + total;
    }
    return end;
  }
  /**
   * An effect built letter by letter or word by word (p:iterate): the shape's background first when the build
   * animates it (bldP animBg), for the effect's own duration, then every unit with the whole effect, each the
   * interval later than the one before (tmAbs, or tmPct of the effect's duration); measured with PowerPoint's video.
   */
  function iterate(node, plan, begin, out, context) {
    const children = node.children || [];
    let span = 0;
    for (const child of children) span = Math.max(span, schedule(child, begin, [], context) - begin);
    const it = node.iterate;
    const interval = typeof it.abs === 'number' ? it.abs : (it.pct || 0) / 100000 * span;
    let at = begin;
    if (plan.background) {
      for (const child of children) schedule(child, at, out, Object.assign({}, context, { unit: { type: 'bg' } }));
      at += span;
    }
    for (let k = 0; k < plan.units; k++) {
      const index = it.backwards ? plan.units - 1 - k : k;
      for (const child of children) schedule(child, at + k * interval, out, Object.assign({}, context, { unit: { type: it.type, index } }));
    }
    return at + (plan.units - 1) * interval + span;
  }

  /**
   * Repeats of a container (repeatCount in thousandths, repeatDur, "indefinite"; Spin twice): its behaviours scheduled
   * again one pass after the other, the last pass of a fractional count stopped where it ends; an indefinite repeat
   * cycles its behaviours (until the next step) and counts as one pass. Returns the container's end.
   */
  function repeatChildren(node, begin, end, first, out, context) {
    const once = end - begin;
    const count = node.repeatCount === 'indefinite' || node.repeatDur === 'indefinite' ? Infinity
      : typeof node.repeatCount === 'number' ? node.repeatCount / 1000
      : typeof node.repeatDur === 'number' && once > 0 ? node.repeatDur / once : 1;
    if (!(count > 1) || once <= 0) return end;
    const behaviors = out.slice(first);
    if (count === Infinity) {
      // Until the next click (endCondLst onNext) or the end of the slide.
      const untilNext = (node.end || []).some(c => c.evt === 'onNext');
      for (const b of behaviors) b.loop = { start: begin, period: once, untilNext };
      return end;
    }
    const stop = begin + once * count;
    for (let k = 1; k < Math.ceil(count); k++) {
      for (const b of behaviors) {
        const copy = Object.assign({}, b, { start: b.start + k * once });
        if (k + 1 > count) copy.stop = stop;
        out.push(copy);
      }
    }
    return stop;
  }

  /** The first target of the behaviours under a time node. */
  function firstTarget(node) {
    if (node.target && node.target.spid !== undefined) return node.target;
    for (const child of node.children || []) { const t = firstTarget(child); if (t) return t; }
    return null;
  }

  function inherit(node, context) {
    const result = context.tick ? { tick: context.tick } : {};
    if (context.unitsOf) result.unitsOf = context.unitsOf;
    if (context.effects) result.effects = context.effects;
    if (context.unit) result.unit = context.unit;
    if (node.presetClass) result.presetClass = node.presetClass;
    if (node.presetID !== undefined) result.presetID = node.presetID;
    if (node.accel || node.decel) result.ease = { accel: (node.accel || 0) / 100000, decel: (node.decel || 0) / 100000 };
    return result;
  }

  /** Steps of the main sequence and of the interactive sequences (triggers) of a slide; tick: see schedule. */
  function buildTimeline(slide, timing, tick) {
    const result = { steps: [], triggers: new Map(), hidden: new Set(), targets: new Map() };
    if (!timing || !timing.root) return result;
    // Text an effect builds letter by letter or word by word: its units, and whether the shape's background goes first.
    const unitsOf = node => {
      // By element (type el, the default): the target as a whole.
      if (node.iterate.type !== 'lt' && node.iterate.type !== 'wd') return null;
      const target = firstTarget(node);
      const shape = target && !target.graphic ? slide.querySelector(`[data-id="${target.spid}"]`) : null;
      const units = shape ? textUnits(shape, target, node.iterate.type).length : 0;
      if (!units) return null;
      const build = (timing.builds || []).find(b => b.t === 'bldP' && String(b.spid) === String(target.spid));
      // The shape's own fill and line go first: for entrances and exits when the build animates the background
      // (bldP animBg), for emphases always (Grow with color turns the fill before the letters; measured).
      const background = [...shape.querySelectorAll('g[data-bg] path')].some(p =>
        (p.getAttribute('fill') || 'none') !== 'none' || (p.getAttribute('stroke') || 'none') !== 'none');
      return { units, background: !target.pRg && background && (node.presetClass === 'emph' || !!(build && build.animBg)) };
    };
    for (const sequence of timing.root.children || []) {
      if (sequence.t !== 'seq') continue;
      const steps = (sequence.children || []).map(stepNode => {
        const behaviors = [];
        const context = Object.assign(tick ? { tick } : {}, { unitsOf, effects: [] });
        const end = schedule(Object.assign({}, stepNode, { begin: [] }), 0, behaviors, context);
        return { behaviors, end, auto: !isIndefinite(stepNode) || isAuto(stepNode), effects: context.effects };
      });
      // After effects and sounds (p:subTnLst): those of the same click when their effect begins or ends (as their
      // condition says; a sound with its effect, hiding after it), the others (dim, hide on the next click) when the
      // next effect of the sequence begins, even one after the previous (measured).
      const effects = steps.flatMap(step => step.effects.map(e => Object.assign({ step }, e)));
      effects.forEach((e, i) => {
        for (const sub of e.node.sub || []) {
          const cond = (sub.begin || []).find(c => c.evt === 'begin' || c.evt === 'end');
          const delay = cond && typeof cond.delay === 'number' ? cond.delay : 0;
          const at = sub.masterRel === 'nextClick' ? effects[i + 1]
            : { step: e.step, begin: (cond && cond.evt === 'begin' ? e.begin : e.end) + delay, context: e.context };
          if (at) schedule(Object.assign({}, sub, { begin: [] }), at.begin, at.step.behaviors, Object.assign({}, at.context, { unit: undefined }));
        }
      });
      for (const step of steps) {
        for (const b of step.behaviors) b.targets = resolveTargets(slide, b.node.target, result.targets, b.context.unit);
        for (const b of step.behaviors) {
          if (b.node.t !== 'animScale' || !b.node.to || b.node.from) continue;
          const before = step.behaviors.find(o => o !== b && o.node.t === 'animScale' && o.node.to && Math.abs(o.start + o.total - b.start) < 1
            && o.targets.length && o.targets[0].key === (b.targets[0] || {}).key);
          if (before) b.chainedFrom = before.node.to;
        }
      }
      if (sequence.nodeType === 'interactiveSeq') {
        // Triggers: a click on a shape, or playback reaching a media bookmark (key bmk:spid:name).
        const trigger = (sequence.begin || []).find(c => (c.evt === 'onClick' || c.evt === 'onMediaBookmark') && c.target);
        if (trigger) result.triggers.set(trigger.target.bmk !== undefined ? `bmk:${trigger.target.spid}:${trigger.target.bmk}` : String(trigger.target.spid), { steps, played: 0, restart: sequence.restart });
      } else {
        result.steps.push(...steps);
      }
    }
    // Targets that become visible through their first visibility behaviour start hidden (entrance effects).
    const seen = new Set();
    const all = result.steps.concat(...[...result.triggers.values()].map(t => t.steps));
    for (const step of all) {
      const ordered = step.behaviors.slice().sort((a, b) => a.start - b.start);
      for (const b of ordered) {
        if (b.node.t !== 'set' || !(b.node.attrs || []).includes('style.visibility')) continue;
        for (const { key } of b.targets) {
          if (seen.has(key)) continue;
          seen.add(key);
          if (b.node.to === 'visible') result.hidden.add(key);
        }
      }
    }
    return result;
  }

  /* ---------------------------------------------------------------- behaviour evaluation */

  /**
   * Acceleration and deceleration (accel, decel: fractions of the duration) as PowerPoint plays them: the speed rises
   * from 0 along a smoothstep curve, cruises, and falls along one again (not linearly as in SMIL; measured with its
   * video: under 1.5 px off along 640 px for six combinations).
   */
  function ease(p, e) {
    if (!e) return p;
    const a = Math.min(e.accel, 1), d = Math.min(e.decel, 1 - a);
    if (a + d <= 0) return p;
    const v = 1 / (1 - (a + d) / 2);
    const rise = x => x * x * x - x * x * x * x / 2;   // the distance covered while the speed follows smoothstep
    if (p < a) return v * a * rise(p / a);
    if (p <= 1 - d) return v * (a / 2 + p - a);
    return 1 - v * d * rise((1 - p) / d);
  }

  /** Progress (0…1) of a behaviour at time t (ms in its step), or null before its start or after a removed end. */
  function progress(b, t) {
    if (b.loop && Number.isFinite(t) && t >= b.loop.start) t = b.loop.start + (t - b.loop.start) % b.loop.period;
    if (b.stop !== undefined) t = Math.min(t, b.stop);
    if (t < b.start || (b.until !== undefined && t >= b.until)) return null;
    const node = b.node;
    const local = t - b.start;
    if (local >= b.total) {
      // Without fill (or fill="remove") a behaviour's effect ends with it, a set's too when it has a duration of
      // its own (Transparency); a set without one holds its value (SMIL: fill auto freezes elements without dur).
      if ((node.fill === undefined || node.fill === 'remove') && (node.t !== 'set' || typeof node.dur === 'number')) return null;
      // The end through the time filter too (Flash bulb's fade comes back to 0).
      const end = ease(node.autoRev ? 0 : 1, b.context.ease);
      return node.tmFilter ? timeFilter(node.tmFilter, end) : end;
    }
    let cycle = local / b.dur;
    let p = cycle - Math.floor(cycle);
    if (node.autoRev) { const half = local / b.dur % 2; p = half > 1 ? 2 - half : half; }
    const easing = node.accel || node.decel ? { accel: (node.accel || 0) / 100000, decel: (node.decel || 0) / 100000 } : b.context.ease;
    const shaped = q => { const e = ease(Math.min(1, q), easing); return node.tmFilter ? timeFilter(node.tmFilter, e) : e; };
    return node.bounceEnd ? bounceEnd(p * b.dur, b.dur, node.bounceEnd / 100000, shaped) : shaped(p);
  }

  /**
   * A bounce at the end (p14:bounceEnd: the share b of the duration it takes; "a mass-spring-damper system"): the
   * behaviour runs in the rest of the duration, then swings about its end with the speed it arrived with, damped.
   * Measured with PowerPoint's video for b of 25, 50 and 75 % and durations of 1 and 2 s (its 60 fps videos run 4 %
   * slow; under 1.1 px off): damping and frequency times the bounce's time are 5.33 + 2.48 (b - ½) - 1.92 (b - ½)²
   * and 12.35 + 12.26 (b - ½) - 13.04 (b - ½)².
   */
  function bounceEnd(local, dur, b, shaped) {
    const reach = (1 - b) * dur;
    if (local <= reach) return shaped(local / reach);
    const speed = (shaped(1) - shaped(1 - 1e-3)) / 1e-3 / reach;   // per ms
    const left = Math.max(1, dur - reach), d = b - 0.5;
    const damping = (5.33 + 2.48 * d - 1.92 * d * d) / left, frequency = (12.35 + 12.26 * d - 13.04 * d * d) / left;
    const s = local - reach;
    return shaped(1) + speed / frequency * Math.exp(-damping * s) * Math.sin(frequency * s);
  }

  /** A time filter (tmFilter "t,p; t,p; …"): progress remapped along the polyline through its points (Bounce). */
  const filterCache = new Map();
  function timeFilter(text, p) {
    let points = filterCache.get(text);
    if (!points) {
      points = String(text).split(';').map(s => s.split(',').map(Number)).filter(a => a.length === 2 && a.every(Number.isFinite));
      filterCache.set(text, points);
    }
    if (points.length < 2) return p;
    for (let i = 1; i < points.length; i++) {
      const [t0, v0] = points[i - 1], [t1, v1] = points[i];
      if (p <= t1 || i === points.length - 1) return t1 > t0 ? v0 + (v1 - v0) * Math.min(1, Math.max(0, (p - t0) / (t1 - t0))) : v1;
    }
    return p;
  }

  const attributeKey = attribute => /^embedded\d+$/.test(attribute) ? 'model' : ({
    ppt_x: 'x', ppt_y: 'y', ppt_w: 'w', ppt_h: 'h', r: 'rot', 'style.rotation': 'rot', 'style.opacity': 'opacity',
    'style.visibility': 'visible', fillcolor: 'fill', 'fill.color': 'fill', 'stroke.color': 'stroke', 'style.color': 'color',
    'style.fontSize': 'fontSize', xshear: 'shx', yshear: 'shy', 'style.fontWeight': 'weight', 'style.fontStyle': 'fontStyle',
    'style.textDecorationUnderline': 'underline', 'style.fontFamily': 'family', fillColor: 'fill', 'fill.on': 'fillOn',
    'stroke.on': 'strokeOn', 'fill.opacity': 'fillOpacity', 'stroke.opacity': 'strokeOpacity', 'stroke.weight': 'strokeWidth',
    ppt_r: 'rot', 'imageData.grayscale': 'gray',
  })[attribute];

  function hexColor(c) {
    if (typeof c === 'string') return c;
    if (!c || typeof c !== 'object') return null;
    if ('r' in c) return `rgb(${c.r},${c.g},${c.b})`;
    return null;
  }
  function mixColor(a, b, p) {
    const pa = parseColor(a), pb = parseColor(b);
    if (!pa || !pb) return p < 1 ? a : b;
    const v = pa.map((x, i) => Math.round(x + (pb[i] - x) * p));
    return `rgb(${v[0]},${v[1]},${v[2]})`;
  }
  function parseColor(c) {
    if (!c) return null;
    let m = /^#([0-9a-f]{6})$/i.exec(c);
    if (m) return [0, 2, 4].map(i => parseInt(m[1].substr(i, 2), 16));
    m = /^rgb\((\d+),(\d+),(\d+)\)$/.exec(c);
    return m ? [+m[1], +m[2], +m[3]] : null;
  }

  /** Whether a behaviour's formulas take the current values (ppt_x, not #ppt_x: Fly Out, Bounce Out). */
  function takesCurrent(b) {
    if (b.takesCurrent === undefined) {
      const n = b.node;
      b.takesCurrent = n.t === 'anim' && /(^|[^#a-z_])ppt_[xywh]/i.test(JSON.stringify([n.tav, n.from, n.to, n.by]));
    }
    return b.takesCurrent;
  }

  /**
   * The states of a behaviour's targets when it begins (by key), which its formulas' current values are: PowerPoint
   * takes them then. Behaviours of the step that began before it count as they are at that moment, one that ends as
   * it begins with its end (Bounce Out's fall and bounces build on each other, each ending as the next begins).
   */
  function beganWith(step, b, before) {
    if (b.begun) return b.begun;
    const states = new Map();
    for (const { key } of b.targets) states.set(key, Object.assign({}, before && before.get(key)));
    const index = step.behaviors.indexOf(b);
    step.behaviors.forEach((o, i) => {
      if (o === b || o.start > b.start || (o.start === b.start && i > index)) return;
      const end = o.start + o.total;
      const p = progress(o, end <= b.start && end > b.start - 1 ? end - 1e-3 : b.start);
      if (p === null) return;
      const begun = takesCurrent(o) ? beganWith(step, o, before) : null;
      for (const { key, target } of o.targets) if (states.has(key)) evaluate(o, p, target, states.get(key), begun && begun.get(key));
    });
    b.begun = states;
    return states;
  }

  /**
   * Applies one behaviour at progress p to a target's state; begun: the target's state when the behaviour began,
   * for formulas that take the current values (ppt_x …).
   */
  function evaluate(b, p, target, state, begun) {
    const node = b.node;
    const base = target.base, now = begun || state;
    const vars = { x: base.x, y: base.y, w: base.w, h: base.h, ppt_x: now.x, ppt_y: now.y, ppt_w: now.w, ppt_h: now.h, $: p };
    switch (node.t) {
      case 'set': {
        for (const attribute of node.attrs || []) assign(state, attribute, node.to, vars, false, node);
        break;
      }
      case 'anim': {
        for (const attribute of node.attrs || []) {
          const value = animValue(node, p, vars, state, attribute);
          if (value !== undefined) assign(state, attribute, value, vars, true, node);
        }
        break;
      }
      case 'animEffect': {
        const filter = node.filter || 'fade';
        const shown = node.transition === 'out' ? 1 - p : p;
        if (node.transition === 'in' || node.transition === 'out') {
          state.visible = true;
          if (filter.startsWith('fade')) state.opacity = Math.min(state.opacity, shown);
          else if (shown < 1) state.clip = { filter, p, out: node.transition === 'out' };
          if (node.transition === 'out' && p >= 1) state.visible = false;
        }
        break;
      }
      case 'animMotion': {
        const point = motion(node, p);
        state.mx += point[0];
        state.my += point[1];
        break;
      }
      case 'animRot': {
        const by = typeof node.by === 'number' ? node.by : (typeof node.to === 'number' ? node.to - (node.from || 0) : 0);
        state.rot += (typeof node.from === 'number' ? node.from / 60000 : 0) + by / 60000 * p;
        break;
      }
      case 'animScale': {
        if (node.to) {
          // A scale to a size replaces the scale so far, from it unless the behaviour names its start (SMIL to and
          // from-to animation); one starting where another scale of the target ends starts from that one's end
          // (Bounce squashes to 60 % and back to 100 % from there, measured).
          const start = node.from || b.chainedFrom;
          const from = start ? [start[0] / 100000, start[1] / 100000] : [state.sx, state.sy];
          state.sx = from[0] + (node.to[0] / 100000 - from[0]) * p;
          state.sy = from[1] + (node.to[1] / 100000 - from[1]) * p;
          break;
        }
        // A scale by a factor multiplies the scale so far.
        const by = node.by || [100000, 100000];
        state.sx *= 1 + (by[0] / 100000 - 1) * p;
        state.sy *= 1 + (by[1] / 100000 - 1) * p;
        break;
      }
      case 'animClr': {
        if ((node.attrs || [])[0] === 'ppt_c') {
          // Dimming after an effect colors the whole shape: fill, line and text (measured).
          const to = hexColor(node.to);
          const color = mixColor(state.fill || currentColor(target, 'fill') || to, to, p);
          state.fill = state.stroke = state.color = color;
          break;
        }
        const key = attributeKey((node.attrs || [])[0]) || 'fill';
        const from = hexColor(node.from) || state[key] || currentColor(target, key);
        state[key] = animateColor(node, from, p);
        break;
      }
      case 'anim': break;
    }
  }

  /**
   * A color animation's value at p (p:animClr, catalog: animation.behaviors): to a color, or by a change (p:by) in
   * RGB (percentages of full intensity) or HSL (hue in 60000ths of a degree, saturation and lightness added in
   * thousandths of a percent: Desaturate, Lighten, Darken, Complementary); in HSL the hue turns in the direction
   * given (dir cw: increasing).
   */
  function animateColor(node, from, p) {
    const start = parseColor(from);
    if (!start) return from;
    const by = node.by;
    if (node.clrSpc === 'hsl') {
      const [h, s, l] = rgbToHsl(start);
      let target;
      if (by && 'h' in by) {
        // The hue turns the way dir says whatever the sign (Complementary Color 2: -120° clockwise is +240°).
        const turn = ((by.h / 60000) % 360 + 360) % 360;
        target = [h + (node.dir === 'ccw' && turn > 0 ? turn - 360 : turn), s + by.s / 100000, l + by.l / 100000];
      }
      else {
        const end = parseColor(hexColor(node.to)) || start;
        const [h2, s2, l2] = rgbToHsl(end);
        let dh = ((h2 - h) % 360 + 360) % 360;
        if (node.dir === 'ccw' && dh > 0) dh -= 360;
        target = [h + dh, s2, l2];
      }
      const clamp = v => Math.min(1, Math.max(0, v));
      const rgb = hslToRgb(h + (target[0] - h) * p, clamp(s + (clamp(target[1]) - s) * p), clamp(l + (clamp(target[2]) - l) * p));
      return `rgb(${rgb.map(v => Math.round(v)).join(',')})`;
    }
    if (by && 'r' in by) {
      const end = start.map((v, i) => Math.min(255, Math.max(0, v + [by.r, by.g, by.b][i] / 100000 * 255)));
      return mixColor(from, `rgb(${end.map(Math.round).join(',')})`, p);
    }
    return mixColor(from, node.to ? hexColor(node.to) : from, p);
  }

  function rgbToHsl([r, g, b]) {
    [r, g, b] = [r / 255, g / 255, b / 255];
    const max = Math.max(r, g, b), min = Math.min(r, g, b), l = (max + min) / 2, d = max - min;
    if (d === 0) return [0, 0, l];
    const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    const h = max === r ? ((g - b) / d + (g < b ? 6 : 0)) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return [h * 60, s, l];
  }

  function hslToRgb(h, s, l) {
    h = ((h % 360) + 360) % 360 / 360;
    if (s === 0) return [l * 255, l * 255, l * 255];
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
    const channel = t => {
      t = (t + 1) % 1;
      return 255 * (t < 1 / 6 ? p + (q - p) * 6 * t : t < 1 / 2 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p);
    };
    return [channel(h + 1 / 3), channel(h), channel(h - 1 / 3)];
  }

  function currentColor(target, key) {
    const selector = key === 'color' ? '[data-tx] g[fill], [data-tx] text' : (target.svg ? 'path' : '[data-bg] path, svg > path');
    const el = target.all(selector)[0];
    return el ? (key === 'stroke' ? el.getAttribute('stroke') : el.getAttribute('fill')) : null;
  }

  function assign(state, attribute, value, vars, numeric, node) {
    const key = attributeKey(attribute);
    if (!key) return;
    if (key === 'visible') state.visible = value !== 'hidden' && value !== false;
    else if (key === 'opacity') state.opacity = Number(value);
    // A rotation set or animated replaces the one so far (Swivel sets -45 and animates back to 0), unless it adds.
    else if (key === 'rot') state.rot = (node && node.additive === 'sum' ? state.rot : 0) + Number(numeric ? value : formula(value)(vars));
    else if (key === 'fill' || key === 'stroke' || key === 'color') state[key] = hexColor(value);
    else if (key === 'fontSize') state.fontSize = Number(value) || 1;
    else if (key === 'weight' || key === 'fontStyle' || key === 'underline' || key === 'family') state[key] = String(value);
    else if (key === 'fillOn' || key === 'strokeOn' || key === 'gray') state[key] = value === true || value === 'true';
    else if (key === 'model') state.model = { anim: Number(attribute.slice(8)) - 1, value: Number(value) };
    else state[key] = numeric ? Number(value) : Number(formula(value)(vars));
  }

  function animValue(node, p, vars, state, attribute) {
    const tav = node.tav;
    const valueOf = v => (typeof v === 'number' ? v : (node.valueType === 'str' ? v : formula(v)(vars)));
    if (tav && tav.length) {
      const times = tav.map((k, i) => (typeof k.tm === 'number' ? k.tm / 100000 : i / Math.max(1, tav.length - 1)));
      let i = 0;
      while (i < tav.length - 2 && p > times[i + 1]) i++;
      const a = tav[i], b = tav[Math.min(i + 1, tav.length - 1)];
      const span = times[Math.min(i + 1, tav.length - 1)] - times[i];
      // A bounce overshoots the last key frame: the last segment goes on beyond it.
      const local = span > 0 ? Math.max(0, i + 1 === tav.length - 1 ? (p - times[i]) / span : Math.min(1, (p - times[i]) / span)) : 1;
      // Colors (valueType clr: Color Reveal) step or blend, they are no formulas.
      if (node.valueType === 'clr') return node.calcmode === 'discrete' ? hexColor(local < 1 ? a.val : b.val) : mixColor(hexColor(a.val), hexColor(b.val), Math.min(1, local));
      if (node.valueType === 'str' || node.calcmode === 'discrete') return valueOf(local < 1 ? a.val : b.val);
      const va = Number(valueOf(a.val)), vb = Number(valueOf(b.val));
      let v = va + (vb - va) * local;
      if (a.fmla) v = formula(a.fmla)(Object.assign({}, vars, { $: v }));
      return v;
    }
    if (node.to !== undefined || node.by !== undefined) {
      const current = state[attributeKey(attribute)];
      const from = node.from !== undefined ? Number(valueOf(node.from)) : Number(current);
      const to = node.to !== undefined ? Number(valueOf(node.to)) : from + Number(valueOf(node.by));
      return from + (to - from) * p;
    }
    return undefined;
  }

  /** A point on a motion path ("M 0 0 L 0.25 0 E", fractions of the slide) at progress p. */
  const pathCache = new Map();
  function motion(node, p) {
    if (!node.path) {
      const from = node.from || [0, 0], to = node.to || (node.by ? [from[0] + node.by[0], from[1] + node.by[1]] : from);
      return [(from[0] + (to[0] - from[0]) * p) / 100000, (from[1] + (to[1] - from[1]) * p) / 100000];
    }
    let samples = pathCache.get(node.path);
    if (!samples) { samples = samplePath(node.path); pathCache.set(node.path, samples); }
    if (samples.length < 2) return samples[0] || [0, 0];
    const total = samples[samples.length - 1][2];
    const d = p * total;
    let i = 1;
    while (i < samples.length - 1 && samples[i][2] < d) i++;
    const a = samples[i - 1], b = samples[i];
    const t = b[2] > a[2] ? (d - a[2]) / (b[2] - a[2]) : 0;
    return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t];
  }
  function samplePath(path) {
    const tokens = path.match(/[a-zA-Z]|-?[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?/g) || [];
    const points = [];
    let i = 0, x = 0, y = 0, sx = 0, sy = 0, command = 'M';
    const num = () => parseFloat(tokens[i++]);
    // PowerPoint moves along the path at a speed even in the path's own units (fractions of the slide's width and
    // height), not on the screen (measured with its video).
    const add = (px, py) => {
      const last = points[points.length - 1];
      const length = last ? last[2] + Math.hypot(px - last[0], py - last[1]) : 0;
      points.push([px, py, length]);
    };
    while (i < tokens.length) {
      if (/[a-zA-Z]/.test(tokens[i])) command = tokens[i++];
      const relative = command === command.toLowerCase();
      const ox = relative ? x : 0, oy = relative ? y : 0;
      switch (command.toUpperCase()) {
        case 'M': x = ox + num(); y = oy + num(); sx = x; sy = y; add(x, y); command = relative ? 'l' : 'L'; break;
        case 'L': x = ox + num(); y = oy + num(); add(x, y); break;
        case 'C': {
          const c1x = ox + num(), c1y = oy + num(), c2x = ox + num(), c2y = oy + num(), ex = ox + num(), ey = oy + num();
          for (let k = 1; k <= 16; k++) {
            const t = k / 16, u = 1 - t;
            add(u * u * u * x + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * ex,
              u * u * u * y + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * ey);
          }
          x = ex; y = ey;
          break;
        }
        case 'Z': x = sx; y = sy; add(x, y); break;
        case 'E': i = tokens.length; break;
        default: i++;
      }
    }
    return points;
  }

  /* ---------------------------------------------------------------- filters (catalog: animation.behaviors) */

  // PowerPoint softens the edges of its filters: a linear ramp 0.14 of the way the edge travels, which runs from just
  // before the target at the start to just past it at the end (measured with its videos).
  const SOFT = 0.14;

  /**
   * The mask of an animEffect filter at progress p over a box (x, y, width, height in the masked elements'
   * coordinates: the bounds they draw, outline included): SVG markup, white where the target shows, or null for none.
   * Every point of the box has its moment u (0 … 1) at which the filter's edge passes it, in the box's own proportions:
   * the distance from the side a wipe starts at (6 bands for blinds, 6 × 6 cells for a checkerboard, every other row
   * half a cell over), from the middle of a box, circle or diamond opening out, the angle of a wheel's spokes or of a
   * wedge opening from 12 o'clock, a diagonal of 16 × 16 blocks for strips; dissolves and random bars are random. An
   * entrance shows what the edge has passed. An exit hides it, except that blinds, checkerboards and strips play
   * their entrance backwards (all measured).
   */
  function effectMask(filter, p, out, id, box) {
    const m = /^([a-z]+)(?:\(([^)]*)\))?/.exec(filter) || [];
    const name = m[1], option = m[2] || '';
    const backwards = out && (name === 'blinds' || name === 'checkerboard' || name === 'strips');
    const s = backwards ? 1 - p : p, hides = out && !backwards;
    const clear = (1 + SOFT) * s;
    const alpha = u => {
      const a = Math.min(1, Math.max(0, (clear - u) / SOFT));
      return hides ? 1 - a : a;
    };
    const num = v => +v.toFixed(5);
    const defs = [], unit = [];
    let count = 0;
    const nextId = () => `${id}-${count++}`;
    // Pieces of a mask meet without antialiasing (their opacities agree where they meet; antialiased, the seams would
    // show).
    const PIECE = 'shape-rendering="crispEdges"';
    /** Stops of a gradient whose offsets 0 … 1 run through the moments u0 … u1. */
    const stops = (u0, u1) => {
      const at = u => (u - u0) / (u1 - u0);
      const offsets = [0, 1, at(clear - SOFT), at(clear)].filter(o => o >= 0 && o <= 1).sort((a, b) => a - b);
      return offsets.map(o => `<stop offset="${num(o)}" stop-color="#fff" stop-opacity="${num(alpha(u0 + (u1 - u0) * o))}"/>`).join('');
    };
    /** A region of the unit square where the moment changes along a line: u0 at (x1, y1), u1 at (x2, y2). */
    const ramp = (d, [x1, y1, x2, y2], u0, u1, spread = 'pad', piece = '') => {
      const gid = nextId();
      defs.push(`<linearGradient id="${gid}" gradientUnits="userSpaceOnUse" x1="${num(x1)}" y1="${num(y1)}" x2="${num(x2)}" y2="${num(y2)}" spreadMethod="${spread}">${stops(u0, u1)}</linearGradient>`);
      unit.push(`<path d="${d}" fill="url(#${gid})" ${piece}/>`);
    };
    const solid = (d, a) => { if (a > 0) unit.push(`<path d="${d}" fill="#fff" fill-opacity="${num(a)}" ${PIECE}/>`); };
    const square = 'M0 0H1V1H0Z';
    const cell = (x, y, w, h) => `M${num(x)} ${num(y)}h${num(w)}v${num(h)}h${num(-w)}Z`;
    // Angles clockwise from 12 o'clock: fans of equal opacity reaching past the square's corners.
    const angular = uOf => {
      const steps = 720, runs = [];
      for (let k = 0; k < steps; k++) {
        const q = Math.round(alpha(uOf((k + 0.5) / steps * 360)) * 64) / 64;
        const last = runs[runs.length - 1];
        if (last && last.q === q) last.to = k + 1;
        else runs.push({ q, from: k, to: k + 1 });
      }
      for (const run of runs) {
        if (run.q <= 0) continue;
        const [a0, a1] = [run.from / steps * 360, run.to / steps * 360];
        const pts = [[0.5, 0.5]];
        const n = Math.max(1, Math.ceil((a1 - a0) / 40));
        for (let j = 0; j <= n; j++) {
          const a = (a0 + (a1 - a0) * j / n) * Math.PI / 180;
          pts.push([0.5 + Math.sin(a), 0.5 - Math.cos(a)]);
        }
        solid('M' + pts.map(([x, y]) => `${num(x)} ${num(y)}`).join('L') + 'Z', run.q);
      }
    };
    switch (name) {
      case 'wipe': {
        // wipe(left) starts at the left edge ("From Left"), wipe(up) at the top, wipe(down) at the bottom.
        const line = { left: [0, 0, 1, 0], right: [1, 0, 0, 0], down: [0, 1, 0, 0] }[option] || [0, 0, 0, 1];
        ramp(square, line, 0, 1);
        break;
      }
      case 'barn': {
        // Two edges from the middle out (u = |2x - 1|) or from the sides in.
        const line = option.includes('Vertical') ? [0.5, 0, 1, 0] : [0, 0.5, 0, 1];
        ramp(square, line, option.startsWith('out') ? 0 : 1, option.startsWith('out') ? 1 : 0, 'reflect');
        break;
      }
      case 'blinds':
        ramp(square, option === 'vertical' ? [0, 0, 1 / 6, 0] : [0, 0, 0, 1 / 6], 0, 1, 'repeat');
        break;
      case 'checkerboard': {
        const down = option === 'down';
        for (let k = 0; k < 6; k++) {
          const shift = k % 2 ? 0.5 / 6 : 0;
          ramp(down ? cell(k / 6, 0, 1 / 6, 1) : cell(0, k / 6, 1, 1 / 6),
            down ? [0, shift, 0, shift + 1 / 6] : [shift, 0, shift + 1 / 6, 0], 0, 1, 'repeat', PIECE);
        }
        break;
      }
      case 'box': case 'plus': {
        // Triangles between the diagonals, each with its side's distance: a box takes the larger of the distances
        // from the middle lines, a plus the smaller.
        const opens = option === 'out', across = 'M0 0L1 0L0.5 0.5ZM0 1L1 1L0.5 0.5Z', sides = 'M0 0L0 1L0.5 0.5ZM1 0L1 1L0.5 0.5Z';
        const [vertical, horizontal] = [[0, 0.5, 0, 1], [0.5, 0, 1, 0]];
        const [u0, u1] = opens ? [0, 1] : [1, 0];
        ramp(across, name === 'box' ? vertical : horizontal, u0, u1, 'reflect', PIECE);
        ramp(sides, name === 'box' ? horizontal : vertical, u0, u1, 'reflect', PIECE);
        break;
      }
      case 'diamond': {
        const [u0, u1] = option === 'out' ? [0, 1] : [1, 0];
        for (const [cx, cy] of [[0, 0], [1, 0], [0, 1], [1, 1]]) {
          ramp(cell(Math.min(cx, 0.5), Math.min(cy, 0.5), 0.5, 0.5), [0.5, 0.5, cx, cy], u0, u1, 'pad', PIECE);
        }
        break;
      }
      case 'circle': {
        // The circle (an ellipse in the box) reaches the corners.
        const gid = nextId(), [u0, u1] = option === 'out' ? [0, 1] : [1, 0];
        defs.push(`<radialGradient id="${gid}" gradientUnits="userSpaceOnUse" cx="0.5" cy="0.5" r="${num(Math.SQRT1_2)}">${stops(u0, u1)}</radialGradient>`);
        unit.push(`<path d="${square}" fill="url(#${gid})"/>`);
        break;
      }
      case 'strips': {
        // Blocks of a 16 × 16 grid, a diagonal at a time from the corner the strips leave (downLeft: top right).
        const way = option || 'downLeft', diagonals = new Map();
        for (let r = 0; r < 16; r++) {
          for (let c = 0; c < 16; c++) {
            const d = (way.endsWith('Left') ? 15 - c : c) + (way.startsWith('up') ? 15 - r : r);
            diagonals.set(d, (diagonals.get(d) || '') + cell(c / 16, r / 16, 1 / 16, 1 / 16));
          }
        }
        for (const [d, path] of diagonals) solid(path, alpha(d / 31));
        break;
      }
      case 'wedge':
        angular(a => (a <= 180 ? a : 360 - a) / 180);
        break;
      case 'wheel': {
        const sector = 360 / Math.max(1, parseInt(option, 10) || 1);
        angular(a => (a % sector) / sector);
        break;
      }
      case 'dissolve': case 'randombar': {
        // Thresholds of a noise image: specks of a pixel, in an S-shaped pace (the average of three random numbers)
        // or lines of a pixel across the box (measured).
        const covered = name === 'dissolve' ? irwinHall3(s) : randomBars(s);
        const t = covered * 256 / 255 - 0.5 / 255, k = 1020;
        const [slope, intercept] = hides ? [k, -k * t] : [-k, k * t];
        const func = c => `<feFunc${c} type="linear" slope="${num(slope)}" intercept="${num(intercept)}"/>`;
        const fid = nextId();
        defs.push(`<filter id="${fid}" x="0" y="0" width="1" height="1" color-interpolation-filters="sRGB"><feComponentTransfer>${func('R')}${func('G')}${func('B')}</feComponentTransfer></filter>`);
        const at = `x="${num(box.x)}" y="${num(box.y)}" width="${num(box.width)}" height="${num(box.height)}"`;
        if (name === 'dissolve') {
          const pid = nextId();
          defs.push(`<pattern id="${pid}" patternUnits="userSpaceOnUse" x="${num(box.x)}" y="${num(box.y)}" width="128" height="128"><image href="${noise(128, 128)}" width="128" height="128" style="image-rendering:pixelated"/></pattern>`);
          return `<defs>${defs.join('')}</defs><rect ${at} fill="url(#${pid})" filter="url(#${fid})"/>`;
        }
        const lines = Math.max(1, Math.round(option === 'vertical' ? box.width : box.height));
        const image = option === 'vertical' ? noise(lines, 1) : noise(1, lines);
        return `<defs>${defs.join('')}</defs><image href="${image}" ${at} preserveAspectRatio="none" style="image-rendering:pixelated" filter="url(#${fid})"/>`;
      }
      default:
        return null;
    }
    const place = `matrix(${num(box.width)},0,0,${num(box.height)},${num(box.x)},${num(box.y)})`;
    return `<defs>${defs.join('')}</defs><g transform="${place}">${unit.join('')}</g>`;
  }

  /** The share of a dissolve shown at s: the distribution of the average of three random numbers. */
  function irwinHall3(s) {
    const x = Math.min(3, Math.max(0, 3 * s));
    if (x < 1) return x ** 3 / 6;
    if (x < 2) return (-2 * x ** 3 + 9 * x ** 2 - 9 * x + 3) / 6;
    return 1 - (3 - x) ** 3 / 6;
  }

  /** The share of random bars shown at s (PowerPoint's pace, measured: slow at first, ahead of time in the middle). */
  function randomBars(s) {
    const table = [0, 0.003, 0.024, 0.07, 0.143, 0.231, 0.313, 0.383, 0.448, 0.515, 0.585, 0.65, 0.705, 0.757, 0.81, 0.868, 0.925, 0.966, 0.988, 0.999, 1];
    const x = Math.min(1, Math.max(0, s)) * 20, i = Math.min(19, Math.floor(x));
    return table[i] + (table[i + 1] - table[i]) * (x - i);
  }

  /** A grayscale noise image (data URL) of w × h pixels, the same for every frame. */
  const noises = new Map();
  function noise(w, h) {
    const key = `${w}x${h}`;
    let url = noises.get(key);
    if (!url) {
      const canvas = document.createElement('canvas');
      [canvas.width, canvas.height] = [w, h];
      const context = canvas.getContext('2d'), image = context.createImageData(w, h);
      let seed = 0x2545f491;
      for (let i = 0; i < w * h; i++) {
        // mulberry32
        seed = seed + 0x6d2b79f5 | 0;
        let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
        t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
        const v = (t ^ t >>> 14) >>> 24;
        image.data.set([v, v, v, 255], i * 4);
      }
      context.putImageData(image, 0, 0);
      url = canvas.toDataURL();
      noises.set(key, url);
    }
    return url;
  }

  /**
   * Lines a letter drawn in another face up with the letter it stands for: their middles meet across, and down too
   * when the other layout put it on another line.
   */
  function alignVariant(g) {
    g.dataset.aligned = '1';
    const original = g.parentNode && g.parentNode.querySelector(':scope > [data-v=""]');
    if (!original) return;
    try {
      const a = original.getBBox(), b = g.getBBox();
      if (!b.width && !b.height) return;
      const dx = a.x + a.width / 2 - (b.x + b.width / 2);
      const dy = Math.abs(a.y + a.height / 2 - (b.y + b.height / 2)) > a.height / 2 ? a.y + a.height - (b.y + b.height) : 0;
      g.setAttribute('transform', `translate(${dx} ${dy})`);
    } catch { /* not drawn */ }
  }

  /** The bounds elements draw, their outlines included, in their own coordinates. */
  function drawnBox(els) {
    let box = null, pad = 0;
    for (const e of els) {
      let b;
      try { b = e.getBBox(); } catch { continue; }
      if (b.width === 0 && b.height === 0) continue;
      box = box ? {
        x: Math.min(box.x, b.x), y: Math.min(box.y, b.y),
        width: Math.max(box.x + box.width, b.x + b.width) - Math.min(box.x, b.x),
        height: Math.max(box.y + box.height, b.y + b.height) - Math.min(box.y, b.y),
      } : { x: b.x, y: b.y, width: b.width, height: b.height };
      for (const stroked of [e, ...e.querySelectorAll('[stroke]')]) {
        const stroke = stroked.getAttribute('stroke');
        if (stroke && stroke !== 'none') pad = Math.max(pad, (parseFloat(stroked.getAttribute('stroke-width')) || 1) / 2);
      }
    }
    if (!box) return { x: 0, y: 0, width: 0, height: 0 };
    return { x: box.x - pad, y: box.y - pad, width: box.width + 2 * pad, height: box.height + 2 * pad };
  }

  /* ---------------------------------------------------------------- media (catalog: picture.media) */

  const mediaOf = (slide, spid) => onlineMedia(slide.querySelector(`[data-id="${spid}"] .fs-media`));
  const trimStart = el => (parseFloat(el.dataset.trimStart) || 0) / 1000;

  /**
   * An online video (a:videoFile linked to a web player such as YouTube's or Vimeo's) is a frame with the media
   * element's methods: playing loads the player (nothing is fetched before), stopping unloads it, pause and resume
   * go to the player as messages (YouTube's and Vimeo's player APIs). Other media come back as they are.
   */
  function onlineMedia(el) {
    if (!el || el.tagName !== 'IFRAME' || el.play) return el;
    let playing = false;
    const url = el.dataset.src || '';
    const youtube = /youtube(-nocookie)?\.com|youtu\.be/i.test(url);
    const send = command => {
      try {
        el.contentWindow.postMessage(youtube ? JSON.stringify({ event: 'command', func: command + 'Video', args: [] }) : JSON.stringify({ method: command }), '*');
      } catch { /* not loaded */ }
    };
    el.play = () => {
      if (!el.getAttribute('src')) {
        const address = new URL(url, location.href);
        address.searchParams.set('autoplay', '1');
        if (youtube) address.searchParams.set('enablejsapi', '1');
        el.setAttribute('src', address.href);
      } else send('play');
      playing = true;
      return Promise.resolve();
    };
    el.pause = () => { send('pause'); playing = false; };
    el.unload = () => { el.removeAttribute('src'); playing = false; };
    Object.defineProperty(el, 'paused', { get: () => !playing });
    return el;
  }

  /** Plays media from a position (seconds after the trimmed start), honouring trim end, fade in and volume. */
  function playMedia(el, from, volume) {
    el = onlineMedia(el);
    if (!el.dataset.watched) {
      el.dataset.watched = '1';
      let last = el.currentTime;
      el.addEventListener('timeupdate', () => {
        const end = (el.duration || Infinity) - (parseFloat(el.dataset.trimEnd) || 0) / 1000;
        // Bookmarks passed since the last update start their trigger sequences.
        if (el.dataset.bookmarks && typeof show !== 'undefined' && show && show.animation) {
          const owner = el.closest('[data-id]');
          for (const b of JSON.parse(el.dataset.bookmarks)) {
            const at = b.t / 1000;
            if (owner && at > last && at <= el.currentTime) show.animation.trigger(`bmk:${owner.getAttribute('data-id')}:${b.n}`);
          }
        }
        last = el.currentTime;
        if (el.currentTime >= end) stopMedia(el);
      });
      el.addEventListener('ended', () => stopMedia(el));
    }
    if (volume !== undefined) el.dataset.volume = volume;
    const options = el.fsOptions;
    if (options && show) {
      if (options.hideStopped) showMediaShape(el, true);
      if (options.fullScreen) fullScreenMedia(el, true);
      // Playing across slides: counted down as the show moves on.
      if (options.slides > 1) (show.acrossSlides = show.acrossSlides || new Map()).set(el, options.slides);
    }
    el.currentTime = trimStart(el) + (from || 0);
    const target = el.dataset.volume !== undefined ? +el.dataset.volume : 1;
    const fadeIn = parseFloat(el.dataset.fadeIn) || 0;
    el.volume = fadeIn > 0 ? 0 : target;
    el.classList.add('playing');
    el.play().catch(() => {});
    if (fadeIn > 0) {
      const started = performance.now();
      const step = () => {
        const p = Math.min(1, (performance.now() - started) / fadeIn);
        el.volume = target * p;
        if (p < 1 && !el.paused) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
    }
  }
  function stopMedia(el) {
    el = onlineMedia(el);
    el.pause();
    if (el.unload) el.unload();
    el.classList.remove('playing');
    if (el.fsOptions && show) {
      if (el.fsOptions.hideStopped) showMediaShape(el, false);
      if (el.fsOptions.fullScreen) fullScreenMedia(el, false);
      if (show.acrossSlides) show.acrossSlides.delete(el);
    }
    try { el.currentTime = trimStart(el); } catch { /* not loaded yet */ }
  }
  function toggleMedia(el) {
    el = onlineMedia(el);
    if (el.paused) {
      if (!el.classList.contains('playing')) playMedia(el, 0);
      else el.play().catch(() => {});
    } else {
      el.pause();
    }
  }

  /** Runs a p:cmd behaviour (playFrom(t), togglePause, pause, resume, stop) on its target's media. */
  /** Sounds effects play (p:audio with p:sndTgt: "Play sound" of an effect), stopped when the slide ends. */
  const effectSounds = new Set();
  function stopEffectSounds() {
    for (const audio of effectSounds) audio.pause();
    effectSounds.clear();
  }

  function command(slide, node, volumes) {
    if (node.t === 'audio' && node.target && node.target.snd) {
      const audio = new Audio(node.target.snd);
      audio.volume = node.mute ? 0 : Math.min(1, typeof node.vol === 'number' ? node.vol / 100000 : 1);
      effectSounds.add(audio);
      audio.addEventListener('ended', () => effectSounds.delete(audio));
      audio.play().catch(() => effectSounds.delete(audio));
      return;
    }
    const el = node.target && mediaOf(slide, node.target.spid);
    if (!el) return;
    if (el.fsOptions && el.fsOptions.narration && deck.show && deck.show.narration === false) return;
    const cmd = String(node.cmd || '');
    const volume = volumes.get(String(node.target.spid));
    const from = /^playFrom\(([-0-9.]+)\)/.exec(cmd);
    if (from) playMedia(el, parseFloat(from[1]), volume);
    else if (cmd === 'togglePause') toggleMedia(el);
    else if (cmd === 'pause') el.pause();
    else if (cmd === 'resume') el.play().catch(() => {});
    else if (cmd === 'stop') stopMedia(el);
  }

  /** Volumes of the slide's media nodes (p:audio / p:video, cMediaNode vol in 1/100000). */
  function mediaVolumes(timing) {
    return new Map([...mediaOptions(timing)].map(([spid, o]) => [spid, o.volume]));
  }

  /**
   * Playback options of the slide's media (p:audio, p:video and their cMediaNode, catalog: animation.media): volume,
   * hidden while not playing (showWhenStopped 0), playing on across slides (numSld), full screen (fullScrn) and
   * narration (isNarration).
   */
  function mediaOptions(timing) {
    const result = new Map();
    const visit = node => {
      if (!node) return;
      if ((node.t === 'audio' || node.t === 'video') && node.target && node.target.spid !== undefined) {
        result.set(String(node.target.spid), {
          volume: node.mute ? 0 : (typeof node.vol === 'number' ? node.vol / 100000 : 1),
          hideStopped: node.showWhenStopped === false || node.showWhenStopped === 0,
          slides: typeof node.numSld === 'number' ? node.numSld : 1,
          fullScreen: node.fullScrn === true || node.fullScrn === 1,
          narration: node.isNarration === true || node.isNarration === 1,
        });
      }
      (node.children || []).forEach(visit);
      (node.sub || []).forEach(visit);
    };
    visit(timing && timing.root);
    return result;
  }

  /** Applies the media options of a slide shown in the show: hidden stopped media, and remembers the others. */
  function prepareMedia(slide, index) {
    for (const [spid, o] of mediaOptions(info(index).timing)) {
      const el = mediaOf(slide, spid);
      if (!el) continue;
      el.fsOptions = o;
      if (o.hideStopped && !el.classList.contains('playing')) showMediaShape(el, false);
    }
  }

  /** Shows or hides the shape a medium belongs to (media hidden while stopped), over what animations set. */
  function showMediaShape(el, visible) {
    const owner = el.closest('[data-id]');
    if (!owner || (!visible && !(show && show.stage.contains(owner)))) return;
    if (!document.getElementById('fs-media-style')) {
      const style = document.createElement('style');
      style.id = 'fs-media-style';
      style.textContent = '.fs-stopped{visibility:hidden!important}';
      document.head.appendChild(style);
    }
    owner.classList.toggle('fs-stopped', !visible);
    if (!visible && show) (show.hiddenMedia = show.hiddenMedia || new Set()).add(owner);
  }

  /** A video played full screen (fullScrn) fills the slide, centered, while it plays. */
  function fullScreenMedia(el, on) {
    const owner = el.closest('.sp');
    if (!owner || el.tagName === 'AUDIO') return;
    if (on && owner.dataset.fullTransform === undefined) {
      owner.dataset.fullTransform = owner.style.transform;
      const w = parseFloat(owner.style.width) || owner.offsetWidth, h = parseFloat(owner.style.height) || owner.offsetHeight;
      const k = Math.min(W / w, H / h);
      owner.style.transform = `matrix(${k},0,0,${k},${(W - w * k) / 2},${(H - h * k) / 2})`;
      owner.style.zIndex = '1000';
    } else if (!on && owner.dataset.fullTransform !== undefined) {
      owner.style.transform = owner.dataset.fullTransform;
      owner.style.zIndex = '';
      delete owner.dataset.fullTransform;
    }
  }

  /* ---------------------------------------------------------------- the animation player of one slide */

  class SlideAnimation {
    constructor(slide, index, tick) {
      this.slide = slide;
      this.timeline = buildTimeline(slide, info(index).timing, tick);
      this.volumes = mediaVolumes(info(index).timing);
      this.done = [];         // completed steps (main and triggered), in play order
      this.position = 0;      // next main step
      this.current = null;    // { step, started }
      this.frame = 0;
    }

    get pending() { return this.position < this.timeline.steps.length || this.current !== null; }

    /** Shows the slide before any animation (entrance targets hidden). */
    start(onIdle) {
      this.onIdle = onIdle;
      this.done = [];
      this.position = 0;
      this.render(null, 0);
      this.autoPlay();
    }

    /** Shows the slide with every main step played. */
    finish() {
      this.stop();
      this.done = this.timeline.steps.slice();
      this.position = this.timeline.steps.length;
      this.render(null, 0);
    }

    stop() {
      if (this.current) { this.done.push(this.current.step); this.current = null; }
      cancelAnimationFrame(this.frame);
      clearTimeout(this.timer);
    }

    /** Plays the next main step; false when there is none. */
    next() {
      if (this.current) { this.stop(); this.render(null, 0); this.autoPlay(); return true; }
      if (this.position >= this.timeline.steps.length) return false;
      this.play(this.timeline.steps[this.position++]);
      return true;
    }

    /** Takes back the last main step; false when none was played. */
    previous() {
      this.stop();
      if (this.position === 0) return false;
      const step = this.timeline.steps[--this.position];
      const i = this.done.lastIndexOf(step);
      if (i >= 0) this.done.splice(i, 1);
      // Steps that played automatically after it go back too.
      while (this.position > 0 && this.timeline.steps[this.position].auto && this.position < this.timeline.steps.length) {
        const s = this.timeline.steps[--this.position];
        const j = this.done.lastIndexOf(s);
        if (j >= 0) this.done.splice(j, 1);
        if (!s.auto) break;
      }
      this.render(null, 0);
      return true;
    }

    trigger(spid) {
      const sequence = this.timeline.triggers.get(String(spid));
      if (!sequence || sequence.steps.length === 0) return false;
      if (sequence.played >= sequence.steps.length) {
        // The sequence has played (it ends with its last effect, endSync all): a click while that runs seeks it to
        // its end (restart whenNotActive, PowerPoint's); after it the sequence starts again, unless restart is never.
        if (this.current && sequence.steps.includes(this.current.step)) { this.stop(); this.render(null, 0); return true; }
        if (sequence.restart === 'never') return true;
        sequence.played = 0;
        this.done = this.done.filter(s => !sequence.steps.includes(s));
      }
      this.stop();
      this.play(sequence.steps[sequence.played++]);
      return true;
    }

    play(step) {
      cancelAnimationFrame(this.frame);
      clearTimeout(this.timer);
      const current = this.current = { step, started: performance.now() };
      const commands = step.behaviors.filter(b => b.node.t === 'cmd' || (b.node.t === 'audio' && b.node.target && b.node.target.snd));
      // A click ends what repeats until the next click.
      if (!step.auto) for (const s of this.done) s.clicked = true;
      step.startedAt = performance.now();
      const runCommands = t => {
        for (let i = commands.length - 1; i >= 0; i--) {
          if (t >= commands[i].start) { command(this.slide, commands[i].node, this.volumes); commands.splice(i, 1); }
        }
      };
      const finish = () => {
        if (this.current !== current) return;
        runCommands(Infinity);
        cancelAnimationFrame(this.frame);
        clearTimeout(this.timer);
        this.done.push(step);
        this.current = null;
        this.render(null, 0);
        this.autoPlay();
        this.keepLooping();
      };
      const tick = () => {
        if (this.current !== current) return;
        const t = performance.now() - current.started;
        runCommands(t);
        if (t >= step.end) { finish(); return; }
        this.render(step, t);
        this.frame = requestAnimationFrame(tick);
      };
      // Frames may be throttled (hidden tabs); the step still completes on time.
      this.timer = setTimeout(finish, step.end + 20);
      tick();
    }

    /** Keeps drawing while repeats of finished steps go on and no step plays. */
    keepLooping() {
      const looping = () => this.done.some(s => s.startedAt !== undefined && s.behaviors.some(b => b.loop && !(b.loop.untilNext && s.clicked)));
      const tick = () => {
        if (this.current || !looping()) return;
        this.render(null, 0);
        this.frame = requestAnimationFrame(tick);
      };
      if (!this.current && looping()) this.frame = requestAnimationFrame(tick);
    }

    autoPlay() {
      const steps = this.timeline.steps;
      if (this.position < steps.length && steps[this.position].auto) {
        this.play(steps[this.position++]);
      } else if (this.onIdle) {
        this.onIdle();
      }
    }

    /** Evaluates every played step at its end and the current step at time t, then renders all targets. */
    render(current, t) {
      const states = new Map();
      for (const [key, target] of this.timeline.targets) if (target) states.set(key, target.fresh(this.timeline.hidden.has(key)));
      const now = performance.now();
      const run = (step, time, done) => {
        // The states before the step, while values its behaviours take when they begin are still to be found.
        const before = step.behaviors.some(b => takesCurrent(b) && !b.begun) ? new Map([...states].map(([k, v]) => [k, Object.assign({}, v)])) : null;
        for (const b of step.behaviors) {
          // Repeats until the end of the slide (or until a click) go on after their step.
          const looping = done && b.loop && step.startedAt !== undefined && !(b.loop.untilNext && step.clicked);
          const p = progress(b, looping ? now - step.startedAt : time);
          if (p === null) continue;
          const begun = takesCurrent(b) ? beganWith(step, b, before) : null;
          for (const { key, target } of b.targets) evaluate(b, p, target, states.get(key), begun && begun.get(key));
        }
      };
      for (const step of this.done) run(step, Infinity, true);
      if (current) run(current, t, false);
      for (const [key, target] of this.timeline.targets) if (target) target.render(states.get(key));
    }

    reset() {
      this.stop();
      for (const step of this.done) { delete step.startedAt; delete step.clicked; for (const b of step.behaviors) delete b.begun; }
      for (const target of this.timeline.targets.values()) if (target) target.reset();
      this.slide.querySelectorAll('.fs-media').forEach(el => {
        if (!(show && show.acrossSlides && show.acrossSlides.has(el))) stopMedia(el);
      });
      stopEffectSounds();
    }
  }

  /* ---------------------------------------------------------------- transitions */

  /** Runs a transition from the outgoing to the incoming slide; both are absolutely positioned in the stage. */
  function transition(spec, outgoing, incoming, cover) {
    return new Promise(resolve => {
      const type = spec && spec.type ? spec.type : 'none';
      const duration = Math.max(0, spec && spec.dur !== undefined ? spec.dur : 0);
      let effect = null;
      try { effect = transitionEffect(type, spec || {}, outgoing, incoming, cover); } catch (e) { console.error(e); }
      // A transition that fails ends at once: the show goes on with the new slide.
      const finish = () => {
        try { if (effect && effect.end) effect.end(); } catch (e) { console.error(e); }
        resolve();
      };
      if (!effect || duration === 0 || !outgoing) { finish(); return; }
      // 3D transitions draw the slides as images first.
      const ready = effect.prepare ? Promise.resolve().then(() => effect.prepare()) : Promise.resolve();
      ready.then(() => {
        const started = performance.now();
        const tick = () => {
          try {
            const p = Math.min(1, (performance.now() - started) / duration);
            effect.frame(p);
            if (p < 1) requestAnimationFrame(tick);
            else finish();
          } catch (e) { console.error(e); finish(); }
        };
        tick();
      }, e => { console.error(e); finish(); });
    });
  }

  /* ---------------------------------------------------------------- 3D transitions (WebGL) */

  /**
   * A slide drawn as an image (for the 3D transitions, catalog: transition.office2010): its markup as it shows now,
   * with the document's styles and the shared glyphs, in an SVG foreignObject, which the browser draws like the
   * page itself.
   */
  function rasterize(slide, layer = 'all') {
    const copy = slide.cloneNode(true);
    copy.removeAttribute('id');
    // Only the content (no background, transparent) or only the background.
    if (layer === 'content') { copy.querySelectorAll(':scope > .bg').forEach(e => e.remove()); copy.style.background = 'transparent'; }
    if (layer === 'background') copy.querySelectorAll(':scope > :not(.bg)').forEach(e => e.remove());
    for (const k of ['transform', 'opacity', 'clipPath', 'maskImage', 'zIndex', 'visibility', 'filter']) copy.style[k] = '';
    if (layer === 'content') copy.style.background = 'transparent';
    Object.assign(copy.style, { margin: '0', position: 'relative', left: '0', top: '0', display: 'block' });
    const css = [...document.querySelectorAll('style')].map(e => e.textContent).join('\n');
    const glyphs = [...document.querySelectorAll('svg.glyphs')].map(e => new XMLSerializer().serializeToString(e)).join('');
    const markup = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}"><foreignObject width="${W}" height="${H}">` +
      `<div xmlns="http://www.w3.org/1999/xhtml" style="width:${W}px;height:${H}px;overflow:hidden"><style>${css}</style>${glyphs}` +
      `${new XMLSerializer().serializeToString(copy)}</div></foreignObject></svg>`;
    return new Promise(resolve => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = () => resolve(null);
      image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(markup);
    });
  }

  /**
   * A WebGL scene over the stage: textured triangles in 3D seen by a camera on the slide's middle axis (eye
   * distance in slide heights), each vertex with its own brightness, each mesh with its own opacity. Meshes are
   * { tex, pos: [x, y, z …] (slide pixels, z toward the viewer), uv: [u, v …], shade: [s …] or a number, alpha }.
   */
  function glScene(stage) {
    const canvas = document.createElement('canvas');
    const scale = (stage.getBoundingClientRect().width / W || 1) * (window.devicePixelRatio || 1);
    canvas.width = Math.max(1, Math.round(W * scale));
    canvas.height = Math.max(1, Math.round(H * scale));
    canvas.style.cssText = `position:absolute;left:0;top:0;width:${W}px;height:${H}px;z-index:4;pointer-events:none`;
    const gl = canvas.getContext('webgl', { premultipliedAlpha: false, alpha: true, antialias: true });
    if (!gl) return null;
    const shader = (kind, code) => { const sh = gl.createShader(kind); gl.shaderSource(sh, code); gl.compileShader(sh); return sh; };
    const program = gl.createProgram();
    gl.attachShader(program, shader(gl.VERTEX_SHADER, `
      attribute vec3 pos; attribute vec2 uv; attribute float shade;
      uniform mat4 projection; varying vec2 vUv; varying float vShade;
      void main() { gl_Position = projection * vec4(pos, 1.0); vUv = uv; vShade = shade; }`));
    gl.attachShader(program, shader(gl.FRAGMENT_SHADER, `
      precision mediump float; uniform sampler2D tex; uniform float alpha; uniform vec4 tint;
      varying vec2 vUv; varying float vShade;
      void main() { vec4 c = texture2D(tex, vUv); vec3 rgb = mix(c.rgb * vShade, tint.rgb, tint.a); gl_FragColor = vec4(rgb, c.a * alpha); }`));
    gl.linkProgram(program);
    gl.useProgram(program);
    const loc = name => gl.getAttribLocation(program, name);
    const buffers = { pos: gl.createBuffer(), uv: gl.createBuffer(), shade: gl.createBuffer() };
    const textures = new Map();
    const texture = image => {
      let t = textures.get(image);
      if (!t) {
        t = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, t);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
        for (const [k, v] of [[gl.TEXTURE_MIN_FILTER, gl.LINEAR], [gl.TEXTURE_MAG_FILTER, gl.LINEAR], [gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE], [gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE]]) gl.texParameteri(gl.TEXTURE_2D, k, v);
        textures.set(image, t);
      }
      return t;
    };
    stage.appendChild(canvas);
    return {
      canvas,
      /** Draws the meshes (in order, with depth) over a background color [r, g, b, a] (0 … 1). */
      draw(meshes, eye = 2, background = [0, 0, 0, 1]) {
        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(...background);
        gl.clearDepth(1);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.LEQUAL);
        gl.enable(gl.BLEND);
        // Colors mix by the source's alpha; the canvas stays opaque where something opaque lies beneath.
        gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        // Perspective: the slide plane (z = 0) fills the view; a point at z comes nearer by D / (D - z).
        const D = eye * H, Z = 20 * H;
        gl.uniformMatrix4fv(gl.getUniformLocation(program, 'projection'), false, new Float32Array([
          2 / W, 0, 0, 0, 0, -2 / H, 0, 0, 0, 0, -1 / Z, -1 / D, -1, 1, 0, 1]));
        for (const mesh of meshes) {
          const n = mesh.pos.length / 3;
          const shade = typeof mesh.shade === 'number' || mesh.shade === undefined ? new Float32Array(n).fill(mesh.shade === undefined ? 1 : mesh.shade) : new Float32Array(mesh.shade);
          for (const [name, data, size] of [['pos', new Float32Array(mesh.pos), 3], ['uv', new Float32Array(mesh.uv), 2], ['shade', shade, 1]]) {
            gl.bindBuffer(gl.ARRAY_BUFFER, buffers[name]);
            gl.bufferData(gl.ARRAY_BUFFER, data, gl.DYNAMIC_DRAW);
            gl.enableVertexAttribArray(loc(name));
            gl.vertexAttribPointer(loc(name), size, gl.FLOAT, false, 0, 0);
          }
          gl.activeTexture(gl.TEXTURE0);
          gl.bindTexture(gl.TEXTURE_2D, texture(mesh.tex));
          gl.uniform1i(gl.getUniformLocation(program, 'tex'), 0);
          gl.uniform1f(gl.getUniformLocation(program, 'alpha'), mesh.alpha === undefined ? 1 : mesh.alpha);
          gl.uniform4fv(gl.getUniformLocation(program, 'tint'), mesh.tint || [0, 0, 0, 0]);
          // flat: hides nothing drawn after it; over: lies over everything drawn before it.
          gl.depthMask(!mesh.flat);
          if (mesh.over) gl.disable(gl.DEPTH_TEST);
          gl.drawArrays(gl.TRIANGLES, 0, n);
          gl.enable(gl.DEPTH_TEST);
          gl.depthMask(true);
        }
      },
      remove() { canvas.remove(); },
    };
  }

  /** Two triangles of a quad: corners (x, y, z) a, b, c, d clockwise from the top left, their texture corners. */
  function quad(a, b, c, d, [u0, v0, u1, v1] = [0, 0, 1, 1]) {
    return {
      pos: [...a, ...b, ...c, ...a, ...c, ...d],
      uv: [u0, v0, u1, v0, u1, v1, u0, v0, u1, v1, u0, v1],
    };
  }

  /** A point turned about an axis through a center: angles in degrees about x (tilting back) and y (turning right). */
  function turn([x, y, z], [cx, cy, cz], ax = 0, ay = 0, az = 0) {
    let [px, py, pz] = [x - cx, y - cy, z - cz];
    if (ax) { const r = ax * Math.PI / 180, [c, s] = [Math.cos(r), Math.sin(r)]; [py, pz] = [py * c - pz * s, py * s + pz * c]; }
    if (ay) { const r = ay * Math.PI / 180, [c, s] = [Math.cos(r), Math.sin(r)]; [px, pz] = [px * c + pz * s, -px * s + pz * c]; }
    if (az) { const r = az * Math.PI / 180, [c, s] = [Math.cos(r), Math.sin(r)]; [px, py] = [px * c - py * s, px * s + py * c]; }
    return [px + cx, py + cy, pz + cz];
  }

  /**
   * A 3D transition: the slides as images, drawn by draw(p, old, new) into a WebGL scene over the stage while the
   * slides themselves hide; without WebGL the new slide fades in.
   */
  function glTransition(a, b, draw, reset, layers = ['all']) {
    let scene = null, images = null;
    return {
      async prepare() {
        // Per slide: the whole slide, or its content and its background apart (old ones first).
        images = await Promise.all(layers.flatMap(layer => [rasterize(a, layer), rasterize(b, layer)]));
        if (images.some(image => !image)) return;
        scene = glScene(a.parentNode);
      },
      frame(p) {
        if (!scene) { b.style.zIndex = 2; b.style.opacity = String(p); return; }
        a.style.visibility = b.style.visibility = 'hidden';
        draw(scene, p, ...images);
      },
      end() {
        if (scene) scene.remove();
        scene = null;
        a.style.visibility = b.style.visibility = '';
        reset();
      },
    };
  }

  const offsets = { l: [-1, 0], r: [1, 0], u: [0, -1], d: [0, 1], lu: [-1, -1], ru: [1, -1], ld: [-1, 1], rd: [1, 1] };

  // Easing of PowerPoint's transitions (measured with its videos: Push's edge follows a quadratic ease in and out
  // within 0.1 %, Cover's and Uncover's steeper ones, Fade a sine).
  const quadInOut = p => (p < 0.5 ? 2 * p * p : 1 - 2 * (1 - p) * (1 - p));
  const powInOut = k => p => (p < 0.5 ? 2 ** (k - 1) * p ** k : 1 - 2 ** (k - 1) * (1 - p) ** k);
  const sineInOut = p => (1 - Math.cos(Math.PI * Math.min(1, Math.max(0, p)))) / 2;
  const smooth = x => { x = Math.min(1, Math.max(0, x)); return x * x * (3 - 2 * x); };
  /** A curve through measured points [p, value] (straight between them, flat beyond). */
  const measured = points => p => {
    if (p <= points[0][0]) return points[0][1];
    for (let i = 1; i < points.length; i++) {
      const [p1, v1] = points[i];
      if (p <= p1) { const [p0, v0] = points[i - 1]; return v0 + (v1 - v0) * (p - p0) / (p1 - p0); }
    }
    return points[points.length - 1][1];
  };
  // The diagonal Cover and Uncover move otherwise (measured with PowerPoint's video, the share of the way covered):
  // the new slide comes in at once and slows down, the old one leaves within half the time.
  const coverAcross = measured([[0, 0], [0.036, 0.004], [0.069, 0.03], [0.102, 0.075], [0.135, 0.133], [0.168, 0.204], [0.201, 0.282],
    [0.234, 0.365], [0.267, 0.45], [0.3, 0.535], [0.333, 0.616], [0.366, 0.691], [0.399, 0.759], [0.432, 0.813], [0.465, 0.852],
    [0.498, 0.881], [0.564, 0.922], [0.63, 0.951], [0.696, 0.973], [0.762, 0.986], [0.828, 0.995], [0.894, 0.998], [0.96, 1]]);
  const uncoverAcross = measured([[0, 0], [0.036, 0.003], [0.069, 0.0125], [0.102, 0.036], [0.135, 0.068], [0.168, 0.12], [0.201, 0.224],
    [0.234, 0.395], [0.267, 0.59], [0.3, 0.758], [0.333, 0.858], [0.366, 0.92], [0.399, 0.961], [0.432, 0.988], [0.465, 1]]);

  /**
   * The soft edge of PowerPoint's masking transitions (measured frame by frame with its videos): every point has its
   * moment u (0 … 1: the distance from where the edge starts), and shows the new slide by a smoothstep ramp w wide
   * that passes from just before u = 0 at the start to just after u = 1 at the end; linear in time. Wipes, splits
   * and strips have w = 1 (the ramp as long as the way), the shapes 1/3, the wheels 0.11 of a spoke's sector.
   */
  const reveal = (u, p, w) => smooth(((1 + w) * p - u) / w);

  /**
   * The mask of a masking transition at p, as SVG over the slide (viewBox in the slide's pixels), white where the
   * new slide shows; null for transitions that do not mask. Shapes and angles are in the slide's own proportions
   * (a diamond reaches the corners, a wheel's spokes stand at the slide's 12, 3, 6 and 9 o'clock), the circle is
   * round and reaches the corners.
   */
  function transitionMask(type, spec, p) {
    const [cx, cy] = [W / 2, H / 2];
    const defs = [], parts = [];
    let id = 0;
    const num = v => +v.toFixed(3);
    // A gradient whose offsets 0 … 1 run through u0 … u1, sampled finely enough for the smoothstep ramp.
    const stops = (u0, u1, w) => {
      const list = [];
      for (let k = 0; k <= 32; k++) {
        const o = k / 32;
        list.push(`<stop offset="${num(o)}" stop-color="#fff" stop-opacity="${num(reveal(u0 + (u1 - u0) * o, p, w))}"/>`);
      }
      return list.join('');
    };
    const linear = (d, [x1, y1, x2, y2], u0, u1, w, spread = 'pad') => {
      const gid = `tg${id++}`;
      defs.push(`<linearGradient id="${gid}" gradientUnits="userSpaceOnUse" x1="${num(x1)}" y1="${num(y1)}" x2="${num(x2)}" y2="${num(y2)}" spreadMethod="${spread}">${stops(u0, u1, w)}</linearGradient>`);
      parts.push(`<path d="${d}" fill="url(#${gid})" shape-rendering="crispEdges"/>`);
    };
    const all = `M0 0H${W}V${H}H0Z`;
    const box = (x, y, w, h) => `M${num(x)} ${num(y)}h${num(w)}v${num(h)}h${num(-w)}Z`;
    const dir = spec.dir;
    switch (type) {
      case 'wipe': {
        // dir l: the edge moves left (the new slide comes in from the right).
        const line = { l: [W, 0, 0, 0], r: [0, 0, W, 0], u: [0, H, 0, 0], d: [0, 0, 0, H] }[dir || 'l'] || [W, 0, 0, 0];
        linear(all, line, 0, 1, 1);
        break;
      }
      case 'split': {
        // Two edges from the middle out (or from the sides in): horz moves them up and down.
        const out = spec.dir !== 'in';
        const line = spec.orient === 'vert' ? [cx, 0, W, 0] : [0, cy, 0, H];
        linear(all, line, out ? 0 : 1, out ? 1 : 0, 1, 'reflect');
        break;
      }
      case 'strips': {
        // From the corner the strips leave (lu: from the bottom right), along the slide's own diagonal: u = (x/W +
        // y/H) / 2 counted from that corner; the gradient vector is u's gradient over its squared length.
        const [x1, y1, sx, sy] = { lu: [W, H, -1, -1], ru: [0, H, 1, -1], ld: [W, 0, -1, 1], rd: [0, 0, 1, 1] }[dir || 'lu'] || [W, H, -1, -1];
        const k = 2 / (W * W + H * H);
        linear(all, [x1, y1, x1 + sx * k * W * H * H, y1 + sy * k * W * W * H], 0, 1, 1);
        break;
      }
      case 'ripple': {
        // From the middle, or from the corner the direction leaves (ru: from the bottom left), a soft circle grows at a
        // steady pace and covers the slide as the transition ends (measured with PowerPoint's video: its middle at
        // 680 p − 40 px of a 1280-wide slide, 185 px soft; from a corner at 1440 p − 163, 294 px soft).
        const corner = { ru: [0, H], lu: [W, H], ld: [W, 0], rd: [0, 0] }[dir];
        const [ox, oy] = corner || [cx, cy], k = W / 1280;
        const [mid, soft] = corner ? [(1440 * p - 163) * k, 294 * k] : [(680 * p - 40) * k, 185 * k];
        const outer = Math.max(1, mid + soft / 2), at = r => Math.min(1, Math.max(0, 0.5 - (r - mid) / soft));
        const knee = Math.max(0, (mid - soft / 2) / outer);
        const gid = `tg${id++}`;
        defs.push(`<radialGradient id="${gid}" gradientUnits="userSpaceOnUse" cx="${num(ox)}" cy="${num(oy)}" r="${num(outer)}">` +
          `<stop offset="0" stop-color="#fff" stop-opacity="${num(at(0))}"/><stop offset="${num(knee)}" stop-color="#fff" stop-opacity="${num(at(knee * outer))}"/>` +
          `<stop offset="1" stop-color="#fff" stop-opacity="0"/></radialGradient>`);
        parts.push(`<path d="${all}" fill="url(#${gid})"/>`);
        break;
      }
      case 'circle': {
        const gid = `tg${id++}`;
        defs.push(`<radialGradient id="${gid}" gradientUnits="userSpaceOnUse" cx="${cx}" cy="${cy}" r="${num(Math.hypot(cx, cy))}">${stops(0, 1, 1 / 3)}</radialGradient>`);
        parts.push(`<path d="${all}" fill="url(#${gid})"/>`);
        break;
      }
      case 'diamond': {
        // Each quarter: u = (|dx| / cx + |dy| / cy) / 2, 0 in the middle, 1 in the corner.
        const k = 2 / (cx * cx + cy * cy);
        for (const [sx, sy] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
          linear(box(sx < 0 ? 0 : cx, sy < 0 ? 0 : cy, cx, cy), [cx, cy, cx + sx * k * cx * cy * cy, cy + sy * k * cx * cx * cy], 0, 1, 1 / 3);
        }
        break;
      }
      case 'plus': {
        // u = the smaller of |dx| / cx and |dy| / cy: the triangles above and below the slide's diagonals take the
        // distance across, those beside them the distance down.
        linear(`M0 0L${W} 0L${cx} ${cy}ZM0 ${H}L${W} ${H}L${cx} ${cy}Z`, [cx, 0, W, 0], 0, 1, 1 / 3, 'reflect');
        linear(`M0 0L0 ${H}L${cx} ${cy}ZM${W} 0L${W} ${H}L${cx} ${cy}Z`, [0, cy, 0, H], 0, 1, 1 / 3, 'reflect');
        break;
      }
      case 'wedge': case 'wheel': case 'wheelReverse': {
        // Angles clockwise from 12 o'clock in the slide's proportions: fans of equal opacity.
        const spokes = type === 'wedge' ? 1 : Math.max(1, spec.spokes || 4);
        const sector = 360 / spokes, w = type === 'wedge' ? 1 / 3 : 0.11;
        const uOf = a => (type === 'wedge' ? Math.min(a, 360 - a) / 180 : ((type === 'wheelReverse' ? 360 - a : a) % sector) / sector);
        const steps = 720, runs = [];
        for (let k = 0; k < steps; k++) {
          const q = Math.round(reveal(uOf((k + 0.5) / steps * 360), p, w) * 64) / 64;
          const last = runs[runs.length - 1];
          if (last && last.q === q) last.to = k + 1; else runs.push({ q, from: k, to: k + 1 });
        }
        const R = 3;   // far past the corners, in the slide's proportions
        for (const run of runs) {
          if (run.q <= 0) continue;
          const [a0, a1] = [run.from / steps * 360, run.to / steps * 360];
          const pts = [[cx, cy]];
          const n = Math.max(1, Math.ceil((a1 - a0) / 30));
          for (let j = 0; j <= n; j++) {
            const a = (a0 + (a1 - a0) * j / n) * Math.PI / 180;
            pts.push([cx + Math.sin(a) * cx * R, cy - Math.cos(a) * cy * R]);
          }
          parts.push(`<path d="M${pts.map(([x, y]) => `${num(x)} ${num(y)}`).join('L')}Z" fill="#fff" fill-opacity="${num(run.q)}" shape-rendering="crispEdges"/>`);
        }
        break;
      }
      case 'randomBar': {
        // A noise image stretched across (horizontal bars) or down: smooth streaks, each point shown by the same ramp
        // as a wipe at the moment its noise gives (measured: PowerPoint's bars fade in, varying slowly along).
        const vertical = spec.dir === 'vert';
        const image = noiseImage(vertical ? 180 : 6, vertical ? 6 : 180, 'bars');
        const table = Array.from({ length: 33 }, (_, k) => num(reveal(k / 32, p, 1))).join(' ');
        // A CSS mask takes an image's alpha: the noise goes there (white), then through the ramp.
        defs.push(`<filter id="tf" x="0" y="0" width="1" height="1" color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="0 0 0 0 1 0 0 0 0 1 0 0 0 0 1 1 0 0 0 0"/><feComponentTransfer><feFuncA type="table" tableValues="${table}"/></feComponentTransfer></filter>`);
        parts.push(`<image href="${image}" width="${W}" height="${H}" preserveAspectRatio="none" filter="url(#tf)"/>`);
        break;
      }
      case 'dissolve': {
        // Blocks of a 54 × 42 grid, each shown at once at a random moment between 7 and 93 % (measured).
        const [cols, rows] = [54, 42];
        const shown = Math.min(1, Math.max(0, (p - 0.07) / 0.86));
        const order = noiseValues(cols * rows, 'dissolve');
        let d = '';
        for (let r = 0; r < rows; r++) {
          let c = 0;
          while (c < cols) {
            if (order[r * cols + c] >= shown) { c++; continue; }
            let e = c;
            while (e < cols && order[r * cols + e] < shown) e++;
            d += box(c * W / cols, r * H / rows, (e - c) * W / cols, H / rows);
            c = e;
          }
        }
        if (d) parts.push(`<path d="${d}" fill="#fff" shape-rendering="crispEdges"/>`);
        break;
      }
      default:
        return null;
    }
    return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" preserveAspectRatio="none"><defs>${defs.join('')}</defs>${parts.join('')}</svg>`;
  }

  /** Uniform pseudo-random values (0 … 1), the same every time for a name. */
  const noiseCache = new Map();
  function noiseValues(n, name) {
    const key = `${name}:${n}`;
    let values = noiseCache.get(key);
    if (!values) {
      values = new Float64Array(n);
      let seed = 0x9e3779b9 ^ n;
      for (let i = 0; i < n; i++) {
        seed = seed + 0x6d2b79f5 | 0;
        let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
        t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
        values[i] = ((t ^ t >>> 14) >>> 0) / 4294967296;
      }
      noiseCache.set(key, values);
    }
    return values;
  }

  /** A grayscale image (data URL) of w × h noise values. */
  function noiseImage(w, h, name) {
    const key = `img:${name}:${w}x${h}`;
    let url = noiseCache.get(key);
    if (!url) {
      const values = noiseValues(w * h, name);
      const canvas = document.createElement('canvas');
      [canvas.width, canvas.height] = [w, h];
      const context = canvas.getContext('2d'), image = context.createImageData(w, h);
      for (let i = 0; i < w * h; i++) { const v = Math.round(values[i] * 255); image.data.set([v, v, v, 255], i * 4); }
      context.putImageData(image, 0, 0);
      url = canvas.toDataURL();
      noiseCache.set(key, url);
    }
    return url;
  }

  /**
   * Morph (catalog: transition.morph; measured with PowerPoint's video): shapes of the two slides with the same name
   * (!! names too, whatever their geometry) move, grow and turn from the one to the other at PowerPoint's ease
   * (accelerating for 0.1, slowing down for 0.85), the old one fading out on top of the new one, so that their colors
   * mix; by words or letters the text goes unit by unit to where the same word or letter stands on the new slide.
   * Shapes only on the old slide fade out early (0.02 … 0.26), shapes only on the new one late (from 0.43, slowing
   * down); the backgrounds crossfade.
   */
  function morphTransition(spec, a, b, reset) {
    // PowerPoint's morph starts half a frame late (measured: its ease fits from frame 31.4).
    const curve = p => ease(Math.min(1, Math.max(0, (p - 0.015) / 0.985)), { accel: 0.1, decel: 0.85 });
    const leave = p => 1 - Math.min(1, Math.max(0, (p - 0.02) / 0.24));
    const arrive = p => { const u = Math.min(1, Math.max(0, (p - 0.43) / 0.52)); return 1 - (1 - u) * (1 - u); };
    const byUnits = spec.option === 'byWord' ? 'w' : spec.option === 'byChar' ? 'c' : null;
    let plan = null;
    const shapes = slide => [...slide.querySelectorAll(':scope > .sp')];
    /** A shape's place: its middle, size and turn on the slide, and the map from its own box to the unit square. */
    const placeOf = el => {
      const m = parseMatrix(el.style.transform);
      const [w0, h0] = [parseFloat(el.style.width) || el.offsetWidth || 1, parseFloat(el.style.height) || el.offsetHeight || 1];
      const [cx, cy] = apply(m, w0 / 2, h0 / 2);
      const w = w0 * Math.hypot(m[0], m[1]), h = h0 * Math.hypot(m[2], m[3]);
      const angle = Math.atan2(m[1], m[0]) * 180 / Math.PI;
      const local = multiply(invert(multiply(translate(cx, cy), multiply(rotate(angle), scale(Math.max(w, 1e-6), Math.max(h, 1e-6))))), m);
      return { cx, cy, w, h, angle, local, original: el.style.transform };
    };
    const placed = (place, from, to, e) => {
      let turn = to.angle - from.angle;
      turn -= 360 * Math.round(turn / 360);
      const [cx, cy, w, h] = [from.cx + (to.cx - from.cx) * e, from.cy + (to.cy - from.cy) * e, from.w + (to.w - from.w) * e, from.h + (to.h - from.h) * e];
      return css(multiply(translate(cx, cy), multiply(rotate(from.angle + turn * e), multiply(scale(w, h), place.local))));
    };
    /** The text units of a shape (letters, or words of letters) with their text, in reading order. */
    const unitsOf = el => {
      const group = el.querySelector('[data-tx][data-text]');
      if (!group) return [];
      const text = group.dataset.text, letters = [...el.querySelectorAll('[data-tx] [data-c]')];
      if (byUnits === 'c') return letters.map(g => ({ key: text[+g.dataset.c] || '', els: [g] })).filter(u => u.key.trim());
      const words = new Map();
      for (const g of letters) {
        if (g.dataset.w === '-1') continue;
        if (!words.has(g.dataset.w)) words.set(g.dataset.w, { key: '', els: [] });
        const word = words.get(g.dataset.w);
        word.key += text[+g.dataset.c] || '';
        word.els.push(g);
      }
      return [...words.values()];
    };
    /** The middle of elements on the slide, in the slide's own pixels. */
    const middleOf = (els, slide) => {
      const r = slide.getBoundingClientRect(), k = (r.width / W) || 1;
      let [x0, y0, x1, y1] = [Infinity, Infinity, -Infinity, -Infinity];
      for (const e of els) { const b = e.getBoundingClientRect(); x0 = Math.min(x0, b.left); y0 = Math.min(y0, b.top); x1 = Math.max(x1, b.right); y1 = Math.max(y1, b.bottom); }
      return [((x0 + x1) / 2 - r.left) / k, ((y0 + y1) / 2 - r.top) / k];
    };
    const setup = () => {
      const named = new Map();
      for (const el of shapes(b)) if (el.dataset.name) { if (!named.has(el.dataset.name)) named.set(el.dataset.name, []); named.get(el.dataset.name).push(el); }
      const pairs = [], gone = [];
      for (const x of shapes(a)) {
        const list = x.dataset.name && named.get(x.dataset.name);
        if (list && list.length) pairs.push({ x, y: list.shift() }); else gone.push(x);
      }
      const taken = new Set(pairs.map(pair => pair.y));
      const come = shapes(b).filter(el => !taken.has(el));
      for (const pair of pairs) {
        pair.from = placeOf(pair.x);
        pair.to = placeOf(pair.y);
        // Shapes of different geometry turn the one outline into the other (measured: a rectangle bulges into a
        // circle): both outlines sampled alike, from the points that lie closest, and the colors mixed.
        if (pair.x.dataset.geom !== pair.y.dataset.geom) pair.outline = outlines(pair);
        if (!byUnits) continue;
        // Units with the same text pair in reading order; each keeps its place in its shape while the shape moves.
        const [ux, uy] = [unitsOf(pair.x), unitsOf(pair.y)];
        if (!ux.length && !uy.length) continue;
        const waiting = new Map();
        for (const u of uy) { if (!waiting.has(u.key)) waiting.set(u.key, []); waiting.get(u.key).push(u); }
        pair.moves = [];
        pair.leaving = [];
        for (const u of ux) {
          const list = waiting.get(u.key);
          const v = list && list.shift();
          if (!v) { pair.leaving.push(u); continue; }
          pair.moves.push({ u, v, start: middleOf(u.els, a), end: middleOf(v.els, b), local: (() => {
            try { const bb = u.els.map(e => e.getBBox()); const [x0, y0] = [Math.min(...bb.map(q => q.x)), Math.min(...bb.map(q => q.y))]; return [(x0 + Math.max(...bb.map(q => q.x + q.width))) / 2, (y0 + Math.max(...bb.map(q => q.y + q.height))) / 2]; } catch { return [0, 0]; }
          })() });
        }
        pair.arriving = [...waiting.values()].flat();
      }
      // The backgrounds crossfade beneath both slides' shapes: copies of them on a layer of their own.
      const layer = document.createElement('div');
      layer.style.cssText = `position:absolute;left:0;top:0;width:${W}px;height:${H}px;z-index:0;pointer-events:none`;
      const grounds = [a, b].map(slide => {
        const holder = document.createElement('div');
        holder.style.cssText = `position:absolute;inset:0;background:${getComputedStyle(slide).backgroundColor}`;
        const bg = background(slide);
        if (bg) { holder.appendChild(bg.cloneNode(true)); bg.style.visibility = 'hidden'; }
        slide.style.background = 'transparent';
        layer.appendChild(holder);
        return holder;
      });
      a.parentNode.insertBefore(layer, a.parentNode.firstChild);
      // Outlines turning into others are drawn between the slides' shapes.
      const drawing = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      drawing.setAttribute('width', W);
      drawing.setAttribute('height', H);
      drawing.style.cssText = 'position:absolute;left:0;top:0;z-index:2;pointer-events:none;overflow:visible';
      for (const pair of pairs) if (pair.outline) drawing.appendChild(pair.outline.path);
      a.parentNode.appendChild(drawing);
      plan = { pairs, gone, come, layer, grounds, drawing };
    };
    const background = slide => slide.querySelector(':scope > .bg');
    return {
      frame(p) {
        if (!plan) setup();
        const e = curve(p);
        // The old slide's shapes lie on top of the new one's, the backgrounds beneath both.
        a.style.zIndex = 3;
        b.style.zIndex = 1;
        plan.grounds[1].style.opacity = String(e);
        for (const el of plan.gone) el.style.opacity = String(leave(p));
        for (const el of plan.come) el.style.opacity = String(arrive(p));
        for (const pair of plan.pairs) {
          pair.x.style.transform = placed(pair.from, pair.from, pair.to, e);
          pair.y.style.transform = placed(pair.to, pair.from, pair.to, e);
          if (pair.outline) morphOutline(pair, e);
          if (!pair.moves) { pair.x.style.opacity = String(1 - e); continue; }
          // Text by units: the old shape's units travel, the new shape's paired ones wait hidden.
          pair.x.style.opacity = '';
          const m = multiply(translate(0, 0), parseMatrix(pair.x.style.transform));
          const inverse = invert(m);
          for (const move of pair.moves) {
            const target = [move.start[0] + (move.end[0] - move.start[0]) * e, move.start[1] + (move.end[1] - move.start[1]) * e];
            const [lx, ly] = apply(inverse, target[0], target[1]);
            const shift = `translate(${(lx - move.local[0]).toFixed(2)} ${(ly - move.local[1]).toFixed(2)})`;
            for (const g of move.u.els) g.setAttribute('transform', shift);
            for (const g of move.v.els) g.style.visibility = 'hidden';
          }
          for (const u of pair.leaving) for (const g of u.els) g.style.opacity = String(leave(p));
          for (const u of pair.arriving) for (const g of u.els) g.style.opacity = String(arrive(p));
        }
      },
      end() {
        if (plan) {
          for (const el of [...plan.gone, ...plan.come]) el.style.opacity = '';
          for (const pair of plan.pairs) {
            pair.x.style.transform = pair.from.original;
            pair.y.style.transform = pair.to.original;
            pair.x.style.opacity = '';
            for (const move of pair.moves || []) {
              for (const g of move.u.els) g.removeAttribute('transform');
              for (const g of move.v.els) g.style.visibility = '';
            }
            for (const u of [...(pair.leaving || []), ...(pair.arriving || [])]) for (const g of u.els) g.style.opacity = '';
          }
        }
        if (plan) {
          plan.layer.remove();
          plan.drawing.remove();
          for (const pair of plan.pairs) if (pair.outline) for (const g of pair.outline.hidden) g.style.visibility = '';
        }
        for (const slide of [a, b]) {
          slide.style.background = '';
          if (background(slide)) background(slide).style.visibility = '';
        }
        plan = null;
        reset();
      },
    };
  }

  /**
   * The outlines of a morph pair whose geometry differs: both shapes' first filled path sampled at 128 points in
   * their unit squares, each from where its path starts; null when either has none.
   */
  function outlines(pair) {
    const N = 128;
    const sample = (el, place) => {
      // The shape's drawing: its background group, or (without text) its paths themselves.
      const path = el.querySelector('[data-bg] path') || el.querySelector(':scope > svg > path');
      if (!path || !path.getTotalLength) return null;
      let length;
      try { length = path.getTotalLength(); } catch { return null; }
      if (!(length > 0)) return null;
      const points = [];
      for (let i = 0; i < N; i++) {
        const q = path.getPointAtLength(length * i / N);
        points.push(apply(place.local, q.x, q.y));
      }
      return { points, fill: getComputedStyle(path).fill, group: el.querySelector('[data-bg]') || path };
    };
    const [from, to] = [sample(pair.x, pair.from), sample(pair.y, pair.to)];
    if (!from || !to) return null;
    // Start to start, as the outlines are drawn (measured: a rectangle from its top left corner turns into a circle
    // from its left, so that the shape between seems to turn).
    const best = { points: to.points };
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    const color = c => { const m = /^rgba?\((\d+),\s*(\d+),\s*(\d+)/.exec(c || ''); return m ? [+m[1], +m[2], +m[3]] : null; };
    from.group.style.visibility = to.group.style.visibility = 'hidden';
    return { path, from: from.points, to: best.points, fills: [color(from.fill), color(to.fill)], fill: to.fill, hidden: [from.group, to.group] };
  }

  /** Draws a morph pair's outline at e: the points mixed and placed in the pair's box of the moment. */
  function morphOutline(pair, e) {
    const o = pair.outline, from = pair.from, to = pair.to;
    let turn = to.angle - from.angle;
    turn -= 360 * Math.round(turn / 360);
    const m = multiply(translate(from.cx + (to.cx - from.cx) * e, from.cy + (to.cy - from.cy) * e),
      multiply(rotate(from.angle + turn * e), scale(from.w + (to.w - from.w) * e, from.h + (to.h - from.h) * e)));
    const d = o.from.map(([x0, y0], i) => {
      const [x1, y1] = o.to[i];
      const [x, y] = apply(m, x0 + (x1 - x0) * e, y0 + (y1 - y0) * e);
      return `${i ? 'L' : 'M'}${x.toFixed(2)} ${y.toFixed(2)}`;
    }).join('') + 'Z';
    o.path.setAttribute('d', d);
    const [c0, c1] = o.fills;
    o.path.setAttribute('fill', c0 && c1 ? `rgb(${c0.map((v, i) => Math.round(v + (c1[i] - v) * e)).join(',')})` : o.fill);
  }

  /**
   * Cube, Box, Rotate and Orbit (p14:prism; measured with PowerPoint's videos): the slides are faces of a pentagonal
   * prism turning by 72° with a sine ease, seen from 1.49 slide heights in front (at the middle each face is turned by
   * 36°). From outside (Cube) the prism keeps its nearest edge in the slide plane (silhouettes within 0.3 %); from
   * inside (isInverted: Box) its sides are 7 % wider than the slides, which sit in their middles, and its axis comes
   * from the old slide's apothem in front of it to 0.3 of that and back (sin²). isContent turns only the content
   * while the backgrounds crossfade. A face turned by φ darkens to 1 - 0.19 (1 - cos φ).
   */
  function prismTransition(spec, a, b, reset) {
    const vertical = spec.dir === 'u' || spec.dir === 'd';
    const sign = spec.dir === 'r' || spec.dir === 'd' ? -1 : 1;
    const inverted = !!spec.isInverted, content = !!spec.isContent;
    const ext = 72 * Math.PI / 180, eye = 1.493;
    return glTransition(a, b, (scene, p, oldImage, newImage, oldGround, newGround) => {
      const r = ext * sineInOut(p);
      const span = vertical ? H : W;
      const side = inverted ? span * 1.0703 : span;
      const apothem = side / (2 * Math.tan(ext / 2)), radius = side / (2 * Math.sin(ext / 2));
      const axis = inverted ? apothem * (1 - 0.704 * Math.sin(Math.PI * p) ** 2) : -radius * Math.cos(ext / 2 - r);
      const meshes = [];
      if (content) {
        meshes.push({ tex: oldGround, ...quad([0, 0, 0], [W, 0, 0], [W, H, 0], [0, H, 0]), flat: true });
        meshes.push({ tex: newGround, ...quad([0, 0, 0], [W, 0, 0], [W, H, 0], [0, H, 0]), flat: true, alpha: sineInOut(p) });
      }
      // Faces: the old one turning away, the new one coming from the side (angles of their normals).
      for (const [image, angle] of [[content ? oldImage : oldImage, -r], [newImage, ext - r]]) {
        const phi = sign * (inverted ? -angle : angle);
        const [n, t] = [Math.sin(phi), Math.cos(phi)];
        // Face middle and its direction along the slide's width (or height), in the turning plane (across, depth).
        const [cu, cz] = inverted ? [-apothem * n, axis - apothem * t] : [apothem * n, axis + apothem * t];
        const [du, dz] = [t, -n];
        // Faces the eye sees from behind are left out (the eye stands eye · H in front of the slide's middle).
        if ((-cu) * n + (eye * H - cz) * t <= 0) continue;
        const corner = (k, v) => {
          const u = (k - 0.5) * span;
          const across = span / 2 + cu + u * du, depth = cz + u * dz;
          return vertical ? [v * W, across, depth] : [across, v * H, depth];
        };
        const q = vertical
          ? quad(corner(0, 0), corner(0, 1), corner(1, 1), corner(1, 0), [0, 0, 1, 1])
          : quad(corner(0, 0), corner(1, 0), corner(1, 1), corner(0, 1));
        if (vertical) q.uv = [0, 0, 1, 0, 1, 1, 0, 0, 1, 1, 0, 1];
        meshes.push({ tex: image, ...q, shade: 1 - 0.19 * (1 - Math.abs(t)) });
      }
      scene.draw(meshes, eye, [0, 0, 0, 1]);
    }, reset, content ? ['content', 'background'] : ['all']);
  }

  /**
   * Flip (p14:flip; measured with PowerPoint's video, per-frame poses within 0.5 %): the old slide turns about its
   * vertical middle, its far side going back first (dir l: the left one), the new slide on its back; the turn
   * follows a quartic ease centered at 0.465, the slide going back up to 0.44 slide heights at the middle.
   */
  function flipTransition(spec, a, b, reset) {
    const sign = spec.dir === 'r' ? -1 : 1, eye = 1.493;
    const quart = powInOut(4);
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const theta = Math.PI * quart(Math.min(1, Math.max(0, (p + 0.033) / 0.984)));
      const depth = -0.439 * H * Math.max(0, 1 - 2 * Math.abs(p - 0.465)) ** 2.35;
      const [c, sn] = [Math.cos(theta), sign * Math.sin(theta)];
      const corner = (u, v) => [W / 2 + u * c, v, depth + u * sn];
      const back = c < 0;
      const q = quad(corner(-W / 2, 0), corner(W / 2, 0), corner(W / 2, H), corner(-W / 2, H), back ? [1, 0, 0, 1] : [0, 0, 1, 1]);
      scene.draw([{ tex: back ? newImage : oldImage, ...q, shade: 1 - 0.19 * (1 - Math.abs(c)) }], eye, [0, 0, 0, 1]);
    }, reset);
  }

  /** Values between keyframes [p, v1, v2 …] (straight between them, flat beyond). */
  function keyframes(frames, p) {
    if (p <= frames[0][0]) return frames[0].slice(1);
    for (let i = 1; i < frames.length; i++) {
      const [p1, ...v1] = frames[i];
      if (p <= p1) {
        const [p0, ...v0] = frames[i - 1], t = (p - p0) / (p1 - p0);
        return v0.map((v, k) => v + (v1[k] - v) * t);
      }
    }
    return frames[frames.length - 1].slice(1);
  }

  /** A slide's quad turned about its vertical middle (degrees, its right side coming forward), moved by (x, z). */
  function standing(theta, x, z, y = 0, uv) {
    const r = theta * Math.PI / 180, [c, sn] = [Math.cos(r), Math.sin(r)];
    const corner = (u, v) => [W / 2 + x + u * c, v + y, z + u * sn];
    return quad(corner(-W / 2, 0), corner(W / 2, 0), corner(W / 2, H), corner(-W / 2, H), uv);
  }

  /**
   * Switch (p14:switch; poses fitted frame by frame to PowerPoint's video): the old slide moves aside turning away
   * while the new one waits behind it on the other side, turned toward it; at the middle they change places in depth,
   * the new one comes forward and straightens, the old one leaves behind it.
   */
  function switchTransition(spec, a, b, reset) {
    const sign = spec.dir === 'r' ? -1 : 1;
    // p: old turn, across, depth | new turn, across, depth (pixels of a 1280-wide slide)
    const frames = [[0, 0, 0, 0, 60, 900, -900], [0.2, -10, -200, 0, 25, 440, -300], [0.46, -22, -590, -10, 22, 430, -190],
      [0.53, -20, -480, -150, 20, 590, -95], [0.66, -21, -430, -170, 17, 490, -90], [0.79, -27, -480, -240, 12, 220, -55],
      [0.92, -35, -1500, -250, 4, 60, 0], [1, -35, -2000, -300, 0, 0, 0]];
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const [t0, x0, z0, t1, x1, z1] = keyframes(frames, p);
      const k = W / 1280;
      scene.draw([
        { tex: oldImage, ...standing(sign * t0, sign * x0 * k, z0 * k), shade: 1 - 0.19 * (1 - Math.cos(t0 * Math.PI / 180)) },
        { tex: newImage, ...standing(sign * t1, sign * x1 * k, z1 * k), shade: 1 - 0.19 * (1 - Math.cos(t1 * Math.PI / 180)) },
      ], 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /** A slide's quad scaled about a point (cx, cy) and turned about z there, at depth z. */
  function flatQuad(scale, cx = W / 2, cy = H / 2, z = 0, turnDeg = 0, uv) {
    const r = turnDeg * Math.PI / 180, [c, sn] = [Math.cos(r), Math.sin(r)];
    const corner = (x, y) => {
      const [dx, dy] = [(x - W / 2) * scale, (y - H / 2) * scale];
      return [cx + dx * c - dy * sn, cy + dx * sn + dy * c, z];
    };
    return quad(corner(0, 0), corner(W, 0), corner(W, H), corner(0, H), uv);
  }

  /** The backgrounds of a content transition: the old one, the new one over it fading in by fade (0 … 1). */
  function grounds(fade, oldGround, newGround) {
    return [
      { tex: oldGround, ...quad([0, 0, -1], [W, 0, -1], [W, H, -1], [0, H, -1]), flat: true },
      { tex: newGround, ...quad([0, 0, -1], [W, 0, -1], [W, H, -1], [0, H, -1]), flat: true, alpha: fade },
    ];
  }

  // Fly Through with a bounce: the new content's depth (share of the eye's distance; toward the viewer for out)
  // arrives early and springs back (measured with PowerPoint's video).
  const flyBounce = measured([[0, -1.78], [0.462, -0.776], [0.528, -0.658], [0.593, -0.522], [0.659, -0.348], [0.692, -0.263],
    [0.725, -0.166], [0.757, -0.066], [0.79, 0.013], [0.823, 0.029], [0.856, 0.018], [0.889, -0.001], [0.921, -0.018], [0.954, -0.012],
    [0.987, -0.003], [1, 0]]);
  // Ferris Wheel from the left: the new content's turn (degrees), scale and offset (pixels of a 1280 × 720 slide) as
  // it comes down from the top left (measured with PowerPoint's video).
  const ferrisArrival = [[0.45, -20, 0.64, -410, -620], [0.528, -17.9, 0.649, -332, -516], [0.593, -14.5, 0.66, -268, -417],
    [0.659, -14.5, 0.652, -225, -324], [0.725, -11, 0.691, -164, -237], [0.79, -5.8, 0.758, -88, -152], [0.856, -3.4, 0.857, -45, -82],
    [0.921, -1.1, 0.952, -12, -24], [1, 0, 1, 0, 0]];

  /**
   * The Office 2010 transitions that move the slides' content while their backgrounds crossfade (catalog:
   * transition.office2010; poses fitted frame by frame to PowerPoint's videos, depths in shares of the eye's
   * distance D):
   * - Pan slides the content across at PowerPoint's ease (accelerating for 0.12, slowing down for 0.8), the
   *   backgrounds crossfading at the same pace;
   * - Conveyor turns the content's plane (its far side away, up to 14.8°) and moves it back until 0.36, a belt on it
   *   carries the old content off and the new one on (0.36 … 0.66), then the plane comes back;
   * - Ferris Wheel swings the old content down and forward about an axis above it (112.5° p^1.82) and brings the new
   *   one down from the top left, growing and turning straight;
   * - Fly Through flies the old content toward the viewer (out: away; 2.4 D p², with a bounce 1.95 D p²) fading as
   *   it goes, while the new one comes from the other side (2.4 D (1 − p)², or arriving early and springing back),
   *   fading in linearly;
   * - Window opens the old content in two halves turning their outer edges toward the viewer (200° p²) as they come
   *   forward (1.8 D p²) and fade, while the new one comes from 1.9 D behind at a steady pace until 0.72, fading in.
   * The other backgrounds crossfade along a sine.
   */
  function contentTransition(type, spec, a, b, reset) {
    const dir = spec.dir || 'l';
    const [vx, vy] = offsets[dir] || offsets.l;
    const k = W / 1280, D = 1.493 * H;
    const clamp = x => Math.min(1, Math.max(0, x));
    const panEase = p => ease(clamp(p), { accel: 0.12, decel: 0.8 });
    return glTransition(a, b, (scene, p, oldContent, newContent, oldGround, newGround) => {
      const meshes = grounds(type === 'pan' ? panEase(p) : sineInOut(p), oldGround, newGround);
      // The content, drawn far to near over the backgrounds: [depth, mesh].
      const content = [];
      if (type === 'pan') {
        const e = panEase(p);
        content.push([0, { tex: oldContent, ...flatQuad(1, W / 2 + vx * e * W, H / 2 + vy * e * H) }]);
        content.push([0, { tex: newContent, ...flatQuad(1, W / 2 - vx * (1 - e) * W, H / 2 - vy * (1 - e) * H) }]);
      } else if (type === 'conveyor') {
        const sign = dir === 'r' ? -1 : 1;
        const tilt = p < 0.66 ? Math.min(1, p / 0.33) : (1 - p) / 0.34;
        const theta = sign * (p < 0.66 ? Math.min(14.8, 43 * p) : 14.8 * (1 - p) / 0.34);
        const back = -155 * k * tilt, r = theta * Math.PI / 180;
        const e = smooth((p - 0.36) / 0.3), span = 1.108 * W;
        // Along the belt: s across it, the plane turned by theta.
        const onBelt = s => standing(theta, s * Math.cos(r), back + s * Math.sin(r));
        content.push([back, { tex: oldContent, ...onBelt(-sign * e * span) }]);
        content.push([back, { tex: newContent, ...onBelt(sign * (1 - e) * span) }]);
      } else if (type === 'ferris') {
        const sign = dir === 'r' ? -1 : 1;
        if (p < 0.6) {
          // The old content swings down and forward about an axis above it and in front (pixels of a 1280 × 720
          // slide), turning and drifting a little sideways.
          const t = p ** 1.82, [ya, za] = [-896 * k, 1171 * k];
          const psi = 112.5 * t * Math.PI / 180, roll = sign * 17.3 * t * Math.PI / 180, drift = sign * 467 * t * k;
          const swing = (x, y) => {
            const [dy, dz] = [y - H / 2 - ya, -za];
            const [Y, Z] = [ya + dy * Math.cos(psi) - dz * Math.sin(psi), za + dy * Math.sin(psi) + dz * Math.cos(psi)];
            const X = x - W / 2;
            return [W / 2 + drift + X * Math.cos(roll) - Y * Math.sin(roll), H / 2 + X * Math.sin(roll) + Y * Math.cos(roll), Z];
          };
          content.push([0, { tex: oldContent, ...quad(swing(0, 0), swing(W, 0), swing(W, H), swing(0, H)) }]);
        }
        if (p > ferrisArrival[0][0]) {
          const [turnDeg, scale, x, y] = keyframes(ferrisArrival, p);
          content.push([0, { tex: newContent, ...flatQuad(scale, W / 2 + sign * x * k, H / 2 + y * k, 0, sign * turnDeg) }]);
        }
      } else if (type === 'flythrough') {
        const out = spec.dir === 'out' ? -1 : 1, bounce = !!spec.hasBounce;
        const zOld = out * (bounce ? 1.95 : 2.4) * p * p;
        const zNew = bounce ? out * flyBounce(p) : -out * 2.4 * (1 - p) * (1 - p);
        content.push([zOld, { tex: oldContent, ...flatQuad(1, W / 2, H / 2, zOld * D), alpha: clamp(1 - Math.abs(zOld)) }]);
        content.push([zNew, { tex: newContent, ...flatQuad(1, W / 2, H / 2, zNew * D), alpha: clamp(p) }]);
      } else if (type === 'window') {
        const vertical = spec.dir === 'vert', q = p * p;
        const phi = 200 * q * Math.PI / 180, forward = 1.8 * q * D, apart = 220 * q * k;
        const [c, sn] = [Math.cos(phi), Math.sin(phi)];
        for (const half of [0, 1]) {
          const side = half ? 1 : -1;
          const uv = vertical ? [half / 2, 0, (half + 1) / 2, 1] : [0, half / 2, 1, (half + 1) / 2];
          // Each half turns about its own middle line, its outer edge coming forward.
          const place = (x, y) => {
            if (vertical) {
              const mid = (half ? 3 : 1) * W / 4, u = x - mid;
              return [mid + side * apart + u * c, y, forward + side * u * sn];
            }
            const mid = (half ? 3 : 1) * H / 4, v = y - mid;
            return [x, mid + side * apart + v * c, forward + side * v * sn];
          };
          const [x0, y0, x1, y1] = [uv[0] * W, uv[1] * H, uv[2] * W, uv[3] * H];
          content.push([forward / D, { tex: oldContent, ...quad(place(x0, y0), place(x1, y0), place(x1, y1), place(x0, y1), uv), alpha: clamp(1 - 2.3 * q) }]);
        }
        const zNew = -1.9 * Math.max(0, 1 - p / 0.72);
        content.push([zNew, { tex: newContent, ...flatQuad(1, W / 2, H / 2, zNew * D), alpha: clamp(p) }]);
      }
      content.sort((m, n) => m[0] - n[0]);
      scene.draw([...meshes, ...content.map(([, mesh]) => ({ ...mesh, flat: true }))], 1.493, [0, 0, 0, 1]);
    }, reset, ['content', 'background']);
  }

  /**
   * Doors and Warp (catalog: transition.office2010; from PowerPoint's videos): Doors opens the old slide in two
   * halves swinging toward the viewer about their outer edges while the new one comes forward from behind; Warp in
   * lets the old slide sink into black while the new one grows from the middle, Warp out shrinks the old slide into
   * the middle over the new one.
   */
  function doorsTransition(type, spec, a, b, reset) {
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const e = sineInOut(p), meshes = [];
      if (type === 'doors') {
        // (measured with PowerPoint's video) the doors swing open by 150° p², darkening, while the new slide grows
        // from half size over them, fading in.
        const vertical = spec.dir === 'vert';
        const swing = 150 * p * p;
        for (const half of [0, 1]) {
          const r = swing * Math.PI / 180 * (half ? 1 : -1);
          const [c, sn] = [Math.cos(r), Math.abs(Math.sin(r))];
          const uv = vertical ? [half / 2, 0, (half + 1) / 2, 1] : [0, half / 2, 1, (half + 1) / 2];
          // Hinged at the outer edge, the inner edge coming toward the viewer.
          const place = (x, y) => {
            if (vertical) {
              const hinge = half ? W : 0, d = x - hinge;
              return [hinge + d * c * (half ? 1 : 1), y, Math.abs(d) * sn];
            }
            const hinge = half ? H : 0, d = y - hinge;
            return [x, hinge + d * c, Math.abs(d) * sn];
          };
          const [x0, y0, x1, y1] = [uv[0] * W, uv[1] * H, uv[2] * W, uv[3] * H];
          if (Math.cos(r) <= 0.05) continue;
          meshes.push({ tex: oldImage, ...quad(place(x0, y0), place(x1, y0), place(x1, y1), place(x0, y1), uv), shade: 1 - 0.6 * smooth(p / 0.7) });
        }
        meshes.push({ tex: newImage, ...flatQuad(0.5 + 0.5 * smooth((p - 0.2) / 0.75)), alpha: smooth(p / 0.7), flat: true, over: true });
      } else {
        // Warp
        // (measured with PowerPoint's videos) in: the old slide grows (1 + 3 p²) and darkens out by 0.68 while the new
        // one grows from half size over it, fading in; out: the old slide shrinks to half size over the new one, fading
        // out, while the new one comes back from large.
        if (spec.dir === 'in') {
          meshes.push({ tex: oldImage, ...flatQuad(1 + 3 * p * p), shade: Math.max(0, 1 - (p / 0.68) ** 2), flat: true });
          meshes.push({ tex: newImage, ...flatQuad(0.5 + 0.5 * smooth((p - 0.2) / 0.75)), alpha: Math.min(1, 1.1 * p), flat: true });
        } else {
          meshes.push({ tex: newImage, ...flatQuad(1 + 3 * (1 - p) * (1 - p)), flat: true });
          meshes.push({ tex: oldImage, ...flatQuad(1 - 0.5 * smooth(p / 0.85)), alpha: Math.max(0, 1 - p), flat: true });
        }
      }
      scene.draw(meshes, 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /**
   * Gallery (p14:gallery; from PowerPoint's video): the slides hang side by side on a wall that turns a little
   * (right side forward) and moves back while the old one slides off and the new one into place; a floor below
   * mirrors them, darkening.
   */
  function galleryTransition(spec, a, b, reset) {
    const sign = spec.dir === 'r' ? -1 : 1;
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      // The wall turns early and stays turned while the slides pass along it (from 0.33 to 0.75).
      const e = smooth((p - 0.33) / 0.42), turned = smooth(p / 0.15) * (1 - smooth((p - 0.75) / 0.25));
      const angle = sign * 16 * turned * Math.PI / 180, depth = -0.15 * W * smooth(p / 0.3) * (1 - smooth((p - 0.6) / 0.4)), gap = 0.12 * W;
      const [c, sn] = [Math.cos(angle), Math.sin(angle)];
      const meshes = [];
      const hang = (image, along) => {
        const corner = (u, y) => { const t = along + u; return [W / 2 + t * c, y, depth + t * sn]; };
        const [l, r] = [-W / 2, W / 2];
        meshes.push({ tex: image, ...quad(corner(l, 0), corner(r, 0), corner(r, H), corner(l, H)) });
        // The floor's mirror image beneath, fading into the dark.
        const q = quad(corner(l, H), corner(r, H), corner(r, 2 * H), corner(l, 2 * H), [0, 1, 1, 0]);
        meshes.push({ tex: image, ...q, shade: [0.15, 0.15, 0, 0.15, 0, 0] });
      };
      hang(oldImage, -sign * (W + gap) * e);
      hang(newImage, sign * (W + gap) * (1 - e));
      scene.draw(meshes, 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /** A random number generator from a seed (Park–Miller), the same numbers for every frame of a transition. */
  function seeded(seed) {
    let state = Math.max(1, Math.floor(seed) % 2147483647);
    return () => (state = (state * 16807) % 2147483647) / 2147483647;
  }

  /**
   * Glitter (p14:glitter; measured with PowerPoint's video): the slide in diamonds (30 across, 15 down, staggered)
   * or hexagons (pointy, 36 × 30 px of a 1280 × 720 slide, rows 22.5 px apart) that turn over about their vertical
   * middles, the new slide on their backs, darker as they turn edge on; from the side the direction names (r: from
   * the right) each starts at 0.057 + 0.569 d (d: its distance from that side, 0 … 1) give or take 0.1 and turns
   * evenly for 0.32.
   */
  function glitterTransition(spec, a, b, reset) {
    const hex = spec.pattern === 'hexagon', dir = spec.dir || 'l', random = seeded(4711);
    const tiles = [];
    if (hex) {
      const [w, h] = [36 * W / 1280, 30 * H / 720];
      const shape = [[0, -h / 2], [w / 2, -h / 4], [w / 2, h / 4], [0, h / 2], [-w / 2, h / 4], [-w / 2, -h / 4]];
      for (let j = -1; j * 0.75 * h < H + h; j++) for (let i = -1; i * w < W + w; i++) tiles.push({ x: i * w + (j & 1 ? w / 2 : 0), y: j * 0.75 * h, shape });
    } else {
      const [w, h] = [W / 30, H / 15];
      const shape = [[0, -h / 2], [w / 2, 0], [0, h / 2], [-w / 2, 0]];
      for (let j = -1; j * h / 2 < H + h; j++) for (let i = -1; i * w < W + w; i++) tiles.push({ x: i * w + (j & 1 ? w / 2 : 0), y: j * h / 2, shape });
    }
    for (const t of tiles) {
      const d = { l: t.x / W, r: 1 - t.x / W, u: t.y / H, d: 1 - t.y / H }[dir] ?? t.x / W;
      t.start = 0.057 + 0.569 * Math.min(1, Math.max(0, d)) + 0.197 * (random() - 0.5);
    }
    return turningTiles(a, b, reset, tiles, 0.32);
  }

  /**
   * Tiles of the slide (polygons about their middles x, y) that turn over about their vertical middle line (or their
   * horizontal one) evenly for a while from their start, the old slide on their fronts and the new one on their
   * backs, darker as they turn edge on.
   */
  function turningTiles(a, b, reset, tiles, duration, horizontal = false) {
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const front = { tex: oldImage, pos: [], uv: [], shade: [] }, back = { tex: newImage, pos: [], uv: [], shade: [] };
      for (const t of tiles) {
        const theta = Math.PI * Math.min(1, Math.max(0, (p - t.start) / duration));
        const [c, sn] = [Math.cos(theta), Math.sin(theta)];
        // The back mirrored, so that it shows the new slide in place when turned over.
        const [mesh, mirror] = c >= 0 ? [front, 1] : [back, -1];
        const shade = 0.15 + 0.85 * Math.abs(c) ** 0.6;
        const n = t.shape.length;
        for (let k = 0; k < n; k++) {
          for (const [lx, ly] of [[0, 0], t.shape[k], t.shape[(k + 1) % n]]) {
            if (horizontal) {
              mesh.pos.push(t.x + lx, t.y + ly * c, ly * sn);
              mesh.uv.push((t.x + lx) / W, (t.y + mirror * ly) / H);
            } else {
              mesh.pos.push(t.x + lx * c, t.y + ly, lx * sn);
              mesh.uv.push((t.x + mirror * lx) / W, (t.y + ly) / H);
            }
            mesh.shade.push(shade);
          }
        }
      }
      scene.draw([front, back], 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /**
   * Blinds and Checkerboard as PowerPoint plays them now (measured with its videos): Blinds turns bands 40 px wide
   * (of a 1280-wide slide) over about their long middle lines, from the middle of the slide out to its edges (0.08 …
   * 0.53, each for 0.22); Checkerboard turns 14 × 10 tiles over, across (dir vert: down) the slide give or take a
   * little (0.12 … 0.57, each for 0.22).
   */
  function blindsTransition(type, spec, a, b, reset) {
    const random = seeded(type === 'blinds' ? 769 : 1025), tiles = [];
    const rect = (w, h) => [[-w / 2, -h / 2], [w / 2, -h / 2], [w / 2, h / 2], [-w / 2, h / 2]];
    if (type === 'blinds') {
      // dir horz (the default): bands across the slide turning about horizontal lines.
      const across = spec.dir !== 'vert', size = 40 * W / 1280;
      const count = Math.max(1, Math.round((across ? H : W) / size)), step = (across ? H : W) / count;
      for (let k = 0; k < count; k++) {
        const at = (k + 0.5) * step, d = Math.abs(at - (across ? H : W) / 2) / ((across ? H : W) / 2);
        tiles.push({ x: across ? W / 2 : at, y: across ? at : H / 2, shape: across ? rect(W, step) : rect(step, H), start: 0.08 + 0.45 * d });
      }
      return turningTiles(a, b, reset, tiles, 0.22, across);
    }
    const down = spec.dir === 'vert', [nx, ny] = [14, 10], [w, h] = [W / nx, H / ny];
    for (let j = 0; j < ny; j++) {
      for (let i = 0; i < nx; i++) {
        const [x, y] = [(i + 0.5) * w, (j + 0.5) * h];
        tiles.push({ x, y, shape: rect(w, h), start: 0.12 + 0.45 * (down ? y / H : x / W) + 0.12 * (random() - 0.5) });
      }
    }
    return turningTiles(a, b, reset, tiles, 0.22, down);
  }

  /**
   * Vortex (p14:vortex; from PowerPoint's video): the old slide breaks into small squares that, from the side the
   * direction names (l: from the right) to the other, swirl off into the distance toward that side; the new slide's
   * squares come back the same way and land from the other side, the last as the transition ends.
   */
  function vortexTransition(spec, a, b, reset) {
    const dir = spec.dir || 'l', across = dir === 'l' || dir === 'r', sign = dir === 'l' || dir === 'u' ? 1 : -1;
    const size = W / 128, [nx, ny] = [128, Math.round(H / size)];
    const random = seeded(3863);
    const tiles = [];
    for (let j = 0; j < ny; j++) {
      for (let i = 0; i < nx; i++) {
        const [x, y] = [(i + 0.5) * W / nx, (j + 0.5) * H / ny];
        // d: 0 on the side the squares leave first, 1 on the other.
        const d = across ? (sign > 0 ? 1 - x / W : x / W) : (sign > 0 ? 1 - y / H : y / H);
        tiles.push({ x, y, i, j, leave: 0.02 + 0.24 * d + 0.12 * (random() - 0.5), land: 0.98 - 0.36 * d + 0.08 * (random() - 0.5),
          orbit: 3 + 4 * random(), spin: (random() - 0.5) * 12, drift: 0.8 + 0.4 * random() });
      }
    }
    // Along the swirl s after leaving (or before landing): off toward the side, away, about the slide's middle line.
    const flight = (t, s, out) => {
      const [hx, hy] = [W / nx / 2, H / ny / 2];
      const reach = s ** 1.5 * t.drift * W / 1280, [far, side] = [9000 * reach, 4200 * reach];
      const turn = t.orbit * s, [c, sn] = [Math.cos(turn), Math.sin(turn)], spin = t.spin * s;
      const [sc, ss] = [Math.cos(spin), Math.sin(spin)];
      const corners = [[-hx, -hy], [hx, -hy], [hx, hy], [-hx, hy]].map(([dx, dy]) => {
        const [rx, ry] = [dx * sc - dy * ss, dx * ss + dy * sc];
        let [x, y, z] = [t.x + rx, t.y + ry, 0];
        if (across) { const dy2 = y - H / 2; [y, z] = [H / 2 + dy2 * c, dy2 * sn]; x -= sign * side; } else { const dx2 = x - W / 2; [x, z] = [W / 2 + dx2 * c, dx2 * sn]; y -= sign * side; }
        return [x, y, z - far];
      });
      const [u0, v0, u1, v1] = [t.i / nx, t.j / ny, (t.i + 1) / nx, (t.j + 1) / ny];
      out.pos.push(...corners[0], ...corners[1], ...corners[2], ...corners[0], ...corners[2], ...corners[3]);
      out.uv.push(u0, v0, u1, v0, u1, v1, u0, v0, u1, v1, u0, v1);
      const shade = 0.75 + 0.25 * Math.abs(c);
      out.shade.push(shade, shade, shade, shade, shade, shade);
    };
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const old = { tex: oldImage, pos: [], uv: [], shade: [] }, neu = { tex: newImage, pos: [], uv: [], shade: [] };
      for (const t of tiles) {
        if (p < t.leave + 0.6) flight(t, Math.max(0, p - t.leave), old);
        if (p > t.land - 0.6) flight(t, Math.max(0, t.land - p), neu);
      }
      scene.draw([old, neu], 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  // Shred: the old slide's pieces as a group shrink (scale, about a point x, y of a 1280 × 720 slide) and fly off far
  // to the top left; the new slide's come from there, large, and settle (measured with PowerPoint's video).
  const shredOld = [[0, 1, 457, 567], [0.1, 0.99, 457, 567], [0.2, 0.94, 457, 567], [0.3, 0.91, 457, 567], [0.4, 0.88, 457, 567],
    [0.5, 0.82, 457, 567], [0.53, 0.82, 457, 567], [0.56, 0.7, 430, 450], [0.59, 0.28, 380, 130], [0.62, 0.02, 350, 60]];
  const shredNew = [[0.59, 0.02, 380, 120], [0.63, 0.4, 430, 230], [0.66, 0.95, 457, 567], [0.69, 0.85, 457, 567], [0.72, 0.86, 457, 567],
    [0.76, 0.9, 457, 567], [0.79, 0.95, 457, 567], [0.82, 0.97, 457, 567], [0.86, 0.98, 457, 567], [0.95, 1, 457, 567]];

  /**
   * Shred (p14:shred; from PowerPoint's video): the old slide cut in strips of random widths (pattern rectangle: in
   * small pieces) that part in depth and height as the slide sinks back, then flies off into the distance; the new
   * slide's pieces come out of it, close up and apart, and settle into place.
   */
  function shredTransition(spec, a, b, reset) {
    const random = seeded(3910), pieces = [];
    if (spec.pattern === 'rectangle') {
      const [nx, ny] = [256, 60];
      for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) pieces.push({ x0: i / nx, x1: (i + 1) / nx, y0: j / ny, y1: (j + 1) / ny });
    } else {
      for (let x = 0; x < 1;) {
        const w = (30 + 60 * random()) / 1280;
        pieces.push({ x0: x, x1: Math.min(1, x + w), y0: 0, y1: 1 });
        x += w;
      }
    }
    for (const piece of pieces) Object.assign(piece, { depth: random(), rise: random() - 0.5, depth2: random() - 0.5, rise2: random() - 0.5 });
    const draw = (image, frames, p, spread, second, out) => {
      if (p < frames[0][0] || p > frames[frames.length - 1][0] + 0.001 && frames === shredOld) return;
      const [scale, cx, cy] = keyframes(frames, p).map((v, i) => (i === 1 ? v * W / 1280 : i === 2 ? v * H / 720 : v));
      for (const piece of pieces) {
        const k = scale * (second ? 1 + 0.4 * piece.depth2 * spread : 1 / (1 + 0.35 * piece.depth * spread));
        const dy = (second ? piece.rise2 : piece.rise) * 60 * H / 720 * spread;
        // leaning a little as the group tilts back
        const corner = (u, v) => [cx + (u * W - cx) * k + (0.5 - v) * 40 * W / 1280 * spread * scale, cy + (v * H - cy) * k + dy * scale, 0];
        out.pos.push(...corner(piece.x0, piece.y0), ...corner(piece.x1, piece.y0), ...corner(piece.x1, piece.y1),
          ...corner(piece.x0, piece.y0), ...corner(piece.x1, piece.y1), ...corner(piece.x0, piece.y1));
        out.uv.push(piece.x0, piece.y0, piece.x1, piece.y0, piece.x1, piece.y1, piece.x0, piece.y0, piece.x1, piece.y1, piece.x0, piece.y1);
        for (let n = 0; n < 6; n++) out.shade.push(1);
      }
    };
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const old = { tex: oldImage, pos: [], uv: [], shade: [] }, neu = { tex: newImage, pos: [], uv: [], shade: [] };
      if (p < 0.62) draw(oldImage, shredOld, p, smooth(p / 0.5), false, old);
      if (p > 0.59) draw(newImage, shredNew, p, 1 - smooth((p - 0.62) / 0.33), true, neu);
      scene.draw([{ ...old, flat: true }, { ...neu, flat: true }], 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  // Honeycomb: how far the old slide comes toward the viewer as it breaks up (measured with PowerPoint's video).
  const honeycombZoom = measured([[0, 1], [0.07, 1.02], [0.13, 1.1], [0.2, 1.35], [0.27, 1.9], [0.33, 2.6], [0.38, 3.8], [0.4, 5]]);
  const honeycombReturn = measured([[0.42, 0.85], [0.53, 0.9], [0.76, 1.08], [0.89, 1.02], [0.95, 1]]);

  /**
   * Honeycomb (p14:honeycomb; from PowerPoint's video): the old slide parts into hexagons with dark seams and comes
   * at the viewer while hexagons drop out, until it has passed; then the new slide's hexagons pop up from the middle
   * outward with dark seams that close at the end.
   */
  function honeycombTransition(spec, a, b, reset) {
    const random = seeded(3898), w = 110 * W / 1280, h = w * Math.sqrt(3) / 2;
    const cells = [];
    for (let i = -1; i * 0.75 * w < W + w; i++) {
      for (let j = -1; j * h < H + h; j++) {
        const [x, y] = [i * 0.75 * w, j * h + (i & 1 ? h / 2 : 0)];
        const d = Math.hypot((x - W / 2) / (W / 2), (y - H / 2) / (H / 2)) / Math.SQRT2;
        cells.push({ x, y, vanish: 0.2 + 0.7 * random(), appear: 0.42 + 0.42 * (0.55 * Math.min(1, d) + 0.45 * random()) });
      }
    }
    const shape = [[w / 2, 0], [w / 4, h / 2], [-w / 4, h / 2], [-w / 2, 0], [-w / 4, -h / 2], [w / 4, -h / 2]];
    const hexagon = (out, cell, size, zoom, shade) => {
      const place = ([dx, dy]) => [W / 2 + (cell.x + dx * size - W / 2) * zoom, H / 2 + (cell.y + dy * size - H / 2) * zoom, 0];
      for (let k = 0; k < 6; k++) {
        const [p0, p1] = [shape[k], shape[(k + 1) % 6]];
        out.pos.push(...place([0, 0]), ...place(p0), ...place(p1));
        out.uv.push(cell.x / W, cell.y / H, (cell.x + p0[0]) / W, (cell.y + p0[1]) / H, (cell.x + p1[0]) / W, (cell.y + p1[1]) / H);
        out.shade.push(shade, shade, shade);
      }
    };
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const old = { tex: oldImage, pos: [], uv: [], shade: [] }, neu = { tex: newImage, pos: [], uv: [], shade: [] };
      if (p < 0.4) {
        const zoom = honeycombZoom(p), seam = 1 - 0.12 * smooth(p / 0.15);
        for (const cell of cells) if (p < cell.vanish) hexagon(old, cell, seam, zoom, 1 - 0.5 * smooth((p - 0.2) / 0.2));
      }
      const seam = 1 - 0.12 * (1 - smooth((p - 0.86) / 0.07));
      for (const cell of cells) {
        if (p < cell.appear) continue;
        const t = (p - cell.appear) / 0.04;
        hexagon(neu, cell, seam * Math.min(1, 0.7 + 0.3 * t), honeycombReturn(p), 0.5 + 0.5 * smooth(t));
      }
      scene.draw([{ ...old, flat: true }, { ...neu, flat: true }], 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /**
   * The zoom transition of a zoom object (catalog: interactivity.zoom): the view closes in on the zoom's frame (rect,
   * slide pixels) on its slide until the slide it leads to, growing out of the frame, fills the screen; out: the
   * other way round, back into the frame. The zoom grows evenly in scale.
   */
  function zoomObjectTransition(spec, a, b, reset) {
    const [rx, ry, rw, rh] = spec.rect;
    const k = W / Math.max(1, rw);
    // The point the zoom leaves in place: the one the slide and its frame share.
    const [fx, fy] = k > 1.0001 ? [rx / (1 - rw / W), ry / (1 - rh / H)] : [0, 0];
    const [host, target] = spec.out ? [b, a] : [a, b];
    return {
      frame(p) {
        const e = sineInOut(spec.out ? 1 - p : p), z = Math.pow(k, e);
        host.style.transformOrigin = target.style.transformOrigin = '0 0';
        host.style.transform = `translate(${fx * (1 - z)}px,${fy * (1 - z)}px) scale(${z})`;
        target.style.transform = `translate(${fx + (rx - fx) * z}px,${fy + (ry - fy) * z}px) scale(${rw * z / W},${rh * z / H})`;
        target.style.zIndex = 2;
        // The frame shows the slide already: the slide itself takes over at once.
        target.style.opacity = String(Math.min(1, e * 8));
      },
      end: reset,
    };
  }

  /**
   * A slide's image as a sheet of nx × ny cells whose corners place(u, v) puts in the scene (u, v: 0 … 1 across and
   * down) as [x, y, z] or [x, y, z, shade]; the texture follows the sheet, or uvOf(u, v) gives its coordinates.
   */
  function sheetMesh(tex, nx, ny, place, uvOf = (u, v) => [u, v]) {
    const pts = [];
    for (let j = 0; j <= ny; j++) for (let i = 0; i <= nx; i++) pts.push(place(i / nx, j / ny));
    const pos = [], uv = [], shade = [];
    const put = (i, j) => {
      const q = pts[j * (nx + 1) + i];
      pos.push(q[0], q[1], q[2]);
      uv.push(...uvOf(i / nx, j / ny));
      shade.push(q.length > 3 ? q[3] : 1);
    };
    for (let j = 0; j < ny; j++) {
      for (let i = 0; i < nx; i++) {
        put(i, j); put(i + 1, j); put(i + 1, j + 1);
        put(i, j); put(i + 1, j + 1); put(i, j + 1);
      }
    }
    return { tex, pos, uv, shade };
  }

  /** A slide's image lying in place behind everything (not hiding what is drawn after it). */
  const backdrop = image => ({ tex: image, ...quad([0, 0, 0], [W, 0, 0], [W, H, 0], [0, H, 0]), flat: true });

  // The Office 2013 transitions (p15:prstTrans; measured with PowerPoint's videos, pixels of a 1280 × 720 slide).
  // Fall Over: the old slide's fall back about its bottom edge in degrees (in its middle).
  const fallOverAngle = measured([[0, 0], [0.134, 0.2], [0.2, 1.2], [0.266, 2.3], [0.331, 3.6], [0.397, 5.5], [0.462, 8.4], [0.528, 12.8],
    [0.593, 19.6], [0.659, 29.4], [0.725, 44.3], [0.79, 65.3], [0.823, 78.7], [0.856, 90], [0.9, 125], [1, 125]]);
  // Drape: the new slide's swing about its top edge (degrees, its lower edge toward the viewer), swaying back.
  const drapeAngle = measured([[0, 110], [0.3, 72], [0.331, 70.8], [0.364, 68.4], [0.397, 65.6], [0.43, 62.2], [0.462, 57.4], [0.495, 50.5],
    [0.528, 36], [0.561, 17], [0.593, -1.5], [0.626, -4], [0.659, -7], [0.692, -8.5], [0.725, -8.5], [0.757, -7], [0.79, -6], [0.823, -4],
    [0.856, -3], [0.889, -2], [0.921, -1.5], [0.954, -0.5], [1, 0]]);
  // Curtains: the inner edges of the left and the right curtain at half height and at the bottom.
  const curtainMiddle = [[0, 640, 640], [0.069, 630, 642], [0.134, 578, 678], [0.2, 494, 766], [0.266, 412, 871], [0.3, 382, 913],
    [0.397, 381, 966], [0.495, 381, 926], [0.561, 420, 866], [0.626, 448, 823], [0.692, 390, 867], [0.725, 382, 942], [0.757, 382, 1046],
    [0.79, 146, 1158], [0.823, 64, 1254], [0.856, -40, 1320]];
  const curtainBottom = [[0, 640, 640], [0.134, 626, 642], [0.2, 550, 693], [0.266, 420, 829], [0.3, 358, 917], [0.331, 304, 999],
    [0.364, 266, 1058], [0.397, 248, 1089], [0.43, 258, 1082], [0.462, 290, 1043], [0.495, 338, 987], [0.528, 392, 922], [0.561, 444, 852],
    [0.593, 480, 800], [0.659, 456, 900], [0.692, 354, 1034], [0.725, 308, 1154], [0.757, 252, 1208], [0.79, 200, 1240], [0.823, 100, 1270],
    [0.856, -40, 1320]];
  // Wind: the old slide blown off across (toward the right; invX: the left) and up.
  const windAcross = measured([[0, 0], [0.3, 0], [0.331, 12], [0.364, 42], [0.397, 86], [0.43, 144], [0.462, 214], [0.495, 296], [0.528, 390],
    [0.561, 492], [0.593, 600], [0.626, 716], [0.659, 838], [0.692, 954], [0.725, 1054], [0.757, 1144], [0.79, 1262], [0.83, 1420]]);
  const windUp = measured([[0, 0], [0.134, 4], [0.2, 10], [0.266, 22], [0.331, 28], [0.397, 44], [0.462, 76], [0.528, 144], [0.593, 230],
    [0.659, 272], [0.725, 344], [0.79, 432], [0.83, 520]]);
  // Prestige: the old slide pulled up off the new one: its lower edge, and how wide it stays.
  const prestigeBottom = measured([[0, 720], [0.5, 708], [0.528, 704], [0.561, 692], [0.593, 660], [0.626, 602], [0.659, 524], [0.692, 428],
    [0.725, 314], [0.757, 192], [0.79, 74], [0.82, -20]]);
  const prestigeWidth = measured([[0, 1], [0.626, 0.96], [0.659, 0.95], [0.692, 0.89], [0.725, 0.72], [0.757, 0.17], [0.79, 0.09], [0.82, 0.05]]);
  // Crush: the box of the crumpled old slide (left, right, top, height), which then falls out at the bottom.
  const crushBox = [[0, 0, 1280, 0, 720], [0.069, 36, 1252, 14, 690], [0.102, 58, 1248, 18, 676], [0.134, 74, 1234, 22, 660],
    [0.167, 92, 1202, 28, 644], [0.2, 104, 1158, 46, 618], [0.233, 116, 1116, 70, 586], [0.266, 134, 1080, 90, 558], [0.298, 158, 1050, 108, 534],
    [0.331, 188, 1018, 128, 506], [0.364, 228, 984, 144, 480], [0.397, 270, 958, 156, 454], [0.43, 312, 934, 170, 426], [0.462, 348, 906, 176, 404],
    [0.495, 380, 882, 190, 374], [0.528, 408, 862, 198, 352], [0.561, 422, 852, 210, 336], [0.593, 426, 850, 214, 332], [0.626, 420, 856, 222, 334],
    [0.659, 418, 860, 238, 336], [0.692, 416, 864, 262, 336], [0.725, 412, 866, 292, 336], [0.757, 410, 866, 330, 336], [0.79, 408, 868, 376, 336],
    [0.823, 406, 870, 424, 336], [0.856, 400, 874, 478, 336], [0.889, 394, 852, 536, 336], [0.921, 400, 810, 592, 336], [0.954, 420, 800, 650, 336],
    [1, 440, 790, 740, 336]];
  // Page Curl (single): the old slide pulled off toward the left (invX: the right).
  const curlAcross = measured([[0, 0], [0.233, 0], [0.266, 3], [0.298, 8], [0.331, 17], [0.364, 32], [0.397, 57], [0.43, 95], [0.462, 155],
    [0.495, 237], [0.528, 327], [0.561, 409], [0.593, 487], [0.626, 577], [0.659, 685], [0.692, 825], [0.725, 995], [0.757, 1177], [0.79, 1300]]);
  // Page Curl (double): the turn of the right page (invX: the left one) over the middle, degrees.
  const pageTurn = measured([[0, 0], [0.1, 0], [0.134, 9], [0.2, 22], [0.266, 33], [0.331, 41], [0.397, 51], [0.462, 66], [0.495, 75.5],
    [0.528, 86], [0.561, 98.6], [0.593, 113], [0.626, 128], [0.659, 142], [0.692, 156], [0.725, 180]]);
  // Peel Off: the edge of the uncovered new slide, x + 0.83 y = c, moving from the bottom right corner (invX: left).
  const peelEdge = measured([[0, 1900], [0.15, 1878], [0.167, 1853], [0.2, 1797], [0.233, 1749], [0.266, 1693], [0.298, 1637], [0.331, 1581],
    [0.364, 1525], [0.397, 1469], [0.43, 1409], [0.462, 1341], [0.495, 1269], [0.528, 1189], [0.561, 1077], [0.593, 933], [0.626, 789],
    [0.659, 685], [0.692, 597], [0.725, 497], [0.757, 377], [0.79, 257], [0.823, 153], [0.856, 41], [0.889, -80]]);
  // Airplane and Origami: the middle and the size of the folded old slide as it flies off.
  // Airplane: the folded dart's nose, left wing tip, keel and right wing tip (x, y each).
  const airplaneDart = [[0.3, 778, 208, 118, 450, 300, 820, 650, 860], [0.62, 650, 330, 0, 470, 130, 760, 450, 800],
    [0.79, 562, 386, -80, 500, 60, 800, 380, 830], [0.823, 616, 336, 30, 470, 180, 760, 500, 790], [0.856, 844, 156, 200, 270, 420, 550, 690, 560],
    [0.889, 1076, 2, 722, 65, 843, 219, 991, 224], [0.93, 1400, -200, 1100, -150, 1180, -40, 1300, -40]];
  const origamiPath = [[0, 640, 360, 1280, 720], [0.069, 622, 325, 1176, 688], [0.102, 640, 358, 654, 630], [0.134, 649, 375, 664, 498],
    [0.2, 635, 365, 700, 418], [0.3, 633, 392, 698, 432], [0.4, 618, 427, 748, 348], [0.5, 702, 435, 674, 352], [0.6, 909, 383, 526, 384],
    [0.7, 1088, 277, 360, 242], [0.79, 1199, 163, 178, 182], [0.856, 1238, 94, 96, 120], [0.9, 1320, 30, 50, 60]];

  /**
   * The Office 2013 transitions (p15:prstTrans; from PowerPoint's videos): over the new slide the old one falls
   * over, opens as curtains, is blown away, pulled off, crumpled, shattered, peeled, turned like a page or folded and
   * flown off; Drape lets the new slide swing down over the old one. invX mirrors them.
   */
  function presetTransition(spec, a, b, reset) {
    const preset = spec.prst || 'fade', flip = spec.invX ? -1 : 1;
    const [kx, ky] = [W / 1280, H / 720];
    const mirrorX = x => (flip < 0 ? W - x : x);
    const random = seeded(2013);
    // Fracture: long splinters between cracks from the point of impact to the slide's edges, each broken once along
    // its length; they part a little, turn about their length and fall.
    const shards = [];
    if (preset === 'fracture') {
      const [ix, iy] = [0.5 * W, 0.55 * H];
      const reach = angle => {
        const [c, sn] = [Math.cos(angle), Math.sin(angle)];
        return Math.min(c > 1e-9 ? (W - ix) / c : c < -1e-9 ? -ix / c : Infinity, sn > 1e-9 ? (H - iy) / sn : sn < -1e-9 ? -iy / sn : Infinity);
      };
      const corners = [[W, H], [0, H], [0, 0], [W, 0]].map(([x, y]) => ({ x, y, angle: (Math.atan2(y - iy, x - ix) + 2 * Math.PI) % (2 * Math.PI) }));
      const rays = [];
      for (let k = 0; k < 28; k++) rays.push((k + 0.2 + 0.6 * random()) / 28 * 2 * Math.PI);
      const at = (angle, radius) => [ix + radius * Math.cos(angle), iy + radius * Math.sin(angle)];
      for (let k = 0; k < rays.length; k++) {
        const [a0, a1] = [rays[k], k + 1 < rays.length ? rays[k + 1] : rays[0] + 2 * Math.PI];
        const [b0, b1] = [Math.min(reach(a0), reach(a1)) * (0.3 + 0.4 * random()), Math.min(reach(a0), reach(a1)) * (0.3 + 0.4 * random())];
        const between = corners.filter(c => { const angle = c.angle < a0 ? c.angle + 2 * Math.PI : c.angle; return angle > a0 && angle < a1; })
          .sort((c, d) => ((c.angle - a0 + 4 * Math.PI) % (2 * Math.PI)) - ((d.angle - a0 + 4 * Math.PI) % (2 * Math.PI))).map(c => [c.x, c.y]);
        for (const pts of [[[ix, iy], at(a0, b0), at(a1, b1)], [at(a0, b0), at(a0, reach(a0)), ...between, at(a1, reach(a1)), at(a1, b1)]]) {
          const cx = pts.reduce((sum, q) => sum + q[0], 0) / pts.length, cy = pts.reduce((sum, q) => sum + q[1], 0) / pts.length;
          const d = Math.max(1, Math.hypot(cx - ix, cy - iy)), mid = (a0 + a1) / 2;
          shards.push({ pts, cx, cy, vx: (cx - ix) / d * (30 + 90 * random()) * kx, vy: (cy - iy) / d * (20 + 60 * random()) * ky,
            vz: (random() - 0.4) * 300 * kx, spin: (random() < 0.5 ? -1 : 1) * (0.5 + 1.5 * random()), ax: Math.cos(mid), ay: Math.sin(mid) });
        }
      }
    }
    return glTransition(a, b, (scene, p, oldImage, newImage) => {
      const meshes = [];
      switch (preset) {
        case 'fallOver': {
          meshes.push(backdrop(newImage));
          const middle = fallOverAngle(p);
          meshes.push(sheetMesh(oldImage, 24, 16, (u, v) => {
            const theta = Math.min(150, middle * (1 + 0.33 * flip * (1 - 2 * u))) * Math.PI / 180, r = (1 - v) * H;
            return [u * W, H - r * Math.cos(theta), -r * Math.sin(theta), 0.4 + 0.6 * Math.max(0, Math.cos(theta))];
          }));
          break;
        }
        case 'drape': {
          meshes.push(backdrop(oldImage));
          const angle = drapeAngle(p);
          meshes.push(sheetMesh(newImage, 24, 16, (u, v) => {
            const theta = (angle < 0 ? angle * (1 + 0.6 * flip * (1 - 2 * u)) : angle) * Math.PI / 180, r = v * H;
            return [u * W, r * Math.cos(theta), r * Math.sin(theta), 0.55 + 0.45 * Math.cos(theta)];
          }));
          break;
        }
        case 'curtains': {
          meshes.push(backdrop(newImage));
          const [ml, mr] = keyframes(curtainMiddle, p).map(v => v * kx);
          const [bl, br] = keyframes(curtainBottom, p).map(v => v * kx);
          const edge = (middle, bottom, v) => middle + (bottom - middle) * (v - 0.5) / (v > 0.5 ? 0.46 : 0.92);
          for (const side of [0, 1]) {
            // u across the curtain from its outer edge (left) or its inner edge (right); gathered in folds.
            meshes.push(sheetMesh(oldImage, 24, 12, (u, v) => {
              const inner = side ? edge(mr, br, v) : edge(ml, bl, v);
              const squeeze = Math.min(1, Math.max(0, side ? (inner - W / 2) / (W / 2) : (W / 2 - inner) / (W / 2)));
              const x = side ? inner + u * (W - inner) : u * inner;
              return [x, v * H, 40 * kx * squeeze * Math.sin(u * Math.PI * 12), 1 - 0.25 * squeeze * (0.5 - 0.5 * Math.cos(u * Math.PI * 24))];
            }, (u, v) => [side ? 0.5 + u / 2 : u / 2, v]));
          }
          break;
        }
        case 'wind': {
          meshes.push(backdrop(newImage));
          const [dx, dy] = [windAcross(p) * kx, windUp(p) * ky];
          const amplitude = 30 * kx * Math.min(1, p / 0.4);
          meshes.push(sheetMesh(oldImage, 24, 14, (u, v) => {
            const phase = 2 * Math.PI * (1.2 * (flip > 0 ? u : 1 - u) + 0.6 * v - 2.5 * p);
            return [u * W + flip * dx, v * H - dy * (0.6 + 0.4 * v), amplitude * Math.sin(phase), 0.94 + 0.06 * Math.cos(phase)];
          }));
          break;
        }
        case 'prestige': {
          meshes.push(backdrop(newImage));
          const bottom = prestigeBottom(p) * ky, width = prestigeWidth(p), center = W * (0.5 + 0.19 * (1 - width));
          const lifted = 1 - bottom / H;
          meshes.push(sheetMesh(oldImage, 24, 14, (u, v) => {
            const gather = 1 - (1 - width) * (0.3 + 0.7 * v);
            return [center + (u * W - W / 2) * gather, v * bottom, 30 * kx * lifted * Math.sin(u * Math.PI * 10),
              1 - 0.3 * lifted * (0.5 - 0.5 * Math.cos(u * Math.PI * 20))];
          }));
          break;
        }
        case 'crush': {
          meshes.push(backdrop(newImage));
          const [x0, x1, y0, height] = keyframes(crushBox, p);
          const [l, r, t, h] = [x0 * kx, x1 * kx, y0 * ky, height * ky];
          const crumple = Math.min(1, p / 0.5);
          const bump = (u, v, k) => Math.sin(u * 17.3 + k) * Math.cos(v * 13.1 - k) + 0.5 * Math.sin(u * 31.7 + v * 23.3 + 2 * k);
          meshes.push(sheetMesh(oldImage, 32, 18, (u, v) => {
            // Rounder as it crumples: the square's corners drawn in toward a disc.
            const [a1, b1] = [2 * u - 1, 2 * v - 1];
            const [ra, rb] = [a1 * Math.sqrt(1 - b1 * b1 / 2), b1 * Math.sqrt(1 - a1 * a1 / 2)];
            const [qa, qb] = [a1 + (ra - a1) * crumple, b1 + (rb - b1) * crumple];
            const [n1, n2] = [bump(u, v, 0), bump(v, u, 1.7)];
            return [(l + r) / 2 + qa * (r - l) / 2 + n1 * 18 * kx * crumple, t + h / 2 + qb * h / 2 + n2 * 18 * ky * crumple,
              60 * kx * crumple * n1, 1 - 0.35 * crumple * (0.5 + 0.5 * n2)];
          }));
          break;
        }
        case 'fracture': {
          meshes.push(backdrop(newImage));
          const t = Math.max(0, p - 0.13), fall = 1227 * ky * t * t;
          for (const shard of shards) {
            const turn = shard.spin * t, [c, sn] = [Math.cos(turn), Math.sin(turn)];
            // Turned about an axis in the slide's plane through the shard's middle, drifting and falling.
            const place = ([x, y]) => {
              const [dx, dy] = [x - shard.cx, y - shard.cy];
              const along = dx * shard.ax + dy * shard.ay, [px, py] = [dx - along * shard.ax, dy - along * shard.ay];
              return [shard.cx + along * shard.ax + px * c + shard.vx * t, shard.cy + along * shard.ay + py * c + shard.vy * t + fall,
                (shard.ax * py - shard.ay * px) * sn + shard.vz * t];
            };
            const mesh = { tex: oldImage, pos: [], uv: [], shade: [] };
            for (let k = 1; k + 1 < shard.pts.length; k++) {
              for (const q of [shard.pts[0], shard.pts[k], shard.pts[k + 1]]) {
                mesh.pos.push(...place(q));
                mesh.uv.push(q[0] / W, q[1] / H);
                mesh.shade.push(0.55 + 0.45 * Math.abs(c));
              }
            }
            meshes.push(mesh);
          }
          break;
        }
        case 'peelOff': {
          meshes.push(backdrop(newImage));
          // In the frame where the peel starts at the bottom right: the fold a curl's radius before the uncovered edge,
          // the sheet beyond it around the curl and back over the slide.
          const c = peelEdge(p), R = 50, len = Math.hypot(1, 0.83), [nx, ny] = [1 / len, 0.83 / len];
          meshes.push(sheetMesh(oldImage, 40, 24, (u, v) => {
            const [x, y] = [(flip > 0 ? u : 1 - u) * 1280, v * 720];
            const s = (x + 0.83 * y - c) / len + R;
            if (s <= 0) return [u * W, v * H, 0, 1];
            let along, z, shade;
            if (s <= Math.PI * R) {
              const phi = s / R;
              [along, z, shade] = [R * Math.sin(phi), R * (1 - Math.cos(phi)), phi < Math.PI / 2 ? 0.55 + 0.45 * Math.cos(phi) : 0.55 - 0.25 * Math.cos(phi)];
            } else {
              [along, z, shade] = [-(s - Math.PI * R), 2 * R, 0.8];
            }
            const [fx, fy] = [x - s * nx + along * nx, y - s * ny + along * ny];
            return [(flip > 0 ? fx : 1280 - fx) * kx, fy * ky, z * kx, shade];
          }));
          break;
        }
        case 'pageCurlSingle': {
          meshes.push(backdrop(newImage));
          const shift = curlAcross(p) * kx, curl = 150 * kx;
          meshes.push(sheetMesh(oldImage, 16, 1, (u, v) => {
            const x = u * W - flip * shift;
            return [x, v * H, 0, 0.75 + 0.25 * Math.min(1, Math.max(0, (flip > 0 ? x : W - x) / curl))];
          }));
          break;
        }
        case 'pageCurlDouble': {
          // Beneath: the old slide's half that stays and the new slide's other half; over them the turning page, the
          // old slide's other half in front and the new slide's first half on its back.
          const theta = pageTurn(p) * Math.PI / 180, cos = Math.cos(theta), front = cos >= 0;
          const half = (image, left) => ({ tex: image, ...quad([left ? 0 : W / 2, 0, 0], [left ? W / 2 : W, 0, 0], [left ? W / 2 : W, H, 0],
            [left ? 0 : W / 2, H, 0], [left ? 0 : 0.5, 0, left ? 0.5 : 1, 1]), flat: true });
          meshes.push(half(oldImage, flip > 0), half(newImage, flip < 0));
          meshes.push(sheetMesh(front ? oldImage : newImage, 12, 1, (u, v) => [W / 2 + flip * u * W / 2 * cos, v * H, 0, 0.55 + 0.45 * Math.abs(cos)],
            (u, v) => [front ? 0.5 + flip * u / 2 : 0.5 - flip * u / 2, v]));
          break;
        }
        case 'airplane': {
          meshes.push(backdrop(newImage));
          // From the flat slide to the dart: its top edge to the nose, its bottom edge along the wings and the keel.
          const [nx, ny, lx, ly, cx, cy, rx, ry] = keyframes(airplaneDart, p).map((v, i) => v * (i % 2 ? ky : kx));
          const folded = smooth(p / 0.3);
          meshes.push(sheetMesh(oldImage, 16, 8, (u, v) => {
            const [bx, by] = u < 0.5 ? [lx + (cx - lx) * u * 2, ly + (cy - ly) * u * 2] : [cx + (rx - cx) * (u - 0.5) * 2, cy + (ry - cy) * (u - 0.5) * 2];
            const [dx, dy] = [nx + (bx - nx) * v, ny + (by - ny) * v];
            return [mirrorX(u * W + (dx - u * W) * folded), v * H + (dy - v * H) * folded, 0, 1 - folded * (u < 0.5 ? 0.08 : 0.2)];
          }, (u, v) => [flip > 0 ? u : 1 - u, v]));
          break;
        }
        case 'origami': {
          meshes.push(backdrop(newImage));
          const [cx, cy, w, h] = keyframes(origamiPath, p).map((v, i) => v * (i % 2 ? ky : kx));
          const folded = Math.min(1, p / 0.134);
          // From the slide's rectangle to a diamond in its box: its corners to the middles of the box's sides.
          const rect = [[-0.5, -0.5], [0.5, -0.5], [0.5, 0.5], [-0.5, 0.5]], kite = [[-0.5, 0], [0, -0.5], [0.5, 0], [0, 0.5]];
          const at = k => [mirrorX(cx + (rect[k][0] + (kite[k][0] - rect[k][0]) * folded) * w), cy + (rect[k][1] + (kite[k][1] - rect[k][1]) * folded) * h, 0];
          meshes.push({ tex: oldImage, ...quad(at(0), at(1), at(2), at(3), flip > 0 ? [0, 0, 1, 1] : [1, 0, 0, 1]), shade: 1 - 0.15 * folded });
          break;
        }
        default: {
          meshes.push(backdrop(oldImage));
          meshes.push({ ...backdrop(newImage), alpha: p });
        }
      }
      scene.draw(meshes, 1.493, [0, 0, 0, 1]);
    }, reset);
  }

  /** A CSS filter blurring across only (an SVG filter in the document, made once), or none for nothing. */
  function acrossBlur(deviation) {
    if (deviation < 0.5) return '';
    let filter = document.getElementById('fs-across-blur');
    if (!filter) {
      const holder = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      holder.setAttribute('width', '0');
      holder.setAttribute('height', '0');
      holder.style.position = 'absolute';
      holder.innerHTML = '<filter id="fs-across-blur" x="-20%" y="0" width="140%" height="100%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="0 0" edgeMode="duplicate"/></filter>';
      document.body.appendChild(holder);
      filter = holder.firstChild;
    }
    filter.firstChild.setAttribute('stdDeviation', `${deviation.toFixed(2)} 0`);
    return 'url(#fs-across-blur)';
  }

  /** A CSS mask fading a slide's edges over a band (the slide's own pixels) from nothing at the edge. */
  function feather(band) {
    const layer = (side, size) => {
      const f = Math.min(50, band / size * 100);
      return f >= 50
        ? `linear-gradient(to ${side},transparent 0%,rgba(0,0,0,${+(50 / f).toFixed(4)}) 50%,transparent 100%)`
        : `linear-gradient(to ${side},transparent 0%,#000 ${+f.toFixed(3)}%,#000 ${+(100 - f).toFixed(3)}%,transparent 100%)`;
    };
    return `${layer('right', W)},${layer('bottom', H)}`;
  }

  function transitionEffect(type, spec, a, b, cover) {
    const reset = () => {
      for (const el of [a, b]) {
        if (el) {
          el.style.transform = el.style.opacity = el.style.clipPath = el.style.zIndex = el.style.transformOrigin = '';
          el.style.maskImage = el.style.maskSize = el.style.maskRepeat = el.style.maskComposite = el.style.filter = '';
        }
      }
      cover.style.opacity = '0';
    };
    const masked = { frame: p => {
      b.style.zIndex = 2;
      const svg = transitionMask(type, spec, p);
      Object.assign(b.style, { maskImage: `url("data:image/svg+xml,${encodeURIComponent(svg)}")`, maskSize: '100% 100%', maskRepeat: 'no-repeat' });
    }, end: reset };
    const clip = (fn) => ({ frame: p => { b.style.zIndex = 2; b.style.clipPath = fn(p); }, end: reset });
    const dir = spec.dir || 'l';
    const vector = offsets[dir] || offsets.l;
    const poly = pts => `polygon(${pts.map(([x, y]) => `${x * 100}% ${y * 100}%`).join(',')})`;
    const bandsClip = (count, horizontal, p, random) => {
      const pts = [];
      for (let k = 0; k < count; k++) {
        const order = random ? random[k] : 0;
        const a0 = k / count, local = random ? Math.min(1, Math.max(0, p * 2 - order)) : p;
        const b0 = a0 + local / count;
        if (horizontal) pts.push([0, a0], [1, a0], [1, b0], [0, b0], [0, a0]);
        else pts.push([a0, 0], [a0, 1], [b0, 1], [b0, 0], [a0, 0]);
      }
      return poly(pts);
    };
    const three = (fn) => ({ frame: p => fn(p), end: reset, three: true });
    switch (type) {
      case 'none': return null;
      case 'cut':
        // Through black: the old slide for the first half, black for the second (measured).
        return spec.thruBlk ? { frame: p => {
          cover.style.background = '#000';
          cover.style.opacity = p >= 0.5 && p < 1 ? '1' : '0';
          b.style.opacity = p < 0.5 ? '0' : '';
        }, end: reset } : null;
      case 'fade':
        if (spec.thruBlk) {
          // To black with a sine in the first half, a moment black, from black in the rest (measured).
          return { frame: p => {
            cover.style.background = '#000';
            cover.style.opacity = String(p < 0.5 ? sineInOut(p / 0.5) : 1 - sineInOut((p - 0.525) / 0.46));
            b.style.opacity = p < 0.5 ? '0' : '1';
            b.style.zIndex = 2;
          }, end: reset };
        }
        return { frame: p => { b.style.zIndex = 2; b.style.opacity = String(sineInOut(p)); }, end: reset };
      case 'glitter':
        return glitterTransition(spec, a, b, reset);
      case 'shred':
        return shredTransition(spec, a, b, reset);
      case 'honeycomb':
        return honeycombTransition(spec, a, b, reset);
      case 'wipe': case 'split': case 'strips': case 'circle': case 'diamond': case 'plus': case 'wedge': case 'wheel':
      case 'wheelReverse': case 'randomBar': case 'dissolve': case 'ripple':
        return masked;
      case 'pan': case 'conveyor': case 'flythrough':
        return contentTransition(type, spec, a, b, reset);
      case 'warp':
        return doorsTransition('warp', spec, a, b, reset);
      case 'push':
        return { frame: p => {
          const q = quadInOut(p);
          a.style.transform = `translate(${vector[0] * q * W}px,${vector[1] * q * H}px)`;
          b.style.transform = `translate(${-vector[0] * (1 - q) * W}px,${-vector[1] * (1 - q) * H}px)`;
        }, end: reset };
      case 'cover':
        return { frame: p => {
          const q = vector[0] && vector[1] ? coverAcross(p) : powInOut(3.5)(p);
          b.style.zIndex = 2;
          b.style.transform = `translate(${-vector[0] * (1 - q) * W}px,${-vector[1] * (1 - q) * H}px)`;
        }, end: reset };
      case 'pull':
        return { frame: p => {
          const q = vector[0] && vector[1] ? uncoverAcross(p) : powInOut(3)(p);
          a.style.zIndex = 2;
          a.style.transform = `translate(${vector[0] * q * W}px,${vector[1] * q * H}px)`;
        }, end: reset };
      case 'reveal':
        if (spec.thruBlk) {
          // The old slide darkens to black, the side it leaves toward first (dir l: the left, 0.11 ahead of the
          // right), and the new slide lightens from black the other way round, each within 0.33 (measured).
          const ahead = dir === 'r' ? x => 1 - x : x => x;
          const shade = (p, start, lead) => {
            const stops = [];
            for (let k = 0; k <= 16; k++) {
              const x = k / 16, t = (p - start - 0.11 * lead(x)) / 0.33;
              stops.push(`rgba(0,0,0,${+(start < 0.5 ? sineInOut(t) : 1 - sineInOut(t)).toFixed(4)}) ${x * 100}%`);
            }
            return `linear-gradient(to right,${stops.join(',')})`;
          };
          return { frame: p => {
            cover.style.opacity = '1';
            cover.style.background = p < 0.5 ? shade(p, 0.055, ahead) : shade(p, 0.515, x => 1 - ahead(x));
            b.style.opacity = p < 0.5 ? '0' : '1';
            b.style.zIndex = 2;
          }, end: () => { cover.style.background = '#000'; reset(); } };
        }
        // Smoothly: the backgrounds crossfade with a sine over the whole time, the old slide's content fades out
        // before the middle and the new one's in after it, each within 0.3 (measured: only the backgrounds' mixed
        // color at 0.5).
        return { frame: p => {
          b.style.zIndex = 2;
          b.style.opacity = String(sineInOut(p));
          const content = (slide, alpha) => slide.querySelectorAll(':scope > :not(.bg)').forEach(e => { e.style.opacity = alpha < 1 ? String(alpha) : ''; });
          content(a, 1 - sineInOut((p - 0.12) / 0.3));
          content(b, 1 - sineInOut((1 - p - 0.12) / 0.3));
        }, end: () => {
          for (const slide of [a, b]) slide.querySelectorAll(':scope > :not(.bg)').forEach(e => { e.style.opacity = ''; });
          reset();
        } };
      case 'blinds': case 'checker':
        return blindsTransition(type, spec, a, b, reset);
      case 'comb': {
        // Bands of the old slide leave by turns to either side, speeding up evenly (4.78 slide heights per p²): seven
        // rows across, the last one first, each 0.046 later from 0.05; columns down, all at 0.178 (measured with
        // PowerPoint's video; ten in a 16:9 slide). The new slide lies beneath.
        const vertical = spec.dir === 'vert', bands = vertical ? Math.max(2, Math.round(W / H * 5.625)) : 7, copies = [];
        return { frame: p => {
          if (!copies.length) {
            for (let k = 0; k < bands; k++) {
              const copy = a.cloneNode(true);
              copy.removeAttribute('id');
              const [from, to] = [k / bands * 100, (k + 1) / bands * 100];
              copy.style.clipPath = vertical ? `inset(0 ${100 - to}% 0 ${from}%)` : `inset(${from}% 0 ${100 - to}% 0)`;
              copy.style.zIndex = 3;
              a.parentNode.appendChild(copy);
              copies.push(copy);
            }
            a.style.opacity = '0';
          }
          copies.forEach((copy, k) => {
            const t = Math.max(0, p - (vertical ? 0.178 : 0.05 + 0.046 * (bands - 1 - k)));
            const shift = Math.min(vertical ? H : W, 4.78 * H * t * t) * (vertical ? (k % 2 ? 1 : -1) : (k % 2 ? -1 : 1));
            copy.style.transform = vertical ? `translateY(${shift}px)` : `translateX(${shift}px)`;
          });
        }, end: () => { copies.forEach(c => c.remove()); copies.length = 0; reset(); } };
      }
      case 'zoom': {
        // Out: the new slide grows from the middle in the second half; in: the old one shrinks into it in the first
        // (linear, measured). Its edge is soft, a band 3 % of the slide's height wide on the screen.
        const out = spec.dir !== 'in';
        return { frame: p => {
          const el = out ? b : a;
          const s = Math.min(1, Math.max(0, out ? (p - 0.493) / 0.482 : 1 - (p - 0.029) / 0.484));
          el.style.zIndex = 2;
          el.style.transformOrigin = '50% 50%';
          el.style.transform = `scale(${s})`;
          el.style.opacity = s > 0 ? '' : '0';
          el.style.maskImage = s > 0 && s < 1 ? feather(0.03 * H / Math.max(s, 1e-3)) : '';
          el.style.maskComposite = 'intersect';
        }, end: reset };
      }
      case 'newsflash':
        // The new slide grows from the middle (scale p) turning 400° anticlockwise into place (measured).
        return { frame: p => {
          b.style.zIndex = 2;
          b.style.transformOrigin = '50% 50%';
          b.style.opacity = p > 0 ? '' : '0';
          b.style.transform = `rotate(${40 - 400 * p}deg) scale(${Math.max(p, 1e-4)})`;
        }, end: reset };
      case 'vortex':
        return vortexTransition(spec, a, b, reset);
      case 'flash':
        // White at once; the new slide comes out of it with its colors scaled by a and raised by b, both falling
        // linearly to plain at 0.894 (measured: R 29 · 2.1 + 90 = 151 at 0.63).
        return { frame: p => {
          b.style.zIndex = 2;
          const left = Math.max(0, 0.894 - p), gain = 1 + 4.17 * left, lift = 341 * left / 255;
          const contrast = gain / (gain + 2 * lift), brightness = gain + 2 * lift;
          b.style.filter = left > 0 ? `contrast(${contrast.toFixed(4)}) brightness(${brightness.toFixed(4)})` : '';
        }, end: reset };
      case 'prism':
        return prismTransition(spec, a, b, reset);
      case 'flip':
        return flipTransition(spec, a, b, reset);
      case 'switch':
        return switchTransition(spec, a, b, reset);
      case 'gallery':
        return galleryTransition(spec, a, b, reset);
      case 'doors':
        return doorsTransition('doors', spec, a, b, reset);
      case 'window': case 'ferris':
        return contentTransition(type, spec, a, b, reset);
      case 'cube': case 'rotate': case 'box': case 'orbit':
        return three(p => {
          const sign = dir === 'r' || dir === 'd' ? -1 : 1;
          const vertical = dir === 'u' || dir === 'd';
          const axis = vertical ? 'X' : 'Y';
          a.style.transformOrigin = b.style.transformOrigin = '50% 50%';
          if (type === 'doors' || type === 'window') {
            b.style.transform = `scale(${0.7 + 0.3 * p})`;
            b.style.opacity = String(p);
            a.style.zIndex = 2;
            a.style.opacity = String(1 - p);
            a.style.transform = `perspective(${W * 2}px) rotate${spec.dir === 'horz' ? 'X' : 'Y'}(${90 * p}deg)`;
            return;
          }
          if (p < 0.5) {
            a.style.zIndex = 2;
            a.style.transform = `perspective(${W * 2}px) rotate${axis}(${sign * 180 * p}deg) scale(${1 - 0.2 * Math.sin(p * Math.PI)})`;
            b.style.opacity = '0';
          } else {
            b.style.zIndex = 2;
            b.style.opacity = '1';
            a.style.opacity = '0';
            b.style.transform = `perspective(${W * 2}px) rotate${axis}(${sign * -180 * (1 - p)}deg) scale(${1 - 0.2 * Math.sin(p * Math.PI)})`;
          }
        });
      case 'prstTrans':
        return presetTransition(spec, a, b, reset);
      case 'morph':
        return morphTransition(spec, a, b, reset);
      case 'zoomObject':
        return zoomObjectTransition(spec, a, b, reset);
      default:
        return { frame: p => { b.style.zIndex = 2; b.style.opacity = String(p); }, end: reset };
    }
  }

  /* ---------------------------------------------------------------- the show */

  let show = null;

  function order() {
    const params = new URLSearchParams(location.search);
    const named = params.get('show');
    const custom = (deck.customShows || []).find(c => c.name === named || (deck.show && deck.show.customShow === c.id && !named));
    if (custom) return custom.slides.map(n => n - 1).filter(i => i >= 0 && i < slides.length);
    const first = deck.show && deck.show.first ? deck.show.first - 1 : 0;
    const last = deck.show && deck.show.last ? deck.show.last - 1 : slides.length - 1;
    const list = [];
    for (let i = first; i <= last && i < slides.length; i++) if (!info(i).hidden) list.push(i);
    return list;
  }

  function start(fromIndex) {
    if (show) return;
    const overlay = document.createElement('div');
    overlay.className = 'fs-show';
    overlay.innerHTML = '<div class="fs-stage"></div><div class="fs-cover"></div><div class="fs-end">End of slide show, click to exit.</div>';
    document.body.appendChild(overlay);
    const sequence = order();
    show = {
      overlay, stage: overlay.firstChild, cover: overlay.children[1], end: overlay.children[2],
      sequence, position: Math.max(0, sequence.indexOf(fromIndex)), placeholders: new Map(), current: null,
      animation: null, timer: 0, busy: false, history: [], digits: '', kiosk: deck.show && deck.show.mode === 'kiosk',
    };
    if (!(deck.show && deck.show.mode === 'browse') && overlay.requestFullscreen) {
      overlay.requestFullscreen().then(() => { if (show && show.overlay === overlay) show.fullscreen = true; }).catch(() => {});
    }
    resize();
    goTo(show.sequence[show.position], { transition: false });
  }

  function stop() {
    if (!show) return;
    stopLaser();
    stopEvents();
    if (show.acrossSlides) { for (const el of show.acrossSlides.keys()) stopMedia(el); show.acrossSlides = null; }

    clearTimeout(show.timer);
    clearInterval(show.clock);
    if (show.presenter && !show.presenter.closed) show.presenter.close();
    if (show.animation) show.animation.reset();
    for (const [slide, placeholder] of show.placeholders) {
      slide.style.transform = slide.style.opacity = slide.style.clipPath = slide.style.zIndex = '';
      placeholder.replaceWith(slide);
    }
    // Media hidden while stopped show again outside the show.
    document.querySelectorAll('.fs-stopped').forEach(owner => owner.classList.remove('fs-stopped'));
    if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
    show.overlay.remove();
    show = null;
  }

  function resize() {
    if (!show) return;
    const s = Math.min(window.innerWidth / W, window.innerHeight / H);
    show.stage.style.width = W + 'px';
    show.stage.style.height = H + 'px';
    show.stage.style.transform = `translate(${(window.innerWidth - W * s) / 2}px,${(window.innerHeight - H * s) / 2}px) scale(${s})`;
  }

  function mount(slide) {
    if (!show.placeholders.has(slide)) {
      const placeholder = document.createComment('slide');
      slide.replaceWith(placeholder);
      show.placeholders.set(slide, placeholder);
    }
    show.stage.appendChild(slide);
  }

  function unmount(slide) {
    const placeholder = show.placeholders.get(slide);
    if (!placeholder) return;
    slide.style.transform = slide.style.opacity = slide.style.clipPath = slide.style.zIndex = '';
    slide.querySelectorAll('.fs-stopped').forEach(owner => owner.classList.remove('fs-stopped'));
    placeholder.replaceWith(slide);
    show.placeholders.delete(slide);
  }

  /** Shows the slide with the given index; atEnd shows it with all animations played (going back). */
  async function goTo(index, options = {}) {
    if (!show || show.busy) return;
    clearTimeout(show.timer);
    show.end.style.display = 'none';
    const slide = slides[index];
    const previous = show.current;
    if (previous === slide) return;
    show.busy = true;
    stopLaser();
    stopEvents();
    if (show.animation) show.animation.stop();
    // Media playing across slides (numSld) go on until their count runs out.
    if (show.acrossSlides) {
      for (const [el, left] of [...show.acrossSlides]) {
        if (left <= 1) { show.acrossSlides.delete(el); stopMedia(el); } else show.acrossSlides.set(el, left - 1);
      }
    }
    mount(slide);
    prepareMedia(slide, index);
    const animation = new SlideAnimation(slide, index);
    // "Show without animation" (showPr showAnimation="0") shows every slide in its final state.
    const animated = !deck.show || deck.show.animation !== false;
    if (!animated) options.atEnd = true;
    if (options.atEnd) animation.finish(); else animation.render(null, 0);
    const spec = options.zoom ? { type: 'zoomObject', ...options.zoom } : info(index).transition;
    if (previous && options.transition !== false && spec) {
      playSound(spec);
      await transition(spec, previous, slide, show.cover);
    }
    if (previous) { if (show.animation) show.animation.reset(); unmount(previous); }
    show.current = slide;
    show.index = index;
    show.animation = animation;
    show.busy = false;
    if (options.atEnd) idle(); else animation.start(idle);
    startLaser(index);
    if (!options.atEnd) startEvents(index);
    updatePresenter();
  }

  /* ---------------------------------------------------------------- recorded media and trigger events */

  /**
   * Replays what was done with the slide's media and triggers while the show was recorded (p14:showEvtLst,
   * catalog: animation.media) when the show uses its timings: play, pause, resume, stop and seek of a video or
   * sound and clicks on triggers, at their times from the start of the slide's timeline after the transition.
   */
  function startEvents(index) {
    const events = info(index).events;
    if (!events || (deck.show && deck.show.timings === false)) return;
    const slide = slides[index];
    show.eventTimers = events.map(([kind, time, id, extra]) => setTimeout(() => {
      if (!show || show.current !== slide) return;
      if (kind === 'trigger') { if (extra === 'onClick' && show.animation) show.animation.trigger(id); return; }
      const el = mediaOf(slide, id);
      if (!el) return;
      if (kind === 'play') { if (el.paused && !el.classList.contains('playing')) playMedia(el, 0); else el.play().catch(() => {}); }
      else if (kind === 'pause') el.pause();
      else if (kind === 'resume') el.play().catch(() => {});
      else if (kind === 'stop') stopMedia(el);
      else if (kind === 'seek') el.currentTime = trimStart(el) + (extra || 0) / 1000;
    }, time));
  }

  function stopEvents() {
    if (!show || !show.eventTimers) return;
    show.eventTimers.forEach(clearTimeout);
    show.eventTimers = null;
  }

  /* ---------------------------------------------------------------- recorded laser pointer */

  /**
   * Replays the laser pointer recorded with the slide (catalog: animation.laser) when narrations play
   * (showPr showNarration): the pointer follows every trace from its first to its last point, timed from the start
   * of the slide's timeline after the transition, moving in straight lines between the recorded points.
   */
  function startLaser(index) {
    stopLaser();
    const traces = info(index).laser;
    if (!traces || !deck.show || !deck.show.narration) return;
    const color = deck.show.laser || '#ff0000';
    const dot = document.createElement('div');
    dot.className = 'fs-laser';
    dot.style.cssText = `position:absolute;left:-14px;top:-14px;width:28px;height:28px;border-radius:50%;pointer-events:none;z-index:2147483646;display:none;` +
      `background:radial-gradient(closest-side,#fff 0 16%,${color} 30% 55%,color-mix(in srgb,${color} 35%,transparent) 75%,transparent)`;
    show.stage.appendChild(dot);
    const end = Math.max(...traces.map(trace => trace[trace.length - 1][0]));
    const start = performance.now();
    const laser = { dot, frame: 0 };
    const frame = now => {
      if (!show || show.laser !== laser) return;
      const t = now - start;
      let position = null;
      for (const trace of traces) {
        if (t < trace[0][0] || t > trace[trace.length - 1][0]) continue;
        let k = 1;
        while (k < trace.length - 1 && trace[k][0] < t) k++;
        const a = trace[k - 1] || trace[0], b = trace[k] || a;
        const f = b[0] > a[0] ? Math.min(1, Math.max(0, (t - a[0]) / (b[0] - a[0]))) : 1;
        position = [a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f];
        break;
      }
      dot.style.display = position ? 'block' : 'none';
      if (position) dot.style.transform = `translate(${position[0]}px,${position[1]}px)`;
      if (t <= end) laser.frame = requestAnimationFrame(frame);
      else dot.remove();
    };
    show.laser = laser;
    laser.frame = requestAnimationFrame(frame);
  }

  function stopLaser() {
    if (!show || !show.laser) return;
    cancelAnimationFrame(show.laser.frame);
    show.laser.dot.remove();
    show.laser = null;
  }

  function playSound(spec) {
    if (show.sound && spec.stopSound) { show.sound.pause(); show.sound = null; }
    if (!spec.sound) return;
    if (show.sound) show.sound.pause();
    show.sound = new Audio(spec.sound);
    show.sound.loop = !!spec.loopSound;
    show.sound.play().catch(() => {});
  }

  /** Called when the slide waits for input: starts the automatic advance timer. */
  function idle() {
    if (!show) return;
    clearTimeout(show.timer);
    const spec = info(show.index).transition;
    const timings = !deck.show || deck.show.timings !== false;
    if (spec && typeof spec.after === 'number' && timings) show.timer = setTimeout(next, spec.after);
  }

  function next() {
    if (!show || show.busy) return;
    if (show.end.style.display === 'block') { stop(); return; }
    if (show.animation && show.animation.next()) { setTimeout(updatePresenter, 0); return; }
    const zoom = show.zooms && show.zooms[show.zooms.length - 1];
    if (zoom && zoom.last === show.index) {
      show.zooms.pop();
      show.history.push(show.position);
      const at = show.sequence.indexOf(zoom.index);
      if (at >= 0) show.position = at;
      goTo(zoom.index, { atEnd: true, zoom: zoom.zoom && { ...zoom.zoom, out: true } });
      return;
    }
    if (show.position < show.sequence.length - 1) {
      show.history.push(show.position);
      goTo(show.sequence[++show.position]);
    } else if (deck.show && (deck.show.loop || show.kiosk)) {
      show.history.push(show.position);
      show.position = 0;
      goTo(show.sequence[0]);
    } else {
      clearTimeout(show.timer);
      show.end.style.display = 'block';
    }
  }

  function previous() {
    if (!show || show.busy) return;
    if (show.end.style.display === 'block') { show.end.style.display = 'none'; return; }
    if (show.animation && show.animation.previous()) return;
    if (show.position > 0) goTo(show.sequence[--show.position], { transition: false, atEnd: true });
  }

  function jump(index, transitionToo = true, zoom = null) {
    if (!show) return;
    const at = show.sequence.indexOf(index);
    if (at >= 0) show.position = at;
    else { show.sequence.splice(show.position + 1, 0, index); show.position++; }   // hidden slides by link
    goTo(index, { transition: transitionToo, zoom });
  }

  /**
   * A click on a zoom object (catalog: interactivity.zoom) goes to its slide, or the first of its section, with the
   * zoom transition from the zoom's frame (transitionDur long; without one the slide's own transition plays) and,
   * returning to the zoom, comes back to the zoom's slide after the last of them, zooming out into the frame.
   */
  function zoomTo(link) {
    const [first, last, back, duration] = (link.getAttribute('data-zoom') || '').split(',');
    const index = parseInt(first, 10) - 1;
    if (!show || !(index >= 0 && index < slides.length)) return false;
    const box = link.closest('.sp'), slide = show.current;
    let zoom = null;
    if (box && slide && duration) {
      const [r, b] = [slide.getBoundingClientRect(), box.getBoundingClientRect()], k = (r.width / W) || 1;
      zoom = { rect: [(b.left - r.left) / k, (b.top - r.top) / k, b.width / k, b.height / k], dur: parseFloat(duration) };
    }
    if (back === '1') (show.zooms = show.zooms || []).push({ index: show.index, last: parseInt(last, 10) - 1, zoom });
    jump(index, true, zoom);
    return true;
  }

  /** Actions of hyperlinks and action settings (ppaction://hlinkshowjump?jump=…, slide links, custom shows). */
  function action(link) {
    const value = link.getAttribute('data-action') || '';
    if (value === 'next') next();
    else if (value === 'previous') previous();
    else if (value === 'first') jump(show.sequence[0]);
    else if (value === 'last') jump(show.sequence[show.sequence.length - 1]);
    else if (value === 'last-viewed') { if (show.history.length) { show.position = show.history.pop(); goTo(show.sequence[show.position]); } }
    else if (value === 'end') stop();
    else if (value.startsWith('slide:')) jump(parseInt(value.slice(6), 10) - 1);
    else if (value.startsWith('show:')) {
      const custom = (deck.customShows || []).find(c => String(c.id) === value.slice(5));
      if (custom) { show.sequence = custom.slides.map(n => n - 1); show.position = 0; goTo(show.sequence[0]); }
    } else return false;
    return true;
  }

  /* ---------------------------------------------------------------- presenter view */

  /**
   * A second window with the current and the next slide, the speaker notes, a timer and navigation (Alt+F5 or
   * V during the show). It is filled from this document: styles and glyph outlines are copied once, the slides
   * are cloned whenever the show moves on.
   */
  function openPresenter() {
    if (!show) return;
    if (show.presenter && !show.presenter.closed) { show.presenter.focus(); return; }
    const w = window.open('', 'freeslate-presenter', 'width=1100,height=720');
    if (!w) return;
    show.presenter = w;
    show.started = show.started || Date.now();
    const styles = [...document.querySelectorAll('style')].map(s => s.textContent).join('\n');
    const glyphs = document.querySelector('svg.glyphs');
    w.document.open();
    w.document.write(`<!DOCTYPE html><html><head><meta charset="utf-8"><title>Presenter view</title><style>${styles}
      body{margin:0;background:#1f1f1f;color:#eee;font:15px system-ui,sans-serif;display:grid;grid-template-columns:62% 38%;grid-template-rows:1fr auto;height:100vh;overflow:hidden}
      .pv-current,.pv-next{position:relative;overflow:hidden;margin:16px;background:#000}
      .pv-side{display:grid;grid-template-rows:auto 1fr;min-height:0}
      .pv-next{height:38vh;margin-bottom:8px}
      .pv-notes{margin:8px 16px 16px;overflow:auto;font-size:20px;line-height:1.4;background:#2b2b2b;padding:12px;border-radius:6px}
      .pv-bar{grid-column:1/3;display:flex;gap:16px;align-items:center;padding:8px 16px;background:#111}
      .pv-bar button{background:#444;color:#fff;border:0;border-radius:4px;padding:6px 14px;font-size:15px;cursor:pointer}
      .pv-label{position:absolute;left:8px;top:6px;z-index:5;font-size:12px;color:#aaa}
      .pv-frame{position:absolute;left:0;top:0;transform-origin:0 0}
      .pv-frame>.slide{margin:0!important;display:block!important;opacity:1!important;outline:none!important}
      .pv-frame .fs-media{display:none}
      .pv-timer{font-size:22px;font-variant-numeric:tabular-nums}
    </style></head><body>
      <div class="pv-current"><div class="pv-frame"></div></div>
      <div class="pv-side"><div class="pv-next"><span class="pv-label">Next</span><div class="pv-frame"></div></div><div class="pv-notes"></div></div>
      <div class="pv-bar"><button data-do="previous">\u25C0</button><button data-do="next">\u25B6</button>
        <span class="pv-count"></span><span class="pv-timer">0:00:00</span><button data-do="stop">End show</button></div>
      ${glyphs ? glyphs.outerHTML : ''}</body></html>`);
    w.document.close();
    w.document.addEventListener('click', e => {
      const action = e.target.closest('[data-do]');
      if (action) ({ next, previous, stop })[action.getAttribute('data-do')]();
    });
    w.document.addEventListener('keydown', e => onKey(e));
    w.addEventListener('resize', updatePresenter);
    clearInterval(show.clock);
    show.clock = setInterval(() => {
      if (!show || !show.presenter || show.presenter.closed) return;
      const s = Math.floor((Date.now() - show.started) / 1000);
      const timer = show.presenter.document.querySelector('.pv-timer');
      if (timer) timer.textContent = `${Math.floor(s / 3600)}:${String(Math.floor(s / 60) % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
    }, 1000);
    updatePresenter();
  }

  /** Shows the current state in the presenter window. */
  function updatePresenter() {
    const w = show && show.presenter;
    if (!w || w.closed) return;
    const d = w.document;
    const place = (box, slide) => {
      const frame = box.querySelector('.pv-frame');
      frame.innerHTML = '';
      if (!slide) return;
      const copy = slide.cloneNode(true);
      copy.removeAttribute('id');
      copy.style.transform = copy.style.opacity = copy.style.clipPath = copy.style.zIndex = '';
      frame.appendChild(copy);
      const s = Math.min(box.clientWidth / W, box.clientHeight / H);
      frame.style.transform = `translate(${(box.clientWidth - W * s) / 2}px,${(box.clientHeight - H * s) / 2}px) scale(${s})`;
    };
    place(d.querySelector('.pv-current'), show.current);
    const nextIndex = show.sequence[show.position + 1];
    place(d.querySelector('.pv-next'), nextIndex !== undefined ? slides[nextIndex] : null);
    const notes = show.current && show.current.querySelector('.notes');
    d.querySelector('.pv-notes').innerHTML = notes ? notes.innerHTML : '<i>No notes</i>';
    d.querySelector('.pv-count').textContent = `Slide ${show.index + 1} of ${slides.length}`;
  }

  /* ---------------------------------------------------------------- input */

  document.addEventListener('keydown', e => onKey(e));

  function onKey(e) {
    if (!show) {
      if (e.key === 'F5') {
        e.preventDefault();
        start(e.shiftKey ? visibleSlide() : order()[0]);
        if (e.altKey) openPresenter();
      }
      return;
    }
    if (e.key === 'v' || e.key === 'V') { openPresenter(); return; }
    const key = e.key;
    if (/^[0-9]$/.test(key)) { show.digits += key; return; }
    if (key === 'Enter' && show.digits) { const n = parseInt(show.digits, 10) - 1; show.digits = ''; if (n >= 0 && n < slides.length) jump(n, false); return; }
    show.digits = '';
    if (key === 'Escape') { e.preventDefault(); stop(); return; }
    if (show.kiosk) return;
    if (['ArrowRight', 'ArrowDown', 'PageDown', ' ', 'Enter', 'n', 'N'].includes(key)) { e.preventDefault(); next(); }
    else if (['ArrowLeft', 'ArrowUp', 'PageUp', 'Backspace', 'p', 'P'].includes(key)) { e.preventDefault(); previous(); }
    else if (key === 'Home') jump(show.sequence[0], false);
    else if (key === 'End') jump(show.sequence[show.sequence.length - 1], false);
    else if (key === 'b' || key === 'B' || key === '.') blank('#000');
    else if (key === 'w' || key === 'W' || key === ',') blank('#fff');
  }

  function blank(color) {
    const c = show.cover;
    const on = c.style.opacity === '1' && c.style.background === color;
    c.style.background = color;
    c.style.opacity = on ? '0' : '1';
  }

  document.addEventListener('click', e => {
    if (!show) {
      // Outside the show a click on audio or video plays or pauses it.
      const media = e.target.closest('.sp') && e.target.closest('.sp').querySelector('.fs-media');
      if (media) { e.preventDefault(); toggleMedia(media); }
      return;
    }
    if (!show.overlay.contains(e.target)) return;
    const link = e.target.closest('[data-action]');
    if (link && link.hasAttribute('data-zoom') && zoomTo(link)) { e.preventDefault(); return; }
    if (link && action(link)) { e.preventDefault(); return; }
    const href = e.target.closest('a[href]');
    if (href) return;   // ordinary links open as usual
    const shape = e.target.closest('[data-id]');
    if (shape && show.animation && show.animation.trigger(shape.getAttribute('data-id'))) return;
    if (!show.kiosk) next();
  });

  // Mouse-over actions (a:hlinkHover): run once each time the pointer enters the shape.
  let hovered = null;
  document.addEventListener('mouseover', e => {
    if (!show || !show.overlay.contains(e.target)) return;
    const shape = e.target.closest('[data-hover-action],[data-hover-href]');
    if (shape === hovered) return;
    hovered = shape;
    if (!shape) return;
    const value = shape.getAttribute('data-hover-action');
    if (value) {
      const proxy = document.createElement('a');
      proxy.setAttribute('data-action', value);
      action(proxy);
    } else if (shape.getAttribute('data-hover-href')) {
      const href = shape.getAttribute('data-hover-href');
      if (href.startsWith('#')) location.hash = href; else window.open(href, '_blank', 'noopener');
    }
  });

  let touchX = null;
  document.addEventListener('touchstart', e => { if (show) touchX = e.touches[0].clientX; }, { passive: true });
  document.addEventListener('touchend', e => {
    if (!show || touchX === null) return;
    const dx = e.changedTouches[0].clientX - touchX;
    touchX = null;
    if (Math.abs(dx) > 50 && !show.kiosk) { if (dx < 0) next(); else previous(); }
  }, { passive: true });

  window.addEventListener('resize', resize);
  // Leaving full screen (Esc in the browser) ends the show.
  document.addEventListener('fullscreenchange', () => { if (show && show.fullscreen && !document.fullscreenElement) stop(); });

  function visibleSlide() {
    let best = 0, distance = Infinity;
    slides.forEach((s, i) => { const r = s.getBoundingClientRect(); const d = Math.abs(r.top); if (d < distance) { distance = d; best = i; } });
    return best;
  }

  /* ---------------------------------------------------------------- the play button */

  const button = document.createElement('button');
  button.className = 'fs-play';
  button.type = 'button';
  button.title = 'Play slide show (F5, Shift+F5 from this slide)';
  button.textContent = '▶';
  button.addEventListener('click', e => { e.stopPropagation(); start(e.shiftKey ? visibleSlide() : order()[0]); });
  document.body.appendChild(button);

  /* ---------------------------------------------------------------- review comments */

  // Comments (catalog: review.comments, review.modernComments) stay hidden like in PowerPoint's slide show; the
  // button (or C outside the show) shows the markers on the slides and the threads below them.
  const threads = document.querySelectorAll('.comments .cm').length;
  function toggleComments(on) {
    const visible = on === undefined ? !document.body.classList.contains('fs-comments') : on;
    document.body.classList.toggle('fs-comments', visible);
    // Modern comments carry UTC times; show them in the viewer's time zone.
    document.querySelectorAll('.comments time[datetime$="Z"]').forEach(t => {
      const date = new Date(t.getAttribute('datetime'));
      if (!isNaN(date)) t.textContent = date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
    });
  }
  if (threads > 0) {
    const toggle = document.createElement('button');
    toggle.className = 'fs-comments-toggle';
    toggle.type = 'button';
    toggle.title = 'Show or hide comments (C)';
    toggle.textContent = String.fromCodePoint(0x1F4AC) + ' ' + threads;
    toggle.addEventListener('click', e => { e.stopPropagation(); toggleComments(); });
    document.body.appendChild(toggle);
    document.addEventListener('keydown', e => {
      if (!show && (e.key === 'c' || e.key === 'C') && !e.ctrlKey && !e.metaKey && !e.altKey) toggleComments();
    });
    if (/^#cm-/.test(location.hash)) toggleComments(true);
  }

  /**
   * Draws slide index (in the document, outside the show) with its first `steps` main steps played and, at t ms,
   * the following one; returns the end times of the main steps (ms from their start). For comparisons with
   * PowerPoint's rendering of the animations.
   */
  const frames = new Map();
  function frame(index, steps, t, tick) {
    if (!slides[index]) return null;
    let animation = frames.get(index);
    if (!animation) frames.set(index, animation = new SlideAnimation(slides[index], index, tick));
    const main = animation.timeline.steps;
    animation.done = main.slice(0, Math.min(steps, main.length));
    animation.render(t !== undefined && steps < main.length ? main[steps] : null, t || 0);
    return main.map(step => ({ end: step.end, auto: step.auto }));
  }

  // ?frame=ms&fps=n#slide-N draws slide N as PowerPoint's video of the slide (n frames per second) shows it ms after
  // the slide began: the main steps play one after another without waiting for clicks, each starting on the first
  // frame after the previous one ended by the video's time and the second one frame later still; within a step
  // PowerPoint's clock advances whole milliseconds per frame (33 ms at 30 fps, 16 at 60: the steps run 1 and 4 %
  // slow and are cut off a little where the next begins), and the last step shows its last frame unfinished
  // (measured; tools/Fidelity animate).
  const frameParams = new URLSearchParams(location.search);
  const frameSlide = /^#slide-(\d+)$/.exec(location.hash);
  if (frameParams.get('frame') !== null && frameSlide) {
    const index = +frameSlide[1] - 1;
    const fps = +frameParams.get('fps') || 30;
    const [video, tick] = [1000 / fps, Math.floor(1000 / fps)];
    const main = frame(index, 0, 0, undefined) || [];
    const shown = Math.round(+frameParams.get('frame') / video);
    let steps = 0, start = 0;
    while (steps < main.length) {
      const next = start + Math.ceil(main[steps].end / video - 1e-6) + (steps === 0 ? 1 : 0);
      if (shown < next || steps === main.length - 1) break;
      start = next;
      steps++;
    }
    frame(index, steps, (shown - start) * tick, undefined);
  }

  // ?transition=ms&fps=n#slide-N draws the transition into slide N ms after it began, the slide before it at its end
  // and slide N at its start, on a stage in the slide's place (tools/Fidelity transitions).
  const transitionAt = frameParams.get('transition');
  if (transitionAt !== null && frameSlide) {
    const index = +frameSlide[1] - 1;
    const spec = slides[index] && info(index).transition;
    if (spec && slides[index - 1]) {
      const [a, b] = [slides[index - 1], slides[index]];
      const steps = frame(index - 1, 0, 0, undefined) || [];
      frame(index - 1, steps.length, undefined, undefined);
      frame(index, 0, 0, undefined);
      const stage = document.createElement('div');
      stage.className = 'fs-stage';
      // A stage that clips without scrolling: the slide the address targets would scroll into view.
      stage.style.cssText = `position:fixed;left:0;top:0;width:${W}px;height:${H}px;z-index:1;background:#000;overflow:clip`;
      const cover = document.createElement('div');
      cover.className = 'fs-cover';
      cover.style.cssText = `position:fixed;left:0;top:0;width:${W}px;height:${H}px;z-index:2`;
      stage.append(a, b);
      document.body.append(stage, cover);
      const effect = transitionEffect(spec.type || 'none', spec, a, b, cover);
      const duration = Math.max(1, spec.dur || 0);
      if (effect) (effect.prepare ? effect.prepare() : Promise.resolve()).then(() => effect.frame(Math.min(1, Math.max(0, +transitionAt / duration))));
    }
  }

  window.FreeSlate = { start: i => start(i === undefined ? order()[0] : i), stop, next, previous, presenter: openPresenter, comments: toggleComments, frame };
})();
