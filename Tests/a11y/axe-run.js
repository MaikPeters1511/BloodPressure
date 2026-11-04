// Barrierefreiheits- und Mobilprüfung der Blazor-Oberfläche (lokal, im Browser).
//
// Ablauf (siehe docs/a11y/screenreader-pruefung.md, Abschnitt "Automatisierte Prüfung"):
//   1. API + UI lokal starten, Testnutzer mit Messungen/Hinweisen/Erinnerungen anlegen, anmelden.
//   2. Diese Datei in der Browser-Konsole der angemeldeten Anwendung ausführen (Viewport 375 px und 320 px, hell und dunkel).
//   3. Ergebnis: je Seite Verstöße von axe-core (WCAG 2.0/2.1/2.2 A und AA), Seitenüberlauf und zu kleine Bedienelemente.
// Zulässig sind nur Ergebnisse ohne Verstöße der Schwere "serious"/"critical", ohne Seitenüberlauf und ohne Elemente < 24 px.
(async () => {
  await new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://cdnjs.cloudflare.com/ajax/libs/axe-core/4.10.2/axe.min.js';
    script.onload = resolve;
    script.onerror = () => reject(new Error('axe-core konnte nicht geladen werden'));
    document.head.appendChild(script);
  });

  const wait = (ms) => new Promise((r) => setTimeout(r, ms));
  const result = { scheme: matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark', viewport: innerWidth, pages: {} };

  for (const path of ['measurements', 'statistics', 'export', 'reminders', 'notifications']) {
    const toggler = document.querySelector('.navbar-toggler');
    if (toggler && toggler.getAttribute('aria-expanded') === 'false') toggler.click();
    await wait(300);
    [...document.querySelectorAll('a[href]')].find((a) => a.getAttribute('href') === path)?.click();
    await wait(1800);

    const axeResult = await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } });
    const small = [...document.querySelectorAll('a[href],button,input,select,textarea')]
      .filter((e) => e.offsetParent !== null && !e.classList.contains('visually-hidden'))
      .map((e) => ({ el: e.id || e.tagName, w: Math.round(e.getBoundingClientRect().width), h: Math.round(e.getBoundingClientRect().height) }))
      .filter((x) => x.w < 24 || x.h < 24);

    result.pages[path] = {
      violations: axeResult.violations.map((v) => `${v.id}:${v.impact}:${v.nodes.length}`),
      overflow: document.documentElement.scrollWidth > innerWidth,
      tooSmall: small,
    };
  }
  return JSON.stringify(result);
})();
