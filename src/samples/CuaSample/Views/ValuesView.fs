namespace FsPlaySamples.Cua.Views

open FSharp.Control
open Fabulous
open Fabulous.Maui
open type Fabulous.Maui.View
open type Fabulous.Context
open FsPlaySamples.Cua.Navigation

module ValuesView =   
    type DomModel = {values:Map<string,string>; isActive:bool;}
    and DomMsg = BackButtonPressed | Active | InActive | Nop of string 
    
    let init values =
        {values=values; isActive=false}, Cmd.none        

    let update nav msg (model:DomModel) =
        match msg with
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true}, Cmd.none
        | InActive ->{model with isActive = false}, Cmd.none
        | Nop string -> model, Cmd.none        

    let subscribe (appMsgDispatcher: IAppMessageDispatcher) model =
        let localAppMsgSub dispatch =
            appMsgDispatcher.Dispatched.Subscribe(fun msg ->
                match msg with
                | AppMsg.BackButtonPressed -> dispatch BackButtonPressed)

        localAppMsgSub
        
    let subscriptions appMsgDispatcher model =
        let sub1 = subscribe appMsgDispatcher model
        [
            if model.isActive then
                [nameof sub1], sub1
        ]

    let program nav appMsgDispatcher =
        Program.statefulWithCmd init (update nav)
        |> Program.withSubscription(subscriptions appMsgDispatcher)
       
                    
    let view nav appMsDispatcher values =
        Component("Values") {            
            let! model = Context.Mvu(program nav appMsDispatcher, values)            
            (ContentPage(                
                Grid([ Dimension.Star; Dimension.Star],[Dimension.Absolute 50.0; Dimension.Star]) {
                    Label($"Extracted Values:")
                        .gridRow(0)
                        .alignStartHorizontal()
                        .centerVertical()                        
                        .margin(2.)                     
                    ScrollView(
                        (CollectionView (model.values |> Map.toSeq) (fun (k,v) ->
                            (HStack() {
                                Label($"{k}").width(120)
                                Label($"{v}")
                            }).margin(2.0)
                        ))
                    )
                        .margin(5)
                        .gridRow(1)
                        .gridColumnSpan(2)
                })
                    .padding(5.)
            )
                .title("Values")
                .hasBackButton(true)
                .onNavigatedTo(Active)
                .onNavigatedFrom(InActive)                
        }

