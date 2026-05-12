import { describe, it, expect } from 'vitest';
import { snapshotScroll, restoreScroll } from '../src/scrollAnchor';

describe('scrollAnchor', () => {
  it('snapshot returns ratio in [0,1]', () => {
    const el = makeScrollEl(1000, 400, 250);
    const s = snapshotScroll(el);
    expect(s.ratio).toBeCloseTo(250 / (1000 - 400));
  });

  it('restore applies ratio to new scrollHeight', () => {
    const el = makeScrollEl(2000, 400, 0);
    restoreScroll(el, { ratio: 0.5 });
    expect(el.scrollTop).toBe((2000 - 400) * 0.5);
  });

  it('zero scrollable height → 0', () => {
    const el = makeScrollEl(300, 400, 0);
    const s = snapshotScroll(el);
    expect(s.ratio).toBe(0);
  });
});

function makeScrollEl(scrollHeight: number, clientHeight: number, scrollTop: number) {
  const el = document.createElement('div');
  Object.defineProperty(el, 'scrollHeight', { value: scrollHeight, configurable: true });
  Object.defineProperty(el, 'clientHeight', { value: clientHeight, configurable: true });
  let st = scrollTop;
  Object.defineProperty(el, 'scrollTop', { get: () => st, set: v => st = v, configurable: true });
  return el;
}
