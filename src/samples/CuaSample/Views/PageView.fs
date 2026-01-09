namespace FsPlaySamples.Cua.Views
open System
open Fabulous
open Microsoft.Maui
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics
open FSharp.Control
open Fabulous.Maui
open type Fabulous.Maui.View
open type Fabulous.Context
open Microsoft.Maui.Layouts
open FsPlaySamples.Cua
open FsPlaySamples.Cua.Navigation

module PageView =
    
    let subscribe (appMsgDispatcher: IAppMessageDispatcher) (model:Model)=        
        let localAppMsgSub dispatch =
            appMsgDispatcher.Dispatched.Subscribe(fun msg ->
                match msg with
                | AppMsg.BackButtonPressed -> dispatch Msg.BackButtonPressed)
        localAppMsgSub

    let subscribeBackground (model:Model) =
        let backgroundEvent dispatch =
            let ctx = new System.Threading.CancellationTokenSource()
            let comp =
                async{
                    let comp =
                         model.mailbox.Reader.ReadAllAsync(ctx.Token)
                         |> AsyncSeq.ofAsyncEnum
                         |> AsyncSeq.iter dispatch            
                    match! Async.Catch(comp) with
                    | Choice1Of2 _ -> () //debug "dispose subscribeBackground"
                    | Choice2Of2 ex -> debug ex.Message
                }
            Async.Start(comp,ctx.Token)            
            {new IDisposable with member _.Dispose() = ctx.Dispose()} //debug "disposing subscription backgroundEvent";
        backgroundEvent
        
    let webviewInteraction (model:Model) =
        let rec webviewInteraction dispatch =
            let ctx = new System.Threading.CancellationTokenSource()
            let comp =
                async{
                    let comp =
                         FsPlay.ServiceEvents.mailbox.Value.Reader.ReadAllAsync(ctx.Token)
                         |> AsyncSeq.ofAsyncEnum
                         |> AsyncSeq.bufferByTime 1000
                         |> AsyncSeq.choose Array.tryLast
                         |> AsyncSeq.map WebviewInteraction                         
                         |> AsyncSeq.iter dispatch            
                    match! Async.Catch(comp) with
                    | Choice1Of2 _ -> debug $"disposed subscribe {nameof(webviewInteraction)}"
                    | Choice2Of2 ex -> debug ex.Message
                }
            Async.Start(comp,ctx.Token)            
            {new IDisposable with member _.Dispose() = ctx.Dispose(); debug $"disposing subscribe {nameof(webviewInteraction)}";}
        webviewInteraction
        
    let subscriptions appMsgDispatcher model =
        let sub1 = subscribe appMsgDispatcher model
        let sub2 = subscribeBackground model
        let sub3 = webviewInteraction model
        [
            if model.isActive then
                [nameof sub1], sub1
                [nameof sub2], sub2                
                [nameof sub3], sub3                
        ]
       
    let program nav appMsgDispatcher =
        Program.statefulWithCmd Update.initModel (Update.update nav)
        |> Program.withSubscription(subscriptions appMsgDispatcher)

    let headerView (model:Model) :WidgetBuilder<Msg,IFabGrid> =
        (Grid([Dimension.Star; Dimension.Star;],[Dimension.Absolute 50.0]) {
            (HStack(){
                Button(UiOps.playIcon model, UiOps.playMsg model)
                    .font(size=20.0, fontFamily=C.FA)
                    .background(Colors.Transparent)
                    .centerVertical()
                    .alignStartHorizontal()
                Label(model.action |> Option.defaultValue "")
                    .verticalTextAlignment(TextAlignment.Center)                    
                    .lineBreakMode(LineBreakMode.WordWrap)
             })
                .isClippedToBounds(true)
                .centerVertical()
                .alignStartHorizontal()
                .gridColumn(0)               
            (HStack(){
                Button(Icons.settings,ToggleSettings)
                    .font(size=20.0, fontFamily=C.FONT_SYMBOLS)
                    .background(Colors.Transparent)
                    .textColor(Colors.Magenta)
                    .centerVertical()
                    .alignStartHorizontal()
                Button(Icons.fa_bars,ToggleNavBar)
                    .font(size=20.0, fontFamily=C.FA)
                    .background(Colors.Transparent)
                    .textColor(Colors.Magenta)
                    .centerVertical()
                    .alignStartHorizontal()
             })
                .centerVertical()
                .alignEndHorizontal()
                .gridColumn(1)                
        })
            .gridRow(0)
            .background(Colors.LightGray)
            .gridColumnSpan(2)
            .margin(3,0,3,0)       
           
    let navBar (m:Model) =
        (VStack() {
            (Grid([Dimension.Star],[Dimension.Star;Dimension.Star; Dimension.Star; Dimension.Star; Dimension.Star])) {
                Button(Icons.fa_xmark,ToggleNavBar).font(size=20,fontFamily=C.FA).alignEndHorizontal().margin(10)
                Button(Icons.fa_chart,Nav ViewStats)
                    .font(size=20.0, fontFamily=C.FA)
                    .textColor(Colors.Magenta)
                    .background(Colors.Transparent)
                    .gridRow(1)
                Button(Icons.fa_user_pen,Nav ViewSummary)
                    .font(size=20.0, fontFamily=C.FA)
                    .background(Colors.Transparent)
                    .textColor(Colors.Magenta)
                    .gridRow(2)
                Button(Icons.fa_key, Nav ViewCreds)
                     .font(size=20.0, fontFamily=C.FA)
                     .background(Colors.Transparent)
                     .textColor(Colors.Magenta)
                     .gridRow(3)

            }
        })
            .background(Colors.LightGray)
            .alignStartVertical()
            .alignEndHorizontal()
            .margin(0,5,10,0)
            .gridRow(1)
            .shadow(Shadow(Brush.Gray, Point(5., 5.)).opacity(0.5).blurRadius(40.))
            .zIndex(2)
      
    module Browser =
        type Model = {url:Uri}
        type Msg = SetUrl of Uri | Navigated
        
        let init navigated= {url=Uri "about:blank"}, Cmd.none
        
        let update msg model =
            match msg with
            | SetUrl uri -> {model with url=uri}, Cmd.none
            | Navigated -> model,Cmd.none

        let program = Program.statefulWithCmd init update
            
        let view() =
            Component("browser") {
                let! model = Context.Mvu(program) 
                (WebView(model.url))
                    .reference(Model.webviewCache)
                    
            }
            
    let controlsView (model:Model) =
        (AbsoluteLayout() {
            (Browser.view())
                .layoutBounds(0.,0.,1.,1.)
                .layoutFlags(AbsoluteLayoutFlags.All)
            match model.pointer, Model.webviewCache.TryValue with
            | None,_ -> ()
            | _, None -> ()
            | Some (x,y),Some wv->
                Border(
                    Label("")
                     .inputTransparent(true)
                )
                    .inputTransparent(true)
                    .stroke(Colors.Red)
                    .strokeThickness(3.0)
                    .layoutBounds(x,y,10,10)
        })
            .gridRow(1)
            .margin(5.)

    let popup (model: Model) =
        (Grid([Dimension.Star], [Dimension.Star]) {
            Border(
                VStack(spacing=12.) {
                    (Grid([Dimension.Star;Dimension.Star],[Dimension.Star]) {
                        Label("Settings").font(size = 20.).alignStartHorizontal().centerVertical()                        
                        Button(Icons.fa_xmark, ToggleSettings)
                            .font(size=20.0, fontFamily=C.FA)
                            .margin(5,0,0,0)
                            .alignEndHorizontal()
                            .centerVertical()
                            .gridColumn(1)
                    }).margin(0,0,5,0)                      
                    (Grid([Dimension.Absolute 200; Dimension.Star],[Dimension.Star; Dimension.Star;]) {
                        Label("Preview Clicks").gridRow(0).margin(10)
                        (CheckBox(model.settings.PreviewClicks,CheckPreview))
                                .gridRow(0).gridColumn(1).margin(10)
                        Label("Run Count:").gridRow(1).margin(10)
                    }).margin(5.)
                })
                    .background(SolidColorBrush Colors.LightGray)
                    .padding(10.)
                    .strokeShape(RoundRectangle(CornerRadius(8.)))
                    .centerHorizontal()
                    .centerVertical()
        })
            .gridRow(1)
            .shadow(Shadow(Brush.Gray, Point(5., 5.)).opacity(0.5).blurRadius(40.))
            .zIndex(10)
            
    let view nav appMsgDispatcher = //: WidgetBuilder<Msg,_> =
        Component("Main") {
            let! settings = EnvironmentObject Settings.Environment.settingsKey
            let! model = Context.Mvu(program nav appMsgDispatcher,settings)
            let page =
                ContentPage(
                    (Grid([Dimension.Star],[Dimension.Absolute 53.0; Dimension.Star]) {
                        //controlsView model
                        //headerView model //needed for android
                        if model.isOpenSettings then popup model
                        if model.isOpenNavBar then navBar model
                    })
                        .margin(5.)
                )
                    .title("Main")
                    .onNavigatedTo(Active)
                    .onNavigatedFrom(InActive)

            //let () = animateSettingsPopup model.isOpenSettings
            page
        }
