import hljs from 'highlight.js/lib/common';

export function highlight(code: string, lang: string | null): { html: string; lang: string | null } {
  if (lang && hljs.getLanguage(lang)) {
    try { return { html: hljs.highlight(code, { language: lang, ignoreIllegals: true }).value, lang }; }
    catch { /* fallthrough */ }
  }
  try { const r = hljs.highlightAuto(code); return { html: r.value, lang: r.language ?? null }; }
  catch { return { html: escapeHtml(code), lang: null }; }
}

function escapeHtml(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
