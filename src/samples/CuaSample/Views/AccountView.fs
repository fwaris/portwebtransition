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
open FsPlaySamples.Cua
open WebFlows

module AccountView =
    let private (!-) (a:string option) = a |> Option.defaultValue "" 
    type AcctModel = {accountInfo: ArticleSummary; isActive:bool}
    and AcctMsg = BackButtonPressed | Active | InActive | Nop of string
    
    let init acctInfo =
        { accountInfo=acctInfo; isActive=false;}, Cmd.none        

    let update nav msg (model: AcctModel) =
        match msg with
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true}, Cmd.none
        | InActive -> {model with isActive = false}, Cmd.none
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

    let view nav appMsDispatcher accountInfo =
        Component("Data") {            
            let! model = Context.Mvu(program nav appMsDispatcher, accountInfo)            
            (ContentPage(                
                (Grid([Dimension.Absolute 150; Dimension.Star],
                     [Dimension.Absolute 50.0; Dimension.Absolute 50; Dimension.Absolute 50; Dimension.Star]) {
                    Label("Account Number:").gridColumn(0).gridRow(0)
                    Label(!- model.accountInfo.AccountNumber).gridColumn(1).gridRow(0)
                    Label("Billing Zip:").gridColumn(0).gridRow(1)
                    Label(!- model.accountInfo.BillingZip).gridColumn(1).gridRow(1)
                    Label("Transfer Pin:").gridColumn(0).gridRow(2)
                    Label(!- model.accountInfo.TransferPin).gridColumn(1).gridRow(2)
                    Label("Bill:").gridColumn(0).gridRow(3)
                    Label(string model.accountInfo.Bill).gridColumn(1).gridRow(3)
                }).margin(5)                
                ).padding(5.)
                    
            )
                .title("Account Info")
                .hasBackButton(true)
                .onNavigatedTo(Active)
                .onNavigatedFrom(InActive)                
        }

