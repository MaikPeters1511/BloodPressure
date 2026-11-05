using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Services;

public sealed class MauiShareService : IShareService
{
    public async Task ShareFileAsync(string fileName, Stream content, string title)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        try
        {
            await using (var file = File.Create(path))
            {
                await content.CopyToAsync(file);
            }

            await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path))));
        }
        finally
        {
            // Die temporäre Datei enthält Gesundheitsdaten und wird nach dem Teilen entfernt.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
