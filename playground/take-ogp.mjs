// Takes the playground's OGP image (playground/wwwroot/ogp.png): the page at 1200 × 670 in the dark theme, with a
// sample's YAML and its demo, as the seiton playground's ogp.png shows its page.
//
// Usage, from the repository root, with the playground served (the "playground" entry in .claude/launch.json, or
// node playground/preview.mjs):
//   npm install --no-save playwright && npx playwright install chromium
//   node playground/take-ogp.mjs
//
// Options:
//   --url <url>        The served playground (default: http://127.0.0.1:5180/)
//   --sample <name>    The sample to show (default: fetch)
//   --time <seconds>   The moment of the demo to show; the SVG's animations are paused there (default: 6)
//   --output <path>    Output file (default: playground/wwwroot/ogp.png)
//   --channel <name>   A browser Playwright did not download, such as msedge or chrome

import { chromium } from 'playwright';
import { parseArgs } from 'node:util';
import { stat } from 'node:fs/promises';

const { values: args } = parseArgs({
  options: {
    url: { type: 'string', default: 'http://127.0.0.1:5180/' },
    sample: { type: 'string', default: 'fetch' },
    time: { type: 'string', default: '6' },
    output: { type: 'string', default: 'playground/wwwroot/ogp.png' },
    channel: { type: 'string' },
  },
});

// The size of seiton's image, which X and other sites show without cropping the sides.
const WIDTH = 1200;
const HEIGHT = 670;

const browser = await chromium.launch({ headless: true, ...(args.channel && { channel: args.channel }) });
try {
  const context = await browser.newContext({ viewport: { width: WIDTH, height: HEIGHT }, colorScheme: 'dark' });
  // Before the page loads, so the inline script in <head> picks the theme on the first paint.
  await context.addInitScript(() => localStorage.setItem('scenetake-playground-color-mode', 'dark'));
  const page = await context.newPage();
  await page.goto(args.url, { waitUntil: 'networkidle' });
  await page.waitForSelector('.CodeMirror', { timeout: 120_000 });
  await page.waitForFunction(() => document.getElementById('loading')?.hidden, null, { timeout: 120_000 });

  // The default sample renders first. Select the sample after that, and wait for its demo to replace it.
  const preview = '#panel-scenario img';
  await page.waitForFunction((selector) => document.querySelector(selector)?.src.startsWith('blob:'), preview, { timeout: 60_000 });
  if (await page.inputValue('#sample-select') !== args.sample) {
    const before = await page.$eval(preview, (img) => img.src);
    await page.selectOption('#sample-select', args.sample);
    await page.waitForFunction(
      ([selector, previous]) => {
        const src = document.querySelector(selector)?.src ?? '';
        return src.startsWith('blob:') && src !== previous;
      },
      [preview, before],
      { timeout: 60_000 });
  }
  await page.waitForTimeout(1000);

  // The version changes with each release, so it stays out of the image, as in seiton's.
  await page.addStyleTag({ content: '.version-badge { display: none !important; }' });

  // An <img> SVG plays on its own clock. The same SVG in an <object> exposes its animations, so every one of them
  // can be paused at the same moment, and the image shows that frame whenever it is taken.
  await page.evaluate(async (seconds) => {
    const img = document.querySelector('#panel-scenario img');
    const object = document.createElement('object');
    object.type = 'image/svg+xml';
    object.data = img.src;
    object.className = img.className;
    object.style.cssText = img.style.cssText;
    object.width = img.width;
    object.height = img.height;
    await new Promise((resolve) => {
      object.addEventListener('load', resolve, { once: true });
      img.replaceWith(object);
    });
    for (const animation of object.contentDocument.getAnimations()) {
      animation.pause();
      animation.currentTime = seconds * 1000;
    }
  }, Number(args.time));
  await page.waitForTimeout(300);

  await page.screenshot({ path: args.output });
  const { size } = await stat(args.output);
  console.log(`Saved: ${args.output} (${(size / 1024).toFixed(1)} KB, ${WIDTH}x${HEIGHT})`);
} finally {
  await browser.close();
}
