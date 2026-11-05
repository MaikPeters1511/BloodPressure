using Foundation;

namespace CleanArchitecture.Presentation.Maui;

// iOS/macOS 26+ verlangen die Scene-Lifecycle-Anbindung; ohne sie bricht die App beim Start ab
// ("UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption"). Die Scene-Konfiguration steht in der Info.plist.
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
