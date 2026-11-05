// Erzeugt die Screenshots der Blazor-Oberfläche für das README (docs/screenshots/web-*.png).
//
// Erwartet eine laufende API und UI wie für check.mjs (API_URL, UI_URL) und legt einen Testnutzer mit Beispieldaten an
// (Passwort wird zur Laufzeit erzeugt und nie gespeichert). Die Werte sind frei erfunden und keine Gesundheitsdaten.
//   node screenshots.mjs            (nutzt A11Y_CHROMIUM wie check.mjs, falls gesetzt)
import { randomBytes } from 'node:crypto';
import { mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const API = process.env.API_URL ?? 'http://127.0.0.1:5000';
const UI = process.env.UI_URL ?? 'http://localhost:5296';
const OUT = resolve(dirname(fileURLToPath(import.meta.url)), '../../docs/screenshots');

const email = `demo-${randomBytes(4).toString('hex')}@example.test`;
const credential = randomBytes(12).toString('hex').toUpperCase() + randomBytes(12).toString('hex') + '1!';

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
  // 28 Tage, morgens und abends, leicht schwankend, mit gemischten Kategorien und einigen höheren Werten.
  for (let i = 28; i >= 1; i--) {
    for (const [hour, offset] of [[8, 4], [20, -2]]) {
      const wave = Math.round(6 * Math.sin(i / 2.3));
      const trend = Math.round((i - 14) / 5);
      const systolic = 120 + offset + wave + trend + (i % 9 === 0 ? 17 : 0);
      const diastolic = 67 + Math.round(offset / 2) + Math.round(wave / 2) + Math.round(trend / 2);
      const at = new Date(Date.now() - i * day);
      at.setUTCHours(hour - 1, (i * 7) % 50, 0, 0);
      await api('/api/v1/BloodPressure', { method: 'POST', body: JSON.stringify({
        systolisch: systolic, diastolisch: diastolic, dateTime: at.toISOString(), pulse: 62 + ((i * 3) % 14), oxygen: 96 + (i % 3),
      }) }, accessToken);
    }
  }
  // Ein stark erhöhter Wert erzeugt einen Hinweis.
  await api('/api/v1/BloodPressure', { method: 'POST', body: JSON.stringify({
    systolisch: 186, diastolisch: 112, dateTime: new Date(Date.now() - 2 * 3600 * 1000).toISOString(), pulse: 88, oxygen: 96,
  }) }, accessToken);
  await api('/api/v1/reminders', { method: 'PUT', body: JSON.stringify({ timeZoneId: 'Europe/Berlin', times: ['08:00', '20:00'] }) }, accessToken);
  await api('/api/v1/profile', { method: 'PUT', body: JSON.stringify({ age: 42, heightCm: 180, weightKg: 82.5 }) }, accessToken);
}

async function open(page, path) {
  await page.goto(`${UI}/${path}`);
  await page.waitForSelector('main h1, article h1', { timeout: 30000 });
  await page.waitForFunction(() => !document.querySelector('[aria-busy="true"]'), null, { timeout: 15000 });
  await page.addStyleTag({ content: '*, *::before, *::after { transition: none !important; animation: none !important; }' });
  await page.waitForTimeout(300);
}

async function session(browser, { width, height, scheme = 'light', lang = null, theme = null }) {
  const context = await browser.newContext({ viewport: { width, height }, colorScheme: scheme, locale: 'de-DE', deviceScaleFactor: 1 });
  await context.addInitScript(({ lang, theme }) => {
    try {
      if (lang) localStorage.setItem('bp.lang', lang);
      if (theme) localStorage.setItem('bp.theme', theme);
    } catch (e) { /* ignorieren */ }
  }, { lang, theme });
  const page = await context.newPage();
  await page.goto(`${UI}/login`);
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(credential);
  await page.locator('#login-button').click();
  await page.waitForURL('**/measurements', { timeout: 30000 });
  return { context, page };
}

await mkdir(OUT, { recursive: true });
await seed();
const browser = await chromium.launch(process.env.A11Y_CHROMIUM ? { executablePath: process.env.A11Y_CHROMIUM } : {});
try {
  // Desktop, hell, deutsch
  {
    const { context, page } = await session(browser, { width: 1280, height: 860 });
    await open(page, 'measurements');
    await page.screenshot({ path: `${OUT}/web-messwerte.png` });
    await open(page, 'statistics');
    await page.screenshot({ path: `${OUT}/web-auswertung.png` });
    await open(page, 'notifications');
    await page.screenshot({ path: `${OUT}/web-hinweise.png` });
    await context.close();
  }
  // Desktop, dunkel, englisch: Profil mit Darstellung und Sprache
  {
    const { context, page } = await session(browser, { width: 1280, height: 900, scheme: 'dark', lang: 'en', theme: 'dark' });
    await open(page, 'profile');
    await page.screenshot({ path: `${OUT}/web-profil-dunkel-en.png`, fullPage: true });
    await context.close();
  }
  // Smartphone-Breite, hell
  {
    const { context, page } = await session(browser, { width: 375, height: 812 });
    await open(page, 'measurements');
    await page.screenshot({ path: `${OUT}/web-mobil-messwerte.png` });
    await context.close();
  }
  console.log(`Screenshots in ${OUT}`);
} finally {
  await browser.close();
}
