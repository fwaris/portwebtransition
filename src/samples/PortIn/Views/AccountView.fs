namespace FsPlaySamples.PortIn.Views

open FSharp.Control
open Fabulous
open Fabulous.Maui
open type Fabulous.Maui.View
open type Fabulous.Context
open FsPlaySamples.PortIn.Navigation
open FsPlaySamples.PortIn
open FsPlaySamples.PortIn.Agentic
open Microsoft.Maui.Graphics


module AccountView =
    let private (!-) (a:string option) = a |> Option.defaultValue "Not captured"
    type AcctModel = { accountInfo: AccountInfo; isActive:bool}
    and AcctMsg = BackButtonPressed | Active | InActive | Nop of string

    let init accountInfo =
        { accountInfo = accountInfo; isActive = false }, Cmd.none

    let update nav msg (model: AcctModel) =
        match msg with
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true}, Cmd.none
        | InActive -> {model with isActive = false}, Cmd.none
        | Nop _ -> model, Cmd.none

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

    let private billText (bill: Bill option) =
        match bill with
        | Some b ->
            let charges = b.PlanCharges |> String.concat ", "
            let total = b.TotalAmount |> Option.defaultValue "N/A"
            $"Charges: {charges}\nTotal: {total}"
        | None -> "Not captured"

    let view nav appMsDispatcher accountInfo =
        Component("AccountInfo") {
            let! model = Context.Mvu(program nav appMsDispatcher, accountInfo)
            let info = model.accountInfo
            (ContentPage(
                ScrollView(
                    (VStack(spacing = 15.) {
                        // Account Number
                        VStack() {
                            Label("Account Number").font(size=18.)
                            Label(!- info.AccountNumber).font(size=14.)
                        }

                        // Billing Zip
                        VStack() {
                            Label("Billing Zip Code").font(size=18.)
                            Label(!- info.BillingZip).font(size=14.)
                        }

                        // Transfer PIN
                        VStack() {
                            Label("Transfer PIN").font(size=18.)
                            Label(!- info.TransferPin).font(size=14.)
                        }

                        // Transfer PIN Page Located
                        VStack() {
                            Label("Transfer PIN Page Located").font(size=18.)
                            Label(if info.TransferPinPageLocated then "Yes" else "No").font(size=14.)
                        }

                        // Bill Details
                        VStack() {
                            Label("Bill Details").font(size=18.)
                            Label(billText info.Bill).font(size=14.)
                        }
                    }).padding(20.)
                )
            ))
                .title("Account Info")
                .hasBackButton(true)
                .onNavigatedTo(Active)
                .onNavigatedFrom(InActive)
        }
