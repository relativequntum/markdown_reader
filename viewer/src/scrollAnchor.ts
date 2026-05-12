export interface ScrollSnapshot { ratio: number; }

export function snapshotScroll(el: HTMLElement): ScrollSnapshot {
  const scrollable = el.scrollHeight - el.clientHeight;
  return { ratio: scrollable > 0 ? el.scrollTop / scrollable : 0 };
}

export function restoreScroll(el: HTMLElement, s: ScrollSnapshot) {
  const scrollable = el.scrollHeight - el.clientHeight;
  el.scrollTop = Math.max(0, scrollable * s.ratio);
}
