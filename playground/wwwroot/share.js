// The share URL: the hash holds JSON {v, s, y, t, o} — version, sample, YAML, tab, render options — deflated
// with zlib and base64url-encoded. The sample names the outputs file to replay; the file itself is fetched from
// the site, because a recording can be large.
export const SHARE_VERSION = 1;
export const MAX_SHARE_HASH_LENGTH = 16384;

/**
 * @param {{ sample: string, yaml: string, tab: string, options: Record<string, unknown> }} state
 * @returns {Promise<string>} the hash without '#'
 */
export async function encodeShare(state) {
  const payload = { v: SHARE_VERSION, s: state.sample, y: state.yaml };
  if (state.tab && state.tab !== 'scenario') payload.t = state.tab;
  const options = Object.fromEntries(Object.entries(state.options ?? {}).filter(([, value]) => value !== null && value !== ''));
  if (Object.keys(options).length > 0) payload.o = options;
  const bytes = new TextEncoder().encode(JSON.stringify(payload));
  const compressed = await transform(bytes, new CompressionStream('deflate'));
  return toBase64Url(compressed);
}

/**
 * @param {string} hash the hash without '#'
 * @returns {Promise<{ sample: string, yaml: string, tab: string, options: Record<string, unknown> }>}
 */
export async function decodeShare(hash) {
  const bytes = fromBase64Url(hash);
  const inflated = await transform(bytes, new DecompressionStream('deflate'));
  const payload = JSON.parse(new TextDecoder().decode(inflated));
  if (payload?.v !== SHARE_VERSION || typeof payload.y !== 'string' || typeof payload.s !== 'string') {
    throw new Error('not a scenetake playground link');
  }

  return {
    sample: payload.s,
    yaml: payload.y,
    tab: payload.t === 'cast' ? 'cast' : 'scenario',
    options: typeof payload.o === 'object' && payload.o !== null ? payload.o : {},
  };
}

async function transform(bytes, stream) {
  const response = new Response(new Blob([bytes]).stream().pipeThrough(stream));
  return new Uint8Array(await response.arrayBuffer());
}

function toBase64Url(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(text) {
  const base64 = text.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}
