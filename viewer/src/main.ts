import 'highlight.js/styles/github.css';
import { renderMarkdown, sanitize } from './parser';
import { enhanceLinks } from './linkRules';
import { snapshotScroll, restoreScroll, type ScrollSnapshot } from './scrollAnchor';

type Theme = 'light' | 'dark';

declare global {
  interface Window {
    chrome?: { webview?: { postMessage: (m: unknown) => void } };
  }
}

const WORKER_THRESHOLD = 200 * 1024;
let worker: Worker | null = null;
function getWorker(): Worker {
  if (!worker) worker = new Worker(new URL('./worker.ts', import.meta.url), { type: 'module' });
  return worker;
}

const content = document.getElementById('content') as HTMLElement;
const banners = document.getElementById('banner-host') as HTMLElement;

function applyTheme(theme: Theme) {
  document.documentElement.classList.remove('theme-light', 'theme-dark');
  document.documentElement.classList.add(`theme-${theme}`);
}

function showBanner(kind: 'warn' | 'error', text: string) {
  banners.innerHTML = `<div class="banner ${kind}">${escapeHtml(text)}</div>`;
}
function clearBanners() { banners.innerHTML = ''; }
function escapeHtml(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function postNative(msg: unknown) {
  window.chrome?.webview?.postMessage(msg);
}

content.addEventListener('click', (ev) => {
  const a = (ev.target as HTMLElement).closest('a');
  if (!a) return;
  const href = a.getAttribute('href') ?? '';
  const kind = a.dataset.linkKind ?? '';
  if (kind === 'anchor') return;
  ev.preventDefault();
  postNative({ type: 'linkClick', href, kind });
});

let lastSnapshot: ScrollSnapshot = { ratio: 0 };

window.addEventListener('message', (ev) => {
  const msg = ev.data;
  if (!msg || typeof msg !== 'object') return;

  if (msg.type === 'render') {
    clearBanners();
    applyTheme(msg.theme as Theme);
    const md = msg.md as string;
    const baseDir = msg.baseDir as string;
    const t0 = performance.now();

    const apply = (html: string) => {
      content.innerHTML = html;
      enhanceLinks(content);
      // double-rAF: let content-visibility blocks compute sizes before scroll restore
      requestAnimationFrame(() => requestAnimationFrame(() => {
        restoreScroll(document.scrollingElement as HTMLElement, lastSnapshot);
      }));
      postNative({ type: 'rendered', ms: performance.now() - t0, bytes: md.length });
    };

    const onError = (message: string) => {
      showBanner('error', `渲染失败：${message}`);
      postNative({ type: 'error', message });
    };

    if (md.length < WORKER_THRESHOLD) {
      try { apply(renderMarkdown(md, baseDir)); }
      catch (e) { onError(e instanceof Error ? e.message : String(e)); }
    } else {
      const w = getWorker();
      const onMsg = (ev: MessageEvent) => {
        w.removeEventListener('message', onMsg);
        const r = ev.data as { ok: boolean; rawHtml?: string; error?: string };
        if (r.ok && r.rawHtml !== undefined) {
          // sanitize on main thread (DOMPurify needs DOM)
          apply(sanitize(r.rawHtml));
        } else {
          onError(r.error ?? 'unknown worker error');
        }
      };
      w.addEventListener('message', onMsg);
      w.postMessage({ md, baseDir });
    }
  } else if (msg.type === 'setTheme') {
    applyTheme(msg.theme as Theme);
  } else if (msg.type === 'banner') {
    showBanner(msg.kind as 'warn' | 'error', msg.text as string);
  } else if (msg.type === 'snapshotScroll') {
    lastSnapshot = snapshotScroll(document.scrollingElement as HTMLElement);
    postNative({ type: 'scrollSnapshot', ratio: lastSnapshot.ratio });
  }
});

postNative({ type: 'ready' });
