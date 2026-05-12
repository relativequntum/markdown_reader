import { describe, it, expect } from 'vitest';
import { classifyLink, enhanceLinks } from '../src/linkRules';

describe('classifyLink', () => {
  it('http(s) → external', () => {
    expect(classifyLink('https://x').kind).toBe('external');
    expect(classifyLink('http://x').kind).toBe('external');
  });
  it('# → anchor', () => {
    expect(classifyLink('#section').kind).toBe('anchor');
  });
  it('relative .md → mdfile', () => {
    expect(classifyLink('./other.md').kind).toBe('mdfile');
    expect(classifyLink('../up/a.md').kind).toBe('mdfile');
  });
  it('file:// .md → mdfile', () => {
    expect(classifyLink('file:///C:/a.md').kind).toBe('mdfile');
  });
  it('other local → localfile', () => {
    expect(classifyLink('./report.pdf').kind).toBe('localfile');
  });
  it('javascript: → invalid', () => {
    expect(classifyLink('javascript:alert(1)').kind).toBe('invalid');
  });
});

describe('enhanceLinks', () => {
  it('adds target/rel/data-link-kind to anchors', () => {
    document.body.innerHTML = '<a href="https://x">ext</a><a href="#sec">anc</a><a href="javascript:alert(1)">bad</a>';
    enhanceLinks(document.body);
    const [a, b, c] = document.body.querySelectorAll('a');
    expect(a.getAttribute('target')).toBe('_blank');
    expect(a.getAttribute('rel')).toBe('noopener noreferrer');
    expect(a.dataset.linkKind).toBe('external');
    expect(b.dataset.linkKind).toBe('anchor');
    expect(c.getAttribute('href')).toBe('about:blank');
  });
});
