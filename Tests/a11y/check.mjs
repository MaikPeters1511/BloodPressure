// Automatisierte Barrierefreiheits- und Mobilprüfung der Blazor-Oberfläche (axe-core + Playwright).
//
// Erwartet eine laufende API und UI (lokal oder in der CI):
//   API_URL (Standard http://localhost:5000), UI_URL (Standard http://localhost:5296)
// Legt einen Testnutzer mit Testdaten an (Passwort wird zur Laufzeit erzeugt, nie gespeichert), meldet sich an und prüft
// jede Seite bei 1280, 375 und 320 px Breite in heller und dunkler Darstellung.
// Fehler: Verstöße der Schwere "serious"/"critical", horizontaler Seitenüberlauf, Bedienelemente unter 24 x 24 px.
import { randomBytes } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import { chromium } from 'playwright';
import AxeBuilder from '@axe-core/playwright';

const API = process.env.API_URL ?? 'http://localhost:5000';
const UI = process.env.UI_URL ?? 'http://localhost:5296';
const PAGES = ['measurements', 'statistics', 'export', 'reminders', 'notifications', 'profile'];
const VIEWPORTS = [{ width: 1280, height: 800 }, { width: 375, height: 812 }, { width: 320, height: 568 }];
// Systemschema; zusätzlich (nur bei 1280 px) die ausdrücklich gewählte Darstellung, die dem Systemschema widerspricht.
const VARIANTS = [
  { scheme: 'light', forced: null }, { scheme: 'dark', forced: null },
  { scheme: 'light', forced: 'dark' }, { scheme: 'dark', forced: 'light' },
];
const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

const email = `a11y-${randomBytes(4).toString('hex')}@example.test`;
// Einmalige Zufallswerte (Großbuchstabe, Ziffer, Sonderzeichen für die Passwortregeln der Identity-Konfiguration).
const credential = `${randomBytes(12).toString('hex')}`.toUpperCase() + randomBytes(12).toString('hex') + '1!';

async function api(path, options = {}, token) {
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
  });
  if (!response.ok) throw new Error(`${options.method ?? 'GET'} ${path} -> ${response.status}`);
  return response;
}

async function seed() {
  await api('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password: credential }) });
  const { accessToken } = await (await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password: credential }) })).json();
  const day = 24 * 3600 * 1000;
  for (let i = 1; i <= 12; i++) {
    const body = { systolisch: 115 + i * 3, diastolisch: 74 + i, dateTime: new Date(Date.now() - i * day).toISOString(), pulse: 60 + i, oxygen: 97 };
    await api('/api/v1/BloodPressure', { method: 'POST', body: JSON.stringify(body) }, accessToken);
  }
  // Kritischer Wert -> erzeugt einen Hinweis (Warnung).
  await api('/api/v1/BloodPressure', { method: 'POST', body: JSON.stringify({ systolisch: 195, diastolisch: 120, dateTime: new Date().toISOString(), pulse: 90, oxygen: 96 }) }, accessToken);
  await api('/api/v1/reminders', { method: 'PUT', body: JSON.stringify({ timeZoneId: 'Europe/Berlin', times: ['08:00', '20:00'] }) }, accessToken);
}

async function openPage(page, path) {
  await page.goto(`${UI}/${path}`);
  await page.waitForSelector('main h1, article h1', { timeout: 30000 });
  await page.waitForFunction(() => !document.querySelector('[aria-busy="true"]'), null, { timeout: 15000 });
  // Übergänge abschalten: axe würde sonst Zwischenfarben einer laufenden Farbanimation messen (Scheinbefunde).
  await page.addStyleTag({ content: '*, *::before, *::after { transition: none !important; animation: none !important; }' });
  await page.waitForTimeout(100);
}

const results = [];
let failures = 0;

