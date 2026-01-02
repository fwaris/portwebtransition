namespace FsPlay

open Microsoft.Maui.Controls
open System.Threading.Tasks
open System.Threading.Channels

type WvEvent =
    | WvScroll of float*float
    | WvTouch

module ServiceEvents =
    let mailbox = lazy(
        let opts = BoundedChannelOptions(30)
        opts.FullMode <- BoundedChannelFullMode.DropOldest  
        Channel.CreateBounded<WvEvent>(opts))

type IWebViewService =
    abstract Capture : mauiWebView:WebView->Task<byte[]*(int*int)*string>

    