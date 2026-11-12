// Gespeicherte Darstellung vor dem ersten Zeichnen anwenden (kein Aufblitzen im falschen Schema).
(function () {
    try {
        var theme = localStorage.getItem('bp.theme');
        if (theme === 'light' || theme === 'dark') { document.documentElement.setAttribute('data-theme', theme); }
    } catch (e) { /* Speicher blockiert: es gilt das Systemschema */ }
})();
// Sprache: Wahl je Gerät lokal speichern ("system" entfernt sie) und das lang-Attribut setzen.
window.bpLang = {
    get: function () { try { return localStorage.getItem('bp.lang'); } catch (e) { return null; } },
    set: function (choice, language) {
        document.documentElement.setAttribute('lang', language);
        try {
            if (choice === 'de' || choice === 'en') { localStorage.setItem('bp.lang', choice); } else { localStorage.removeItem('bp.lang'); }
        } catch (e) { /* Speicher blockiert: gilt bis zum Neuladen */ }
    }
};
window.bpTheme = {
    get: function () { try { return localStorage.getItem('bp.theme'); } catch (e) { return null; } },
    set: function (theme) {
        if (theme === 'light' || theme === 'dark') { document.documentElement.setAttribute('data-theme', theme); }
        else { document.documentElement.removeAttribute('data-theme'); }
        try {
            if (theme === 'light' || theme === 'dark') { localStorage.setItem('bp.theme', theme); } else { localStorage.removeItem('bp.theme'); }
        } catch (e) { /* Speicher blockiert: gilt bis zum Neuladen */ }
    }
};
