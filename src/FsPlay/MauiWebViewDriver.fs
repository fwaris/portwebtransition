namespace FsPlay

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text.Encodings.Web
open System.Text.Json
open System.Threading.Tasks
open Microsoft.Maui.ApplicationModel
open FsPlay.Abstractions
open Microsoft.Maui.Devices

module MauiWebViewDriver =
    let flSerOpts = lazy(
      let opts = JsonSerializerOptions(JsonSerializerDefaults.Web, WriteIndented=true, AllowTrailingCommas=true)        
      opts.Encoder <- JavaScriptEncoder.UnsafeRelaxedJsonEscaping
      opts)
            
   
    let private escapeScript_ (rawScript:string) =
    
      rawScript
        .Replace("\\", "\\\\")     // Escape backslashes first
        .Replace("\"", "\\\"")     // Escape double quotes
        .Replace("\r", "")         // Remove carriage returns
        .Replace("\n", "\\n");     // Escape newlines
        
    let private escapeSomeChars rawScript =
      if DeviceInfo.Current.Platform = DevicePlatform.Android then
        rawScript
      else
        escapeScript_ rawScript
      
    type WebViewWrapper =
        abstract member EvaluateJavaScriptAsync : string -> Task<string>
        abstract member Source : string with get, set
        abstract member GoBack : unit -> unit
        abstract member GoForward : unit -> unit
        abstract member Reload : unit -> unit
        abstract member CaptureAsync : unit -> Task<byte[]*(int*int)*string>
        abstract member CanGoBack : bool
        abstract member CanGoForward : bool

    type DriverInstance = { driver : IUIDriver }

    type private DriverContext =
        { wrapper : WebViewWrapper
          mutable lastMouse : int * int
          mutable bootstrapped : bool
          mutable lastUrl : string option }

    let mutable private context : DriverContext option = None

    let private httpClient = lazy (new HttpClient())

    let bootstrapScript = """
(function () {
  if (window.__fsDriver) {
    return true;
  }

// ------------------------------------------------------------
// Find deepest element at (x,y), descending through shadow DOM
// ------------------------------------------------------------
function deepElementFromPoint(x, y, root) {
    if (!root) root = document;

    const el = root.elementFromPoint(x, y);
    if (!el) return null;

    if (el.shadowRoot) {
        const deeper = deepElementFromPoint(x, y, el.shadowRoot);
        return deeper || el;
    }
    return el;
}

// ------------------------------------------------------------
// Determine whether an element lives inside a shadow DOM
// ------------------------------------------------------------
function isInsideShadow(el) {
    let node = el;
    while (node) {
        if (node instanceof ShadowRoot) return true;
        node = node.parentNode;
    }
    return false;
}

// ------------------------------------------------------------
// Find nearest scrollable ancestor (including shadow hosts)
// Special case: Ionic ion-content → shadowRoot → .inner-scroll
// ------------------------------------------------------------
function findScrollableParent(el) {
    let node = el;

    while (node && node !== document) {

        // Ionic special case
        if (node.tagName === "ION-CONTENT" && node.shadowRoot) {
            const inner = node.shadowRoot.querySelector(".inner-scroll");
            if (inner) return inner;
        }

        // Generic scrollable container check
        const style = window.getComputedStyle(node);
        const overflow = style.overflow;
        const overflowY = style.overflowY;

        const scrollable =
            (overflow === "auto" || overflow === "scroll" ||
             overflowY === "auto" || overflowY === "scroll");

        if (scrollable && node.scrollHeight > node.clientHeight) {
            return node;
        }

        // Shadow DOM boundary
        if (node.parentNode instanceof ShadowRoot) {
            node = node.parentNode.host;
        } else {
            node = node.parentNode;
        }
    }

    // Fallback to document scroll element
    return document.scrollingElement || document.documentElement;
}

// ------------------------------------------------------------
// Scroll a specific target element
// ------------------------------------------------------------
function performScroll(target, scrollX, scrollY) {
    if (!target) return "no-scroll-target";

    target.scrollTo({
        left: scrollX,
        top: scrollY,
        behavior: "smooth"
    });

    return "scrolled";
}

// ------------------------------------------------------------
// PUBLIC API: scrollByPoint(x, y, scrollX, scrollY)
// ------------------------------------------------------------
function scrollByPoint(x, y, scrollX, scrollY) {
    const el = deepElementFromPoint(x, y);
    if (!el) {
        return {
            error: "no-element-under-point"
        };
    }

    const insideShadow = isInsideShadow(el);
    const scrollTarget = findScrollableParent(el);
    const result = performScroll(scrollTarget, scrollX, scrollY);

    return {
        elementUnderPoint: el.tagName,
        insideShadowDOM: insideShadow,
        scrollTarget: scrollTarget ? (scrollTarget.tagName || "shadow-root") : null,
        result: result
    };
}


  // call: await webView.EvaluateJavaScriptAsync($"({typeIntoActiveElement.toString()})('{text}', {delay})");
function typeIntoActiveElement(text, delayMs = 10) {
  const el = document.activeElement;
  if (!el) return false;
  const str = String(text ?? "");

  // helper to set value via prototype setter if available
  function setValueWithSetter(element, newVal) {
    if (!('value' in element)) { element.textContent = newVal; return; }
    const proto = Object.getPrototypeOf(element);
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) {
      desc.set.call(element, newVal);
    } else {
      element.value = newVal;
    }
  }

  // send events for a single character
  function sendCharEvents(element, ch) {
    const key = ch.length === 1 ? ch : 'Unidentified';
    try {
      element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key }));
      element.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, cancelable: true, key }));
    } catch (e) { /* ignore */ }

    // update value using selection/caret if possible
    if ('value' in element) {
      const start = element.selectionStart ?? element.value.length;
      const end = element.selectionEnd ?? element.value.length;
      const before = element.value.substring(0, start);
      const after = element.value.substring(end);
      setValueWithSetter(element, before + ch + after);
      const pos = before.length + ch.length;
      if (element.setSelectionRange) element.setSelectionRange(pos, pos);
    } else {
      element.textContent = (element.textContent ?? "") + ch;
    }

    // InputEvent with data and inputType
    try {
      const ie = new InputEvent('input', {
        bubbles: true, cancelable: true, composed: true,
        inputType: 'insertText', data: ch
      });
      element.dispatchEvent(ie);
    } catch (e) {
      const ev = document.createEvent('Event');
      ev.initEvent('input', true, true);
      element.dispatchEvent(ev);
    }

    try {
      element.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, cancelable: true, key }));
    } catch (e) { /* ignore */ }
  }

  // type each character with optional delay
  return new Promise(resolve => {
    let i = 0;
    function step() {
      if (i >= str.length) {
        // final change event to commit
        element.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
        resolve(true);
        return;
      }
      sendCharEvents(el, str[i]);
      i++;
      if (!delayMs) step();
      else setTimeout(step, delayMs);
    }
    step();
  });
}

  function toClientPoint(pageX, pageY) {
    const zoom = 1;
    let adjustedX = pageX;
    let adjustedY = pageY;
    if (pageX > window.innerWidth || pageY > window.innerHeight) {
      adjustedX = pageX / zoom;
      adjustedY = pageY / zoom;
    }

    const clientX = adjustedX - window.scrollX;
    const clientY = adjustedY - window.scrollY;

    return {
      clientX: Math.round(clientX),
      clientY: Math.round(clientY)
    };
  }

  function clampToViewport(clientPoint) {
    const width = window.innerWidth;
    const height = window.innerHeight;
    return {
      clientX: Math.min(Math.max(clientPoint.clientX, 0), width - 1),
      clientY: Math.min(Math.max(clientPoint.clientY, 0), height - 1)
    };
  }

  function mouseInit(clientPoint, buttonCode, buttonsMask) {
    return {
      clientX: clientPoint.clientX,
      clientY: clientPoint.clientY,
      button: buttonCode || 0,
      buttons: typeof buttonsMask === 'number' ? buttonsMask : 1,
      bubbles: true,
      cancelable: true,
      composed: true,
      view: window
    };
  }

  function fireMouse(target, type, init) {
    const event = new MouseEvent(type, init);
    target.dispatchEvent(event);
  }

  window.__fsDriver = {
    clickAt: function (pageX, pageY, buttonCode) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = document.elementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }

      const init = mouseInit(client, buttonCode || 0, 1);
      fireMouse(element, 'mousemove', init);
      fireMouse(element, 'mousedown', init);
      fireMouse(element, 'mouseup', mouseInit(client, buttonCode || 0, 0));
      fireMouse(element, 'click', init);
      return true;
    },

    doubleClick: function (pageX, pageY) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = document.elementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }
      const init = mouseInit(client, 0, 1);
      fireMouse(element, 'mousemove', init);
      fireMouse(element, 'mousedown', init);
      fireMouse(element, 'mouseup', mouseInit(client, 0, 0));
      fireMouse(element, 'click', init);
      fireMouse(element, 'dblclick', init);
      return true;
    },

    moveTo: function (pageX, pageY) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = document.elementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }
      fireMouse(element, 'mousemove', mouseInit(client, 0, 0));
      return true;
    },

    dragDrop: function (startX, startY, endX, endY) {
      const start = clampToViewport(toClientPoint(startX, startY));
      const end = clampToViewport(toClientPoint(endX, endY));
      const source = document.elementFromPoint(start.clientX, start.clientY);
      const target = document.elementFromPoint(end.clientX, end.clientY);
      if (!source || !target) {
        return false;
      }

      const startInit = mouseInit(start, 0, 1);
      const moveInit = mouseInit(end, 0, 1);
      const upInit = mouseInit(end, 0, 0);

      fireMouse(source, 'mousemove', startInit);
      fireMouse(source, 'mousedown', startInit);
      fireMouse(source, 'mousemove', moveInit);
      fireMouse(target, 'mousemove', moveInit);
      fireMouse(target, 'mouseup', upInit);
      fireMouse(target, 'drop', upInit);
      fireMouse(source, 'mouseup', upInit);
      return true;
    },

    scrollBy: function (deltaX, deltaY) {
      window.scrollBy(deltaX || 0, deltaY || 0);
      return { scrollX: window.scrollX, scrollY: window.scrollY };
    },

    scrollTo: function (x, y) {
      window.scrollTo(x || 0, y || 0);
      return { scrollX: window.scrollX, scrollY: window.scrollY };
    },

    touchScroll: function (startX, startY, scrollX, scrollY) {
      return scrollByPoint(startX,startY,scrollX,scrollY);
    },

    typeText: function (text) {
      const target = document.activeElement;
      if (!target) {
        return false;
      }
      typeIntoActiveElement(text);
      return true;
    },

    pressKey: function (key, modifiers) {
      const target = document.activeElement || document.body || document.documentElement;
      const init = Object.assign(
        {
          key: key,
          code: key,
          keyCode: key === ' ' ? 32 : key?.length === 1 ? key.toUpperCase().charCodeAt(0) : 0,
          which: key === ' ' ? 32 : key?.length === 1 ? key.toUpperCase().charCodeAt(0) : 0,
          bubbles: true,
          cancelable: true
        },
        modifiers || {}
      );
      try {
        target.dispatchEvent(new KeyboardEvent('keydown', init));
        target.dispatchEvent(new KeyboardEvent('keypress', init));
        target.dispatchEvent(new KeyboardEvent('keyup', init));
        if (key === 'Enter') {
          target.dispatchEvent(new Event('change', { bubbles: true }));
        }
        return true;
      } catch (err) {
        console.error(err);
        return false;
      }
    },

    meta: function () {
      return {
        scrollX: window.scrollX,
        scrollY: window.scrollY,
        width: window.innerWidth,
        height: window.innerHeight,
        url: window.location.href || null,
        title: document.title || null
      };
    },

    navigate: function (targetUrl) {
      if (!targetUrl) {
        return false;
      }
      if (window.location.href === targetUrl) {
        window.location.reload();
      } else {
        window.location.href = targetUrl;
      }
      return true;
    },
  };

  return true;
})();
"""

    let private ensureBootstrap (ctx:DriverContext) = async {
        ctx.bootstrapped <- true
        return()
    }
    
    let private _ensureBootstrap (ctx: DriverContext) =
        async {
            if not ctx.bootstrapped then
                try
                    let! _ =
                        if MainThread.IsMainThread then
                            ctx.wrapper.EvaluateJavaScriptAsync(escapeSomeChars bootstrapScript) |> Async.AwaitTask
                        else
                            MainThread.InvokeOnMainThreadAsync<string>(fun () -> ctx.wrapper.EvaluateJavaScriptAsync(escapeSomeChars bootstrapScript))
                            |> Async.AwaitTask
                    ctx.bootstrapped <- true
                with ex ->
                    ctx.bootstrapped <- false
                    Debug.WriteLine($"[MauiWebViewDriver] bootstrap failed: {ex.Message}")
                    return raise ex
        }

    let private runScript (ctx: DriverContext) (script: string) =
        async {
            do! ensureBootstrap ctx
            try
              let! raw = 
                if MainThread.IsMainThread then                    
                    ctx.wrapper.EvaluateJavaScriptAsync(escapeSomeChars script) |> Async.AwaitTask
                else
                        MainThread.InvokeOnMainThreadAsync<string>(fun () -> ctx.wrapper.EvaluateJavaScriptAsync(escapeSomeChars script))
                        |> Async.AwaitTask
              //printfn $"JS:\n{script}\n{raw}"
              let str = "\"" + raw + "\""
              let js = JsonSerializer.Deserialize<string>(str,flSerOpts.Value)
              return js
            with ex ->
                ctx.bootstrapped <- false
                Debug.WriteLine($"[MauiWebViewDriver] script failed: {ex.Message}")
                return raise ex
        }

    let private jsString (value: string) =
        if isNull value then
            "null"
        else
            JsonSerializer.Serialize(value)

    let private boolLiteral value = if value then "true" else "false"

    type private MetaSnapshot =
        { width: int
          height: int
          scrollX: int
          scrollY: int
          url: string option
          title: string option }

    let private metaScript = """
(function () {
  const meta = window.__fsDriver ? window.__fsDriver.meta() : {
    scrollX: window.scrollX,
    scrollY: window.scrollY,
    width: window.innerWidth,
    height: window.innerHeight,
    url: window.location.href || null,
    title: document.title || null
  };
  return JSON.stringify(meta);
})();
"""

    let private parseMeta (raw: string) : MetaSnapshot option =
      try
        use doc = JsonDocument.Parse(raw)
        let root = doc.RootElement

        let tryNumber (name: string) =
          let mutable prop = Unchecked.defaultof<JsonElement>
          if root.TryGetProperty(name, &prop) && prop.ValueKind = JsonValueKind.Number then
            match prop.TryGetDouble() with
            | true, value -> Some(int (Math.Round value))
            | _ -> None
          else
            None

        let tryString (name: string) =
          let mutable prop = Unchecked.defaultof<JsonElement>
          if root.TryGetProperty(name, &prop) then
            match prop.ValueKind with
            | JsonValueKind.Null -> None
            | JsonValueKind.String -> Some(prop.GetString())
            | _ -> Some(prop.ToString())
          else
            None

        Some
          { width = tryNumber "width" |> Option.defaultValue 0
            height = tryNumber "height" |> Option.defaultValue 0
            scrollX = tryNumber "scrollX" |> Option.defaultValue 0
            scrollY = tryNumber "scrollY" |> Option.defaultValue 0
            url = tryString "url"
            title = tryString "title" }
      with ex ->
        Debug.WriteLine($"[MauiWebViewDriver] parseMeta failed: {ex.Message}")
        None

    let private getMeta ctx =
        async {
            try
              let! raw = runScript ctx metaScript
              if String.IsNullOrWhiteSpace raw then
                return None
              else
                let meta = parseMeta raw
                meta |> Option.iter (fun m -> ctx.lastUrl <- m.url)
                return meta
            with ex ->
              Debug.WriteLine($"[MauiWebViewDriver] meta failed: {ex.Message}")
              return None
        }

    let initialize (wrapper: WebViewWrapper) =
        context <-
            Some
                { wrapper = wrapper
                  lastMouse = (0, 0)
                  bootstrapped = false
                  lastUrl = None }

    let private currentContext () =
        match context with
        | Some ctx -> ctx
        | None -> invalidOp "MauiWebViewDriver has not been initialized. Call initialize with a WebView wrapper first."

    let private resetBootstrap ctx = ctx.bootstrapped <- false

    let private runOnMainThreadUnit (work: unit -> unit) =
        if MainThread.IsMainThread then
            work()
            Task.CompletedTask
        else
          MainThread.InvokeOnMainThreadAsync(Action(fun () -> work()))

    let private capture (ctx: DriverContext) =
        if MainThread.IsMainThread then
            ctx.wrapper.CaptureAsync()
        else
            MainThread.InvokeOnMainThreadAsync<byte[]*(int*int)*string>(fun () -> ctx.wrapper.CaptureAsync())

    let create () =
      let ctx = currentContext ()

      let driver =
        { new IUIDriver with
          member _.doubleClick (x, y) =
            async {
              ctx.lastMouse <- (x, y)
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.doubleClick({x}, {y})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.click (x, y, button) =
            async {
              ctx.lastMouse <- (x, y)
              let buttonCode =
                match button with
                | MouseButton.Left -> 0
                | MouseButton.Middle -> 1
                | MouseButton.Right -> 2
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.clickAt({x}, {y}, {buttonCode})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.typeText text =
            async {
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.typeText({jsString text})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.wheel (deltaX, deltaY) =
            async {
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.scrollBy({deltaX}, {deltaY})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.move (x, y) =
            async {
              ctx.lastMouse <- (x, y)
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.moveTo({x}, {y})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }
(*
          member _._scroll (x, y) (scrollX, scrollY) =
            async {
              ctx.lastMouse <- (x, y)
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.scrollBy({scrollX}, {scrollY})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }
*)
            
          member _.scroll (x, y) (scrollX, scrollY) =
            async {
              let x = if x < 0 then fst ctx.lastMouse else x
              let y = if y < 0 then snd ctx.lastMouse else y
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.touchScroll({x}, {y}, {scrollX}, {scrollY})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }
          member _.pressKeys keys =
            async {
              let keys = DriverUtils.canonicalize keys
              match keys with
              | [] -> return ()
              | _ ->
                let key = List.last keys
                let modifiers = keys |> List.take (List.length keys - 1)
                let ctrl = List.exists ((=) K.Control) modifiers
                let shift = List.exists ((=) K.Shift) modifiers
                let alt = List.exists ((=) K.Alt) modifiers
                let meta = List.exists ((=) K.Meta) modifiers
                let mods =
                  sprintf "{ ctrlKey: %s, shiftKey: %s, altKey: %s, metaKey: %s }"
                    (boolLiteral ctrl)
                    (boolLiteral shift)
                    (boolLiteral alt)
                    (boolLiteral meta)

                let script = $"""
  (function () {{
    const driver = window.__fsDriver;
    if (!driver) return JSON.stringify({{ ok: false }});
    const keyValue = {jsString key};
    const result = driver.pressKey(keyValue, {mods});
    const normalized = (keyValue || '').toLowerCase();
    if (!result && {boolLiteral ctrl} && (normalized === 'a' || normalized === 'keya')) {{
    if (document.execCommand) {{
      document.execCommand('selectAll');
      return JSON.stringify({{ ok: true }});
    }}
    }}
    return JSON.stringify({{ ok: !!result }});
  }})();
  """
                let! _ = runScript ctx script
                return ()
            }

          member _.dragDrop (sourceX, sourceY) (targetX, targetY) =
            async {
              ctx.lastMouse <- (targetX, targetY)
              let script = $"""
  (function () {{
    return JSON.stringify({{ ok: !!(window.__fsDriver && window.__fsDriver.dragDrop({sourceX}, {sourceY}, {targetX}, {targetY})) }});
  }})();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.snapshot () = async {
            let! (img,_,_) as data = capture ctx |> Async.AwaitTask
            
            let fld =
              if DeviceInfo.Platform =  DevicePlatform.MacCatalyst then 
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
              else
                Microsoft.Maui.Storage.FileSystem.AppDataDirectory
            let path = Path.Combine(fld,"screenshot_m.jpeg")
            System.Diagnostics.Debug.WriteLine(path)
            File.WriteAllBytes(path,img)
            return data
          }
          
          member _.goBack () =
            async {
              let! _ =
                runOnMainThreadUnit (fun () ->
                  if ctx.wrapper.CanGoBack then
                    ctx.wrapper.GoBack())
                |> Async.AwaitTask
              resetBootstrap ctx
              return ()
            }

          member _.goForward () =
            async {
              let! _ =
                runOnMainThreadUnit (fun () ->
                  if ctx.wrapper.CanGoForward then
                    ctx.wrapper.GoForward())
                |> Async.AwaitTask
              resetBootstrap ctx
              return ()
            }

          member _.goto target =
            async {
              let! _ =
                runOnMainThreadUnit (fun () -> ctx.wrapper.Source <- target)
                |> Async.AwaitTask
              ctx.lastUrl <- Some target
              resetBootstrap ctx
              return ()
            }

          member _.url () =
            async {
              let! meta = getMeta ctx
              match meta with
              | Some m ->
                match m.url with
                | Some url when not (String.IsNullOrWhiteSpace url) ->
                  ctx.lastUrl <- Some url
                  return Some url
                | _ -> return ctx.lastUrl
              | None -> return ctx.lastUrl
            }

          member _.environment = "browser"
          member _.start arg =
            async {
              if String.IsNullOrWhiteSpace arg then
                return ()
              else
                let! _ =
                  runOnMainThreadUnit (fun () -> ctx.wrapper.Source <- arg)
                  |> Async.AwaitTask
                ctx.lastUrl <- Some arg
                resetBootstrap ctx
                return ()
            }

          member _.saveState () = async.Return ()

          member _.clearCookies () =
            async {
              let script = """
  (function () {
    try {
    if (!document.cookie) return JSON.stringify({ ok: true });
    document.cookie.split(';').forEach(function (cookie) {
      const eqPos = cookie.indexOf('=');
      const name = eqPos > -1 ? cookie.substr(0, eqPos) : cookie;
      document.cookie = name + '=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/';
    });
    return JSON.stringify({ ok: true });
    } catch (err) {
    return JSON.stringify({ ok: false, message: err && err.message });
    }
  })();
  """
              let! _ = runScript ctx script
              return ()
            }

          member _.reload () =
            async {
              let! _ = runOnMainThreadUnit (fun () -> ctx.wrapper.Reload()) |> Async.AwaitTask
              resetBootstrap ctx
              return ()
            }

          member _.getUrlBytes () =
            async {
              let! meta = getMeta ctx
              let currentUrl =
                meta
                |> Option.bind (fun m -> m.url)
                |> Option.orElse ctx.lastUrl
              match currentUrl with
              | Some url when not (String.IsNullOrWhiteSpace url) ->
                try
                  let! data = httpClient.Value.GetByteArrayAsync(url) |> Async.AwaitTask
                  return data
                with ex ->
                  Debug.WriteLine($"[MauiWebViewDriver] getUrlBytes failed: {ex.Message}")
                  return Array.empty
              | _ -> return Array.empty
            }

          member _.evaluateJavaScript expression =
            async {
              let script = $"""
  (function () {{
    try {{
    const result = eval({jsString expression});
    if (result instanceof Promise) {{
      return result.then(x => JSON.stringify({{ ok: true, value: x }}));
    }}
    return JSON.stringify({{ ok: true, value: result }});
    }} catch (err) {{
    return JSON.stringify({{ ok: false, error: err && err.message }});
    }}
  }})();
  """
              let! raw = runScript ctx script
              return if String.IsNullOrWhiteSpace raw then "" else raw
            }
        }

      { driver = driver }
