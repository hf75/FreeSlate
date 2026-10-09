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
        rot: 0, sx: 1, sy: 1, mx: 0, my: 0, clip: null, fill: null, color: null, stroke: null,
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
          const transform = css(multiply(inverse, multiply(change, shape)));
          for (const e of this.els) e.setAttribute('transform', transform);
        } else {
          for (const e of this.els) e.removeAttribute('transform');
        }
        this.renderClip(s.clip);
      } else {
        if (moved) {
          const w = this.local.width, h = this.local.height;
          const [cx, cy] = apply(this.matrix, w / 2, h / 2);
          const kx = (b.w > 0 ? s.w / b.w : 1) * s.sx, ky = (b.h > 0 ? s.h / b.h : 1) * s.sy;
          const change = multiply(translate((s.x + s.mx) * W, (s.y + s.my) * H), multiply(rotate(s.rot), multiply(scale(kx, ky), translate(-cx, -cy))));
          const placed = multiply(change, this.matrix);
          el.style.transform = css(this.host ? multiply(invert(this.host), placed) : placed);
        } else {
          el.style.transform = this.originalTransform;
        }
        const soft = s.clip && s.clip.soft;
        el.style.clipPath = s.clip && !soft ? s.clip.css : '';
        el.style.maskImage = soft ? softGradient(soft) : '';
      }
      this.renderPaint(s);
    }

    /**
     * SVG targets get an SVG clip path (or a mask for soft edges) over the box around their elements: the effect's
     * unit square mapped onto it, in the elements' own coordinates.
     */
    renderClip(clip) {
      const els = this.els, box = this.local, ns = 'http://www.w3.org/2000/svg';
      const unit = `matrix(${box.width},0,0,${box.height},${box.x},${box.y})`;
      if (clip && clip.soft) {
        if (!this.mask) {
          const id = 'fs-mask-' + Math.random().toString(36).slice(2);
          this.mask = document.createElementNS(ns, 'mask');
          this.mask.id = id;
          // The mask's own region reaches past the box, so that strokes on its edge are not cut off.
          const pad = 0.1 * Math.max(box.width, box.height) + 4;
          for (const [k, v] of [['maskUnits', 'userSpaceOnUse'], ['x', box.x - pad], ['y', box.y - pad],
            ['width', box.width + 2 * pad], ['height', box.height + 2 * pad]]) this.mask.setAttribute(k, v);
          this.gradient = document.createElementNS(ns, 'linearGradient');
          this.gradient.id = id + '-g';
          const fill = document.createElementNS(ns, 'rect');
          for (const [k, v] of [['width', 1], ['height', 1], ['fill', `url(#${id}-g)`], ['transform', unit]]) fill.setAttribute(k, v);
          this.mask.append(this.gradient, fill);
          this.el.ownerSVGElement.appendChild(this.mask);
        }
        const [x1, y1, x2, y2] = { right: [0, 0, 1, 0], left: [1, 0, 0, 0], down: [0, 0, 0, 1], up: [0, 1, 0, 0] }[clip.soft.dir];
        for (const [k, v] of [['x1', x1], ['y1', y1], ['x2', x2], ['y2', y2]]) this.gradient.setAttribute(k, v);
        this.gradient.innerHTML = clip.soft.stops.map(([o, a]) => `<stop offset="${o}" stop-color="#fff" stop-opacity="${a}"/>`).join('');
        for (const e of els) { e.removeAttribute('clip-path'); e.setAttribute('mask', `url(#${this.mask.id})`); }
        return;
      }
      for (const e of els) e.removeAttribute('mask');
      if (!clip) { for (const e of els) e.removeAttribute('clip-path'); return; }
      if (!this.clipPath) {
        this.clipPath = document.createElementNS(ns, 'clipPath');
        this.clipPath.id = 'fs-clip-' + Math.random().toString(36).slice(2);
        this.clipPath.setAttribute('clipPathUnits', 'userSpaceOnUse');
        this.clipShape = document.createElementNS(ns, 'path');
        this.clipShape.setAttribute('transform', unit);
        this.clipPath.appendChild(this.clipShape);
        this.el.ownerSVGElement.appendChild(this.clipPath);
      }
      this.clipShape.setAttribute('d', clip.path);
      for (const e of els) e.setAttribute('clip-path', `url(#${this.clipPath.id})`);
    }

    /** The elements matching a selector inside the target's elements. */
    all(selector) {
      return this.els.flatMap(e => [...e.querySelectorAll(selector)]);
    }

    renderPaint(s) {
      if (s.fill !== this.lastFill) {
        this.lastFill = s.fill;
        this.all(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => { if (p.getAttribute('fill') !== 'none') p.style.fill = s.fill || ''; });
      }
      if (s.stroke !== this.lastStroke) {
        this.lastStroke = s.stroke;
        this.all(this.svg ? 'path' : '[data-bg] path, svg > path').forEach(p => {
          if (p.getAttribute('stroke') && p.getAttribute('stroke') !== 'none') p.style.stroke = s.stroke || '';
        });
      }
      if (s.color !== this.lastColor) {
        this.lastColor = s.color;
        this.all('[data-tx] g[fill], [data-tx] text:not(.sel), [data-tx] rect').forEach(t => { t.style.fill = s.color || ''; });
      }
    }

    reset() {
      for (const el of this.els) {
        el.style.visibility = el.style.opacity = '';
        if (this.svg) { el.removeAttribute('transform'); el.removeAttribute('clip-path'); el.removeAttribute('mask'); }
        else { el.style.transform = this.originalTransform; el.style.clipPath = el.style.maskImage = ''; }
      }
      this.renderPaint({ fill: null, stroke: null, color: null });
    }
  }

  /** A soft edge ({ dir, stops: [[offset, alpha]] }) as a CSS mask image. */
  function softGradient(soft) {
    const direction = { right: 'right', left: 'left', down: 'bottom', up: 'top' }[soft.dir];
    return `linear-gradient(to ${direction},${soft.stops.map(([o, a]) => `rgba(0,0,0,${+a.toFixed(4)}) ${+(o * 100).toFixed(3)}%`).join(',')})`;
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
        let previous = begin;
        for (const child of children) {
          // A group after the previous effect starts when the groups before it end, as PowerPoint plays it: the
          // delay it keeps is what the editor reckoned, which text built letter by letter outlasts (measured).
          const after = child.t === 'par' && (child.children || []).some(c => c.nodeType === 'afterEffect');
          const childEnd = after
            ? schedule(Object.assign({}, child, { begin: [] }), Math.max(begin + delayOf(child), previous), out, inner)
            : schedule(child, begin, out, inner);
          previous = Math.max(previous, childEnd);
          end = Math.max(end, childEnd);
        }
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
    // Behaviours without a fill of their own take their effect's (Bounce holds its squash, which has none; the
    // Transparency emphasis, without fill, reverts; measured).
    if (node.presetClass !== undefined) result.effectFill = node.fill;
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
      if (node.iterate.type !== 'lt' && node.iterate.type !== 'wd') return null;
      const target = firstTarget(node);
      const shape = target && !target.graphic ? slide.querySelector(`[data-id="${target.spid}"]`) : null;
      const units = shape ? textUnits(shape, target, node.iterate.type).length : 0;
      if (!units) return null;
      const build = (timing.builds || []).find(b => b.t === 'bldP' && String(b.spid) === String(target.spid));
      return { units, background: !target.pRg && !!(build && build.animBg) && !!shape.querySelector('g[data-bg]') };
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
      }
      if (sequence.nodeType === 'interactiveSeq') {
        // Triggers: a click on a shape, or playback reaching a media bookmark (key bmk:spid:name).
        const trigger = (sequence.begin || []).find(c => (c.evt === 'onClick' || c.evt === 'onMediaBookmark') && c.target);
        if (trigger) result.triggers.set(trigger.target.bmk !== undefined ? `bmk:${trigger.target.spid}:${trigger.target.bmk}` : String(trigger.target.spid), { steps, played: 0 });
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
      const fill = node.fill !== undefined ? node.fill : b.context.effectFill;
      if ((fill === undefined || fill === 'remove') && (node.t !== 'set' || typeof node.dur === 'number')) return null;
      return ease(node.autoRev ? 0 : 1, b.context.ease);
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
    const vars = { x: base.x, y: base.y, w: base.w, h: base.h, ppt_x: state.x, ppt_y: state.y, ppt_w: state.w, ppt_h: state.h, $: p };
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
        if (node.to) {
          // A scale to a size replaces the scale so far, from it unless the behaviour names its start (SMIL to and
          // from-to animation; Bounce squashes to 60 % and then back to 100 % from there, measured).
          const from = node.from ? [node.from[0] / 100000, node.from[1] / 100000] : [state.sx, state.sy];
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
        const to = node.to ? hexColor(node.to) : from;
        state[key] = mixColor(from, to, p);
        break;
      }
    }
  }

  function currentColor(target, key) {
    const selector = key === 'color' ? '[data-tx] g[fill], [data-tx] text' : (target.svg ? 'path' : '[data-bg] path, svg > path');
    const el = target.all(selector)[0];
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
      while (i < tav.length - 2 && p > times[i + 1]) i++;
      const a = tav[i], b = tav[Math.min(i + 1, tav.length - 1)];
      const span = times[Math.min(i + 1, tav.length - 1)] - times[i];
      // A bounce overshoots the last key frame: the last segment goes on beyond it.
      const local = span > 0 ? Math.max(0, i + 1 === tav.length - 1 ? (p - times[i]) / span : Math.min(1, (p - times[i]) / span)) : 1;
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
    // PowerPoint softens the moving edge of wipes and splits: a linear ramp 0.14 of the distance the edge travels,
    // which runs from just before the shape at the start to just past it at the end (measured with its video).
    const SOFT = 0.14;
    const [opaque, clear] = [(1 + SOFT) * s - SOFT, (1 + SOFT) * s];
    const softly = (dir, stops) => ({ soft: { dir, stops } });
    switch (name) {
      case 'wipe':
        // wipe(left) starts at the left edge ("From Left"), wipe(up) at the top.
        return softly({ left: 'right', right: 'left', down: 'up' }[option] || 'down', [[opaque, 1], [clear, 0]]);
      case 'slide':
        return rect(0, 0, 1, 1);
      case 'barn': {
        // Two edges, each travelling half the shape: from the sides to the middle (in) or from the middle out.
        const dir = option.includes('Vertical') ? 'right' : 'down';
        const ramp = u => Math.min(1, Math.max(0, (clear - u) / SOFT));   // u: 0 where an edge starts, 1 at its end
        if (option.startsWith('out')) {
          if (opaque < 0) return softly(dir, [[0.5 - clear / 2, 0], [0.5, ramp(0)], [0.5 + clear / 2, 0]]);
          return softly(dir, [[0.5 - clear / 2, 0], [0.5 - opaque / 2, 1], [0.5 + opaque / 2, 1], [0.5 + clear / 2, 0]]);
        }
        if (clear > 1) return softly(dir, [[opaque / 2, 1], [0.5, ramp(1)], [1 - opaque / 2, 1]]);
        return softly(dir, [[opaque / 2, 1], [clear / 2, 0], [1 - clear / 2, 0], [1 - opaque / 2, 1]]);
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
    el.pause();
    el.classList.remove('playing');
    if (el.fsOptions && show) {
      if (el.fsOptions.hideStopped) showMediaShape(el, false);
      if (el.fsOptions.fullScreen) fullScreenMedia(el, false);
      if (show.acrossSlides) show.acrossSlides.delete(el);
    }
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
    if (!owner || el.tagName !== 'VIDEO') return;
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
        for (const b of step.behaviors) {
          // Repeats until the end of the slide (or until a click) go on after their step.
          const looping = done && b.loop && step.startedAt !== undefined && !(b.loop.untilNext && step.clicked);
          const p = progress(b, looping ? now - step.startedAt : time);
          if (p === null) continue;
          for (const { key, target } of b.targets) evaluate(b, p, target, states.get(key));
        }
      };
      for (const step of this.done) run(step, Infinity, true);
      if (current) run(current, t, false);
      for (const [key, target] of this.timeline.targets) if (target) target.render(states.get(key));
    }

    reset() {
      this.stop();
      for (const step of this.done) { delete step.startedAt; delete step.clicked; }
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

  window.FreeSlate = { start: i => start(i === undefined ? order()[0] : i), stop, next, previous, presenter: openPresenter, comments: toggleComments, frame };
})();
