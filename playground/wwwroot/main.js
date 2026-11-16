// The scenetake playground page. The .NET runtime runs in worker.js; this script owns the UI. A render starts
// 500 ms after the last edit; while one runs, edits only mark the state as changed, and a result that arrives
// for an older state is dropped and the latest state rendered instead.
import { encodeShare, decodeShare, MAX_SHARE_HASH_LENGTH } from './share.js';

const COLOR_MODE_KEY = 'scenetake-playground-color-mode';
const DEFAULT_SAMPLE = 'basic';
const RENDER_DELAY_MS = 500;

const $ = (id) => document.getElementById(id);

const state = {
  tab: 'scenario',
  sample: '',
  samples: [],
  casts: [],
  options: {},
  outputsInfo: null,
  sampleYaml: '',
  alive: true,
  ready: false,
};

// ---------------------------------------------------------------- worker

const worker = new Worker(new URL('./worker.js', import.meta.url), { type: 'module' });
let nextRequestId = 1;
const pending = new Map();

worker.onmessage = (event) => {
  const message = event.data;
  const entry = pending.get(message.id);
  if (!entry) return;
  pending.delete(message.id);
  if (message.type === 'failed') {
    if (message.dead) runtimeDied(message.error);
    entry.reject(new Error(message.error));
  } else {
    entry.resolve(message);
  }
};

worker.onerror = (event) => {
  runtimeDied(event.message || 'the WebAssembly worker failed to start');
};

function request(type, payload = {}) {
  if (!state.alive) return Promise.reject(new Error('runtime stopped'));
  const id = nextRequestId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    worker.postMessage({ id, type, ...payload });
  });
}

function runtimeDied(error) {
  if (!state.alive) return;
  state.alive = false;
  $('loading').hidden = true;
  const fatal = $('fatal');
  fatal.textContent = `The WebAssembly runtime stopped (${error}). Reload the page to start it again.`;
  fatal.hidden = false;
  for (const entry of pending.values()) entry.reject(new Error('runtime stopped'));
  pending.clear();
}

// ---------------------------------------------------------------- editor

let editor = null;
const yamlInput = $('yaml-input');

function createEditor() {
  if (typeof window.CodeMirror !== 'function') {
    yamlInput.addEventListener('input', () => scheduleScenarioRender());
    return;
  }

  editor = window.CodeMirror.fromTextArea(yamlInput, {
    mode: 'yaml',
    lineNumbers: true,
    indentUnit: 2,
    tabSize: 2,
    viewportMargin: Infinity,
    lineWrapping: false,
    extraKeys: { Tab: (cm) => cm.replaceSelection('  ') },
  });
  applyEditorTheme();
  editor.on('change', (_, change) => scheduleScenarioRender(change.origin === 'paste' ? 0 : RENDER_DELAY_MS));
}

function getYaml() {
  return editor ? editor.getValue() : yamlInput.value;
}

function setYaml(text) {
  if (editor) {
    editor.setValue(text);
    editor.clearHistory();
  } else {
    yamlInput.value = text;
  }
}

// ---------------------------------------------------------------- render loop

function makeRenderLoop(render) {
  let timer = 0;
  let inFlight = false;
  let again = false;

  async function run() {
    timer = 0;
    if (!state.ready || !state.alive) return;
    if (inFlight) {
      again = true;
      return;
    }

    inFlight = true;
    try {
      await render();
    } finally {
      inFlight = false;
    }

    if (again) {
      again = false;
      run();
    }
  }

  return function schedule(delay = RENDER_DELAY_MS) {
    if (timer) clearTimeout(timer);
    timer = setTimeout(run, delay);
  };
}

const scheduleScenarioRender = makeRenderLoop(renderScenario);
const scheduleCastRender = makeRenderLoop(renderCast);

const scenarioView = {
  img: $('preview'),
  empty: $('preview-empty'),
  status: $('status'),
  error: $('error'),
  warnings: $('warnings'),
  replay: $('replay-btn'),
  zoom: $('zoom-btn'),
  full: $('full-btn'),
  url: '',
  cast: null,
  svg: null,
};

