window.getBrowserLanguage = () => navigator.language;
window.getBrowserTimeZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone;
window.bpDownload = async (fileName, streamReference) => {
    const buffer = await streamReference.arrayBuffer();
    const url = URL.createObjectURL(new Blob([buffer]));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
};
