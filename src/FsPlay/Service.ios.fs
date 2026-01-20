namespace FsPlay.ios
#if IOS || MACCATALYST

open System
open System.Diagnostics
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open Foundation
open FsPlay
open Microsoft.Maui.Handlers
open Microsoft.Maui.Platform
open UIKit
open CoreGraphics
open WebKit
open Microsoft.Maui.Controls
open Microsoft.Maui.ApplicationModel
open Microsoft.FSharp.Control
open ObjCRuntime

module internal IosScripts =
    let js = """
document.addEventListener('touchend', function() {
    window.webkit.messageHandlers.touchHandler.postMessage("touchend");
});

document.addEventListener('input', function(e) {
    window.webkit.messageHandlers.touchHandler.postMessage(
        JSON.stringify({ type: "input", value: e.target.value })
    );
});
"""
    
    let userScript = new WKUserScript(
            new NSString(js),
            WKUserScriptInjectionTime.AtDocumentEnd,
            true // forMainFrameOnly
    )
    
    let bootstrapScript = new WKUserScript(
            new NSString(Bootstrap.bootstrapScript),
            WKUserScriptInjectionTime.AtDocumentStart,
            false,
            WKContentWorld.Page // forMainFrameOnly
    )

type ProxySchemeHandler
    (
        ?scheme: string,
        ?forwardScheme: string,
        ?httpClient: HttpClient
    ) =
    inherit NSObject()

    static let nonceCache = ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase)

    let schemeName = defaultArg scheme "appx"
    let upstreamScheme = defaultArg forwardScheme Uri.UriSchemeHttps
    let client = defaultArg httpClient (new HttpClient())
    let pending = ConcurrentDictionary<IWKUrlSchemeTask, CancellationTokenSource>()
    let blockedHeaders : Set<string> = Set.empty

    let rewriteUri (source: NSUrl) =
        let builder = UriBuilder(source.AbsoluteString)
        builder.Scheme <- upstreamScheme
        if builder.Port < 0 then
            builder.Port <- if upstreamScheme = Uri.UriSchemeHttps then 443 else 80
        builder.Uri

    let ensureToken (token: string) (tokens: string list) : string list =
        if tokens |> List.exists (fun t -> t.Equals(token, StringComparison.OrdinalIgnoreCase)) then tokens else tokens @ [token]

    let dedupeTokens (tokens: string list) : string list =
        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        tokens
        |> List.fold (fun acc token -> if seen.Add(token) then token :: acc else acc) []
        |> List.rev

    let addHostToken (host: string) (tokens: string list) : string list =
        if String.IsNullOrWhiteSpace(host) then tokens
        else
            let trimmed = host.Trim()
            let withHost = ensureToken trimmed tokens
            if trimmed.StartsWith("'", StringComparison.Ordinal)
               || trimmed.StartsWith("*.", StringComparison.Ordinal)
               || trimmed.Contains("://", StringComparison.Ordinal)
               || trimmed.EndsWith(":", StringComparison.Ordinal) then
                withHost
            else
                ensureToken ($"{upstreamScheme}://{trimmed}") withHost

    let networkDirectives =
        HashSet<string>(
            [| "default-src"
               "script-src"
               "script-src-elem"
               "script-src-attr"
               "style-src"
               "style-src-elem"
               "style-src-attr"
               "img-src"
               "font-src"
               "connect-src"
               "frame-src"
               "child-src"
               "worker-src"
               "media-src"
               "manifest-src"
               "prefetch-src"
               "object-src"
               "base-uri"
               "form-action"
               "navigate-to" |],
            StringComparer.OrdinalIgnoreCase)

    let ensureAuthoritySources (host: string) (tokens: string list) : string list =
        let withHost =
            if String.IsNullOrWhiteSpace(host) then tokens
            else ensureToken ($"{upstreamScheme}://{host}") tokens

        let expanded =
            withHost
            |> List.collect (fun token ->
                let trimmed = token.Trim()
                if String.IsNullOrWhiteSpace(trimmed) then []
                elif trimmed.StartsWith("'", StringComparison.Ordinal) then [trimmed]
                elif trimmed.IndexOf("://", StringComparison.Ordinal) >= 0 then [trimmed]
                elif trimmed.EndsWith(":", StringComparison.Ordinal) then [trimmed]
                else
                    let absolute = $"{upstreamScheme}://{trimmed}"
                    [trimmed; absolute])

        expanded |> dedupeTokens

    let collectPolicyHosts (segments: string[]) : string list =
        let skipPrefixes =
            [| "data:"
               "blob:"
               "filesystem:"
               "mediastream:"
               "appx:"
               "ws:"
               "wss:"
               "http:"
               "https:"
               "ftp:"
               "mailto:"
               "tel:"
               "file:"
               "chrome:"
               "edge:" |]

        let shouldSkipPrefix (token: string) =
            skipPrefixes |> Array.exists (fun prefix -> token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))

        let candidates = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        let addCandidate (token: string) =
            let trimmed = token.Trim()
            if String.IsNullOrWhiteSpace(trimmed) then
                ()
            elif trimmed = "*" then
                ()
            elif trimmed.StartsWith("'", StringComparison.Ordinal) then
                ()
            elif shouldSkipPrefix trimmed then
                ()
            elif trimmed.Equals("self", StringComparison.OrdinalIgnoreCase) then
                ()
            elif trimmed.Equals("none", StringComparison.OrdinalIgnoreCase) then
                ()
            elif trimmed.StartsWith("*.", StringComparison.Ordinal) then
                candidates.Add(trimmed) |> ignore
                let bare = trimmed.Substring(2)
                if bare.Contains(".") then candidates.Add(bare) |> ignore
            else
                let mutable uri = Unchecked.defaultof<Uri>
                if trimmed.Contains("://") && Uri.TryCreate(trimmed, UriKind.Absolute, &uri) then
                    if not (String.IsNullOrWhiteSpace uri.Host) then
                        candidates.Add(uri.Host) |> ignore
                    candidates.Add(trimmed) |> ignore
                else
                    let hostPart =
                        match trimmed.IndexOf('/') with
                        | idx when idx > 0 -> trimmed.Substring(0, idx)
                        | _ -> trimmed

                    if hostPart.Contains('.') then
                        candidates.Add(hostPart) |> ignore
                    candidates.Add(trimmed) |> ignore

        segments
        |> Array.iter (fun segment ->
            let parts = segment.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            if parts.Length > 1 then
                parts
                |> Array.skip 1
                |> Array.iter addCandidate)

        candidates |> Seq.toList

    let adjustDirective (directive: string) (tokens: string list) (host: string) (policyHosts: string list) : string list * string option =
        let lower = directive.ToLowerInvariant()
        let mutable capturedNonce = None

        let baseTokens =
            tokens
            |> List.map (fun token ->
                if token.StartsWith("'nonce-", StringComparison.Ordinal) then
                    let trimmed = token.Trim('\'')
                    if trimmed.StartsWith("nonce-", StringComparison.Ordinal) then
                        capturedNonce <- Some(trimmed.Substring("nonce-".Length))
                token)

        let baseWithDirectiveAdjustments =
            match lower with
            | "script-src"
            | "script-src-elem"
            | "script-src-attr" ->
                baseTokens
                |> ensureToken "'unsafe-inline'"
                |> ensureToken "'unsafe-eval'"
                |> ensureAuthoritySources host
            | "style-src"
            | "style-src-elem"
            | "style-src-attr" ->
                baseTokens
                |> ensureToken "'unsafe-inline'"
                |> ensureAuthoritySources host
            | "connect-src"
            | "frame-src"
            | "child-src"
            | "worker-src"
            | "media-src" ->
                baseTokens
                |> ensureAuthoritySources host
            | _ -> baseTokens

        let finalTokens =
            let withPolicyHosts =
                if networkDirectives.Contains(lower) then
                    policyHosts |> List.fold (fun acc hostToken -> addHostToken hostToken acc) baseWithDirectiveAdjustments
                else baseWithDirectiveAdjustments

            withPolicyHosts |> dedupeTokens

        finalTokens, capturedNonce

    let adjustCsp (host: string) (value: string) : string * string option =
        let mutable discoveredNonce = None

        let segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries)
        let policyHosts = collectPolicyHosts segments

        let rewritten =
            segments
            |> Array.map (fun segment ->
                let trimmed = segment.Trim()
                if String.IsNullOrWhiteSpace(trimmed) then
                    trimmed
                else
                    let parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    if parts.Length = 0 then trimmed else
                    let directive = parts.[0]
                    let tokens = parts |> Array.skip 1 |> Array.toList
                    let updatedTokens, nonce = adjustDirective directive tokens host policyHosts
                    match nonce, discoveredNonce with
                    | Some n, None -> discoveredNonce <- Some n
                    | _ -> ()
                    if List.isEmpty updatedTokens then directive
                    else directive + " " + String.Join(" ", updatedTokens)
            )
            |> Array.map (fun seg -> seg.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> fun segments -> String.Join("; ", segments)

        match discoveredNonce with
        | Some n when (String.IsNullOrWhiteSpace(host) |> not) && (String.IsNullOrWhiteSpace(n) |> not) ->
            nonceCache.AddOrUpdate(host, n, fun _ _ -> n) |> ignore
        | _ -> ()

        rewritten, discoveredNonce

    let mapHeaders (response: HttpResponseMessage) (host: string) =
        response.Headers
        |> Seq.append response.Content.Headers
        |> Seq.collect (fun kv -> kv.Value |> Seq.map (fun value -> kv.Key, value))
        |> Seq.map (fun (k, v) ->
            let adjusted =
                if k.Equals("content-security-policy", StringComparison.OrdinalIgnoreCase) then
                    fst (adjustCsp host v)
                else
                    v
            k, adjusted)
        |> Seq.filter (fun (k, _) -> blockedHeaders |> Set.contains (k.ToLowerInvariant()) |> not)
        |> Seq.toArray
        |> fun pairs ->
            if pairs.Length = 0 then
                new NSDictionary()
            else
                let keys = pairs |> Array.map (fun (k, _) -> new NSString(k) :> NSObject)
                let values = pairs |> Array.map (fun (_, v) -> new NSString(v) :> NSObject)
                NSDictionary.FromObjectsAndKeys(values, keys)

    let tryRemove (key: IWKUrlSchemeTask) =
        let mutable registration = Unchecked.defaultof<CancellationTokenSource>
        if pending.TryRemove(key, &registration) then Some registration else None

    static member SaveNonce(host: string, nonce: string) =
        if String.IsNullOrWhiteSpace(host) |> not && String.IsNullOrWhiteSpace(nonce) |> not then
            nonceCache.AddOrUpdate(host, nonce, fun _ _ -> nonce) |> ignore

    static member TryGetNonce(host: string) =
        match nonceCache.TryGetValue(host) with
        | true, nonce when not (String.IsNullOrWhiteSpace(nonce)) -> Some nonce
        | _ -> None

    member _.Scheme = schemeName

    interface IWKUrlSchemeHandler with
        member _.StartUrlSchemeTask(_, task) =
            let cts = new CancellationTokenSource()
            pending.TryAdd(task, cts) |> ignore

            let work =
                async {
                    let cleanup () =
                        match tryRemove task with
                        | Some registration -> registration.Dispose()
                        | None -> ()

                    try
                        let nsRequest = task.Request
                        let targetUri = rewriteUri nsRequest.Url
                        let host = targetUri.Host

                        let httpMethod =
                            match nsRequest.HttpMethod |> Option.ofObj with
                            | None -> HttpMethod.Get
                            | Some method when method.Equals("GET", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Get
                            | Some method when method.Equals("POST", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Post
                            | Some method when method.Equals("PUT", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Put
                            | Some method when method.Equals("DELETE", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Delete
                            | Some method when method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Head
                            | Some method when method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Options
                            | Some method when method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) -> HttpMethod.Patch
                            | Some method -> new HttpMethod(method)

                        use request = new HttpRequestMessage(httpMethod, targetUri)

                        let bodyBytes =
                            match nsRequest.Body |> Option.ofObj with
                            | Some data when (int data.Length) > 0 -> Some(data.ToArray())
                            | _ -> None

                        match bodyBytes with
                        | Some payload -> request.Content <- new ByteArrayContent(payload)
                        | None -> ()

                        let headerPairs =
                            match nsRequest.Headers |> Option.ofObj with
                            | None -> Seq.empty
                            | Some dict ->
                                dict
                                |> Seq.cast<KeyValuePair<NSObject, NSObject>>
                                |> Seq.map (fun kvp -> kvp.Key.ToString(), kvp.Value.ToString())

                        for (key, value) in headerPairs do
                            if key.Equals("content-type", StringComparison.OrdinalIgnoreCase) then
                                match request.Content |> Option.ofObj with
                                | Some content -> content.Headers.TryAddWithoutValidation(key, value) |> ignore
                                | None -> ()
                            elif key.Equals("content-length", StringComparison.OrdinalIgnoreCase) then
                                ()
                            else
                                if not (request.Headers.TryAddWithoutValidation(key, value)) then
                                    match request.Content |> Option.ofObj with
                                    | Some content -> content.Headers.TryAddWithoutValidation(key, value) |> ignore
                                    | None -> ()

                        use! response =
                            client.SendAsync(
                                request,
                                HttpCompletionOption.ResponseContentRead,
                                cts.Token
                            )
                            |> Async.AwaitTask

                        let headers = mapHeaders response host
                        let status = response.StatusCode |> int |> nativeint
                        let nsUrl = NSUrl.FromString(targetUri.AbsoluteUri)
                        let httpResponse = new NSHttpUrlResponse(nsUrl, status, "HTTP/1.1", headers)
                        task.DidReceiveResponse(httpResponse)

                        let! payload = response.Content.ReadAsByteArrayAsync(cts.Token) |> Async.AwaitTask
                        if payload.Length > 0 then
                            let data = NSData.FromArray(payload)
                            task.DidReceiveData(data)

                        task.DidFinish()
                        with
                        | :? OperationCanceledException ->
                            let error = NSError.FromDomain(NSError.NSUrlErrorDomain, nativeint (int NSUrlError.Cancelled))
                            task.DidFailWithError(error)
                        | ex ->
                            Debug.WriteLine($"[ProxySchemeHandler] {ex.Message}") |> ignore
                            let error = NSError.FromDomain(NSError.NSUrlErrorDomain, nativeint (int NSUrlError.Unknown))
                            task.DidFailWithError(error)
                    cleanup()
                }

            Async.Start(work, cancellationToken = cts.Token)

        member _.StopUrlSchemeTask(_, task) =
            match tryRemove task with
            | Some registration ->
                registration.Cancel()
                registration.Dispose()
            | None -> ()

type EventPoster() =
    inherit NSObject()
    interface IWKScriptMessageHandler with 
        member this.DidReceiveScriptMessage (userContentController: WKUserContentController, message: WKScriptMessage): unit =
            ServiceEvents.mailbox.Value.Writer.TryWrite(WvTouch) |> ignore
            System.Diagnostics.Debug.WriteLine($"js handler called {message.Name}")
            
type CreateHandler() =
    inherit WebViewHandler()
    let schemeHandler = new ProxySchemeHandler()
    do
        System.Diagnostics.Debug.WriteLine("handler called")
            
    override this.CreatePlatformView (): WKWebView =
        let cfg = MauiWKWebView.CreateConfiguration()
        cfg.UserContentController.AddUserScript(IosScripts.userScript)
        cfg.UserContentController.AddUserScript(IosScripts.bootstrapScript)
        cfg.UserContentController.AddScriptMessageHandler(new EventPoster(),"touchHandler") 
        cfg.SetUrlSchemeHandler(schemeHandler, schemeHandler.Scheme)
        let wv = new MauiWKWebView(CGRect.Empty,this,cfg)
#if !IOS
        wv.CustomUserAgent <- "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
                               "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 " +
                               "Mobile/15E148 Safari/604.1";
#endif
        wv.Inspectable <- true
        wv.ScrollView.Scrolled.Add(fun (e:EventArgs) ->
            let offset = wv.ScrollView.ContentOffset
            let x = offset.X.Value
            let y = offset.Y.Value
            ServiceEvents.mailbox.Value.Writer.TryWrite(WvScroll(x,y))|>ignore
            System.Diagnostics.Debug.WriteLine($"ios scroll({x},{y})") |> ignore)       
        wv
        
    override this.ConnectHandler (platformView: WKWebView): unit =
        base.ConnectHandler(platformView)
        
       
module internal Service =
    
    let private capture2 (wv:WKWebView) : Task<byte[]*(int*int)*string> = task {
        try
            UIGraphics.BeginImageContextWithOptions(wv.Bounds.Size, false, 1.0f);
            let r = wv.DrawViewHierarchy(wv.Bounds, afterScreenUpdates = true)
            let image = UIGraphics.GetImageFromCurrentImageContext()
            UIGraphics.EndImageContext()
            let data = image.AsJPEG()
            return data.ToArray(),(int image.Size.Width, int image.Size.Height),"image/jpeg"
        with ex ->
            System.Diagnostics.Debug.WriteLine(ex.Message)
            return raise ex            
    }
    
    let private dimensions (wv:WKWebView) : Task<int*int> = task {
        let sz = wv.Bounds.Size
        return (sz.Width.Value |> int, sz.Height.Value |> int)
    }
        
    //alt method
    let private capture_ (wv:WKWebView) = 
        task {
            try
                let sz = wv.Bounds
                let rect = CGRect(0.,0.,sz.Width,sz.Height)
                use cfg = new WKSnapshotConfiguration(Rect = rect)
                use! sn  = wv.TakeSnapshotAsync(cfg)
                let data = sn.AsJPEG()
                return data.ToArray()
            with ex ->
                System.Diagnostics.Debug.WriteLine(ex.Message)
                return raise ex
        }
    
    let internal currentDimensions(wv:WebView) : Task<int*int> =task {
          match wv.Handler with
            | null -> return failwith "not ready"
            | h -> 
                match h.PlatformView with 
                | :? WKWebView as wv ->
                    do! MainThread.InvokeOnMainThreadAsync(wv.LayoutIfNeeded)
                    let f() = dimensions(wv)
                    let! data = MainThread.InvokeOnMainThreadAsync<int*int>(f)
                    return data
                | _ -> return failwith "webview not ready"                
        }
        
    let internal capture(wv:WebView) : Task<byte[]*(int*int)*string> = task {    
            match wv.Handler with
            | null -> return failwith "not ready"
            | h -> 
                match h.PlatformView with 
                | :? WKWebView as wv ->
                    do! MainThread.InvokeOnMainThreadAsync(wv.LayoutIfNeeded)
                    let f() = capture2 wv
                    let! data = MainThread.InvokeOnMainThreadAsync<byte[]*(int*int)*string>(f)
                    return data
                | _ -> return failwith "webview not ready"
        }
    
    let internal evalJs(wv:WebView) (js:string) : Task<string> = task {    
            match wv.Handler with
            | null -> return failwith "not ready"
            | h -> 
                match h.PlatformView with 
                | :? WKWebView as wv ->
                    do! MainThread.InvokeOnMainThreadAsync(wv.LayoutIfNeeded)                    
                    let f() = wv.EvaluateJavaScriptAsync(js,null, WKContentWorld.Page)
                    let! data = MainThread.InvokeOnMainThreadAsync<NSObject>(f)
                    let data = data.ToString()
                    return data
                | _ -> return failwith "webview not ready"
        }
        
          
#endif

