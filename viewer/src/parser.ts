import MarkdownIt from 'markdown-it';
import taskLists from 'markdown-it-task-lists';
import DOMPurify, { type Config } from 'dompurify';
import { rewriteSrc } from './rewriteSrc';
import { highlight } from './highlight';

const md = new MarkdownIt({
  html: false,
  linkify: true,
  typographer: false,
  breaks: false,
  highlight: (code, lang) => {
    const r = highlight(code, lang || null);
    return `<pre><code class="hljs${r.lang ? ' language-' + r.lang : ''}">${r.html}</code></pre>`;
  }
});
md.use(taskLists, { enabled: false, label: false });

let currentBaseDir = '';

const defaultImage = md.renderer.rules.image!;
md.renderer.rules.image = (tokens, idx, opts, env, self) => {
  const t = tokens[idx];
  const src = t.attrGet('src') ?? '';
  t.attrSet('src', rewriteSrc(src, currentBaseDir));
  const li = t.attrIndex('loading');
  if (li < 0) t.attrPush(['loading', 'lazy']);
  else t.attrs![li][1] = 'lazy';
  return defaultImage(tokens, idx, opts, env, self);
};

const purifyConfig: Config = {
  ALLOWED_URI_REGEXP: /^(?:(?:https?|mailto|tel|mdimg|file|#|\.\/|\/|data):|[^:]+$)/i,
  FORBID_TAGS: ['style', 'script', 'iframe', 'object', 'embed', 'form', 'button'],
  FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover', 'onfocus'],
  ALLOW_DATA_ATTR: false
};

export function renderMarkdown(source: string, baseDir: string): string {
  currentBaseDir = baseDir;
  const raw = md.render(source);
  return DOMPurify.sanitize(raw, purifyConfig) as string;
}