const castView = {
  img: $('cast-preview'),
  empty: $('cast-preview-empty'),
  status: $('cast-status'),
  error: $('cast-error'),
  warnings: $('cast-warnings'),
  replay: $('cast-replay-btn'),
  zoom: $('cast-zoom-btn'),
  full: $('cast-full-btn'),
  url: '',
  cast: null,
  svg: null,
};

function scenarioKey() {
  return JSON.stringify([state.sample, getYaml(), state.options]);
}

async function renderScenario() {
  const key = scenarioKey();
  const yaml = getYaml();
  let reply;
  try {
    reply = await request('renderScenario', { yaml, options: state.options });
  } catch (error) {
    showError(scenarioView, error.message);
    return;
  }

  if (key !== scenarioKey()) {
    // The editor changed while this render ran; render the current text instead.
    scheduleScenarioRender(0);
    return;
  }

  showResult(scenarioView, reply);
  if (!reply.meta.error) {
    renderStepNotes(reply.meta.steps);
    renderNotApplied(reply.meta.notApplied);
  }

  $('download-yaml').disabled = false;
  $('download-cast').disabled = !scenarioView.cast;
}

async function renderCast() {
  const text = $('cast-input').value;
  if (!text.trim()) {
    showError(castView, '');
    return;
  }

  const key = JSON.stringify([text.length, text.slice(0, 256), text.slice(-256), state.options]);
  let reply;
  try {
    reply = await request('renderCast', { cast: text, options: state.options });
  } catch (error) {
    showError(castView, error.message);
    return;
  }

  const now = $('cast-input').value;
  if (key !== JSON.stringify([now.length, now.slice(0, 256), now.slice(-256), state.options])) {
    scheduleCastRender(0);
    return;
  }

  showResult(castView, reply);
}

function showResult(view, reply) {
  const meta = reply.meta;
  renderList(view.warnings, 'Warnings', meta.warnings ?? []);
  if (meta.error) {
    showError(view, meta.error);
    return;
  }

  view.error.hidden = true;
  view.cast = reply.cast && reply.cast.length > 0 ? reply.cast : null;
  view.svg = reply.svg;
  showSvg(view);
  const parts = [`${Math.round(reply.ms)} ms`];
  if (view.cast) parts.push(`cast ${formatBytes(view.cast.length)}`);
  parts.push(`SVG ${formatBytes(view.svg.length)}`);
  view.status.textContent = parts.join(' · ');
  view.replay.disabled = false;
  view.zoom.disabled = false;
  view.full.disabled = false;
  if (view === castView) $('cast-download-svg').disabled = false;
  else $('download-svg').disabled = false;
}

function showError(view, message) {
  view.error.textContent = message;
  view.error.hidden = !message;
}

function showSvg(view) {
  if (!view.svg) return;
  if (view.url) URL.revokeObjectURL(view.url);
  view.url = URL.createObjectURL(new Blob([view.svg], { type: 'image/svg+xml' }));
  view.img.src = view.url;
  setSvgMetrics(view.img, view.svg);
  view.img.hidden = false;
  view.empty.hidden = true;
}

// The preview fits the SVG to its width, but style.css stops before the terminal text gets smaller than a set size
// (13 px on a phone, the editor's size; 8 px elsewhere) and scrolls the rest sideways. For that it needs the SVG's
// width and text size. No web standard sets a minimum text size: WCAG has none, and 1.4.10 Reflow exempts content
// that needs a two-dimensional layout, such as video.
function setSvgMetrics(img, svg) {
  // The worker sends the SVG as UTF-8 bytes; the root element and the text style are in the first few hundred.
  const head = typeof svg === 'string' ? svg.slice(0, 4096) : new TextDecoder().decode(svg.subarray(0, 4096));
  const width = Number(/<svg\b[^>]*?\swidth="([\d.]+)"/.exec(head)?.[1]);
  const fontSize = Number(/text\s*\{[^}]*?font-size:\s*([\d.]+)px/.exec(head)?.[1]);
  if (width > 0 && fontSize > 0) {
    img.style.setProperty('--svg-width', String(width));
    img.style.setProperty('--svg-text', String(fontSize));
  } else {
    img.style.removeProperty('--svg-width');
    img.style.removeProperty('--svg-text');
  }
}

