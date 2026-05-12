import { describe, it, expect } from 'vitest';
import { renderMarkdown } from '../src/parser';

const base = 'C:\\Docs\\x';

describe('parser', () => {
  it('renders GFM table', () => {
    const md = '| a | b |\n|---|---|\n| 1 | 2 |\n';
    const html = renderMarkdown(md, base);
    expect(html).toContain('<table>');
    expect(html).toContain('<th>a</th>');
  });

  it('renders task list', () => {
    const md = '- [ ] todo\n- [x] done\n';
    const html = renderMarkdown(md, base);
    expect(html).toMatch(/<input[^>]+type="checkbox"/);
  });

  it('renders strikethrough', () => {
    const html = renderMarkdown('~~gone~~', base);
    expect(html).toContain('<s>gone</s>');
  });

  it('rewrites image src to mdimg://', () => {
    const html = renderMarkdown('![](images/a.png)', base);
    expect(html).toMatch(/<img[^>]+src="mdimg:\/\/local\//);
    expect(html).toContain('loading="lazy"');
  });

  it('highlights code block', () => {
    const md = '```js\nconst x = 1;\n```';
    const html = renderMarkdown(md, base);
    expect(html).toContain('class="hljs');
  });

  it('does not execute html when html:false', () => {
    const html = renderMarkdown('<script>alert(1)</script>', base);
    expect(html).not.toContain('<script>');
  });

  it('autolinks bare URLs', () => {
    const html = renderMarkdown('see https://example.com here', base);
    expect(html).toContain('<a href="https://example.com"');
  });
});
