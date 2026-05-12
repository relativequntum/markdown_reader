import { renderMarkdownUnsafe } from './parser';

self.onmessage = (ev: MessageEvent) => {
  const { md, baseDir } = ev.data as { md: string; baseDir: string };
  try {
    const rawHtml = renderMarkdownUnsafe(md, baseDir);
    (self as unknown as Worker).postMessage({ ok: true, rawHtml });
  } catch (e) {
    (self as unknown as Worker).postMessage({ ok: false, error: e instanceof Error ? e.message : String(e) });
  }
};