// Only the steps that differ from a plain replay get a line: a step with no recording, which shows a placeholder
// line, and a step whose recording replays in another terminal size or cursor position. A list of every step
// repeated the YAML, because every step of a sample as published is recorded.
function renderStepNotes(steps) {
  const notes = [];
  for (const step of steps ?? []) {
    const name = `Step ${step.index + 1}, ${shortRun(step.run)}`;
    if (!step.recorded) notes.push(`${name}: no recording has this run text, so the preview shows a placeholder line.`);
    for (const notice of step.notices ?? []) notes.push(`${name}: ${notice}.`);
  }
  renderList($('step-notes'), 'Steps that differ from the recording', notes);
}

function shortRun(run) {
  const first = run.split('\n')[0];
  return first.length > 60 || first !== run ? `${first.slice(0, 60)} …` : first;
}

function renderNotApplied(notes) {
  renderList($('not-applied'), 'Not applied in the browser', notes ?? []);
}

function renderList(container, title, items) {
  container.replaceChildren();
  container.hidden = items.length === 0;
  if (items.length === 0) return;
  const heading = document.createElement('strong');
  heading.textContent = title;
  const list = document.createElement('ul');
  for (const text of items) {
    const item = document.createElement('li');
    item.textContent = text;
    list.append(item);
  }
  container.append(heading, list);
}

function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}

// ---------------------------------------------------------------- samples

async function fetchText(path) {
  const response = await fetch(path, { cache: 'no-cache' });
  if (!response.ok) throw new Error(`${path}: HTTP ${response.status}`);
  return response.text();
}

// What each committed sample shows, so a reader can find a feature without opening every file. A sample that is
// not listed here still appears, with its name only.
const SAMPLE_NOTES = {
  basic: 'typing, step comments, colored output, stderr',
  demo: 'scenetake init, run, and agg in Docker',
  devtools: 'bat, a git graph, and jq in color',
  fetch: 'fastfetch in a macOS window',
  git: 'a git workflow',
  highlight: 'colors for commands, output ranges, stderr',
  'interactive-pty': 'Copilot CLI and vim, operated from the keyboard',
  logo: 'figlet, lolcat, and toilet banners',
  nyancat: 'a full-screen truecolor animation',
  'password-prompt': 'a sudo password kept out of the recording',
  pty: 'a full-screen TUI in a PTY',
  sl: 'the steam locomotive, drawn with curses',
  vim: 'vim edited by scripted keys, then git diff',
  quickstart: 'the README quick start',
  resize: 'a v2 cast that resizes the terminal',
  scenario_format: 'every scenario key in one file',
  'stream-output': 'output replayed with its own timing',
  theme: 'light theme, 16 and 256 colors',
  'theme-macos': 'macOS window frame',
  'theme-windows': 'Windows window frame',
};

function sampleLabel(name) {
  return SAMPLE_NOTES[name] ? `${name} — ${SAMPLE_NOTES[name]}` : name;
}

async function loadIndex() {
  const index = JSON.parse(await fetchText('samples/index.json'));
  state.samples = [...(index.scenarios ?? [])].sort();
  state.casts = [...(index.casts ?? [])].sort();
  fillSelect($('sample-select'), state.samples, sampleLabel);
  fillSelect($('cast-sample-select'), ['', ...state.casts], (name) => name ? sampleLabel(name) : 'choose…');
}

