namespace FsPlay
open Microsoft.Maui.Controls
open FsPlay.MauiWebViewDriver

module Common =
    let script x y = $"""(function() {{
  const x = window.innerWidth * {x};
  const y = window.innerHeight * {y};

  const element = document.elementFromPoint(x, y);
  if (element) {{
    element.click();
  }}
}})();

"""

type Service =
    static member capture (wv:WebView) =
#if IOS || MACCATALYST
        let svc : IWebViewService = new FsPlay.ios.WebViewService()
        svc.Capture(wv)
#else
    #if ANDROID 
        let svc : IWebViewService = new FsPlay.droid.WebViewService()
        svc.Capture(wv)
    #else 
        task {failwith "unsupported platform"}
    #endif
#endif

    static member createWebViewWrapper(wv:WebView) =
            {new WebViewWrapper with
                member this.CaptureAsync() = Service.capture(wv)
                member this.EvaluateJavaScriptAsync(js) = wv.EvaluateJavaScriptAsync(js)
                member this.GoBack() = wv.GoBack()
                member this.GoForward() = wv.GoForward()
                member this.Reload() = wv.Reload()
                member this.CanGoBack = wv.CanGoBack
                member this.Source            
                    with get() =
                        match wv.Source with
                        | :? UrlWebViewSource as s -> s.Url
                        | _ -> ""                        
                    and set(value) = wv.Source <- UrlWebViewSource(Url=value)
                member this.CanGoForward = wv.CanGoForward
            }

            
