// FreeSlate 3D models (catalog: picture.model3d, animation.pictures): a glTF 2.0 reader and WebGL2 renderer for
// PowerPoint's 3D models (am3d:model3d), with their embedded animations (a3danim:embedAnim). The camera, the
// model's transform, the viewport and the lights come from the slide; materials, skins, morph targets and
// animations from the model.
(function () {
  'use strict';

  const EMU_PER_METER = 36000000;

  // ------------------------------------------------------------------------------------------------ math

  const mat4 = {
    identity() { return new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]); },
    multiply(a, b) {
      const out = new Float32Array(16);
      for (let c = 0; c < 4; c++)
        for (let r = 0; r < 4; r++)
          out[c * 4 + r] = a[r] * b[c * 4] + a[4 + r] * b[c * 4 + 1] + a[8 + r] * b[c * 4 + 2] + a[12 + r] * b[c * 4 + 3];
      return out;
    },
    fromTRS(t, q, s) {
      const [x, y, z, w] = q, [sx, sy, sz] = s;
      const xx = x * x, yy = y * y, zz = z * z, xy = x * y, xz = x * z, yz = y * z, wx = w * x, wy = w * y, wz = w * z;
      return new Float32Array([
        (1 - 2 * (yy + zz)) * sx, 2 * (xy + wz) * sx, 2 * (xz - wy) * sx, 0,
        2 * (xy - wz) * sy, (1 - 2 * (xx + zz)) * sy, 2 * (yz + wx) * sy, 0,
        2 * (xz + wy) * sz, 2 * (yz - wx) * sz, (1 - 2 * (xx + yy)) * sz, 0,
        t[0], t[1], t[2], 1]);
    },
    translation(x, y, z) { const m = mat4.identity(); m[12] = x; m[13] = y; m[14] = z; return m; },
    scaling(x, y, z) { const m = mat4.identity(); m[0] = x; m[5] = y; m[10] = z; return m; },
    rotationX(a) { const c = Math.cos(a), s = Math.sin(a); return new Float32Array([1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1]); },
    rotationY(a) { const c = Math.cos(a), s = Math.sin(a); return new Float32Array([c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1]); },
    rotationZ(a) { const c = Math.cos(a), s = Math.sin(a); return new Float32Array([c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]); },
    invert(m) {
      const a = m, out = new Float32Array(16);
      const b00 = a[0] * a[5] - a[1] * a[4], b01 = a[0] * a[6] - a[2] * a[4], b02 = a[0] * a[7] - a[3] * a[4];
      const b03 = a[1] * a[6] - a[2] * a[5], b04 = a[1] * a[7] - a[3] * a[5], b05 = a[2] * a[7] - a[3] * a[6];
      const b06 = a[8] * a[13] - a[9] * a[12], b07 = a[8] * a[14] - a[10] * a[12], b08 = a[8] * a[15] - a[11] * a[12];
      const b09 = a[9] * a[14] - a[10] * a[13], b10 = a[9] * a[15] - a[11] * a[13], b11 = a[10] * a[15] - a[11] * a[14];
      let det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
      if (!det) return mat4.identity();
      det = 1 / det;
      out[0] = (a[5] * b11 - a[6] * b10 + a[7] * b09) * det; out[1] = (a[2] * b10 - a[1] * b11 - a[3] * b09) * det;
      out[2] = (a[13] * b05 - a[14] * b04 + a[15] * b03) * det; out[3] = (a[10] * b04 - a[9] * b05 - a[11] * b03) * det;
      out[4] = (a[6] * b08 - a[4] * b11 - a[7] * b07) * det; out[5] = (a[0] * b11 - a[2] * b08 + a[3] * b07) * det;
      out[6] = (a[14] * b02 - a[12] * b05 - a[15] * b01) * det; out[7] = (a[8] * b05 - a[10] * b02 + a[11] * b01) * det;
      out[8] = (a[4] * b10 - a[5] * b08 + a[7] * b06) * det; out[9] = (a[1] * b08 - a[0] * b10 - a[3] * b06) * det;
      out[10] = (a[12] * b04 - a[13] * b02 + a[15] * b00) * det; out[11] = (a[9] * b02 - a[8] * b04 - a[11] * b00) * det;
      out[12] = (a[5] * b07 - a[4] * b09 - a[6] * b06) * det; out[13] = (a[0] * b09 - a[1] * b07 + a[2] * b06) * det;
      out[14] = (a[13] * b01 - a[12] * b03 - a[14] * b00) * det; out[15] = (a[8] * b03 - a[9] * b01 + a[10] * b00) * det;
      return out;
    },
    lookAt(eye, target, up) {
      const z = norm([eye[0] - target[0], eye[1] - target[1], eye[2] - target[2]]);
      const x = norm(cross(up, z)), y = cross(z, x);
      return new Float32Array([x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0,
        -dot(x, eye), -dot(y, eye), -dot(z, eye), 1]);
    },
    transformPoint(m, p) {
      const w = m[3] * p[0] + m[7] * p[1] + m[11] * p[2] + m[15];
      return [(m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12]) / w, (m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13]) / w,
        (m[2] * p[0] + m[6] * p[1] + m[10] * p[2] + m[14]) / w];
    },
  };
  const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
  const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
  const norm = v => { const l = Math.hypot(v[0], v[1], v[2]) || 1; return [v[0] / l, v[1] / l, v[2] / l]; };

  function slerp(a, b, t) {
    let [ax, ay, az, aw] = a, [bx, by, bz, bw] = b;
    let cos = ax * bx + ay * by + az * bz + aw * bw;
    if (cos < 0) { cos = -cos; bx = -bx; by = -by; bz = -bz; bw = -bw; }
    let k0 = 1 - t, k1 = t;
    if (cos < 0.9995) {
      const angle = Math.acos(cos), sin = Math.sin(angle);
      k0 = Math.sin((1 - t) * angle) / sin; k1 = Math.sin(t * angle) / sin;
    }
    const q = [ax * k0 + bx * k1, ay * k0 + by * k1, az * k0 + bz * k1, aw * k0 + bw * k1];
    const l = Math.hypot(q[0], q[1], q[2], q[3]) || 1;
    return [q[0] / l, q[1] / l, q[2] / l, q[3] / l];
  }

  // ------------------------------------------------------------------------------------------------ glTF reading

  const COMPONENTS = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT2: 4, MAT3: 9, MAT4: 16 };
  const TYPED = { 5120: Int8Array, 5121: Uint8Array, 5122: Int16Array, 5123: Uint16Array, 5125: Uint32Array, 5126: Float32Array };
  const NORMALIZE = { 5120: v => Math.max(v / 127, -1), 5121: v => v / 255, 5122: v => Math.max(v / 32767, -1), 5123: v => v / 65535 };

  /** The JSON and binary chunk of a GLB file (or a glTF JSON file with embedded buffers). */
  function readGltf(bytes) {
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    if (view.getUint32(0, true) !== 0x46546C67)
      return { json: JSON.parse(new TextDecoder().decode(bytes)), bin: null };
    let offset = 12, json = null, bin = null;
    while (offset + 8 <= bytes.byteLength) {
      const length = view.getUint32(offset, true), type = view.getUint32(offset + 4, true);
      const chunk = bytes.subarray(offset + 8, offset + 8 + length);
      if (type === 0x4E4F534A) json = JSON.parse(new TextDecoder().decode(chunk));
      else if (type === 0x004E4942) bin = chunk;
      offset += 8 + length;
    }
    return { json, bin };
  }

  function dataUri(uri) {
    const comma = uri.indexOf(',');
    const raw = atob(uri.slice(comma + 1));
    const out = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
    return out;
  }

  class Gltf {
    constructor(bytes) {
      const { json, bin } = readGltf(bytes);
      this.json = json;
      this.buffers = (json.buffers || []).map((b, i) => b.uri ? (b.uri.startsWith('data:') ? dataUri(b.uri) : new Uint8Array(0)) : (i === 0 && bin ? bin : new Uint8Array(0)));
      this.cache = new Map();
    }

    bufferView(index) {
      const v = this.json.bufferViews[index];
      const buffer = this.buffers[v.buffer];
      return { bytes: buffer.subarray(v.byteOffset || 0, (v.byteOffset || 0) + v.byteLength), stride: v.byteStride || 0 };
    }

    /** An accessor's values as a flat array: floats (normalized integers converted) unless raw integers are asked for. */
    accessor(index, raw = false) {
      const key = index + (raw ? 'r' : 'f');
      if (this.cache.has(key)) return this.cache.get(key);
      const a = this.json.accessors[index];
      const n = COMPONENTS[a.type], count = a.count, Typed = TYPED[a.componentType];
      const out = raw ? new (a.componentType === 5125 ? Uint32Array : a.componentType === 5126 ? Float32Array : Uint32Array)(count * n) : new Float32Array(count * n);
      if (a.bufferView !== undefined) {
        const { bytes, stride } = this.bufferView(a.bufferView);
        const size = Typed.BYTES_PER_ELEMENT, elementSize = size * n;
        const step = stride || elementSize;
        const view = new DataView(bytes.buffer, bytes.byteOffset + (a.byteOffset || 0), bytes.byteLength - (a.byteOffset || 0));
        const get = { 5120: 'getInt8', 5121: 'getUint8', 5122: 'getInt16', 5123: 'getUint16', 5125: 'getUint32', 5126: 'getFloat32' }[a.componentType];
        const normalize = !raw && a.normalized ? NORMALIZE[a.componentType] : null;
        for (let i = 0; i < count; i++)
          for (let c = 0; c < n; c++) {
            const v = view[get](i * step + c * size, true);
            out[i * n + c] = normalize ? normalize(v) : v;
          }
      }
      if (a.sparse) {
        const s = a.sparse;
        const indexView = this.bufferView(s.indices.bufferView), valueView = this.bufferView(s.values.bufferView);
        const IndexTyped = TYPED[s.indices.componentType];
        const indices = new IndexTyped(indexView.bytes.slice((s.indices.byteOffset || 0), (s.indices.byteOffset || 0) + s.count * IndexTyped.BYTES_PER_ELEMENT).buffer);
        const values = new Typed(valueView.bytes.slice((s.values.byteOffset || 0), (s.values.byteOffset || 0) + s.count * n * Typed.BYTES_PER_ELEMENT).buffer);
        const normalize = !raw && a.normalized ? NORMALIZE[a.componentType] : null;
        for (let i = 0; i < s.count; i++)
          for (let c = 0; c < n; c++) out[indices[i] * n + c] = normalize ? normalize(values[i * n + c]) : values[i * n + c];
      }
      this.cache.set(key, out);
      return out;
    }

    /** The scene's images as bitmaps (embedded in a buffer view or a data URI). */
    async images() {
      return Promise.all((this.json.images || []).map(async image => {
        try {
          let bytes, type = image.mimeType || 'image/png';
          if (image.bufferView !== undefined) bytes = this.bufferView(image.bufferView).bytes;
          else if (image.uri && image.uri.startsWith('data:')) { bytes = dataUri(image.uri); type = image.uri.slice(5, image.uri.indexOf(';')); }
          else return null;
          // An image element loads from its blob like any resource, which a page's load (and a headless browser's
          // screenshot) waits for.
          const url = URL.createObjectURL(new Blob([bytes], { type }));
          try {
            const element = new Image();
            await new Promise((resolve, reject) => { element.onload = resolve; element.onerror = reject; element.src = url; });
            return element;
          } finally { URL.revokeObjectURL(url); }
        } catch (e) { return null; }
      }));
    }
  }

  // ------------------------------------------------------------------------------------------------ animation

  /** The value of an animation sampler at a time (seconds): step, linear (quaternions spherically) or cubic spline. */
  function sample(gltf, sampler, path, time) {
    const input = gltf.accessor(sampler.input), output = gltf.accessor(sampler.output);
    const interpolation = sampler.interpolation || 'LINEAR';
    const n = output.length / input.length / (interpolation === 'CUBICSPLINE' ? 3 : 1);
    const value = (i, part) => {
      const base = interpolation === 'CUBICSPLINE' ? (i * 3 + part) * n : i * n;
      return Array.from(output.subarray(base, base + n));
    };
    if (time <= input[0]) return value(0, 1);
    const last = input.length - 1;
    if (time >= input[last]) return value(last, 1);
    let i = 0;
    while (i < last - 1 && input[i + 1] <= time) i++;
    const t0 = input[i], t1 = input[i + 1], span = t1 - t0, t = span > 0 ? (time - t0) / span : 0;
    if (interpolation === 'STEP') return value(i, 1);
    if (interpolation === 'CUBICSPLINE') {
      const p0 = value(i, 1), m0 = value(i, 2), p1 = value(i + 1, 1), m1 = value(i + 1, 0);
      const t2 = t * t, t3 = t2 * t;
      const out = p0.map((v, c) => (2 * t3 - 3 * t2 + 1) * v + (t3 - 2 * t2 + t) * span * m0[c] + (-2 * t3 + 3 * t2) * p1[c] + (t3 - t2) * span * m1[c]);
      if (path === 'rotation') { const l = Math.hypot(...out) || 1; return out.map(v => v / l); }
      return out;
    }
    const a = value(i, 1), b = value(i + 1, 1);
    if (path === 'rotation') return slerp(a, b, t);
    return a.map((v, c) => v + (b[c] - v) * t);
  }

  /** The length of an animation in seconds: its latest key time. */
  function animationLength(gltf, animation) {
    let length = 0;
    for (const s of animation.samplers) {
      const a = gltf.json.accessors[s.input];
      if (a.max) length = Math.max(length, a.max[0]);
      else { const input = gltf.accessor(s.input); length = Math.max(length, input[input.length - 1]); }
    }
    return length;
  }

  // ------------------------------------------------------------------------------------------------ lighting

  // PowerPoint lights every model in its own studio: an environment (a room with windows), the slide's lights and its
  // ambient light; it maps the result with its own tone curve. Radiance is in the units in which an unlit material
  // shows its base color. The environment was reconstructed from PowerPoint's renders of mirror spheres (equirect-
  // angular, longitude atan2(x, z), latitude from +y; RGBE) and the constants fitted to its renders of calibration
  // spheres (see the catalog: picture.model3d).
  const ENVIRONMENT = {
    width: 64, height: 32,
    rgbe: 'xrevgcS2roHCtK2Bv7KqgcCyrIG+samBvbCogbyvqYG8r6iBvK+pgbuuqYG8sKmBu6+pgbyvqYG/sqyBu6+pgb+zrIHBtK2BwrSvgcS2sIHFt7CByLmygci6s4HLvLWBzL22gc7AuIHQwbmB0sO7gdXFvYHXx76B2snAgdvLwYHezcOB4M/FgeHQxoHk08iB5NLGgejUzIHn1cuB6tfNgerYzYHt2s+B69jNge7bz4Hr2c6B7NrOgevYzoHr2c6B79zQgefVyoHp1suB5tTJgePRxoHi0MaB387Egd3MwoHaycCB2Me9gdXFvIHSw7mB0MC3gc6+tYHLvLOByLmxgbWooYGwpJ6BraGbgamdmIGnnJeBpJqVgaKYlIGhl5SBoJeTgaCXlYGhmZWBopmXgaSbmoGmnpuBqJ+dgauioYGtpaKBsKakgbOpp4G2rKmBua6rgbyxrYG/s6+BwraxgcW4s4HIurWBzL64gdHCu4HWx7+B28vDgeDPxoHl08qB6tfNge7b0IHy39SB9+PXgfvn2oH/696BgndxgoN5coKFenOCh3x0goh9doKKfneCi394gop/d4KLf3eCin53gol9doKIfXWChXpzgoN4coKBd3CC/Ofagfbi1oHw3NGB6dbLgeLQxYHbyr+B1MS6gc29tIHHuK+BwLKqgbqtpYGhlY+BmY+KgZOKhYGOhoKBioN/gYeAfoGHgH+Bh4GBgYmEhIGMhoeBj4uLgZKNj4GXkpSBmZWYgZ6anIGinaCBpaCjgaijpoGsp6iBsausgbWtroG4sLCBurGwgbuxsIG6sK6Bua+rgbuwrIHFuLKBzb+4gdTEvIHezMOB5tTJge3az4H14dWB/undgYR5coKHfHWCin94go6CeoKQg3yCk4Z+gpiKgoKej4aCqpmNgs2zn4LetIuD1rqbguC5koKGcV+DuKSUgqOTiYKZjIOCk4Z+go2BeYKHfHWCgndwgvjj1oHs2MyB4M7CgdTDuIHIuK+Bva6lgbKlnYGpnZaBioF7gYB4dIHy5d2A5tvWgOHX1IDh2diA5N3egOrl6IDz7vOAf32AgYWDiIGLipCBkpGXgZmYn4GfnqWBpKOrgaalrYGopq6BrKuygbOwtoG2sbaBtrG1gbWvsoGwqauBrKSlgaigoIGooJ6BtKqngbquqYG7raaBwrOqgdLBt4Hiz8SB79vPgYB2cIKJfneCjoN7gpKHf4KViYGClYiAgpmMg4Kik4qCrp6SgsSvnoKFcmKD6byPgtzPv4LmyqiCo4lxg8+4poK2pZmCq5yRgqKUi4KajYSCkoZ+gop+d4KCd3CC89/SgeLQw4HRwLWBwLKngbCjmoGilo6BlYqEgeXWzYDRxb2AwrizgLmyr4C5s7OAvrq8gMfEyYDU0tmA4+LtgHt7goGFho6Bj5CagZqbpoGkpbGBrK67gbO1woG1tsSBtrjFgbq6xoG/v8mBvbzFgbm2voG0sLaBr6uwgaynqoGfmpyBlZCQgZuVlIGgmJWBmY+JgZSJgYGxopmByritgdvJvYH14dWBioB6gpKHgIKXjISCmI2FgpSIf4KYi4KCpJaMgrGil4K6qp+CwrGmgsi3q4LLuq6CzLqvgsm4rYLFtKmCv6+kgreonYKuoJaCpZeOgpuOhoKRhX2Ch3x0gvnl1oHj0cOBzLywgbaonoGilo2Bj4V+gf/t4YC3rKWApJyWgJeRjoCVkJCAmpeZgKSjp4C0tLuAyMnUgODi8IB9f4iBjI6ZgZqdqYGprLqBtrrJgcLG14HKzuCBztLkgc/T5YHP0uSB0NLigcnK2IG/v8qBsrG7gamnr4GfnKKBi4mNgfv3+4CBfn+BhYB/ge3f2IDLu7CAjIB4gamakIG6qp6B2cq/gYiAe4KUi4aCmY+IgpeMhYKOg3uClomAgqiakIK8rKGCybitgtbEuILezL+C49DDguPQw4LfzMCC2Ma6gs++soLEtKmCuamfgq2flYKhlIuClYmAgol+doL45NWB3cy/gcO0qIGpnJKBkYZ+gffm2IDTxbqAkYqEgIF7eIDx6ed/9vLzf4WEh4CVlZuAqqy1gMXI1YDk6PmAg4WQgZWYpYGnqrqBub3OgcrP4oHZ3vOB5On/gXV4g4J2eYSCdHeCguXp/IHY2+2Bx8nYgbO0wIGkpK+BlZScgYB/hYHi4OaA5ePngOfj5IC/t7OAh311gOLRw4CPg3mBmYyCgbWpoIGFgHyClo+KgpqSjYKUi4SCiX53gpaKgYKwoZeCy7qugt/MwILw3M6C+uXXgv7p24L859mC9ODSgunWyYLbyb2CzLuwgr6uo4KvoZeCoZSLgpOIf4KGe3OC8N3OgdPCtoG3qJ2Bmo+Ggf/t34DRw7iAraGZgPDk3H/UzMh/y8bGf9fU2H/x8Ph/jI2UgKmrtoDMzt+AdXeBgYeKloGbn62BsLTFgcTJ3YHZ3vSBdXiEgnx/jIKBhJGCgoWSgoCDj4J7fomC5uv/gdDU5YG3usiBpaa1gZabq4F8gIyBz9DZgNDQ1YDT0tSArKilgNXHun/Ux7uAkIV8gZCFfIGdlY2BhYF+gpaSj4KYlI+CkomEgoh8dYKbjoWCvKyigt3LvoL449WChnpzg4uAeIOMgXmDiX52g4N4cYP14dOC48/Dgs++soK9raKCrJ6TgpyPhoKNgXmC/ejZgeDOwYHDtKiBp5qQgYuAeIHg0MSAs6efgJGIgoDOxcB/uLKvf7WxsX/DwcR/4eDof46KloCypLyAy8zegOfr/4CHipeBnaGvgbO4yYHKz+OB4Ob8gXp9ioKDhpOCiIyZgoqOm4KHi5iCgYSQgnh6hYLX2+6Bur3Ngau1zoGEnsKBbYCXgdXi/4DDxMqAxsbIgKajoYDRxbp/5NrRgLSspYGupZ2BnZaPgYiFg4KWkpCCmJGOgpGJg4KKfneCo5aMgsq5roLx3M+CiH11g5OGfoOYi4ODl4uCg5KGfoOJfXaD++bYguPQw4LMu6+CtqedgqOWjIKShn2CgndvgufUxYHKuq6Br6GXgZSIgIHz4tWAw7atgJuRi4D26uJ/vLazf6mlo3+npKN/tbO1f97b5X+llLWBn6CsgLm7yoDe4vWAhIaSgZmdrIGxtcaByM3hgeDl/IF7fouChIiVgoqOnIKNkJ6Cio2bgoOGkoJ5fIeC2Nzvgbe6yYGZna2BT2eUg01roYG/yuKAt7i9gL29v4CnpaSA2tHHf/3274Do4t2B39jSgaymn4GKiIeClpKPgpaRjYKQiIKCjIB5gqqbkoLVxLiC/+rcgpGFfYOcj4aDoZOKg5+SiYOXi4KDjIB4g/zn2ILfzMCCw7OogqqckoKViICCgnhwgubTxIHKuq2Br6GWgZaKgoH96t2A0MO5gKifmICHgX2A39bRf9HNy3+5trR/qKalf6upqn/Fw8h/5+fvf4yNlYCrrbqAztLkgHt+iYGRlKKBp6u7gb/E1oHa3/SBeHuHgoKFkoKIi5mCio2bgoeKl4J/go6Cd3qEgtPX6YGxs8GBiouWgd7h84Csr7yAi4uRgKqprIC1tLaAoZ+egODYzn+EgX+BiYeFgoaEgoLCvLeBjIqJgpWSkIKTjouCjoaAgoyBeYKtnpSC28m8goR5cYOWioGDoZSLg6WXjoOilIuDmIyDg4t/d4P24tSC1sS4gripnoKdkIeChnpzguLOwYHAr6SBqZuQgZKGfYH55tiA0cO5gK+moICim5eAko2LgPXw7H+Af3+A4uDff87My3+3tbV/srGyf87N0X99fYOAmZukgLu9zIDg4/eAhIaSgZyfrYG6vc2B0tbogXN2gIJ8f4qCgoWRgoOGkoJ/go6Cd3qFguPo+4HKzd6Bqaq3gX+AiYHIytWAjIyTgLezuX+dnJ2Ar6+vgKKgn4Dn3th/+/j1gJSTk4KTkpGCzcjEgY2KioKUkY+CkYyJgoqDfYKJfneCqpySgtnHu4KDeHCDloqBg6GTioOklo2DoJKJg5WJgIOGe3OD69fJgsm4rIKqnJKCj4J6gu3ZyoHDsqeBoJKJgYR5cYHm08WAxbasgKmfmYCzrKiAu7e0gKqnpoCVk5KAqqmpgJubm4CKiYmA+vj4f9TS0X/BwMF/3dzif4aGjYCkpa+AyszbgIGCi4GfoKuBtbfEgcrL24Hb3vCBdXeAgnl7hYJ5e4WC5+v/gdjb7oHU2OmBv8LQgZ+hq4Hl5vOAtbe/gIB/g4Ckn6F/mZiYgK+vr4CkoqGA8Orgf+jl44CamJiCmZeXgtDMyIGNioqCkpCOgo6JhoKFfnmCg3hxgqKUi4LPvrKC+eXXgpCEfIOajYWDnZCHg5iMg4ONgXmD++bYgtnHuoK4qJ2Cmo2Dgv7o2IHPvbCBp5iNgYR5cIHQvrKAppmQgJOKgoCel5OAxcC9gMfFw4DGxcSAu7q5gMC/vYC/v76Au7u6gKyrq4CenJyAk5GRgJKQkICloqSAxcLHgO/r9ICMipCBnp2lgbGwuoHCwc6B0NDegdra6oHd3++B2NrrgcvN3YG9v86BxMfVgbO2wYGXmKGBy8rUgKSlqoDr6e5/lY+Qf5eWlYCwr6+AqaelgP317n/d2tmAmpqagpuZmYLKxsKBjIqJgpGPjoKLh4SC/vHngfPf0YGUiH+Cva6jguTSxYKFenKDjoJ6g5CEfIOMgHiDgXZvg+LPwoLBsaWCo5WLgod8c4LcyruBsaKWgY2Ad4Hbx7iAppeNgPvn2X/KvrV/j4qHgMnGxYDZ19aA29nXgNfU0YDNy8qA0dDQgOLh4YDa2dmAxsXFgLu5uIC8ubeAycPCgN3W14D68vWAjYmLgZ2ZnYGsqa+BubW+gcK/yYHHxdCBxsXRgb6+yYGurrmBo6Stgbi7xYGsrreBkpOagbSyuICVlpmA29nbf4qGhH+Zl5aAtrW1gK+urICFgn6A2dbVgJmXmIKZl5eCwr25gYuKiIKQjo2CiIWCgvHl24HZyLuBg3hwgqaZj4LIuK2C69jKgvvm14L+6dqC9eHTguPQw4LJuKyCp5mOgot/doLl0sKBu6udgZaJfoHr18SAtKSWgIZ7cIDGt6p/mpCJf6Cbl3+IhoWAyMbEgNLPzoDX1NGAysfGgNzb24Dn5uaA6+vrgNbV1YDIxsWAx8PBgNDIxYDh19SA+e7sgIqEhIGYkpOBpJ6hgaynq4GxrLKBs6+1gbCss4Glo6mBk5KYgY6NkoG0tLyBrK20gZOTmIGhnqKAi4uLgNPQz3+Ffnp/mpiXgLq6uYC0s7KAi4eEgMXBv4CWj4qBhX97gbKon4DPx8CA5NrUgPbq4ICHfneBopSLgdPBtYGKfneCp5qRgsi4rILVw7eC1sW4gs++soK/sKSCrJ6TgpWJgILu28qBva2fgZqNgIH4482Aw7OggJiLfID24cp/8dzJf4F2boD+6dl/+urdf6KdmYC6trOA19TRgKmjo4DQz9GAvLy/gM/R1YC9vsCAtLOygLWwroDAuLSA08fCgOrc14CBeniBjYWEgZaOjoGclJWBn5iZgZ+ZnIGclpmBko2Pgf/3+4CCfX6BuLi+gbW1uoGcm56Bl5KSgIWFg4DQzMh/hHt3f56dm4DBwL+Au7m4gJCNioC8u7iAwbKigJiLfYDhy7d/1MrLf4qOn4CVl6WArqeogPPf04CmmI6B28q9gYZ8dIKlmI6CrqCVgq+hloKom5CCnZCGgo6CeoL96dmB4M7BgbusoIGRhXqB4c66gLeoloCkloeAr6GVgK6gloCglIuAlYqDgJKIgYCPhn+Ai4J9gKiin4Cpo6OA5OLjgKqoqoCZm6GAjZKegJCWo4CVlZyAnZaSgLOnoYDLvbaA49PMgPfm4YCDe3mBiYB/gYyEg4GOhoaBjYWFgYZ+foHp3dqA/fHpgMfHyoHHx8qBq6qsgZONioCEg3+A0s7Hf4J6dH/99/F/nJmXgKGem4CNiYWAr6umgMq7qoCrnY2Ah3tvgJGWqYCRmK2AkpeogJmbqYClpK2Az8XFgKKVjYHUw7eBhnxzgoyBeIKMgXiCh3xzgvzo2IHo1seB1MO3gcOzqIG0pZuBp5iQgZmLhIGJfXaB8dvOgM68sICzpZyAopePgJWLhICMg32AjIR+gId/eoCVj4yAqaOjgI+OjYG2tbWBi4uLgaGhpYB8f4iAgoeVgI6SoYCUjo+ApZiRgL2up4DRwbqA4c/KgO3b1oD35OCA/+3qgIN6eIGBeHWB5dXOgIJ7dYHf3+CB4+PlgcPCwoGYkYuAiYeCgNrVzX+CenN/6dTCfqKXjX/Vy8B/gXx2gJWPiIDVxbGAxLWigKebkICZn7KAkpengI+SoYCRkp2AlpWdgJ6an4C9tLOAkIZ/gdnJuYHgz7+B3829gdfGt4HKuqyBvq+igbannIGsnpWBpZePgZ6RioGYi4SBkYR9gYZ6c4Hz3dGA1cO4gK+jm4CelI2Aj4eBgIV+eYCBenaAj4mHgK+rqYC/v7+B09PTgc3MzIG+vbuBg4GAgZOTmYB/gY2AjJCggJSSmoCckIqAr5+YgL6up4DMu7SA2cfBgOjU0ID449+A/+rlgOvZ0ICKgnmB/Pz9gYODg4Li4OCBp52WgJORiYDm4NZ/iH93f+3YxH6soZZ/5NrOf4eCfICalIyA3865gNjHs4C4sKyAnKCvgJWXpICPkJuAjYyVgI6LkYCRjI+AmJGQgKedmYDHua+A2Ma6gPLdzoCPgniBn5GGgaGUiYGgk4qBn5KKgZuOh4GajIaBmIuFgZaIg4GRhH6Bin54gYJ3cYHi0MeArKKagJuSjICLhH+A//Xtf4J+e4Dh3duAxMLCgcjGxYHHxcOBxMK/gb+7t4Gsp6KB8uvngKGgp4CNkJ2Al5mmgJmSlICfkYqArJyVgLurpIDOvLaA5tLNgPnk34Dz3teAk4qAgYuKioKSkpKC//v7gb6zqICpp5yA//ntf8bK2X98h5+A6Oz8f4F8doCUj4iAqqOZgOXXw4Dbzb2Ap6izgJ2gq4CXl6GAkZCYgIyKj4CKhomAiYOEgIyEg4CXjYmAnpKLgKSXjoCsnpOAsaKXgMGxo4CKf3WBk4d9gZSIgYGTh4GBlIeCgZWIg4GWiISBloiDgZSHgYGQhH6Bin95gePTyoCqoJqAm5OOgI6IhICPioeA4dvYgLGurIG1sq+BtrOwgbezr4G2sq2Bs66ogamln4GYlpGB/fv2gMbGyYCrrbeApqeygJyZnoCgl5aAsqSdgM29toDp18+A8N3UgJaOhIGOjY2CmJiYgoaDhILYzMCAyse5gJmWj4CFkauAhpKtgI6ZsoCWnrGAnaKtgKmqsoDKxb6Aqau1gKGjroCdnaaAl5adgJGPlICMiYyAiIOEgIV/foCHgHyAlYuFgJ2RiYCll42AqZ2RgKyekYCqnpKA08S1gIh+doGJfniBiX55gYp/eoGMgHyBj4J+gZGEgIGThoKBlIaCgZKFgIGNgn2BzsG6gKmgm4CgmJSAp6GdgLGqpoDY0c2Ag4B9gZaSjoGgnJiBoZ2YgZ+cl4GZlpGBj42JgYeGgoGEg3+BhYSAgYiHg4GFhICBvcDIgJmdrICsp6uAz8K6gODRxYCSioKBg4ODgo6OjoL8+PiB6d/UgOHez4CvsLGAh5GpgIWOpoCPl6yAmJ+ygJyjs4CfpLOAoaSxgKChrICdnqaAmZmfgJaUmYCRjpGAjIiJgIiDgoCFfnyAh4B7gJeNhoCfk4qApZmOgKmdkICpnpGAqZ6RgK+jl4DczcGA8ODUgPTi2ID249uA+ubfgP/q5ICDeHWBhnt4gYl9eoGKfnuBiH56gdrLxICwpaCAraOegK6mooCknZmAp6CcgKqjn4Ctp6OAu7axgNLNyIDa1tCA1dHMgMnGw4DHxcOAz87MgOLg3YDz8eyA//33gP79+IC9v8eAmZ+vgJ2hsYCsrLWA9PDvgNnY14Hp6OiB0tDPgeHb1YDIyciAnKOygIePo4CGjaCAj5WngJacq4CZnayAmp2qgJqdp4CZmqOAl5eegJWUmICSj5OAj4uNgIuHh4CJg4GAhn98gIiAfICXjoaAnJKJgJ+Vi4Chl4yAopeMgKOYjICmm5GAq6CWgK+km4C5raSAwLOsgMa4sYDLvLaA0MG8gNfHwoDZycSA1cbBgMO2sYCwpaCArKGcgKifmoClnJiAlo+LgJaQjICYko6AmZSQgJyXk4CempaAoJyZgKGdmoChnpuAoqCegKSjo4CmpqiAp6isgLGytoC9v8KAw8XJgLa5wICdorCAmJ6tgL7AyYCmpaSBrKyrgZ2cm4GYn6+AlZurgI+WpoCGjJ2AhoybgIyRn4CRlaGAkpahgJKVn4CSlJyAkZKZgJCQlYCPjZCAjYqMgIuHiICJhYOAh4J/gIV/fICHgHyAkoqDgJSMhICVjYWAlo6FgJaOhYCXjoaAmZCIgJuSioCdk4yAnpWOgKGXkICimJGAopiSgKSZlIClm5aApZuWgKSaloCjmZWAoJeTgJ2UkICakY6Al5CMgIiCf4CHgn+Ah4J/gIiEgYCJhYKAioaEgIuIhYCNiYeAjYqIgI6LiYCQjo2AkZCQgJOSlICTk5eAk5SagJOVnYCUlp+Ak5eggJSYo4CZnamAsrW+gLa6w4CmqrWAjZKfgIuQnYCIjZmAhIiUgISIk4CIi5WAioyWgIqMlYCKi5KAi4uRgIqJjoCJh4uAiIaIgIeEhICFgoGAhIB+gIR+fICDfXqAhX56gIqDfoCLhH6Ai4R+gIyFfoCMhX+AjIV/gI2Gf4COhoCAjoeBgI+HgYCQiIKAkIiDgI+HgoCPiIOAkImEgJGJhYCRiYWAkIiFgI+HhICNhoOAi4SBgImDgID47+l/9+7of/fu6X/37+p/9/Drf/jx7X/68+5/+/Twf/z28n/+9/N//vj1f4B+fYCBf4CAgoCCgIOChYCDg4eAhIWKgISGjICFh46Ah4mRgIiKkoCIi5SAiIqTgIaJkoCFh5CAg4WOgIKEjICAgoqAgoOKgIOEioCDg4mAg4OHgISDh4CCgYSAgn+BgIF+f4CAfXyA//f0f//18H//9e5/gHt3gIF7eICCfXiAg314gIN9eICDfXiAg314gIN9eICDfXiAhH15gIR9eYCEfXmAhH55gIR9eYCCfHiAgXt3gIJ8eICCfHiAgnx4gIF7eICBe3iA//Tuf/3y7H/68et/6eDbf+jg23/n39t/59/bf+fg23/n4Nx/5+Hcf+jh3n/p499/6ePff+rj4H/q5OF/6+Xjf+zn53/u6ut/7+zvf/Hv83/z8vh/9fT7f/f3/398fIGAfX2CgH1+g4B9foOAfX6DgH19goB8fYGA9/f/f/f2/3/49/1/+Pb7f/j1+X/49Pd/9/Lzf/bw8H/27+1/9u7rf/Tr53/y6eR/8unif/Pp43/16+R/+O3mf/ju53/57ud/+e7nf/nu53/57ud/+e7mf/nu5n/57uZ/+e7mf/nu5n/37OV/9Onif/Po4X/y6OB/8efgf/Dm4H/v5uB/7uXff+3k3n/s491/6uHcf+DY1H/f2NN/3tfTf97X03/e1tN/3dbTf93X03/e19N/3tfUf97Y1H/e2NV/3tjVf97Y1X/f2dZ/4NrYf+Hc23/j3t5/5ODhf+bi5H/n5Od/6ebpf+rn63/r6Ox/6+ntf+zq7n/t6u1/7ertf+3q7H/t6ex/7ejqf+zo6X/t5+h/7Obmf+zl5H/s5eJ/7OTgf+zj33/s491/7OPdf+zj3X/t491/7eTef+7l3n/v5d9/7+bff+/m3n/v5t5/7+Xef+/l3n/u5d5/7uTef+7k3n/t491/7OPcf+vh2n/p4Np/6N/Zf+fe2H/m3dh/5d3Wf+Tc1n/j2tV/4trVf+HZ1H/b1NB/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PQf9vT0H/b1NF/3NXSf93W03/e19V/3tnXf+Da2H/h29l/4tvaf+Lc23/i3dx/493cf+Pd3H/j3dx/5N7cf+Te23/k3tp/5N3af+Xd2X/l3dh/5d3Yf+Xd2H/m3dh/5t3Yf+fe2H/n3tl/597Zf+fe2X/o3tl/6N/Zf+jf2X/o39l/6N/Zf+je2X/n3tl/597Yf+fe2H/m3dd/5t3Xf+Xc1n/k3NZ/5NvVf+Pa1H/i2tR/4dnTf+DY03/g19N/39fTf97X0n/c1dF/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b09B/29TQf9vU0X/c1dJ/3NbSf93X03/e19N/3tfTf9/X03/g19N/4NfTf+DX03/g19N/4NjTf+DY03/g2dN/4dnTf+HZ1H/i2dR/4trUf+La1H/i2tR/4trUf+Pa1H/j2tR/4trUf+La1H/i2tR/4tnUf+LZ03/i2dN/4dnTf+HY03/g2NN/4NfTf+DX03/g19N/4NfTf9/X0n/e19F/3NbQf9vUz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29PPf9vTz3/b089/29TQf9vW0X/b1tF/29fSf93X03/d19N/3tfTf9/X03/f19N/39fTf9/X03/f19N/39fTf9/X03/f19N/4NfTf9/X03/g19N/39fTf9/X03/f19N/39fTf+DX03/f19N/39fTf9/X03/e19J/3tfSf97X0n/d19F/3NfQf9vWz3/b1c9/29TPf9vTz3/b089/29PPf9vTz38=',
  };
  const LIGHTING = {
    lights: 0.9677,          // the slide's lights (candela over square meters) on diffuse surfaces
    environment: 0.1760,     // the environment's irradiance on diffuse surfaces
    ambient: 0.6133,         // the slide's ambient light (lux) on diffuse surfaces
    highlights: 0.9559,      // the lights' highlights on dielectrics (on metals: 1)
    gloss: [0.3312, 1.7967], // the environment's reflection on dielectrics: g0 + (1 - g0) roughness^p (on metals: 1)
    emissive: 0.5,
    tone: [2.35803, 0.6663, 1.88575],
  };
  /** The roughness of the environment's prefiltered levels. */
  const LEVELS = [0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.65, 0.8, 1];

  let studio = null;

  /** The environment's prefiltered levels (stacked), its irradiance (SH9) and the split-sum table; computed once. */
  function lighting() {
    if (studio) return studio;
    const { width: w, height: h } = ENVIRONMENT, n = w * h;
    const raw = atob(ENVIRONMENT.rgbe);
    const env = new Float32Array(n * 3), dirs = new Float32Array(n * 3), solid = new Float32Array(n);
    for (let j = 0, i = 0; j < h; j++) {
      const lat = (j + 0.5) / h * Math.PI;
      for (let c = 0; c < w; c++, i++) {
        const lon = (c + 0.5) / w * 2 * Math.PI - Math.PI;
        dirs[i * 3] = Math.sin(lat) * Math.sin(lon); dirs[i * 3 + 1] = Math.cos(lat); dirs[i * 3 + 2] = Math.sin(lat) * Math.cos(lon);
        solid[i] = 2 * Math.PI / w * Math.PI / h * Math.sin(lat);
        const f = Math.pow(2, raw.charCodeAt(i * 4 + 3) - 136);
        for (let k = 0; k < 3; k++) env[i * 3 + k] = raw.charCodeAt(i * 4 + k) * f;
      }
    }
    // GGX lobes (alpha = roughness squared) around each direction, seen head-on (n = v = r).
    const levels = new Float32Array(LEVELS.length * n * 4);
    LEVELS.forEach((roughness, level) => {
      const a2 = roughness ** 4;
      for (let i = 0; i < n; i++) {
        const o = (level * n + i) * 4;
        levels[o + 3] = 1;
        if (!roughness) { levels.set(env.subarray(i * 3, i * 3 + 3), o); continue; }
        const x = dirs[i * 3], y = dirs[i * 3 + 1], z = dirs[i * 3 + 2];
        let r = 0, g = 0, b = 0, sum = 0;
        for (let j = 0; j < n; j++) {
          const nl = x * dirs[j * 3] + y * dirs[j * 3 + 1] + z * dirs[j * 3 + 2];
          if (nl <= 0) continue;
          const d = (1 + nl) / 2 * (a2 - 1) + 1, weight = a2 / (d * d) * nl * solid[j];
          r += weight * env[j * 3]; g += weight * env[j * 3 + 1]; b += weight * env[j * 3 + 2]; sum += weight;
        }
        levels[o] = r / sum; levels[o + 1] = g / sum; levels[o + 2] = b / sum;
      }
    });
    // Irradiance: the radiance's SH9 coefficients times the cosine lobe's band factors.
    const band = [Math.PI, 2 * Math.PI / 3, 2 * Math.PI / 3, 2 * Math.PI / 3, Math.PI / 4, Math.PI / 4, Math.PI / 4, Math.PI / 4, Math.PI / 4];
    const sh = new Float32Array(27);
    for (let i = 0; i < n; i++) {
      const x = dirs[i * 3], y = dirs[i * 3 + 1], z = dirs[i * 3 + 2];
      const basis = [0.282095, 0.488603 * y, 0.488603 * z, 0.488603 * x, 1.092548 * x * y, 1.092548 * y * z, 0.315392 * (3 * z * z - 1), 1.092548 * x * z, 0.546274 * (x * x - y * y)];
      for (let k = 0; k < 9; k++) for (let c = 0; c < 3; c++) sh[k * 3 + c] += basis[k] * env[i * 3 + c] * solid[i] * band[k];
    }
    studio = { levels, sh, brdf: brdfTable(32, 512) };
    return studio;
  }

  /** The split-sum table (Karis): A and B over (n.v, roughness), GGX with Smith-Schlick k = alpha / 2. */
  function brdfTable(size, samples) {
    const radical = i => {
      let b = ((i << 16) | (i >>> 16)) >>> 0;
      b = (((b & 0x55555555) << 1) | ((b & 0xAAAAAAAA) >>> 1)) >>> 0;
      b = (((b & 0x33333333) << 2) | ((b & 0xCCCCCCCC) >>> 2)) >>> 0;
      b = (((b & 0x0F0F0F0F) << 4) | ((b & 0xF0F0F0F0) >>> 4)) >>> 0;
      b = (((b & 0x00FF00FF) << 8) | ((b & 0xFF00FF00) >>> 8)) >>> 0;
      return b / 4294967296;
    };
    const table = new Float32Array(size * size * 2);
    for (let j = 0; j < size; j++) {
      const a = ((j + 0.5) / size) ** 2, k = a / 2;
      for (let i = 0; i < size; i++) {
        const nv = (i + 0.5) / size, vx = Math.sqrt(1 - nv * nv);
        let A = 0, B = 0;
        for (let s = 0; s < samples; s++) {
          const phi = 2 * Math.PI * (s + 0.5) / samples, e = radical(s);
          const cos = Math.sqrt((1 - e) / (1 + (a * a - 1) * e)), sin = Math.sqrt(1 - cos * cos);
          const hx = sin * Math.cos(phi), hz = cos;
          const vh = vx * hx + nv * hz, nl = 2 * vh * hz - nv;
          if (nl <= 0) continue;
          const g = nl / (nl * (1 - k) + k) * (nv / (nv * (1 - k) + k)) * vh / (hz * nv);
          const fc = Math.pow(1 - Math.min(Math.max(vh, 0), 1), 5);
          A += (1 - fc) * g; B += fc * g;
        }
        table[(j * size + i) * 2] = A / samples; table[(j * size + i) * 2 + 1] = B / samples;
      }
    }
    return table;
  }

  // ------------------------------------------------------------------------------------------------ shaders

  const VERTEX = `#version 300 es
    precision highp float;
    in vec3 aPosition; in vec3 aNormal; in vec2 aUv; in vec2 aUv1; in vec4 aColor; in vec4 aJoints; in vec4 aWeights; in vec4 aTangent;
    uniform mat4 uModel, uView, uProjection;
    uniform bool uSkinned;
    uniform highp sampler2D uJoints;
    out vec3 vWorld; out vec3 vNormal; out vec2 vUv; out vec2 vUv1; out vec4 vColor; out vec4 vTangent;
    mat4 joint(float index) {
      int i = int(index) * 4;
      return mat4(texelFetch(uJoints, ivec2(i, 0), 0), texelFetch(uJoints, ivec2(i + 1, 0), 0),
                  texelFetch(uJoints, ivec2(i + 2, 0), 0), texelFetch(uJoints, ivec2(i + 3, 0), 0));
    }
    void main() {
      mat4 model = uModel;
      if (uSkinned) {
        mat4 skin = aWeights.x * joint(aJoints.x) + aWeights.y * joint(aJoints.y) + aWeights.z * joint(aJoints.z) + aWeights.w * joint(aJoints.w);
        model = uModel * skin;
      }
      vec4 world = model * vec4(aPosition, 1.0);
      vWorld = world.xyz;
      mat3 normalMatrix = transpose(inverse(mat3(model)));
      vNormal = normalMatrix * aNormal;
      vTangent = vec4(mat3(model) * aTangent.xyz, aTangent.w);
      vUv = aUv; vUv1 = aUv1; vColor = aColor;
      gl_Position = uProjection * uView * world;
    }`;

  const FRAGMENT = `#version 300 es
    precision highp float;
    in vec3 vWorld; in vec3 vNormal; in vec2 vUv; in vec2 vUv1; in vec4 vColor; in vec4 vTangent;
    uniform vec4 uBaseColor; uniform float uMetallic, uRoughness; uniform vec3 uEmissive;
    uniform bool uHasBaseTexture, uHasMrTexture, uHasNormalTexture, uHasOcclusionTexture, uHasEmissiveTexture;
    uniform int uBaseUv, uMrUv, uNormalUv, uOcclusionUv, uEmissiveUv;
    uniform sampler2D uBaseTexture, uMrTexture, uNormalTexture, uOcclusionTexture, uEmissiveTexture;
    uniform float uNormalScale, uOcclusionStrength;
    uniform bool uHasNormals, uHasTangents, uUnlit, uDoubleSided;
    uniform int uAlphaMode; uniform float uAlphaCutoff;
    uniform vec3 uEye;
    uniform vec3 uAmbient;
    uniform int uLightCount;
    uniform vec4 uLightPos[8];   // xyz position (or direction towards the light for w = 0), w: 1 point/spot, 0 directional
    uniform vec3 uLightColor[8]; // color times intensity (candela) or illuminance (lux)
    uniform vec4 uLightSpot[8];  // spot direction xyz, cos of the half angle (w <= -1: no cone)
    uniform sampler2D uEnvironment, uBrdf;
    uniform vec3 uSh[9];
    uniform vec4 uLighting;      // the lights, the environment, the ambient light (diffuse) and the lights' highlights on dielectrics
    uniform vec3 uGloss;         // the environment's reflection on dielectrics: g0 + (1 - g0) roughness^p; the emissive factor
    uniform vec3 uTone;          // s = 1 - exp(-(a u^g + b u))
    out vec4 outColor;
    const float PI = 3.14159265358979;
    const float LEVELS[9] = float[9](${LEVELS.map(v => v.toFixed(2)).join(", ")});
    vec3 toLinear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
    vec3 tone(vec3 u) { u = max(u, 0.0); return 1.0 - exp(-(uTone.x * pow(u, vec3(uTone.y)) + uTone.z * u)); }
    vec2 uvOf(int set) { return set == 1 ? vUv1 : vUv; }
    vec3 level(vec2 uv, int k) {
      float rows = float(${ENVIRONMENT.height});
      return texture(uEnvironment, vec2(uv.x, (clamp(uv.y * rows, 0.5, rows - 0.5) + float(k) * rows) / (rows * 9.0))).rgb;
    }
    /** The environment's radiance towards a direction, prefiltered for a roughness. */
    vec3 environment(vec3 r, float roughness) {
      vec2 uv = vec2(atan(r.x, r.z) / (2.0 * PI) + 0.5, acos(clamp(r.y, -1.0, 1.0)) / PI);
      int k = 0;
      for (int i = 1; i < 8; i++) if (roughness >= LEVELS[i]) k = i;
      float t = clamp((roughness - LEVELS[k]) / (LEVELS[k + 1] - LEVELS[k]), 0.0, 1.0);
      return mix(level(uv, k), level(uv, k + 1), t);
    }
    /** The environment's irradiance on a surface facing n. */
    vec3 irradiance(vec3 n) {
      return uSh[0] * 0.282095 + 0.488603 * (uSh[1] * n.y + uSh[2] * n.z + uSh[3] * n.x)
        + 1.092548 * (uSh[4] * n.x * n.y + uSh[5] * n.y * n.z + uSh[7] * n.x * n.z)
        + uSh[6] * 0.315392 * (3.0 * n.z * n.z - 1.0) + uSh[8] * 0.546274 * (n.x * n.x - n.y * n.y);
    }
    void main() {
      vec4 base = uBaseColor * vColor;
      if (uHasBaseTexture) { vec4 t = texture(uBaseTexture, uvOf(uBaseUv)); base *= vec4(toLinear(t.rgb), t.a); }
      if (uAlphaMode == 1 && base.a < uAlphaCutoff) discard;
      float alpha = uAlphaMode == 2 ? base.a : 1.0;
      if (uUnlit) { outColor = vec4(tone(base.rgb), alpha); return; }
      float metallic = uMetallic, roughness = uRoughness;
      if (uHasMrTexture) { vec4 mr = texture(uMrTexture, uvOf(uMrUv)); roughness *= mr.g; metallic *= mr.b; }
      roughness = clamp(roughness, 0.0, 1.0); metallic = clamp(metallic, 0.0, 1.0);
      vec3 dx = dFdx(vWorld), dy = dFdy(vWorld);
      vec3 n = uHasNormals ? normalize(vNormal) : normalize(cross(dx, dy));
      vec3 v = normalize(uEye - vWorld);
      if (uDoubleSided && !gl_FrontFacing) n = -n;
      if (uHasNormals && uDoubleSided == false && dot(n, v) < 0.0 && !gl_FrontFacing) n = -n;
      if (uHasNormalTexture) {
        vec3 t, b;
        if (uHasTangents) { t = normalize(vTangent.xyz); b = cross(n, t) * vTangent.w; }
        else {
          vec2 st = uvOf(uNormalUv), sx = dFdx(st), sy = dFdy(st);
          vec3 q1 = cross(dy, n), q2 = cross(n, dx);
          t = q1 * sx.x + q2 * sy.x; b = q1 * sx.y + q2 * sy.y;
          float scale = inversesqrt(max(dot(t, t), dot(b, b)));
          t *= scale; b *= scale;
        }
        vec3 m = texture(uNormalTexture, uvOf(uNormalUv)).xyz * 2.0 - 1.0;
        m.xy *= uNormalScale;
        n = normalize(mat3(t, b, n) * m);
      }
      vec3 diffuseColor = base.rgb * (1.0 - metallic);
      vec3 f0 = mix(vec3(0.04), base.rgb, metallic);
      float a = max(roughness * roughness, 0.002), a2 = a * a, k = (roughness + 1.0) * (roughness + 1.0) / 8.0;
      float nv = clamp(abs(dot(n, v)), 0.001, 1.0);
      vec3 color = vec3(0.0);
      for (int i = 0; i < 8; i++) {
        if (i >= uLightCount) break;
        vec3 l; float falloff = 1.0;
        if (uLightPos[i].w > 0.5) {
          vec3 d = uLightPos[i].xyz - vWorld;
          float dist2 = max(dot(d, d), 1e-8);
          l = d * inversesqrt(dist2);
          falloff = 1.0 / dist2;
          if (uLightSpot[i].w > -1.0) falloff *= smoothstep(uLightSpot[i].w, mix(uLightSpot[i].w, 1.0, 0.1), dot(-l, uLightSpot[i].xyz));
        } else {
          l = normalize(uLightPos[i].xyz);
        }
        float nl = clamp(dot(n, l), 0.0, 1.0);
        if (nl <= 0.0) continue;
        vec3 e = uLightColor[i] * falloff * nl;
        vec3 h = normalize(l + v);
        float nh = clamp(dot(n, h), 0.0, 1.0), vh = clamp(dot(v, h), 0.0, 1.0);
        vec3 f = f0 + (1.0 - f0) * pow(1.0 - vh, 5.0);
        float dDen = nh * nh * (a2 - 1.0) + 1.0;
        float distribution = a2 / (PI * dDen * dDen);
        float visibility = 0.25 / ((nl * (1.0 - k) + k) * (nv * (1.0 - k) + k));
        color += e * (uLighting.x * diffuseColor / PI + f * distribution * visibility * mix(uLighting.w, 1.0, metallic));
      }
      float occlusion = 1.0;
      if (uHasOcclusionTexture) occlusion = mix(1.0, texture(uOcclusionTexture, uvOf(uOcclusionUv)).r, uOcclusionStrength);
      color += (uLighting.y * irradiance(n) / PI + uLighting.z * uAmbient) * diffuseColor * occlusion;
      vec2 ab = texture(uBrdf, vec2(nv, roughness)).rg;
      float gloss = mix(uGloss.x + (1.0 - uGloss.x) * pow(roughness, uGloss.y), 1.0, metallic);
      vec3 reflection = (f0 * ab.x + ab.y) * (1.0 + f0 * (1.0 / max(ab.x + ab.y, 1e-3) - 1.0));
      color += environment(reflect(-v, n), roughness) * reflection * gloss * occlusion;
      vec3 emissive = uEmissive;
      if (uHasEmissiveTexture) emissive *= toLinear(texture(uEmissiveTexture, uvOf(uEmissiveUv)).rgb);
      color += emissive * uGloss.z;
      outColor = vec4(tone(color), alpha);
    }`;

  // ------------------------------------------------------------------------------------------------ the model

  class Model {
    /** A model drawn into a canvas: the model file's bytes and the slide's description of it (see Model3D in the engine). */
    constructor(canvas, bytes, spec) {
      this.canvas = canvas;
      this.spec = spec;
      this.gltf = new Gltf(bytes);
      const gl = canvas.getContext('webgl2', { premultipliedAlpha: true, alpha: true, antialias: true, preserveDrawingBuffer: true });
      if (!gl) throw new Error('WebGL2 is not available');
      this.gl = gl;
      this.program = this.compile();
      this.time = 0;
      this.animation = null;
    }

    compile() {
      const gl = this.gl;
      const shader = (type, source) => {
        const s = gl.createShader(type);
        gl.shaderSource(s, source);
        gl.compileShader(s);
        if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
        return s;
      };
      const program = gl.createProgram();
      gl.attachShader(program, shader(gl.VERTEX_SHADER, VERTEX));
      gl.attachShader(program, shader(gl.FRAGMENT_SHADER, FRAGMENT));
      gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
      const uniforms = {};
      for (let i = 0, n = gl.getProgramParameter(program, gl.ACTIVE_UNIFORMS); i < n; i++) {
        const info = gl.getActiveUniform(program, i);
        const name = info.name.replace(/\[0\]$/, '');
        uniforms[name] = gl.getUniformLocation(program, info.name);
      }
      const attributes = {};
      for (const name of ['aPosition', 'aNormal', 'aUv', 'aUv1', 'aColor', 'aJoints', 'aWeights', 'aTangent'])
        attributes[name] = gl.getAttribLocation(program, name);
      return { program, uniforms, attributes };
    }

    /** Loads the textures and buffers; resolves when the model can be drawn. */
    async load() {
      const gl = this.gl, json = this.gltf.json;
      const bitmaps = await this.gltf.images();
      this.textures = (json.textures || []).map(t => {
        const source = t.source !== undefined ? t.source : (t.extensions && Object.values(t.extensions)[0] && Object.values(t.extensions)[0].source);
        const bitmap = bitmaps[source];
        if (!bitmap) return null;
        const sampler = (json.samplers || [])[t.sampler] || {};
        const texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
        gl.pixelStorei(gl.UNPACK_COLORSPACE_CONVERSION_WEBGL, gl.NONE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, bitmap);
        const minFilter = sampler.minFilter || gl.LINEAR_MIPMAP_LINEAR;
        if ([gl.NEAREST_MIPMAP_NEAREST, gl.LINEAR_MIPMAP_NEAREST, gl.NEAREST_MIPMAP_LINEAR, gl.LINEAR_MIPMAP_LINEAR].includes(minFilter)) gl.generateMipmap(gl.TEXTURE_2D);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, minFilter);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, sampler.magFilter || gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, sampler.wrapS || gl.REPEAT);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, sampler.wrapT || gl.REPEAT);
        return texture;
      });
      this.meshes = (json.meshes || []).map(mesh => mesh.primitives.map(p => this.primitive(p)));
      this.jointTexture = gl.createTexture();
      this.nodes = (json.nodes || []).map(n => ({
        t: n.translation ? n.translation.slice() : [0, 0, 0], r: n.rotation ? n.rotation.slice() : [0, 0, 0, 1],
        s: n.scale ? n.scale.slice() : [1, 1, 1], matrix: n.matrix ? new Float32Array(n.matrix) : null,
        weights: n.weights ? n.weights.slice() : (n.mesh !== undefined && json.meshes[n.mesh].weights ? json.meshes[n.mesh].weights.slice() : null),
      }));
      this.rest = this.nodes.map(n => ({ t: n.t.slice(), r: n.r.slice(), s: n.s.slice(), weights: n.weights && n.weights.slice() }));
      const { levels, brdf } = lighting();
      const table = (width, height, format, components, data, wrap) => {
        const texture = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, format, width, height, 0, components, gl.FLOAT, data);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrap);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        return texture;
      };
      this.environment = table(ENVIRONMENT.width, ENVIRONMENT.height * LEVELS.length, gl.RGBA16F, gl.RGBA, levels, gl.REPEAT);
      this.brdf = table(32, 32, gl.RG16F, gl.RG, brdf, gl.CLAMP_TO_EDGE);
      return this;
    }

    primitive(p) {
      const gl = this.gl, gltf = this.gltf;
      const vao = gl.createVertexArray();
      gl.bindVertexArray(vao);
      const attributes = this.program.attributes;
      const buffers = {};
      const bind = (name, data, size) => {
        const location = attributes[name];
        if (location < 0) return;
        const buffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(location);
        gl.vertexAttribPointer(location, size, gl.FLOAT, false, 0, 0);
        buffers[name] = buffer;
      };
      const unset = (name, value) => { const location = attributes[name]; if (location >= 0) { gl.disableVertexAttribArray(location); gl.vertexAttrib4fv(location, value); } };
      const a = p.attributes;
      const positions = gltf.accessor(a.POSITION);
      const count = positions.length / 3;
      bind('aPosition', positions, 3);
      if (a.NORMAL !== undefined) bind('aNormal', gltf.accessor(a.NORMAL), 3); else unset('aNormal', [0, 0, 1, 0]);
      if (a.TEXCOORD_0 !== undefined) bind('aUv', gltf.accessor(a.TEXCOORD_0), 2); else unset('aUv', [0, 0, 0, 0]);
      if (a.TEXCOORD_1 !== undefined) bind('aUv1', gltf.accessor(a.TEXCOORD_1), 2); else unset('aUv1', [0, 0, 0, 0]);
      if (a.COLOR_0 !== undefined) {
        const colors = gltf.accessor(a.COLOR_0), size = this.gltf.json.accessors[a.COLOR_0].type === 'VEC3' ? 3 : 4;
        if (size === 3) { const rgba = new Float32Array(count * 4); for (let i = 0; i < count; i++) { rgba.set(colors.subarray(i * 3, i * 3 + 3), i * 4); rgba[i * 4 + 3] = 1; } bind('aColor', rgba, 4); }
        else bind('aColor', colors, 4);
      } else unset('aColor', [1, 1, 1, 1]);
      if (a.JOINTS_0 !== undefined && a.WEIGHTS_0 !== undefined) {
        bind('aJoints', Float32Array.from(gltf.accessor(a.JOINTS_0, true)), 4);
        bind('aWeights', gltf.accessor(a.WEIGHTS_0), 4);
      } else { unset('aJoints', [0, 0, 0, 0]); unset('aWeights', [0, 0, 0, 0]); }
      if (a.TANGENT !== undefined) bind('aTangent', gltf.accessor(a.TANGENT), 4); else unset('aTangent', [1, 0, 0, 1]);
      let indices = null, indexCount = count;
      if (p.indices !== undefined) {
        const data = Uint32Array.from(gltf.accessor(p.indices, true));
        indices = gl.createBuffer();
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indices);
        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, data, gl.STATIC_DRAW);
        indexCount = data.length;
      }
      gl.bindVertexArray(null);
      // Morph targets blend on the CPU into the position and normal buffers.
      const targets = (p.targets || []).map(t => ({
        position: t.POSITION !== undefined ? gltf.accessor(t.POSITION) : null,
        normal: t.NORMAL !== undefined && a.NORMAL !== undefined ? gltf.accessor(t.NORMAL) : null,
      }));
      return {
        vao, buffers, indices, count: indexCount, mode: p.mode === undefined ? 4 : p.mode, material: p.material,
        hasNormals: a.NORMAL !== undefined, hasTangents: a.TANGENT !== undefined, skinned: a.JOINTS_0 !== undefined,
        targets, basePositions: positions, baseNormals: a.NORMAL !== undefined ? gltf.accessor(a.NORMAL) : null, morphKey: '',
      };
    }

    /** Poses the nodes at a time (seconds) of an animation (index), or at rest without one. */
    pose(animationIndex, time) {
      const json = this.gltf.json;
      this.nodes.forEach((n, i) => { const r = this.rest[i]; n.t = r.t.slice(); n.r = r.r.slice(); n.s = r.s.slice(); n.weights = r.weights && r.weights.slice(); });
      const animation = animationIndex !== null && animationIndex !== undefined ? (json.animations || [])[animationIndex] : null;
      if (!animation) return;
      for (const channel of animation.channels) {
        const target = channel.target;
        if (target.node === undefined) continue;
        const node = this.nodes[target.node];
        const value = sample(this.gltf, animation.samplers[channel.sampler], target.path, time);
        if (target.path === 'translation') node.t = value;
        else if (target.path === 'rotation') node.r = value;
        else if (target.path === 'scale') node.s = value;
        else if (target.path === 'weights') node.weights = value;
      }
    }

    /** The nodes' global matrices (model units) for the current pose. */
    globals() {
      const json = this.gltf.json, out = new Array(this.nodes.length);
      const visit = (index, parent) => {
        const n = this.nodes[index];
        const local = n.matrix && !json.animations ? n.matrix : (n.matrix || mat4.fromTRS(n.t, n.r, n.s));
        out[index] = mat4.multiply(parent, local);
        for (const child of json.nodes[index].children || []) visit(child, out[index]);
      };
      const scene = (json.scenes || [])[json.scene || 0];
      for (const root of scene ? scene.nodes : json.nodes.map((_, i) => i)) visit(root, mat4.identity());
      return out;
    }

    /** The model's transform into the slide's world (meters): units to meters, then preTrans, scale, rotation, postTrans. */
    modelMatrix() {
      const t = this.spec.trans || {};
      const meters = t.mpu || 1;
      const pre = (t.pre || [0, 0, 0]).map(v => v / EMU_PER_METER), post = (t.post || [0, 0, 0]).map(v => v / EMU_PER_METER);
      const scale = t.scale || [1, 1, 1], rot = (t.rot || [0, 0, 0]).map(v => v / 60000 * Math.PI / 180);
      let m = mat4.scaling(meters, meters, meters);
      m = mat4.multiply(mat4.translation(...pre), m);
      m = mat4.multiply(mat4.scaling(...scale), m);
      m = mat4.multiply(this.rotation(rot), m);
      m = mat4.multiply(mat4.translation(...post), m);
      return m;
    }

    rotation([ax, ay, az]) {
      // Euler angles about x, then y, then z (see the catalog: picture.model3d).
      return mat4.multiply(mat4.rotationZ(az), mat4.multiply(mat4.rotationY(ay), mat4.rotationX(ax)));
    }

    /** The view and the projection: the full field of view spans the square viewport, of which the frame shows its middle. */
    camera() {
      const c = this.spec.camera, m = v => v.map(x => x / EMU_PER_METER);
      const eye = m(c.pos), target = m(c.lookAt), up = norm(c.up);
      const view = mat4.lookAt(eye, target, up);
      const frame = this.spec.frame, viewport = this.spec.viewport || Math.max(frame.w, frame.h);
      const distance = Math.hypot(eye[0] - target[0], eye[1] - target[1], eye[2] - target[2]);
      const near = Math.max(distance / 1000, 1e-4), far = distance * 100 + 100;
      const projection = new Float32Array(16);
      if (c.fov) {
        const f = 1 / Math.tan(c.fov / 60000 * Math.PI / 360);
        projection[0] = f * viewport / frame.w;
        projection[5] = f * viewport / frame.h;
        projection[10] = (far + near) / (near - far);
        projection[11] = -1;
        projection[14] = 2 * far * near / (near - far);
      } else {
        // Orthographic: one unit (a meter) is sz of the viewport's height.
        const sz = c.ortho || 1;
        projection[0] = 2 * sz * viewport / frame.w;
        projection[5] = 2 * sz * viewport / frame.h;
        projection[10] = -2 / (far - near);
        projection[14] = -(far + near) / (far - near);
        projection[15] = 1;
      }
      return { view, projection, eye };
    }

    /** Draws the model as it is at a time (seconds) of its animation (index, or none). */
    draw(animationIndex, time) {
      const gl = this.gl, json = this.gltf.json, { uniforms: u, program } = this.program;
      this.pose(animationIndex, time);
      const globals = this.globals();
      const width = this.canvas.width, height = this.canvas.height;
      gl.viewport(0, 0, width, height);
      gl.clearColor(0, 0, 0, 0);
      gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
      gl.enable(gl.DEPTH_TEST);
      gl.useProgram(program);
      const { view, projection, eye } = this.camera();
      const model = this.modelMatrix();
      gl.uniformMatrix4fv(u.uView, false, view);
      gl.uniformMatrix4fv(u.uProjection, false, projection);
      gl.uniform3fv(u.uEye, eye);
      this.lights();
      gl.activeTexture(gl.TEXTURE6);
      gl.bindTexture(gl.TEXTURE_2D, this.environment);
      gl.uniform1i(u.uEnvironment, 6);
      gl.activeTexture(gl.TEXTURE7);
      gl.bindTexture(gl.TEXTURE_2D, this.brdf);
      gl.uniform1i(u.uBrdf, 7);
      gl.uniform3fv(u.uSh, lighting().sh);
      gl.uniform4f(u.uLighting, LIGHTING.lights, LIGHTING.environment, LIGHTING.ambient, LIGHTING.highlights);
      gl.uniform3f(u.uGloss, LIGHTING.gloss[0], LIGHTING.gloss[1], LIGHTING.emissive);
      gl.uniform3fv(u.uTone, LIGHTING.tone);
      // Opaque first, then blended surfaces back to front.
      const draws = [];
      const scene = (json.scenes || [])[json.scene || 0];
      const visit = index => {
        const node = json.nodes[index];
        if (node.mesh !== undefined) {
          let skin = null;
          if (node.skin !== undefined) {
            const s = json.skins[node.skin];
            const inverse = s.inverseBindMatrices !== undefined ? this.gltf.accessor(s.inverseBindMatrices) : null;
            skin = new Float32Array(s.joints.length * 16);
            s.joints.forEach((joint, j) => {
              const ibm = inverse ? inverse.subarray(j * 16, j * 16 + 16) : mat4.identity();
              skin.set(mat4.multiply(globals[joint], ibm), j * 16);
            });
          }
          this.meshes[node.mesh].forEach((p, k) => draws.push({ p, primitive: json.meshes[node.mesh].primitives[k], world: skin ? model : mat4.multiply(model, globals[index]), skin, weights: this.nodes[index].weights }));
        }
        for (const child of node.children || []) visit(child);
      };
      for (const root of scene ? scene.nodes : json.nodes.map((_, i) => i)) visit(root);
      const blended = d => { const m = (json.materials || [])[d.p.material]; return m && m.alphaMode === 'BLEND'; };
      const opaque = draws.filter(d => !blended(d)), transparent = draws.filter(blended);
      gl.disable(gl.BLEND);
      gl.depthMask(true);
      for (const d of opaque) this.drawPrimitive(d);
      gl.enable(gl.BLEND);
      gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
      gl.depthMask(false);
      for (const d of transparent) this.drawPrimitive(d);
      gl.depthMask(true);
      gl.disable(gl.BLEND);
    }

    lights() {
      const gl = this.gl, u = this.program.uniforms, spec = this.spec;
      const ambient = spec.ambient && spec.ambient.enabled !== false ? spec.ambient.color.map(c => c * spec.ambient.lux) : [0, 0, 0];
      gl.uniform3fv(u.uAmbient, ambient);
      const positions = [], colors = [], spots = [];
      for (const light of (spec.lights || []).filter(l => l.enabled !== false).slice(0, 8)) {
        const pos = light.pos.map(v => v / EMU_PER_METER);
        if (light.type === 'dir') {
          const at = light.lookAt.map(v => v / EMU_PER_METER);
          positions.push(...norm([pos[0] - at[0], pos[1] - at[1], pos[2] - at[2]]), 0);
          colors.push(...light.color.map(c => c * light.lux));
          spots.push(0, 0, -1, -2);
        } else {
          positions.push(...pos, 1);
          colors.push(...light.color.map(c => c * light.cd));
          if (light.type === 'spot') {
            const at = light.lookAt.map(v => v / EMU_PER_METER);
            spots.push(...norm([at[0] - pos[0], at[1] - pos[1], at[2] - pos[2]]), Math.cos(light.angle / 60000 * Math.PI / 360));
          } else spots.push(0, 0, -1, -2);
        }
      }
      gl.uniform1i(u.uLightCount, positions.length / 4);
      if (positions.length) {
        gl.uniform4fv(u.uLightPos, new Float32Array(positions));
        gl.uniform3fv(u.uLightColor, new Float32Array(colors));
        gl.uniform4fv(u.uLightSpot, new Float32Array(spots));
      }
    }

    drawPrimitive({ p, world, skin, weights }) {
      const gl = this.gl, u = this.program.uniforms, json = this.gltf.json;
      if (p.targets.length && weights) this.morph(p, weights);
      const material = (json.materials || [])[p.material] || {};
      const pbr = material.pbrMetallicRoughness || {};
      gl.uniformMatrix4fv(u.uModel, false, world);
      gl.uniform1i(u.uSkinned, skin ? 1 : 0);
      if (skin) {
        gl.activeTexture(gl.TEXTURE5);
        gl.bindTexture(gl.TEXTURE_2D, this.jointTexture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA32F, skin.length / 4, 1, 0, gl.RGBA, gl.FLOAT, skin);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.uniform1i(u.uJoints, 5);
      }
      // PowerPoint keeps the base color factor in 8 bits (linear).
      const base = (pbr.baseColorFactor || [1, 1, 1, 1]).map((c, i) => i < 3 ? Math.round(c * 255) / 255 : c);
      gl.uniform4fv(u.uBaseColor, base);
      gl.uniform1f(u.uMetallic, pbr.metallicFactor === undefined ? 1 : pbr.metallicFactor);
      gl.uniform1f(u.uRoughness, pbr.roughnessFactor === undefined ? 1 : pbr.roughnessFactor);
      gl.uniform3fv(u.uEmissive, material.emissiveFactor || [0, 0, 0]);
      const texture = (info, unit, has, sampler, set) => {
        const t = info && this.textures[info.index];
        gl.uniform1i(u[has], t ? 1 : 0);
        if (t) {
          gl.activeTexture(gl.TEXTURE0 + unit);
          gl.bindTexture(gl.TEXTURE_2D, t);
          gl.uniform1i(u[sampler], unit);
          gl.uniform1i(u[set], info.texCoord || 0);
        }
      };
      texture(pbr.baseColorTexture, 0, 'uHasBaseTexture', 'uBaseTexture', 'uBaseUv');
      texture(pbr.metallicRoughnessTexture, 1, 'uHasMrTexture', 'uMrTexture', 'uMrUv');
      texture(material.normalTexture, 2, 'uHasNormalTexture', 'uNormalTexture', 'uNormalUv');
      texture(material.occlusionTexture, 3, 'uHasOcclusionTexture', 'uOcclusionTexture', 'uOcclusionUv');
      texture(material.emissiveTexture, 4, 'uHasEmissiveTexture', 'uEmissiveTexture', 'uEmissiveUv');
      gl.uniform1f(u.uNormalScale, material.normalTexture && material.normalTexture.scale !== undefined ? material.normalTexture.scale : 1);
      gl.uniform1f(u.uOcclusionStrength, material.occlusionTexture && material.occlusionTexture.strength !== undefined ? material.occlusionTexture.strength : 1);
      gl.uniform1i(u.uHasNormals, p.hasNormals ? 1 : 0);
      gl.uniform1i(u.uHasTangents, p.hasTangents ? 1 : 0);
      gl.uniform1i(u.uUnlit, material.extensions && material.extensions.KHR_materials_unlit ? 1 : 0);
      gl.uniform1i(u.uDoubleSided, material.doubleSided ? 1 : 0);
      gl.uniform1i(u.uAlphaMode, material.alphaMode === 'MASK' ? 1 : material.alphaMode === 'BLEND' ? 2 : 0);
      gl.uniform1f(u.uAlphaCutoff, material.alphaCutoff === undefined ? 0.5 : material.alphaCutoff);
      if (material.doubleSided) gl.disable(gl.CULL_FACE); else { gl.enable(gl.CULL_FACE); gl.cullFace(gl.BACK); }
      // A negative determinant (mirroring) turns the winding.
      const m = world, det = m[0] * (m[5] * m[10] - m[6] * m[9]) - m[4] * (m[1] * m[10] - m[2] * m[9]) + m[8] * (m[1] * m[6] - m[2] * m[5]);
      gl.frontFace(det < 0 ? gl.CW : gl.CCW);
      gl.bindVertexArray(p.vao);
      if (p.indices) gl.drawElements(p.mode, p.count, gl.UNSIGNED_INT, 0); else gl.drawArrays(p.mode, 0, p.count);
      gl.bindVertexArray(null);
    }

    morph(p, weights) {
      const key = weights.join(',');
      if (key === p.morphKey) return;
      p.morphKey = key;
      const gl = this.gl;
      const blend = (base, field, buffer) => {
        if (!base || !buffer) return;
        const out = Float32Array.from(base);
        p.targets.forEach((t, k) => { const delta = t[field], w = weights[k] || 0; if (delta && w) for (let i = 0; i < out.length; i++) out[i] += w * delta[i]; });
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, out, gl.DYNAMIC_DRAW);
      };
      blend(p.basePositions, 'position', p.buffers.aPosition);
      blend(p.baseNormals, 'normal', p.buffers.aNormal);
    }

    /** The index of the model's animation an embedded animation id names, and its length in seconds. */
    clip(id) {
      const animations = this.gltf.json.animations || [];
      const index = id !== undefined && id < animations.length ? id : 0;
      return animations.length ? { index, length: animationLength(this.gltf, animations[index]) } : null;
    }
  }

  window.FreeSlateModel3D = {
    Model, LIGHTING, mat4,
    /** Creates, loads and draws a model at its poster frame; resolves with the model. */
    async create(canvas, bytes, spec) {
      const model = new Model(canvas, bytes, spec);
      await model.load();
      const poster = spec.poster, clip = poster ? model.clip(poster.id) : null;
      model.draw(clip ? clip.index : null, clip ? (poster.frame || 0) * clip.length : 0);
      return model;
    },
  };
})();
