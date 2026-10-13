// The viewer's script in FreeSlate's HTML of a Word document: zoom, the page in view, the keys the window handles,
// and Word's pages beside, over or against FreeSlate's for comparing. The window calls window.freeSlateViewer; the
// page answers through invokeCSharpAction ("pages 12", "page 3", "reload", "open").
(() => {
  if (window.freeSlateViewer) return;
  const main = document.querySelector('main.document');
  const pages = main ? [...main.querySelectorAll('section.page')] : [];
  const post = message => { try { invokeCSharpAction(message); } catch { } };
  const style = document.createElement('style');
  style.textContent = `
    .fs-row{display:flex;align-items:flex-start;gap:24px;width:max-content;margin:16px auto}
    .fs-row .page{margin:0}
    .fs-cell{display:flex;flex-direction:column;gap:4px}
    .fs-cap{font:12px/16px system-ui,sans-serif;color:#fff;opacity:.85;white-space:nowrap}
    .fs-word{position:relative;overflow:hidden;background:#fff;flex:none}
    .fs-word>img,.page>img.fs-over{position:absolute;display:block;max-width:none;pointer-events:none;user-select:none}
    .page>img.fs-over{z-index:2147483647}
    .fs-none{flex:none;display:flex;align-items:center;justify-content:center;border:2px dashed rgba(255,255,255,.6);color:#fff;font:14px system-ui,sans-serif}
    .fs-extra{width:max-content;margin:16px auto}
    main.document.fs-blend .page>img.fs-over{opacity:var(--fs-blend,.5)}
    main.document.fs-diff .page.fs-paired{filter:invert(1);isolation:isolate}
    main.document.fs-diff .page>img.fs-over{mix-blend-mode:difference}
    @media print{.fs-cap,.fs-word,.fs-none,.fs-over,.fs-extra{display:none!important}.fs-row,.fs-cell{display:block!important;margin:0!important;width:auto!important}}`;
  document.head.appendChild(style);

  const px = value => parseFloat(value) || 0;
  const size = page => ({ w: px(page.style.width), h: px(page.style.height) });

  // Where Word's page lies on FreeSlate's: a page with the markup pane is wider than the paper and is printed made
  // smaller by --s, in the middle of the paper's height (--oy); Word's PDF is that paper.
  const placement = (page, word) => {
    const s = px(page.style.getPropertyValue('--s'));
    if (!page.classList.contains('markup') || !s) return { left: 0, top: 0, w: word.w, h: word.h, box: { w: word.w, h: word.h } };
    const oy = px(page.style.getPropertyValue('--oy'));
    return { left: 0, top: -oy / s, w: word.w / s, h: word.h / s, box: size(page) };
  };
  const image = (word, at) => {
    const img = document.createElement('img');
    img.src = word.src;
    img.alt = '';
    img.draggable = false;
    Object.assign(img.style, { left: at.left + 'px', top: at.top + 'px', width: at.w + 'px', height: at.h + 'px' });
    return img;
  };
  const caption = text => Object.assign(document.createElement('div'), { className: 'fs-cap', textContent: text });
  const none = (w, h, text) => {
    const box = Object.assign(document.createElement('div'), { className: 'fs-none', textContent: text });
    Object.assign(box.style, { width: w + 'px', height: h + 'px' });
    return box;
  };
  const wordBox = (word, at, n) => {
    const cell = document.createElement('div');
    cell.className = 'fs-cell';
    const box = document.createElement('div');
    box.className = 'fs-word';
    Object.assign(box.style, { width: at.box.w + 'px', height: at.box.h + 'px' });
    box.appendChild(image(word, at));
    cell.append(caption(`Word · Seite ${n}`), box);
    return cell;
  };

  let mode = 'off', word = [], rows = [];
  const reset = () => {
    for (const row of rows) {
      const page = row.querySelector('section.page');
      if (page) row.replaceWith(page); else row.remove();
    }
    rows = [];
    main.querySelectorAll('img.fs-over').forEach(img => img.remove());
    pages.forEach(page => page.classList.remove('fs-paired'));
    main.classList.remove('fs-blend', 'fs-diff');
  };
  const compare = (next, pictures) => {
    if (!main) return;
    const position = where();
    reset();
    mode = next;
    word = pictures || [];
    if (mode === 'side') {
      const count = Math.max(pages.length, word.length);
      for (let i = 0; i < count; i++) {
        const page = pages[i], picture = word[i];
        const row = document.createElement('div');
        row.className = 'fs-row';
        const ours = document.createElement('div');
        ours.className = 'fs-cell';
        ours.appendChild(caption(`FreeSlate · Seite ${i + 1}`));
        if (page) {
          page.replaceWith(row);
          ours.appendChild(page);
        } else {
          main.appendChild(row);
          ours.appendChild(none(picture.w, picture.h, 'keine Seite bei FreeSlate'));
        }
        row.appendChild(ours);
        if (picture) row.appendChild(wordBox(picture, placement(page || document.body, picture), i + 1));
        else {
          const theirs = document.createElement('div');
          theirs.className = 'fs-cell';
          const { w, h } = size(page);
          theirs.append(caption(`Word · Seite ${i + 1}`), none(w, h, 'keine Seite bei Word'));
          row.appendChild(theirs);
        }
        rows.push(row);
      }
    } else if (mode === 'blend' || mode === 'diff') {
      main.classList.add(mode === 'blend' ? 'fs-blend' : 'fs-diff');
      pages.forEach((page, i) => {
        if (!word[i]) return;
        const img = image(word[i], placement(page, word[i]));
        img.classList.add('fs-over');
        page.appendChild(img);
        page.classList.add('fs-paired');
      });
      // Pages only Word has come after FreeSlate's last.
      for (let i = pages.length; i < word.length; i++) {
        const extra = document.createElement('div');
        extra.className = 'fs-extra';
        const box = wordBox(word[i], placement(document.body, word[i]), i + 1);
        box.firstChild.textContent = `Nur bei Word · Seite ${i + 1}`;
        extra.appendChild(box);
        main.appendChild(extra);
        rows.push(extra);
      }
    }
    fit();
    go(position);
  };

  // Zoom: 'auto' fits the width but never enlarges, 'width' fits the width, 'page' a whole page, a number is the factor.
  let zoom = 'auto';
  const natural = () => {
    let width = 0, height = 0;
    const count = Math.max(pages.length, mode === 'side' ? word.length : 0);
    for (let i = 0; i < count; i++) {
      const ours = pages[i] ? size(pages[i]) : { w: word[i].w, h: word[i].h };
      let w = ours.w, h = ours.h;
      if (mode === 'side') {
        const theirs = word[i] ? placement(pages[i] || document.body, word[i]).box : ours;
        w += 24 + theirs.w;
        h = Math.max(h, theirs.h) + 20;
      }
      width = Math.max(width, w);
      height = Math.max(height, h);
    }
    return { width: width || 1, height: height || 1 };
  };
  const fit = () => {
    if (!main) return;
    const { width, height } = natural();
    const across = (document.documentElement.clientWidth - 32) / width;
    const down = (document.documentElement.clientHeight - 32) / height;
    const factor = zoom === 'auto' ? Math.min(1, across) : zoom === 'width' ? across : zoom === 'page' ? Math.min(across, down) : zoom;
    main.style.zoom = Math.max(0.05, factor);
  };

  // The page in view is the last whose top is above a third of the window's height.
  let shown = 0;
  const current = () => {
    const mark = window.innerHeight / 3;
    let n = pages.length ? 1 : 0;
    for (let i = 0; i < pages.length; i++) {
      if (pages[i].getBoundingClientRect().top <= mark) n = i + 1;
      else break;
    }
    return n;
  };
  const track = () => {
    const n = current();
    if (n !== shown) { shown = n; post('page ' + n); }
  };
  // A position is a page and how far down it the window's top is (a share of its height), which survives a change of
  // zoom or of the document.
  const where = () => {
    const n = current();
    if (!n) return { page: 0, share: 0 };
    const box = pages[n - 1].getBoundingClientRect();
    return { page: n, share: box.height ? -box.top / box.height : 0 };
  };
  const go = position => {
    if (!position || !position.page || !pages.length) return;
    const page = pages[Math.min(position.page, pages.length) - 1];
    const box = page.getBoundingClientRect();
    window.scrollBy(0, box.top + position.share * box.height);
    track();
  };

  let pending = false;
  window.addEventListener('scroll', () => {
    if (pending) return;
    pending = true;
    requestAnimationFrame(() => { pending = false; track(); });
  }, { passive: true });
  window.addEventListener('resize', () => {
    if (typeof zoom === 'number') return;
    const position = where();
    fit();
    go(position);
  });
  // The keys the window handles while the page has the focus.
  document.addEventListener('keydown', e => {
    if (e.key === 'F5' || (e.ctrlKey && e.key.toLowerCase() === 'r')) { e.preventDefault(); post('reload'); }
    else if (e.ctrlKey && e.key.toLowerCase() === 'o') { e.preventDefault(); post('open'); }
  });

  window.freeSlateViewer = {
    zoom: value => {
      const position = where();
      zoom = value;
      fit();
      go(position);
    },
    goTo: n => {
      if (!pages.length) return;
      pages[Math.max(1, Math.min(n, pages.length)) - 1].scrollIntoView({ block: 'start' });
      window.scrollBy(0, -8);
      track();
    },
    position: () => where(),
    restore: position => go(typeof position === 'string' ? JSON.parse(position) : position),
    compare,
    blend: share => document.documentElement.style.setProperty('--fs-blend', share),
  };
  fit();
  post('pages ' + pages.length);
  track();
})();
