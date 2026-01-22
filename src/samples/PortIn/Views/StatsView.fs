namespace FsPlaySamples.PortIn.Views
open FsPlaySamples.PortIn
open FsPlaySamples.PortIn.Navigation
open Microsoft.Maui
open Syncfusion.Maui.Toolkit.Charts
open Fabulous
open Fabulous.Maui
open type Fabulous.Maui.View

module StatsView =
    
    let histogramSeries (data:Chart): ChartSeries list =
        let series = HistogramSeries()
        series.ItemsSource <- data.Values
        series.XBindingPath <- "X"
        series.YBindingPath <- "Y"
        series.HistogramInterval <- match data.ChartType with Histogram bins -> bins | _ -> 10
        [ series ]
        
    let barSeries (data:Chart): ChartSeries list =
        let series = ColumnSeries()
        series.ItemsSource <- data.Values
        series.XBindingPath <- "ItemLabel"
        series.YBindingPath <- "Y" 
        [ series ]
        
    type StatsModel = { isActive : bool; charts: (int*Chart) list}
    type StatsMsg = BackButtonPressed | Active | InActive | Nop 

    let init data =
        { isActive=false; charts=data |> List.indexed}, Cmd.none        

    let update nav msg (model: StatsModel) =
        //printfn "%A" msg
        match msg with
        | BackButtonPressed -> model, Navigation.navigateBack nav
        | Active -> {model with isActive = true}, Cmd.none
        | InActive -> {model with isActive = false}, Cmd.none
        | Nop -> model, Cmd.none

    let subscribe (appMsgDispatcher: IAppMessageDispatcher) model =
        let localAppMsgSub dispatch =
            appMsgDispatcher.Dispatched.Subscribe(fun msg ->
                match msg with
                | AppMsg.BackButtonPressed -> dispatch BackButtonPressed)

        [ if model.isActive then
              [ nameof localAppMsgSub ], localAppMsgSub ]

    let program nav appMsgDispatcher =
        Program.statefulWithCmd init (update nav)
        |> Program.withSubscription(subscribe appMsgDispatcher)
       
    let categoryAxis title =
        let a = CategoryAxis()
        match title with
        | Some t -> a.Title <- ChartAxisTitle(Text=t)
        | None -> ()
        a

    let numericAxis title =
        let a = NumericalAxis()
        match title with
        | Some t -> a.Title <- ChartAxisTitle(Text=t)
        | None -> ()
        a
                        
    let view nav appMsDispatcher (chs:Chart list) =
        Component("Stats") {    
            let! model = Context.Mvu(program nav appMsDispatcher, chs)
            (ContentPage(
                Grid(
                    [ Dimension.Star],
                    [ for _ in model.charts -> Dimension.Star]
                    )
                    {
                        for (i,c) in model.charts do
                            match c.ChartType with
                            | Histogram _ ->                                
                                SfCartesianChart(histogramSeries c)
                                    .chartTitle(c.Title)
                                    .xAxes([numericAxis c.XTitle])
                                    .yAxes([numericAxis c.YTitle ])
                                    .margin(Thickness(5.0))
                                    .gridRow(i)
                            | Bar ->                                
                                SfCartesianChart(barSeries c)
                                    .isTransposed(true)                                    
                                    .chartTitle(c.Title)                                   
                                    .xAxes([categoryAxis c.XTitle])
                                    .yAxes([numericAxis c.YTitle ])
                                    .margin(Thickness(5.0))
                                    .gridRow(i)
                            
                    })
                    .padding(5.)
            )
                .title("Stats")
                .hasBackButton(true)
                .onNavigatedTo(Active)
                .onNavigatedFrom(InActive)                
        }

