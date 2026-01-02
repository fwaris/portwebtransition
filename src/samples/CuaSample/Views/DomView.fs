namespace FsPlaySamples.Cua.Views

open System
open System.Threading.Channels
open FSharp.Control
open Fabulous
open Fabulous.Maui
open Microsoft.Maui.Graphics
open type Fabulous.Maui.View
open type Fabulous.Context
open FsPlaySamples.Cua.Navigation
open WebFlows

module DomView =   
    type DomModel = {actions:ClickableElement list; isActive:bool; hidden:bool; filter:string
                     applyFilter:string;
                     mailbox : Channel<string> //background messages 
                     }
    and DomMsg = BackButtonPressed | Active | InActive | Nop of string | SetFilter of string | ApplyFilter of string | Search
    
    let init actions =
        { actions=actions; isActive=false; hidden = true; filter=""; applyFilter=""; mailbox=Channel.CreateBounded(30)}, Cmd.none        

    let update nav msg (model:DomModel) =
        match msg with
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true}, Cmd.none
        | InActive -> model.mailbox.Writer.TryComplete() |> ignore; {model with isActive = false}, Cmd.none
        | Nop string -> model, Cmd.none
        | SetFilter f -> model.mailbox.Writer.TryWrite(f) |> ignore; {model with filter=f}, Cmd.none
        | ApplyFilter f -> {model with applyFilter=f}, Cmd.none
        | Search -> model, Cmd.none
        

    let subscribeBackground (model:DomModel) =
        let backgroundEvent dispatch =
            let ctx = new System.Threading.CancellationTokenSource()
            let comp =
                async{
                    let comp =
                         model.mailbox.Reader.ReadAllAsync()
                         |> AsyncSeq.ofAsyncEnum
                         |> AsyncSeq.bufferByTime 500
                         |> AsyncSeq.choose (Array.tryLast)
                         |> AsyncSeq.iter (fun x -> x |> ApplyFilter |> dispatch)            
                    match! Async.Catch(comp) with
                    | Choice1Of2 _ -> debug "dispose subscribeBackground"
                    | Choice2Of2 ex -> debug ex.Message
                }
            Async.Start(comp,ctx.Token)            
            {new IDisposable with member _.Dispose() = ctx.Dispose(); debug "disposing subscription backgroundEvent";}
        backgroundEvent


    let subscribe (appMsgDispatcher: IAppMessageDispatcher) model =
        let localAppMsgSub dispatch =
            appMsgDispatcher.Dispatched.Subscribe(fun msg ->
                match msg with
                | AppMsg.BackButtonPressed -> dispatch BackButtonPressed)

        localAppMsgSub
        
    let subscriptions appMsgDispatcher model =
        let sub1 = subscribe appMsgDispatcher model
        let sub2 = subscribeBackground model
        [
            if model.isActive then
                [nameof sub1], sub1
                [nameof sub2], sub2                
        ]

    let program nav appMsgDispatcher =
        Program.statefulWithCmd init (update nav)
        |> Program.withSubscription(subscriptions appMsgDispatcher)
       
    let filteredActions model =
            if isEmpty model.applyFilter then
               model.actions
            else
               let apply (s:string) = s <> null && s.Contains(model.applyFilter,StringComparison.CurrentCultureIgnoreCase)
               model.actions
               |> List.filter (fun x ->
                   x.id |> Option.map(apply) |> Option.defaultValue false
                   || apply (x.aria_label |> Option.defaultValue null)
                   || apply (x.inner_text |> Option.defaultValue null)
                   || apply x.tag
                   || (x.classList |> List.exists apply)
                   )
                    
    let view nav appMsDispatcher actions =
        Component("actions") {            
            let! model = Context.Mvu(program nav appMsDispatcher, actions)            
            (ContentPage(                
                Grid([ Dimension.Star; Dimension.Star],[Dimension.Absolute 50.0; Dimension.Star]) {
                    Label($"Page Actions:")
                        .gridRow(0)
                        .alignStartHorizontal()
                        .centerVertical()                        
                        .margin(2.)                     
                    Entry(model.filter, SetFilter)
                       .gridColumn(1)
                       .margin(2.)                       
                    ScrollView(
                        (CollectionView (filteredActions model) (fun x ->
                            VStack() {
                                Label($"* {x.aria_label |> Option.defaultValue null}")
                                Label($"{x.inner_text |> Option.defaultValue null}")                                
                                Label($"w: %0.0f{x.width} x h: %0.0f{x.height}")
                                Label($"x: %0.0f{x.x}, y: %0.0f{x.y}")
                                //Label($"z: %0.0f{x.zIndex}")                                
                                Label(x.tag)
                                Label($"""id: {x.id |> Option.defaultValue ""}""")
                                //Label(x.role |> Option.defaultValue "")
                                Label($"%A{x.classList |> Seq.toArray}")
                                //Label(x.path)
                                (BoxView(Colors.Gray)).height(1)
                            }))
                    )
                        .margin(5)
                        .gridRow(1)
                        .gridColumnSpan(2)
                })
                    .padding(5.)
            )
                .title("Clickables")
                .hasBackButton(true)
                .onNavigatedTo(Active)
                .onNavigatedFrom(InActive)                
        }

