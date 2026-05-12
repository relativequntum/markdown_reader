import { describe, it, expect } from 'vitest';
import { rewriteSrc } from '../src/rewriteSrc';

describe('rewriteSrc', () => {
  const base = 'C:\\Docs\\my note';

  it('passes data: through', () => {
    expect(rewriteSrc('data:image/png;base64,AAA', base)).toBe('data:image/png;base64,AAA');
  });

  it('remote https → mdimg remote', () => {
    const out = rewriteSrc('https://i.imgur.com/abc.png', base);
    expect(out.startsWith('mdimg://remote/')).toBe(true);
  });

  it('relative path → mdimg local with base', () => {
    const out = rewriteSrc('images/foo.png', base);
    expect(out.startsWith('mdimg://local/')).toBe(true);
    expect(out).toContain('base=');
  });

  it('windows absolute → mdimg abs', () => {
    const out = rewriteSrc('D:\\pics\\a.jpg', base);
    expect(out.startsWith('mdimg://abs/')).toBe(true);
  });

  it('file:// → mdimg abs', () => {
    const out = rewriteSrc('file:///C:/pics/a.jpg', base);
    expect(out.startsWith('mdimg://abs/')).toBe(true);
  });

  it('empty / null → empty', () => {
    expect(rewriteSrc('', base)).toBe('');
  });
});
