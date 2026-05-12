export type LinkKind = 'external' | 'anchor' | 'mdfile' | 'localfile' | 'invalid';

export function classifyLink(href: string): { kind: LinkKind } {
  if (!href) return { kind: 'invalid' };
  if (/^(?:javascript|vbscript|data|blob):/i.test(href)) return { kind: 'invalid' };
  if (/^https?:\/\//i.test(href)) return { kind: 'external' };
  if (/^(?:mailto|tel|sms):/i.test(href)) return { kind: 'external' };
  if (href.startsWith('#')) return { kind: 'anchor' };
  if (/^file:\/\/.*\.md(\?|#|$)/i.test(href)) return { kind: 'mdfile' };
  if (/\.md(\?|#|$)/i.test(href)) return { kind: 'mdfile' };
  return { kind: 'localfile' };
}

export function enhanceLinks(root: HTMLElement) {
  for (const a of Array.from(root.querySelectorAll('a'))) {
    const href = a.getAttribute('href') ?? '';
    const { kind } = classifyLink(href);
    a.dataset.linkKind = kind;
    if (kind === 'external' || kind === 'mdfile' || kind === 'localfile') {
      a.setAttribute('target', '_blank');
      a.setAttribute('rel', 'noopener noreferrer');
    }
    if (kind === 'invalid') a.setAttribute('href', 'about:blank');
  }
}