function fillSelect(select, names, label = (name) => name) {
  select.replaceChildren(...names.map((name) => {
    const option = document.createElement('option');
    option.value = name;
    option.textContent = label(name);
    return option;
  }));
}

/** Loads a sample's outputs file into the runtime, and its YAML into the editor unless a YAML is given. */
async function selectSample(name, yaml) {
  if (!state.samples.includes(name)) name = state.samples.includes(DEFAULT_SAMPLE) ? DEFAULT_SAMPLE : state.samples[0];
  if (!name) throw new Error('no samples with an outputs file are published');
  const [sampleYaml, outputsJson] = await Promise.all([
    fetchText(`samples/${name}.yaml`),
    fetchText(`samples/${name}.outputs.json`),
  ]);
  const reply = await request('loadOutputs', { json: outputsJson });
  if (reply.meta.error) throw new Error(`samples/${name}.outputs.json: ${reply.meta.error}`);
  state.sample = name;
  state.sampleYaml = sampleYaml;
  state.outputsInfo = reply.meta;
  $('sample-select').value = name;
  $('recording-info').textContent =
    ` ${reply.meta.scenetake} (${reply.meta.shell} on ${osName(reply.meta.os)}, terminal ${reply.meta.width}×${reply.meta.height})`;
  setYaml(yaml ?? sampleYaml);
  scheduleScenarioRender(0);
}

function osName(os) {
  return { windows: 'Windows', linux: 'Linux', macos: 'macOS' }[os] ?? os;
}

async function selectSampleCast(name) {
  if (!name) return;
  $('cast-input').value = await fetchText(`samples/${name}.cast`);
  castFileName = name;
  scheduleCastRender(0);
}

// ---------------------------------------------------------------- options, tabs, theme

const optionInputs = [...document.querySelectorAll('[data-option]')];

function readOptions() {
  const options = {};
  for (const input of optionInputs) {
    const key = input.dataset.option;
    const value = input.value.trim();
    if (value === '') continue;
    options[key] = input.type === 'number' ? Number(value) : value;
  }
  return options;
}

function writeOptions(options) {
  for (const input of optionInputs) {
    const value = options?.[input.dataset.option];
    input.value = value === undefined || value === null ? '' : String(value);
  }
  state.options = readOptions();
}

for (const input of optionInputs) {
  input.addEventListener('input', () => {
    state.options = readOptions();
    if (state.tab === 'cast') scheduleCastRender(300);
    else scheduleScenarioRender(300);
  });
}

function selectTab(tab) {
  state.tab = tab;
  for (const name of ['scenario', 'cast']) {
    $(`tab-${name}`).setAttribute('aria-selected', String(name === tab));
    $(`panel-${name}`).hidden = name !== tab;
  }
  // One set of render options serves both tabs; it sits under the active tab's preview. An unset option keeps the
  // value from the YAML on the Scenario tab and from the cast header on the Cast tab, and the labels say which.
  $(`panel-${tab}`).querySelector('[data-options-slot]').append(document.querySelector('details.options'));
  const source = tab === 'cast' ? 'cast' : 'YAML';
  $('options-hint').textContent = `the CLI flags; empty keeps the ${source}'s value`;
  for (const element of document.querySelectorAll('[data-from-source]')) {
    if (element instanceof HTMLOptionElement) element.textContent = `from ${source}`;
    else element.placeholder = `from ${source}`;
  }
  if (tab === 'scenario') {
    editor?.refresh();
    scheduleScenarioRender(0);
  } else if ($('cast-input').value.trim() === '' && state.casts.length > 0) {
    const castName = state.casts.includes(state.sample) ? state.sample : state.casts[0];
    $('cast-sample-select').value = castName;
    selectSampleCast(castName).catch((error) => showError(castView, error.message));
  } else {
    scheduleCastRender(0);
  }
}

$('tab-scenario').addEventListener('click', () => selectTab('scenario'));
$('tab-cast').addEventListener('click', () => selectTab('cast'));

