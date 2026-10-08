/* FreeSlate slide show player (catalog: interactivity.player, transition.*, animation.*).
 * Reads the presentation data (#freeslate-data) and plays the slides of this document: navigation, transitions
 * and the slides' timing trees (PowerPoint's par/seq time containers and animation behaviours). */
(() => {
  'use strict';
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
    const tokens = String(text).match(/#ppt_[xywh]|\$|[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?|[a-z]+|[-+*/^(),]/gi) || [];
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
      if (t && t[0] === '#') { const key = t.slice(5); return s => s[key]; }
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
   * A target of behaviours: a shape (its div), its background, or a paragraph (SVG groups inside the shape).
   * Base values are PowerPoint's ppt_x/ppt_y (center) and ppt_w/ppt_h, as fractions of the slide size.
   */
  class Target {
    constructor(element, svg) {
      this.el = element;
      this.svg = svg;   // true for SVG elements inside a shape
      if (svg) {
        const shape = element.closest('.sp');
        this.shapeMatrix = parseMatrix(shape ? shape.style.transform : '');
        this.matrix = IDENTITY.slice();
        let box;
        try { box = element.getBBox(); } catch { box = { x: 0, y: 0, width: 0, height: 0 }; }
        this.local = box;
        const [cx, cy] = apply(this.shapeMatrix, box.x + box.width / 2, box.y + box.height / 2);
        this.base = { x: cx / W, y: cy / H, w: box.width / W, h: box.height / H };
      } else {
        this.matrix = parseMatrix(element.style.transform);
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
        rot: 0, sx: 1, sy: 1, mx: 0, my: 0, clip: null, fill: null, color: null, stroke: null,
      };
    }

    /** Writes a state to the element (only what differs from the original). */
    render(s) {
      const el = this.el;
      el.style.visibility = s.visible ? '' : 'hidden';
      el.style.opacity = s.opacity < 1 ? String(Math.max(0, s.opacity)) : '';
      const b = this.base;
      const moved = Math.abs(s.x - b.x) > 1e-6 || Math.abs(s.y - b.y) > 1e-6 || Math.abs(s.w - b.w) > 1e-6 || Math.abs(s.h - b.h) > 1e-6
                    || s.rot !== 0 || s.sx !== 1 || s.sy !== 1 || s.mx !== 0 || s.my !== 0;
      if (this.svg) {
        if (moved) {
          // Slide-space change around the paragraph's center, expressed in the shape's coordinates.
          const [lx, ly] = [this.local.x + this.local.width / 2, this.local.y + this.local.height / 2];
          const kx = (b.w > 0 ? s.w / b.w : 1) * s.sx, ky = (b.h > 0 ? s.h / b.h : 1) * s.sy;
          const shape = this.shapeMatrix;
          const inverse = invert(shape);
          const slideCenter = apply(shape, lx, ly);
          const target = [(s.x + s.mx) * W, (s.y + s.my) * H];
          const change = multiply(translate(target[0], target[1]), multiply(rotate(s.rot), multiply(scale(kx, ky), translate(-slideCenter[0], -slideCenter[1]))));
          el.setAttribute('transform', css(multiply(inverse, multiply(change, shape))));
        } else {
          el.removeAttribute('transform');
        }
        this.renderClip(s.clip);
      } else {
        if (moved) {
          const w = this.local.width, h = this.local.height;
          const [cx, cy] = apply(this.matrix, w / 2, h / 2);
          const kx = (b.w > 0 ? s.w / b.w : 1) * s.sx, ky = (b.h > 0 ? s.h / b.h : 1) * s.sy;
          const change = multiply(translate((s.x + s.mx) * W, (s.y + s.my) * H), multiply(rotate(s.rot), multiply(scale(kx, ky), translate(-cx, -cy))));
          el.style.transform = css(multiply(change, this.matrix));
        } else {
          el.style.transform = this.originalTransform;
        }
        el.style.clipPath = s.clip ? s.clip.css : '';
      }
      this.renderPaint(s);
    }

    /** SVG targets get an SVG clip path in their own coordinates. */
    renderClip(clip) {
      const el = this.el;
      if (!clip) { el.removeAttribute('clip-path'); return; }
      if (!this.clipPath) {
        const svg = el.ownerSVGElement;
        const ns = 'http://www.w3.org/2000/svg';
        this.clipPath = document.createElementNS(ns, 'clipPath');
        this.clipPath.id = 'fs-clip-' + Math.random().toString(36).slice(2);
        this.clipPath.setAttribute('clipPathUnits', 'objectBoundingBox');
        this.clipShape = document.createElementNS(ns, 'path');
        this.clipPath.appendChild(this.clipShape);
        svg.appendChild(this.clipPath);
      }
      this.clipShape.setAttribute('d', clip.path);
      el.setAttribute('clip-path', `url(#${this.clipPath.id})`);
    }

    renderPaint(s) {
      if (s.fill !== this.lastFill) {
        this.lastFill = s.fill;
        const paths = this.el.querySelectorAll(this.svg ? 'path' : '[data-bg] path, svg > path');
        paths.forEach(p => { if (p.getAttribute('fill') !== 'none') p.style.fill = s.fill || ''; });
      }
      if (s.stroke !== this.lastStroke) {
        this.lastStroke = s.stroke;
        this.el.querySelectorAll(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => {
          if (p.getAttribute('stroke') && p.getAttribute('stroke') !== 'none') p.style.stroke = s.stroke || '';
        });
      }
      if (s.color !== this.lastColor) {
        this.lastColor = s.color;
        this.el.querySelectorAll('[data-tx] g[fill], [data-tx] text:not(.sel), [data-tx] rect').forEach(t => { t.style.fill = s.color || ''; });
      }
    }

    reset() {
      const el = this.el;
      el.style.visibility = el.style.opacity = '';
      if (this.svg) { el.removeAttribute('transform'); el.removeAttribute('clip-path'); }
      else { el.style.transform = this.originalTransform; el.style.clipPath = ''; }
      this.renderPaint({ fill: null, stroke: null, color: null });
    }
  }

  function invert(m) {
    const det = m[0] * m[3] - m[1] * m[2] || 1;
    return [m[3] / det, -m[1] / det, -m[2] / det, m[0] / det,
      (m[2] * m[5] - m[3] * m[4]) / det, (m[1] * m[4] - m[0] * m[5]) / det];
  }

  /** The targets of a behaviour's tgtEl on a slide (several for paragraph ranges). */
  function resolveTargets(slide, target, cache) {
    if (!target || target.spid === undefined) return [];
    const shape = slide.querySelector(`[data-id="${target.spid}"]`);
    if (!shape) return [];
    const keys = [];
    if (target.pRg) {
      for (let p = target.pRg[0]; p <= target.pRg[1]; p++) keys.push([`${target.spid}:p${p}`, () => shape.querySelector(`g[data-p="${p}"]`), true]);
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
    const begin = start + delayOf(node);
    const speed = node.spd ? node.spd / 100000 : 1;
    const children = node.children || [];
    let end = begin;
    if (node.t === 'par' || node.t === 'excl') {
      for (const child of children) end = Math.max(end, schedule(child, begin, out, Object.assign({}, context, inherit(node, context))));
      if (typeof node.dur === 'number') end = begin + node.dur / speed;
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
  function inherit(node, context) {
    const result = {};
    if (node.presetClass) result.presetClass = node.presetClass;
    if (node.presetID !== undefined) result.presetID = node.presetID;
    if (node.accel || node.decel) result.ease = { accel: (node.accel || 0) / 100000, decel: (node.decel || 0) / 100000 };
    return result;
  }

  /** Steps of the main sequence and of the interactive sequences (triggers) of a slide. */
  function buildTimeline(slide, timing) {
    const result = { steps: [], triggers: new Map(), hidden: new Set(), targets: new Map() };
    if (!timing || !timing.root) return result;
    for (const sequence of timing.root.children || []) {
      if (sequence.t !== 'seq') continue;
      const steps = (sequence.children || []).map(stepNode => {
        const behaviors = [];
        const end = schedule(Object.assign({}, stepNode, { begin: [] }), 0, behaviors, {});
        for (const b of behaviors) b.targets = resolveTargets(slide, b.node.target, result.targets);
        return { behaviors, end, auto: !isIndefinite(stepNode) || isAuto(stepNode) };
      });
      if (sequence.nodeType === 'interactiveSeq') {
        const trigger = (sequence.begin || []).find(c => c.evt === 'onClick' && c.target);
        if (trigger) result.triggers.set(String(trigger.target.spid), { steps, played: 0 });
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

  function ease(p, e) {
    if (!e) return p;
    const a = Math.min(e.accel, 1), d = Math.min(e.decel, 1 - a);
    if (a + d <= 0) return p;
    const v = 2 / (2 - a - d);
    if (p < a) return v * p * p / (2 * a);
    if (p <= 1 - d) return v * (p - a / 2);
    return 1 - v * (1 - p) * (1 - p) / (2 * d);
  }

  /** Progress (0…1) of a behaviour at time t (ms in its step), or null before its start or after a removed end. */
  function progress(b, t) {
    if (t < b.start) return null;
    const node = b.node;
    const local = t - b.start;
    if (local >= b.total) {
      // Without fill (or fill="remove") a behaviour's effect ends with it; set behaviours hold their value.
      if ((node.fill === undefined || node.fill === 'remove') && node.t !== 'set') return null;
      return ease(node.autoRev ? 0 : 1, b.context.ease);
    }
    let cycle = local / b.dur;
    let p = cycle - Math.floor(cycle);
    if (node.autoRev) { const half = local / b.dur % 2; p = half > 1 ? 2 - half : half; }
    return ease(Math.min(1, p), node.accel || node.decel ? { accel: (node.accel || 0) / 100000, decel: (node.decel || 0) / 100000 } : b.context.ease);
  }

  const attributeKey = attribute => ({
    ppt_x: 'x', ppt_y: 'y', ppt_w: 'w', ppt_h: 'h', r: 'rot', 'style.rotation': 'rot', 'style.opacity': 'opacity',
    'style.visibility': 'visible', fillcolor: 'fill', 'fill.color': 'fill', 'stroke.color': 'stroke', 'style.color': 'color',
  })[attribute];

  function hexColor(c) {
    if (typeof c === 'string') return c;
    if (!c) return null;
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

  /** Applies one behaviour at progress p to a target's state. */
  function evaluate(b, p, target, state) {
    const node = b.node;
    const base = target.base;
    const vars = { x: base.x, y: base.y, w: base.w, h: base.h, $: p };
    switch (node.t) {
      case 'set': {
        for (const attribute of node.attrs || []) assign(state, attribute, node.to, vars);
        break;
      }
      case 'anim': {
        for (const attribute of node.attrs || []) {
          const value = animValue(node, p, vars, state, attribute);
          if (value !== undefined) assign(state, attribute, value, vars, true);
        }
        break;
      }
      case 'animEffect': {
        const filter = node.filter || 'fade';
        const shown = node.transition === 'out' ? 1 - p : p;
        if (node.transition === 'in' || node.transition === 'out') {
          state.visible = true;
          if (filter.startsWith('fade')) state.opacity = Math.min(state.opacity, shown);
          else if (shown < 1) state.clip = effectClip(filter, shown);
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
        const from = node.from || [100000, 100000];
        const to = node.to || (node.by ? [from[0] * node.by[0] / 100000, from[1] * node.by[1] / 100000] : [100000, 100000]);
        state.sx *= (from[0] + (to[0] - from[0]) * p) / 100000;
        state.sy *= (from[1] + (to[1] - from[1]) * p) / 100000;
        break;
      }
      case 'animClr': {
        const key = attributeKey((node.attrs || [])[0]) || 'fill';
        const from = hexColor(node.from) || state[key] || currentColor(target, key);
        const to = node.to ? hexColor(node.to) : from;
        state[key] = mixColor(from, to, p);
        break;
      }
    }
  }

  function currentColor(target, key) {
    const selector = key === 'color' ? '[data-tx] g[fill], [data-tx] text' : (target.svg ? 'path' : '[data-bg] path, svg > path');
    const el = target.el.querySelector(selector);
    return el ? (key === 'stroke' ? el.getAttribute('stroke') : el.getAttribute('fill')) : null;
  }

  function assign(state, attribute, value, vars, numeric) {
    const key = attributeKey(attribute);
    if (!key) return;
    if (key === 'visible') state.visible = value !== 'hidden' && value !== false;
    else if (key === 'opacity') state.opacity = Number(value);
    else if (key === 'rot') state.rot += Number(value);
    else if (key === 'fill' || key === 'stroke' || key === 'color') state[key] = hexColor(value);
    else state[key] = numeric ? Number(value) : Number(formula(value)(vars));
  }

  function animValue(node, p, vars, state, attribute) {
    const tav = node.tav;
    const valueOf = v => (typeof v === 'number' ? v : (node.valueType === 'str' ? v : formula(v)(vars)));
    if (tav && tav.length) {
      const times = tav.map((k, i) => (typeof k.tm === 'number' ? k.tm / 100000 : i / Math.max(1, tav.length - 1)));
      let i = 0;
      while (i < tav.length - 1 && p > times[i + 1]) i++;
      const a = tav[i], b = tav[Math.min(i + 1, tav.length - 1)];
      const span = times[Math.min(i + 1, tav.length - 1)] - times[i];
      const local = span > 0 ? Math.min(1, Math.max(0, (p - times[i]) / span)) : 1;
      if (node.valueType === 'str' || node.calcmode === 'discrete') return valueOf(local < 1 ? a.val : b.val);
      const va = Number(valueOf(a.val)), vb = Number(valueOf(b.val));
      let v = va + (vb - va) * local;
      if (a.fmla) v = formula(a.fmla)(Object.assign({}, vars, { $: v }));
      return v;
    }
    if (node.to !== undefined || node.by !== undefined) {
      const current = state[attributeKey(attribute)];
      const from = node.from !== undefined ? Number(valueOf(node.from)) : Number(current);
      const to = node.to !== undefined ? Number(valueOf(node.to)) : from + Number(node.by);
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
    const add = (px, py) => {
      const last = points[points.length - 1];
      const length = last ? last[2] + Math.hypot(px * W - last[0] * W, py * H - last[1] * H) : 0;
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

  /** Clip shapes of animEffect filters at visibility s (0 hidden … 1 shown); css for divs, path in 0…1 units for SVG. */
  function effectClip(filter, s) {
    const m = /^([a-z]+)(?:\(([^)]*)\))?/.exec(filter) || [];
    const name = m[1], option = m[2] || '';
    const polygon = pts => ({ css: `polygon(${pts.map(([x, y]) => `${(x * 100).toFixed(2)}% ${(y * 100).toFixed(2)}%`).join(',')})`, path: 'M' + pts.map(([x, y]) => `${x.toFixed(4)} ${y.toFixed(4)}`).join('L') + 'Z' });
    const rect = (l, t, r, b) => polygon([[l, t], [r, t], [r, b], [l, b]]);
    const bands = (count, horizontal) => {
      const pts = [];
      for (let k = 0; k < count; k++) {
        const a = k / count, b = a + s / count;
        if (horizontal) pts.push([0, a], [1, a], [1, b], [0, b], [0, a]);
        else pts.push([a, 0], [a, 1], [b, 1], [b, 0], [a, 0]);
      }
      return polygon(pts);
    };
    switch (name) {
      case 'wipe':
        // wipe(left) starts at the left edge ("From Left"), wipe(up) at the top.
        if (option === 'left') return rect(0, 0, s, 1);
        if (option === 'right') return rect(1 - s, 0, 1, 1);
        if (option === 'down') return rect(0, 1 - s, 1, 1);
        return rect(0, 0, 1, s);
      case 'slide':
        return rect(0, 0, 1, 1);
      case 'barn': {
        const vertical = option.includes('Vertical'), out = option.startsWith('out');
        const h = s / 2;
        if (out) return vertical ? rect(0.5 - h, 0, 0.5 + h, 1) : rect(0, 0.5 - h, 1, 0.5 + h);
        return vertical
          ? polygon([[0, 0], [h, 0], [h, 1], [0, 1], [0, 0], [1, 0], [1, 1], [1 - h, 1], [1 - h, 0], [1, 0]])
          : polygon([[0, 0], [1, 0], [1, h], [0, h], [0, 0], [0, 1], [1, 1], [1, 1 - h], [0, 1 - h], [0, 1]]);
      }
      case 'blinds': return bands(6, option === 'horizontal');
      case 'randombar': return bands(12, option === 'horizontal');
      case 'checkerboard': return bands(8, option !== 'across');
      case 'box': {
        const h = s / 2;
        return option === 'in'
          ? polygon([[0, 0], [1, 0], [1, 1], [0, 1], [0, 0], [h, h], [h, 1 - h], [1 - h, 1 - h], [1 - h, h], [h, h]])
          : rect(0.5 - h, 0.5 - h, 0.5 + h, 0.5 + h);
      }
      case 'circle': case 'diamond': case 'plus': {
        const r = s * 0.75;
        if (name === 'diamond') return polygon([[0.5, 0.5 - r * 1.4], [0.5 + r * 1.4, 0.5], [0.5, 0.5 + r * 1.4], [0.5 - r * 1.4, 0.5]]);
        if (name === 'plus') { const a = s / 2; return polygon([[0.5 - a, 0], [0.5 + a, 0], [0.5 + a, 0.5 - a], [1, 0.5 - a], [1, 0.5 + a], [0.5 + a, 0.5 + a], [0.5 + a, 1], [0.5 - a, 1], [0.5 - a, 0.5 + a], [0, 0.5 + a], [0, 0.5 - a], [0.5 - a, 0.5 - a]]); }
        const pts = [];
        for (let k = 0; k < 32; k++) { const a = k / 32 * Math.PI * 2; pts.push([0.5 + Math.cos(a) * r, 0.5 + Math.sin(a) * r]); }
        return polygon(pts);
      }
      case 'wedge': case 'wheel': {
        const spokes = name === 'wheel' ? Math.max(1, parseInt(option, 10) || 1) : 2;
        const pts = [];
        for (let k = 0; k < spokes; k++) {
          const start = name === 'wedge' ? -Math.PI / 2 - s * Math.PI : -Math.PI / 2 + k * 2 * Math.PI / spokes;
          const sweep = name === 'wedge' ? s * 2 * Math.PI : s * 2 * Math.PI / spokes;
          pts.push([0.5, 0.5]);
          for (let j = 0; j <= 12; j++) { const a = start + sweep * j / 12; pts.push([0.5 + Math.cos(a), 0.5 + Math.sin(a)]); }
          pts.push([0.5, 0.5]);
        }
        return polygon(pts);
      }
      case 'strips': {
        const d = s * 2;
        if (option === 'downRight') return polygon([[0, 0], [d, 0], [0, d]]);
        if (option === 'upLeft') return polygon([[1, 1], [1 - d, 1], [1, 1 - d]]);
        if (option === 'upRight') return polygon([[0, 1], [0, 1 - d], [d, 1]]);
        return polygon([[1, 0], [1 - d, 0], [1, d]]);
      }
      case 'dissolve': return rect(0, 0, 1, 1);
      default: return rect(0, 0, 1, 1);
    }
  }

  /* ---------------------------------------------------------------- media (catalog: picture.media) */

  const mediaOf = (slide, spid) => slide.querySelector(`[data-id="${spid}"] .fs-media`);
  const trimStart = el => (parseFloat(el.dataset.trimStart) || 0) / 1000;

  /** Plays media from a position (seconds after the trimmed start), honouring trim end, fade in and volume. */
  function playMedia(el, from, volume) {
    if (!el.dataset.watched) {
      el.dataset.watched = '1';
      el.addEventListener('timeupdate', () => {
        const end = (el.duration || Infinity) - (parseFloat(el.dataset.trimEnd) || 0) / 1000;
        if (el.currentTime >= end) stopMedia(el);
      });
      el.addEventListener('ended', () => stopMedia(el));
    }
    if (volume !== undefined) el.dataset.volume = volume;
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
    el.pause();
    el.classList.remove('playing');
    try { el.currentTime = trimStart(el); } catch { /* not loaded yet */ }
  }
  function toggleMedia(el) {
    if (el.paused) {
      if (!el.classList.contains('playing')) playMedia(el, 0);
      else el.play().catch(() => {});
    } else {
      el.pause();
    }
  }

  /** Runs a p:cmd behaviour (playFrom(t), togglePause, pause, resume, stop) on its target's media. */
  function command(slide, node, volumes) {
    const el = node.target && mediaOf(slide, node.target.spid);
    if (!el) return;
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
    const result = new Map();
    const visit = node => {
      if (!node) return;
      if ((node.t === 'audio' || node.t === 'video') && node.target && node.target.spid !== undefined)
        result.set(String(node.target.spid), node.mute ? 0 : (typeof node.vol === 'number' ? node.vol / 100000 : 1));
      (node.children || []).forEach(visit);
    };
    visit(timing && timing.root);
    return result;
  }

  /* ---------------------------------------------------------------- the animation player of one slide */

  class SlideAnimation {
    constructor(slide, index) {
      this.slide = slide;
      this.timeline = buildTimeline(slide, info(index).timing);
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
      this.stop();
      if (sequence.played >= sequence.steps.length) {
        sequence.played = 0;
        this.done = this.done.filter(s => !sequence.steps.includes(s));
      }
      this.play(sequence.steps[sequence.played++]);
      return true;
    }

    play(step) {
      cancelAnimationFrame(this.frame);
      clearTimeout(this.timer);
      const current = this.current = { step, started: performance.now() };
      const commands = step.behaviors.filter(b => b.node.t === 'cmd');
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
      const run = (step, time) => {
        for (const b of step.behaviors) {
          const p = progress(b, time);
          if (p === null) continue;
          for (const { key, target } of b.targets) evaluate(b, p, target, states.get(key));
        }
      };
      for (const step of this.done) run(step, Infinity);
      if (current) run(current, t);
      for (const [key, target] of this.timeline.targets) if (target) target.render(states.get(key));
    }

    reset() {
      this.stop();
      for (const target of this.timeline.targets.values()) if (target) target.reset();
      this.slide.querySelectorAll('.fs-media').forEach(stopMedia);
    }
  }

  /* ---------------------------------------------------------------- transitions */

  /** Runs a transition from the outgoing to the incoming slide; both are absolutely positioned in the stage. */
  function transition(spec, outgoing, incoming, cover) {
    return new Promise(resolve => {
      const type = spec && spec.type ? spec.type : 'none';
      const duration = Math.max(0, spec && spec.dur !== undefined ? spec.dur : 0);
      const effect = transitionEffect(type, spec || {}, outgoing, incoming, cover);
      if (!effect || duration === 0 || !outgoing) { if (effect && effect.end) effect.end(); resolve(); return; }
      const started = performance.now();
      const tick = () => {
        const p = Math.min(1, (performance.now() - started) / duration);
        effect.frame(p);
        if (p < 1) requestAnimationFrame(tick);
        else { effect.end(); resolve(); }
      };
      tick();
    });
  }

  const offsets = { l: [-1, 0], r: [1, 0], u: [0, -1], d: [0, 1], lu: [-1, -1], ru: [1, -1], ld: [-1, 1], rd: [1, 1] };

  function transitionEffect(type, spec, a, b, cover) {
    const reset = () => {
      for (const el of [a, b]) if (el) { el.style.transform = el.style.opacity = el.style.clipPath = el.style.zIndex = el.style.transformOrigin = ''; }
      cover.style.opacity = '0';
    };
    const clip = (fn) => ({ frame: p => { b.style.zIndex = 2; b.style.clipPath = fn(p); }, end: reset });
    const dir = spec.dir || 'l';
    const vector = offsets[dir] || offsets.l;
    const inset = (t, r, bo, l) => `inset(${t * 100}% ${r * 100}% ${bo * 100}% ${l * 100}%)`;
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
        return spec.thruBlk ? { frame: p => { cover.style.background = '#000'; cover.style.opacity = p < 1 ? '1' : '0'; b.style.opacity = p < 0.5 ? '0' : ''; }, end: reset } : null;
      case 'fade':
        if (spec.thruBlk) {
          return { frame: p => { cover.style.background = '#000'; cover.style.opacity = String(p < 0.5 ? p * 2 : (1 - p) * 2); b.style.opacity = p < 0.5 ? '0' : '1'; b.style.zIndex = 2; }, end: reset };
        }
        return { frame: p => { b.style.zIndex = 2; b.style.opacity = String(p); }, end: reset };
      case 'dissolve': case 'glitter': case 'honeycomb': case 'shred':
        return { frame: p => { b.style.zIndex = 2; b.style.opacity = String(p); }, end: reset };
      case 'push': case 'pan': case 'conveyor':
        return { frame: p => {
          a.style.transform = `translate(${vector[0] * p * W}px,${vector[1] * p * H}px)`;
          b.style.transform = `translate(${-vector[0] * (1 - p) * W}px,${-vector[1] * (1 - p) * H}px)`;
        }, end: reset };
      case 'cover':
        return { frame: p => { b.style.zIndex = 2; b.style.transform = `translate(${-vector[0] * (1 - p) * W}px,${-vector[1] * (1 - p) * H}px)`; }, end: reset };
      case 'pull': case 'reveal':
        return { frame: p => { a.style.zIndex = 2; a.style.transform = `translate(${vector[0] * p * W}px,${vector[1] * p * H}px)`; if (type === 'reveal') a.style.opacity = String(1 - p); }, end: reset };
      case 'wipe':
        return clip(p => dir === 'l' ? inset(0, 0, 0, 1 - p) : dir === 'r' ? inset(0, 1 - p, 0, 0) : dir === 'u' ? inset(1 - p, 0, 0, 0) : inset(0, 0, 1 - p, 0));
      case 'split': {
        const vertical = spec.orient === 'vert', out = spec.dir !== 'in';
        return clip(p => {
          const h = (1 - p) / 2;
          if (out) return vertical ? inset(0, h, 0, h) : inset(h, 0, h, 0);
          return vertical ? poly([[0, 0], [p / 2, 0], [p / 2, 1], [0, 1], [0, 0], [1, 0], [1, 1], [1 - p / 2, 1], [1 - p / 2, 0], [1, 0]])
            : poly([[0, 0], [1, 0], [1, p / 2], [0, p / 2], [0, 0], [0, 1], [1, 1], [1, 1 - p / 2], [0, 1 - p / 2], [0, 1]]);
        });
      }
      case 'blinds': return clip(p => bandsClip(6, spec.dir !== 'vert', p));
      case 'checker': return clip(p => bandsClip(10, spec.dir === 'vert', p));
      case 'comb': return clip(p => bandsClip(8, spec.dir !== 'vert', p));
      case 'randomBar': {
        const random = Array.from({ length: 20 }, () => Math.random());
        return clip(p => bandsClip(20, spec.dir !== 'vert', p, random));
      }
      case 'strips': {
        return clip(p => {
          const d = p * 2;
          if (dir === 'rd') return poly([[0, 0], [d, 0], [0, d]]);
          if (dir === 'lu') return poly([[1, 1], [1 - d, 1], [1, 1 - d]]);
          if (dir === 'ru') return poly([[0, 1], [0, 1 - d], [d, 1]]);
          return poly([[1, 0], [1 - d, 0], [1, d]]);
        });
      }
      case 'circle': return clip(p => `circle(${p * 75}% at 50% 50%)`);
      case 'diamond': return clip(p => poly([[0.5, 0.5 - p], [0.5 + p, 0.5], [0.5, 0.5 + p], [0.5 - p, 0.5]]));
      case 'plus': return clip(p => { const s = p / 2; return poly([[0.5 - s, 0], [0.5 + s, 0], [0.5 + s, 0.5 - s], [1, 0.5 - s], [1, 0.5 + s], [0.5 + s, 0.5 + s], [0.5 + s, 1], [0.5 - s, 1], [0.5 - s, 0.5 + s], [0, 0.5 + s], [0, 0.5 - s], [0.5 - s, 0.5 - s]]); });
      case 'wedge': case 'wheel': case 'wheelReverse': {
        const spokes = type === 'wedge' ? 1 : Math.max(1, spec.spokes || 4);
        return clip(p => {
          const pts = [];
          for (let k = 0; k < spokes; k++) {
            const sweep = (type === 'wedge' ? 1 : 1 / spokes) * Math.PI * 2 * p;
            const start = type === 'wedge' ? -Math.PI / 2 - sweep / 2 : -Math.PI / 2 + k * Math.PI * 2 / spokes;
            pts.push([0.5, 0.5]);
            for (let j = 0; j <= 16; j++) {
              const angle = start + (type === 'wheelReverse' ? -1 : 1) * sweep * j / 16;
              pts.push([0.5 + Math.cos(angle) * 1.5, 0.5 + Math.sin(angle) * 1.5]);
            }
          }
          return poly(pts);
        });
      }
      case 'zoom': case 'warp': case 'flythrough': {
        const inward = spec.dir !== 'out';
        return { frame: p => {
          b.style.zIndex = 2;
          b.style.opacity = String(p);
          const s = inward ? 0.3 + 0.7 * p : 1.6 - 0.6 * p;
          b.style.transform = `scale(${s})`;
          b.style.transformOrigin = '50% 50%';
        }, end: reset };
      }
      case 'newsflash': case 'vortex': case 'ripple':
        return { frame: p => {
          b.style.zIndex = 2;
          b.style.transformOrigin = '50% 50%';
          b.style.opacity = String(Math.min(1, p * 1.5));
          b.style.transform = type === 'ripple' ? `scale(${0.9 + 0.1 * p})` : `rotate(${(1 - p) * (type === 'vortex' ? -180 : 720)}deg) scale(${p})`;
        }, end: reset };
      case 'flash':
        return { frame: p => { cover.style.background = '#fff'; cover.style.opacity = String(p < 0.5 ? p * 2 : (1 - p) * 2); b.style.opacity = p < 0.5 ? '0' : '1'; b.style.zIndex = 2; }, end: reset };
      case 'flip': case 'switch': case 'gallery': case 'prism': case 'doors': case 'window': case 'ferris': case 'cube': case 'rotate': case 'box': case 'orbit':
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
      case 'prstTrans': {
        const preset = spec.prst || 'fade';
        if (preset === 'curtains') {
          return { frame: p => {
            a.style.zIndex = 2;
            a.style.clipPath = poly([[0, 0], [0.5 - p / 2, 0], [0.5 - p / 2, 1], [0, 1], [0, 0], [1, 0], [1, 1], [0.5 + p / 2, 1], [0.5 + p / 2, 0], [1, 0]]);
          }, end: reset };
        }
        if (preset === 'wind' || preset === 'airplane' || preset === 'peelOff' || preset === 'pageCurlSingle' || preset === 'pageCurlDouble') {
          const sign = spec.invX ? -1 : 1;
          return { frame: p => {
            a.style.zIndex = 2;
            a.style.transformOrigin = '0 50%';
            a.style.transform = `perspective(${W * 2}px) translate(${-sign * p * W * 0.5}px,${preset === 'airplane' ? -p * H : 0}px) rotateY(${sign * 90 * p}deg)`;
            a.style.opacity = String(1 - p);
          }, end: reset };
        }
        return { frame: p => {
          a.style.zIndex = 2;
          a.style.transformOrigin = '50% 50%';
          a.style.transform = `scale(${1 - 0.5 * p})`;
          a.style.opacity = String(1 - p);
        }, end: reset };
      }
      case 'morph': default:
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
    clearTimeout(show.timer);
    clearInterval(show.clock);
    if (show.presenter && !show.presenter.closed) show.presenter.close();
    if (show.animation) show.animation.reset();
    for (const [slide, placeholder] of show.placeholders) {
      slide.style.transform = slide.style.opacity = slide.style.clipPath = slide.style.zIndex = '';
      placeholder.replaceWith(slide);
    }
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
    if (show.animation) show.animation.stop();
    mount(slide);
    const animation = new SlideAnimation(slide, index);
    // "Show without animation" (showPr showAnimation="0") shows every slide in its final state.
    const animated = !deck.show || deck.show.animation !== false;
    if (!animated) options.atEnd = true;
    if (options.atEnd) animation.finish(); else animation.render(null, 0);
    const spec = info(index).transition;
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
    updatePresenter();
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

  function jump(index, transitionToo = true) {
    if (!show) return;
    const at = show.sequence.indexOf(index);
    if (at >= 0) show.position = at;
    else { show.sequence.splice(show.position + 1, 0, index); show.position++; }   // hidden slides by link
    goTo(index, { transition: transitionToo });
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

  window.FreeSlate = { start: i => start(i === undefined ? order()[0] : i), stop, next, previous, presenter: openPresenter };
})();
