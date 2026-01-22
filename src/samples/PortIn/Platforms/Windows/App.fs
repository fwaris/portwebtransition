namespace FsPlaySamples.PortIn.WinUI

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
type App() =
    inherit FSharp.Maui.WinUICompat.App()

    override this.CreateMauiApp() = FsPlaySamples.PortIn.MauiProgram.CreateMauiApp()
    