function colorMode() {
  try {
    const value = localStorage.getItem(COLOR_MODE_KEY);
    return value === 'light' || value === 'dark' ? value : 'system';
  } catch {
    return 'system';
  }
}

function isDark() {
  const mode = colorMode();
  return mode === 'dark' || (mode === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
}

function applyColorMode(mode) {
  const root = document.documentElement;
  try {
    if (mode === 'system') localStorage.removeItem(COLOR_MODE_KEY);
    else localStorage.setItem(COLOR_MODE_KEY, mode);
  } catch { /* storage can be blocked */ }
  if (mode === 'system') root.removeAttribute('data-theme');
  else root.setAttribute('data-theme', mode);
  $('meta-color-scheme').setAttribute('content', mode === 'system' ? 'light dark' : mode);
  $('theme-btn').textContent = `Theme: ${mode}`;
  applyEditorTheme();
}

function applyEditorTheme() {
  editor?.setOption('theme', isDark() ? 'material-darker' : 'default');
}

$('theme-btn').addEventListener('click', () => {
  const order = ['system', 'light', 'dark'];
  applyColorMode(order[(order.indexOf(colorMode()) + 1) % order.length]);
});
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', applyEditorTheme);

// ---------------------------------------------------------------- buttons

function download(bytesOrText, fileName, type) {
  const url = URL.createObjectURL(new Blob([bytesOrText], { type }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

let castFileName = 'recording';

$('download-yaml').addEventListener('click', () => download(getYaml(), `${state.sample || 'scenario'}.yaml`, 'text/yaml'));
$('download-cast').addEventListener('click', () => scenarioView.cast && download(scenarioView.cast, `${state.sample || 'scenario'}.cast`, 'application/x-asciicast'));
$('download-svg').addEventListener('click', () => scenarioView.svg && download(scenarioView.svg, `${state.sample || 'scenario'}.svg`, 'image/svg+xml'));
$('cast-download-svg').addEventListener('click', () => castView.svg && download(castView.svg, `${castFileName}.svg`, 'image/svg+xml'));
$('replay-btn').addEventListener('click', () => showSvg(scenarioView));
$('cast-replay-btn').addEventListener('click', () => showSvg(castView));

// An 80-column terminal fitted to a phone's width has text about 4 px high. At its own size the SVG is readable,
// and the preview scrolls sideways.
for (const view of [scenarioView, castView]) {
  view.zoom.addEventListener('click', () => {
    const actual = view.zoom.getAttribute('aria-pressed') !== 'true';
    view.zoom.setAttribute('aria-pressed', String(actual));
    view.img.closest('.preview').classList.toggle('preview--actual', actual);
  });
}

// The full-screen view shows the whole terminal. On a 375 px upright phone the page shows the first 44 of basic's 80
// columns at 13 px; turned sideways on the whole screen, all 80 fit at 11.9 px. Lighthouse counts text under 12 px as
// too small on mobile, and Apple's guidelines set 11 pt as the minimum. The view has its own object URL, so the demo
// starts from the beginning.
const viewer = $('viewer');
const viewerImg = $('viewer-img');
let viewerUrl = '';
let viewerClosing = false;

function openViewer(view) {
  if (!view.svg || viewer.open) return;
  viewerUrl = URL.createObjectURL(new Blob([view.svg], { type: 'image/svg+xml' }));
  viewerImg.src = viewerUrl;
  viewer.showModal();
  document.documentElement.classList.add('viewer-open');
  // Back closes the view instead of leaving the page, as it does for a full-screen video on a phone.
  history.pushState({ viewer: true }, '');
}

function finishCloseViewer() {
  if (viewer.open) viewer.close();
  document.documentElement.classList.remove('viewer-open');
  viewerImg.removeAttribute('src');
  if (viewerUrl) URL.revokeObjectURL(viewerUrl);
  viewerUrl = '';
  viewerClosing = false;
}

function closeViewer() {
  // A second tap before popstate arrives must not go back a second time, which would leave the page.
  if (viewerClosing) return;
  if (history.state?.viewer) {
    viewerClosing = true;
    history.back(); // popstate finishes the close
  } else {
    finishCloseViewer();
  }
}

window.addEventListener('popstate', () => {
  if (viewer.open) finishCloseViewer();
});
viewer.addEventListener('cancel', (event) => {
  event.preventDefault();
  closeViewer();
});
viewer.addEventListener('click', closeViewer);

for (const view of [scenarioView, castView]) {
  view.full.addEventListener('click', () => openViewer(view));
  view.img.addEventListener('click', () => openViewer(view));
}

$('sample-select').addEventListener('change', (event) => {
  selectSample(event.target.value).catch((error) => showError(scenarioView, error.message));
});

$('reset-btn').addEventListener('click', () => {
  setYaml(state.sampleYaml);
  scheduleScenarioRender(0);
});

$('share-btn').addEventListener('click', async () => {
  try {
    const hash = await encodeShare({ sample: state.sample, yaml: getYaml(), tab: state.tab, options: state.options });
    if (hash.length > MAX_SHARE_HASH_LENGTH) {
      scenarioView.status.textContent = 'The scenario is too long for a link. Download the .yaml instead.';
      return;
    }
    history.replaceState(null, '', `#${hash}`);
    await navigator.clipboard.writeText(location.href);
    scenarioView.status.textContent = 'Link copied.';
  } catch (error) {
    scenarioView.status.textContent = `Could not copy the link: ${error.message}`;
  }
});

$('cast-sample-select').addEventListener('change', (event) => {
  selectSampleCast(event.target.value).catch((error) => showError(castView, error.message));
});

$('cast-input').addEventListener('input', () => {
  $('cast-sample-select').value = '';
  castFileName = 'recording';
  scheduleCastRender();
});

async function openCastFile(file) {
  if (!file) return;
  $('cast-input').value = await file.text();
  $('cast-sample-select').value = '';
  castFileName = file.name.replace(/\.cast$/i, '') || 'recording';
  scheduleCastRender(0);
}

$('cast-file').addEventListener('change', (event) => {
  openCastFile(event.target.files?.[0]).catch((error) => showError(castView, error.message));
  event.target.value = '';
});

const drop = $('cast-drop');
drop.addEventListener('dragover', (event) => {
  event.preventDefault();
  drop.classList.add('drop--over');
});
drop.addEventListener('dragleave', () => drop.classList.remove('drop--over'));
drop.addEventListener('drop', (event) => {
  event.preventDefault();
  drop.classList.remove('drop--over');
  openCastFile(event.dataTransfer?.files?.[0]).catch((error) => showError(castView, error.message));
});

// ---------------------------------------------------------------- start

async function start() {
  applyColorMode(colorMode());
  createEditor();

  let shared = null;
  let shareError = '';
  if (location.hash.length > 1) {
    try {
      shared = await decodeShare(location.hash.slice(1));
    } catch (error) {
      shareError = `Could not restore the shared scenario (${error.message}). Showing the default sample.`;
    }
  }

  try {
    const [ready] = await Promise.all([request('init'), loadIndex()]);
    const version = $('version');
    version.textContent = `v${ready.version}`;
    version.href = `https://github.com/guitarrapc/scenetake/releases/tag/v${ready.version}`;
    version.hidden = false;
    state.ready = true;
    $('loading').hidden = true;
    writeOptions(shared?.options ?? {});
    // On a narrow screen the options start closed; a shared URL that sets some shows them.
    if (Object.keys(state.options).length > 0) document.querySelector('details.options').open = true;
    await selectSample(shared?.sample ?? DEFAULT_SAMPLE, shared?.yaml);
    if (shared?.tab === 'cast') selectTab('cast');
    if (shareError) scenarioView.status.textContent = shareError;
  } catch (error) {
    $('loading').hidden = true;
    if (state.alive) showError(scenarioView, error.message);
  }
}

start();