await seed();
// A11Y_CHROMIUM: optional Pfad zu einem vorhandenen Chromium (lokal); in der CI installiert Playwright den Browser.
const browser = await chromium.launch(process.env.A11Y_CHROMIUM ? { executablePath: process.env.A11Y_CHROMIUM } : {});
try {
  for (const { scheme: colorScheme, forced } of VARIANTS) {
    for (const viewport of VIEWPORTS) {
      if (forced && viewport.width !== 1280) continue;
      // Feste deutsche Browsersprache: die Oberfläche richtet sich nach navigator.language, die Prüfung nutzt die deutschen Beschriftungen.
      const context = await browser.newContext({ viewport, colorScheme, locale: 'de-DE', reducedMotion: 'reduce' });
      if (forced) await context.addInitScript((value) => { try { localStorage.setItem('bp.theme', value); } catch (e) { /* ignorieren */ } }, forced);
      const page = await context.newPage();

      await page.goto(`${UI}/login`);
      // Bei Fehlern ein Bild ablegen, damit CI-Fehlschläge ohne Raten auswertbar sind.
      await page.getByLabel('E-Mail').waitFor({ timeout: 30000 }).catch(async (error) => {
        await page.screenshot({ path: 'a11y-failure.png' });
        throw error;
      });
      await page.getByLabel('E-Mail').fill(email);
      await page.getByLabel('Passwort').fill(credential);
      await page.getByRole('button', { name: 'Anmelden' }).click();
      await page.waitForURL('**/measurements', { timeout: 30000 });

      for (const path of PAGES) {
        await openPage(page, path);
        const axe = await new AxeBuilder({ page }).withTags(TAGS).analyze();
        const blocking = axe.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical');
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
        const tooSmall = await page.evaluate(() =>
          [...document.querySelectorAll('a[href],button,input,select,textarea')]
            .filter((e) => e.offsetParent !== null && !e.classList.contains('visually-hidden'))
            .map((e) => ({ el: e.id || e.tagName, w: Math.round(e.getBoundingClientRect().width), h: Math.round(e.getBoundingClientRect().height) }))
            .filter((x) => x.w < 24 || x.h < 24));

        // Ausdrücklich gewählte Darstellung: Attribut gesetzt und die Textfarbe passt (dunkel -> heller Text, hell -> dunkler Text).
        // Die Farbe wird über ein Canvas in RGB umgerechnet (das Stylesheet verwendet oklch()).
        let themeProblem = false;
        if (forced) {
          const state = await page.evaluate(() => {
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 1;
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = getComputedStyle(document.body).color;
            ctx.fillRect(0, 0, 1, 1);
            const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
            return { attr: document.documentElement.getAttribute('data-theme'), luminance: (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255 };
          });
          themeProblem = state.attr !== forced || (forced === 'dark' ? state.luminance < 0.5 : state.luminance > 0.5);
        }

        const failed = blocking.length > 0 || overflow || tooSmall.length > 0 || themeProblem;
        if (failed) failures++;
        results.push({
          page: path, width: viewport.width, scheme: colorScheme, forced, failed, themeProblem,
          violations: axe.violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.map((n) => n.target.join(' ')) })),
          overflow, tooSmall,
        });
        console.log(`${failed ? 'FAIL' : 'ok  '} ${colorScheme.padEnd(5)} ${(forced ?? '-').padEnd(5)} ${String(viewport.width).padStart(4)} px  ${path}` +
          (axe.violations.length ? `  axe: ${axe.violations.map((v) => `${v.id}(${v.impact})`).join(', ')}` : '') +
          (themeProblem ? '  theme' : '') + (overflow ? '  overflow' : '') + (tooSmall.length ? `  tooSmall: ${tooSmall.map((x) => x.el).join(',')}` : ''));
      }
      await context.close();
    }
  }
} finally {
  await browser.close();
  await writeFile('a11y-results.json', JSON.stringify(results, null, 2));
}

console.log(`\n${results.length} Prüfungen, ${failures} fehlgeschlagen`);
process.exit(failures > 0 ? 1 : 0);
