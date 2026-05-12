function b64u(s: string): string {
  const utf8 = new TextEncoder().encode(s);
  let bin = '';
  for (const byte of utf8) bin += String.fromCharCode(byte);
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

const REMOTE = /^https?:\/\//i;
const FILE_URL = /^file:\/\//i;
const WIN_ABS = /^[A-Za-z]:[\\/]/;
const POSIX_ABS = /^\//;
const DATA = /^data:/i;

export function rewriteSrc(src: string, baseDir: string): string {
  if (!src) return '';
  if (DATA.test(src)) return src;

  if (REMOTE.test(src)) return `mdimg://remote/${b64u(src)}`;
  if (FILE_URL.test(src) || WIN_ABS.test(src) || POSIX_ABS.test(src))
    return `mdimg://abs/${b64u(src)}`;

  return `mdimg://local/${b64u(src)}?base=${b64u(baseDir)}`;
}
