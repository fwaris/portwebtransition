namespace FsPlaySamples.Cua.Views

open System.Collections.Generic
open System.Collections.ObjectModel
open System.Runtime.CompilerServices
open System.Windows.Input
open Fabulous
open Fabulous.Maui
open Microsoft.Maui
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics
open Syncfusion.Maui.Toolkit.Charts
open Syncfusion.Maui.Toolkit.Popup

module SfChartHelpers =
    let createSeriesCollection (series: seq<ChartSeries>) =
        let collection = ChartSeriesCollection()
        for item in series do
            collection.Add(item)
        collection

    let createPolarSeriesCollection (series: seq<PolarSeries>) =
        let collection = ChartPolarSeriesCollection()
        for item in series do
            collection.Add(item)
        collection

    let createAnnotations (annotations: seq<ChartAnnotation>) =
        let collection = ChartAnnotationCollection()
        for item in annotations do
            collection.Add(item)
        collection

    let toBrushList (brushes: seq<Brush>) =
        let list = ResizeArray<Brush>()
        for brush in brushes do
            list.Add(brush)
        list :> IList<Brush>

type IFabSfChartBase =
    inherit IFabView

type IFabSfCartesianChart =
    inherit IFabSfChartBase

type IFabSfCircularChart =
    inherit IFabSfChartBase

type IFabSfPolarChart =
    inherit IFabSfChartBase

type IFabSfPopup =
    inherit IFabView

module SfChartBase =
    let Title = Attributes.defineBindableWithEquality<obj>(ChartBase.TitleProperty)
    let Legend = Attributes.defineBindableWithEquality<ChartLegend>(ChartBase.LegendProperty)
    let TooltipBehavior = Attributes.defineBindableWithEquality<ChartTooltipBehavior>(ChartBase.TooltipBehaviorProperty)
    let InteractiveBehavior = Attributes.defineBindableWithEquality<ChartInteractiveBehavior>(ChartBase.InteractiveBehaviorProperty)
    let PlotAreaBackgroundView = Attributes.defineBindableWithEquality<View>(ChartBase.PlotAreaBackgroundViewProperty)

module SfCartesianChart =
    let WidgetKey = Widgets.register<SfCartesianChart>()
    let Series = Attributes.defineBindableWithEquality<ChartSeriesCollection>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.SeriesProperty)
    let EnableSideBySideSeriesPlacement = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.EnableSideBySideSeriesPlacementProperty)
    let IsTransposed = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.IsTransposedProperty)
    let PaletteBrushes = Attributes.defineBindableWithEquality<IList<Brush>>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.PaletteBrushesProperty)
    let ZoomPanBehavior = Attributes.defineBindableWithEquality<ChartZoomPanBehavior>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.ZoomPanBehaviorProperty)
    let SelectionBehavior = Attributes.defineBindableWithEquality<SeriesSelectionBehavior>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.SelectionBehaviorProperty)
    let TrackballBehavior = Attributes.defineBindableWithEquality<ChartTrackballBehavior>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.TrackballBehaviorProperty)
    let Annotations = Attributes.defineBindableWithEquality<ChartAnnotationCollection>(Syncfusion.Maui.Toolkit.Charts.SfCartesianChart.AnnotationsProperty)

    let XAxes =
        Attributes.defineSimpleScalarWithEquality<ChartAxis list>
            "Syncfusion.SfCartesianChart_XAxes"
            (fun _ newValueOpt node ->
                let chart = node.Target :?> SfCartesianChart
                let axes = chart.XAxes
                axes.Clear()
                match newValueOpt with
                | ValueSome values ->
                    for axis in values do
                        axes.Add(axis)
                | ValueNone -> ())

    let YAxes =
        Attributes.defineSimpleScalarWithEquality<RangeAxisBase list>
            "Syncfusion.SfCartesianChart_YAxes"
            (fun _ newValueOpt node ->
                let chart = node.Target :?> SfCartesianChart
                let axes = chart.YAxes
                axes.Clear()
                match newValueOpt with
                | ValueSome values ->
                    for axis in values do
                        axes.Add(axis)
                | ValueNone -> ())

module SfCircularChart =
    let WidgetKey = Widgets.register<SfCircularChart>()
    let Series = Attributes.defineBindableWithEquality<ChartSeriesCollection>(Syncfusion.Maui.Toolkit.Charts.SfCircularChart.SeriesProperty)

module SfPolarChart =
    let WidgetKey = Widgets.register<SfPolarChart>()
    let PrimaryAxis = Attributes.defineBindableWithEquality<ChartAxis>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.PrimaryAxisProperty)
    let SecondaryAxis = Attributes.defineBindableWithEquality<RangeAxisBase>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.SecondaryAxisProperty)
    let Series = Attributes.defineBindableWithEquality<ChartPolarSeriesCollection>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.SeriesProperty)
    let PaletteBrushes = Attributes.defineBindableWithEquality<IList<Brush>>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.PaletteBrushesProperty)
    let GridLineType = Attributes.defineBindableEnum<PolarChartGridLineType>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.GridLineTypeProperty)
    let StartAngle = Attributes.defineBindableEnum<ChartPolarAngle>(Syncfusion.Maui.Toolkit.Charts.SfPolarChart.StartAngleProperty)

module SfPopup =
    let WidgetKey = Widgets.register<SfPopup>()
    let IsOpen = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.IsOpenProperty)
    let StaysOpen = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.StaysOpenProperty)
    let ShowOverlayAlways = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.ShowOverlayAlwaysProperty)
    let ShowHeader = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.ShowHeaderProperty)
    let ShowFooter = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.ShowFooterProperty)
    let ShowCloseButton = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.ShowCloseButtonProperty)
    let IgnoreActionBar = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.IgnoreActionBarProperty)
    let IsFullScreen = Attributes.defineBindableBool(Syncfusion.Maui.Toolkit.Popup.SfPopup.IsFullScreenProperty)
    let AcceptButtonText = Attributes.defineBindableWithEquality<string>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AcceptButtonTextProperty)
    let DeclineButtonText = Attributes.defineBindableWithEquality<string>(Syncfusion.Maui.Toolkit.Popup.SfPopup.DeclineButtonTextProperty)
    let HeaderTitle = Attributes.defineBindableWithEquality<string>(Syncfusion.Maui.Toolkit.Popup.SfPopup.HeaderTitleProperty)
    let Message = Attributes.defineBindableWithEquality<string>(Syncfusion.Maui.Toolkit.Popup.SfPopup.MessageProperty)
    let AcceptCommand = Attributes.defineBindableWithEquality<ICommand>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AcceptCommandProperty)
    let DeclineCommand = Attributes.defineBindableWithEquality<ICommand>(Syncfusion.Maui.Toolkit.Popup.SfPopup.DeclineCommandProperty)
    let ContentTemplate = Attributes.defineBindableWithEquality<DataTemplate>(Syncfusion.Maui.Toolkit.Popup.SfPopup.ContentTemplateProperty)
    let HeaderTemplate = Attributes.defineBindableWithEquality<DataTemplate>(Syncfusion.Maui.Toolkit.Popup.SfPopup.HeaderTemplateProperty)
    let FooterTemplate = Attributes.defineBindableWithEquality<DataTemplate>(Syncfusion.Maui.Toolkit.Popup.SfPopup.FooterTemplateProperty)
    let PopupStyle = Attributes.defineBindableWithEquality<PopupStyle>(Syncfusion.Maui.Toolkit.Popup.SfPopup.PopupStyleProperty)
    let RelativeView = Attributes.defineBindableWithEquality<View>(Syncfusion.Maui.Toolkit.Popup.SfPopup.RelativeViewProperty)
    let Padding = Attributes.defineBindableWithEquality<Thickness>(Syncfusion.Maui.Toolkit.Popup.SfPopup.PaddingProperty)
    let AutoCloseDuration = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AutoCloseDurationProperty)
    let AnimationDuration = Attributes.defineBindableWithEquality<double>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AnimationDurationProperty)
    let AbsoluteX = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AbsoluteXProperty)
    let AbsoluteY = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AbsoluteYProperty)
    let StartX = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.StartXProperty)
    let StartY = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.StartYProperty)
    let HeaderHeight = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.HeaderHeightProperty)
    let FooterHeight = Attributes.defineBindableWithEquality<int>(Syncfusion.Maui.Toolkit.Popup.SfPopup.FooterHeightProperty)
    let AnimationMode = Attributes.defineBindableEnum<PopupAnimationMode>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AnimationModeProperty)
    let AnimationEasing = Attributes.defineBindableEnum<PopupAnimationEasing>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AnimationEasingProperty)
    let AppearanceMode = Attributes.defineBindableEnum<PopupButtonAppearanceMode>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AppearanceModeProperty)
    let AutoSizeMode = Attributes.defineBindableEnum<PopupAutoSizeMode>(Syncfusion.Maui.Toolkit.Popup.SfPopup.AutoSizeModeProperty)
    let OverlayMode = Attributes.defineBindableEnum<PopupOverlayMode>(Syncfusion.Maui.Toolkit.Popup.SfPopup.OverlayModeProperty)
    let RelativePosition = Attributes.defineBindableEnum<PopupRelativePosition>(Syncfusion.Maui.Toolkit.Popup.SfPopup.RelativePositionProperty)

[<AutoOpen>]
module SfPopupBuilders =
    type Fabulous.Maui.View with

        static member inline SfPopup<'msg when 'msg : equality>() =
            WidgetBuilder<'msg, IFabSfPopup>(SfPopup.WidgetKey)

[<AutoOpen>]
module SfCartesianChartBuilders =
    type Fabulous.Maui.View with

        static member inline SfCartesianChart<'msg when 'msg : equality>() =
            WidgetBuilder<'msg, IFabSfCartesianChart>(SfCartesianChart.WidgetKey)

        static member inline SfCartesianChart<'msg when 'msg : equality>(series: seq<ChartSeries>) =
            WidgetBuilder<'msg, IFabSfCartesianChart>(
                SfCartesianChart.WidgetKey,
                SfCartesianChart.Series.WithValue(SfChartHelpers.createSeriesCollection series)
            )

[<AutoOpen>]
module SfCircularChartBuilders =
    type Fabulous.Maui.View with

        static member inline SfCircularChart<'msg when 'msg : equality>() =
            WidgetBuilder<'msg, IFabSfCircularChart>(SfCircularChart.WidgetKey)

        static member inline SfCircularChart<'msg when 'msg : equality>(series: seq<ChartSeries>) =
            WidgetBuilder<'msg, IFabSfCircularChart>(
                SfCircularChart.WidgetKey,
                SfCircularChart.Series.WithValue(SfChartHelpers.createSeriesCollection series)
            )

[<AutoOpen>]
module SfPolarChartBuilders =
    type Fabulous.Maui.View with

        static member inline SfPolarChart<'msg when 'msg : equality>() =
            WidgetBuilder<'msg, IFabSfPolarChart>(SfPolarChart.WidgetKey)

        static member inline SfPolarChart<'msg when 'msg : equality>(series: seq<PolarSeries>) =
            WidgetBuilder<'msg, IFabSfPolarChart>(
                SfPolarChart.WidgetKey,
                SfPolarChart.Series.WithValue(SfChartHelpers.createPolarSeriesCollection series)
            )

[<Extension>]
type SfChartBaseModifiers =

    [<Extension>]
    static member inline chartTitle(this: WidgetBuilder<'msg, #IFabSfChartBase>, value: obj) =
        this.AddScalar(SfChartBase.Title.WithValue(value))

    [<Extension>]
    static member inline legend(this: WidgetBuilder<'msg, #IFabSfChartBase>, value: ChartLegend) =
        this.AddScalar(SfChartBase.Legend.WithValue(value))

    [<Extension>]
    static member inline tooltipBehavior(this: WidgetBuilder<'msg, #IFabSfChartBase>, value: ChartTooltipBehavior) =
        this.AddScalar(SfChartBase.TooltipBehavior.WithValue(value))

    [<Extension>]
    static member inline interactiveBehavior(this: WidgetBuilder<'msg, #IFabSfChartBase>, value: ChartInteractiveBehavior) =
        this.AddScalar(SfChartBase.InteractiveBehavior.WithValue(value))

    [<Extension>]
    static member inline plotAreaBackgroundView(this: WidgetBuilder<'msg, #IFabSfChartBase>, value: View) =
        this.AddScalar(SfChartBase.PlotAreaBackgroundView.WithValue(value))

[<Extension>]
type SfCartesianChartModifiers =

    [<Extension>]
    static member inline reference(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: ViewRef<SfCartesianChart>) =
        this.AddScalar(ViewRefAttributes.ViewRef.WithValue(value.Unbox))

    [<Extension>]
    static member inline series(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: seq<ChartSeries>) =
        this.AddScalar(SfCartesianChart.Series.WithValue(SfChartHelpers.createSeriesCollection value))

    [<Extension>]
    static member inline xAxes(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: ChartAxis list) =
        this.AddScalar(SfCartesianChart.XAxes.WithValue(value))

    [<Extension>]
    static member inline yAxes(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: RangeAxisBase list) =
        this.AddScalar(SfCartesianChart.YAxes.WithValue(value))

    [<Extension>]
    static member inline annotations(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: seq<ChartAnnotation>) =
        this.AddScalar(SfCartesianChart.Annotations.WithValue(SfChartHelpers.createAnnotations value))

    [<Extension>]
    static member inline paletteBrushes(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: seq<Brush>) =
        this.AddScalar(SfCartesianChart.PaletteBrushes.WithValue(SfChartHelpers.toBrushList value))

    [<Extension>]
    static member inline enableSideBySideSeriesPlacement(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: bool) =
        this.AddScalar(SfCartesianChart.EnableSideBySideSeriesPlacement.WithValue(value))

    [<Extension>]
    static member inline isTransposed(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: bool) =
        this.AddScalar(SfCartesianChart.IsTransposed.WithValue(value))

    [<Extension>]
    static member inline zoomPanBehavior(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: ChartZoomPanBehavior) =
        this.AddScalar(SfCartesianChart.ZoomPanBehavior.WithValue(value))

    [<Extension>]
    static member inline selectionBehavior(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: SeriesSelectionBehavior) =
        this.AddScalar(SfCartesianChart.SelectionBehavior.WithValue(value))

    [<Extension>]
    static member inline trackballBehavior(this: WidgetBuilder<'msg, #IFabSfCartesianChart>, value: ChartTrackballBehavior) =
        this.AddScalar(SfCartesianChart.TrackballBehavior.WithValue(value))

[<Extension>]
type SfCircularChartModifiers =

    [<Extension>]
    static member inline reference(this: WidgetBuilder<'msg, #IFabSfCircularChart>, value: ViewRef<SfCircularChart>) =
        this.AddScalar(ViewRefAttributes.ViewRef.WithValue(value.Unbox))

    [<Extension>]
    static member inline series(this: WidgetBuilder<'msg, #IFabSfCircularChart>, value: seq<ChartSeries>) =
        this.AddScalar(SfCircularChart.Series.WithValue(SfChartHelpers.createSeriesCollection value))

[<Extension>]
type SfPolarChartModifiers =

    [<Extension>]
    static member inline reference(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: ViewRef<SfPolarChart>) =
        this.AddScalar(ViewRefAttributes.ViewRef.WithValue(value.Unbox))

    [<Extension>]
    static member inline primaryAxis(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: ChartAxis) =
        this.AddScalar(SfPolarChart.PrimaryAxis.WithValue(value))

    [<Extension>]
    static member inline secondaryAxis(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: RangeAxisBase) =
        this.AddScalar(SfPolarChart.SecondaryAxis.WithValue(value))

    [<Extension>]
    static member inline series(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: seq<PolarSeries>) =
        this.AddScalar(SfPolarChart.Series.WithValue(SfChartHelpers.createPolarSeriesCollection value))

    [<Extension>]
    static member inline paletteBrushes(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: seq<Brush>) =
        this.AddScalar(SfPolarChart.PaletteBrushes.WithValue(SfChartHelpers.toBrushList value))

    [<Extension>]
    static member inline gridLineType(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: PolarChartGridLineType) =
        this.AddScalar(SfPolarChart.GridLineType.WithValue(value))

    [<Extension>]
    static member inline startAngle(this: WidgetBuilder<'msg, #IFabSfPolarChart>, value: ChartPolarAngle) =
        this.AddScalar(SfPolarChart.StartAngle.WithValue(value))

[<Extension>]
type SfPopupModifiers =

    [<Extension>]
    static member inline reference(this: WidgetBuilder<'msg, #IFabSfPopup>, value: ViewRef<SfPopup>) =
        this.AddScalar(ViewRefAttributes.ViewRef.WithValue(value.Unbox))

    [<Extension>]
    static member inline isOpen(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.IsOpen.WithValue(value))

    [<Extension>]
    static member inline staysOpen(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.StaysOpen.WithValue(value))

    [<Extension>]
    static member inline showOverlayAlways(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.ShowOverlayAlways.WithValue(value))

    [<Extension>]
    static member inline showHeader(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.ShowHeader.WithValue(value))

    [<Extension>]
    static member inline showFooter(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.ShowFooter.WithValue(value))

    [<Extension>]
    static member inline showCloseButton(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.ShowCloseButton.WithValue(value))

    [<Extension>]
    static member inline ignoreActionBar(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.IgnoreActionBar.WithValue(value))

    [<Extension>]
    static member inline isFullScreen(this: WidgetBuilder<'msg, #IFabSfPopup>, value: bool) =
        this.AddScalar(SfPopup.IsFullScreen.WithValue(value))

    [<Extension>]
    static member inline acceptButtonText(this: WidgetBuilder<'msg, #IFabSfPopup>, value: string) =
        this.AddScalar(SfPopup.AcceptButtonText.WithValue(value))

    [<Extension>]
    static member inline declineButtonText(this: WidgetBuilder<'msg, #IFabSfPopup>, value: string) =
        this.AddScalar(SfPopup.DeclineButtonText.WithValue(value))

    [<Extension>]
    static member inline headerTitle(this: WidgetBuilder<'msg, #IFabSfPopup>, value: string) =
        this.AddScalar(SfPopup.HeaderTitle.WithValue(value))

    [<Extension>]
    static member inline message(this: WidgetBuilder<'msg, #IFabSfPopup>, value: string) =
        this.AddScalar(SfPopup.Message.WithValue(value))

    [<Extension>]
    static member inline acceptCommand(this: WidgetBuilder<'msg, #IFabSfPopup>, value: ICommand) =
        this.AddScalar(SfPopup.AcceptCommand.WithValue(value))

    [<Extension>]
    static member inline declineCommand(this: WidgetBuilder<'msg, #IFabSfPopup>, value: ICommand) =
        this.AddScalar(SfPopup.DeclineCommand.WithValue(value))

    [<Extension>]
    static member inline contentTemplate(this: WidgetBuilder<'msg, #IFabSfPopup>, value: DataTemplate) =
        this.AddScalar(SfPopup.ContentTemplate.WithValue(value))

    [<Extension>]
    static member inline headerTemplate(this: WidgetBuilder<'msg, #IFabSfPopup>, value: DataTemplate) =
        this.AddScalar(SfPopup.HeaderTemplate.WithValue(value))

    [<Extension>]
    static member inline footerTemplate(this: WidgetBuilder<'msg, #IFabSfPopup>, value: DataTemplate) =
        this.AddScalar(SfPopup.FooterTemplate.WithValue(value))

    [<Extension>]
    static member inline popupStyle(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupStyle) =
        this.AddScalar(SfPopup.PopupStyle.WithValue(value))

    [<Extension>]
    static member inline relativeView(this: WidgetBuilder<'msg, #IFabSfPopup>, value: View) =
        this.AddScalar(SfPopup.RelativeView.WithValue(value))

    [<Extension>]
    static member inline padding(this: WidgetBuilder<'msg, #IFabSfPopup>, value: Thickness) =
        this.AddScalar(SfPopup.Padding.WithValue(value))

    [<Extension>]
    static member inline autoCloseDuration(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.AutoCloseDuration.WithValue(value))

    [<Extension>]
    static member inline animationDuration(this: WidgetBuilder<'msg, #IFabSfPopup>, value: double) =
        this.AddScalar(SfPopup.AnimationDuration.WithValue(value))

    [<Extension>]
    static member inline absoluteX(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.AbsoluteX.WithValue(value))

    [<Extension>]
    static member inline absoluteY(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.AbsoluteY.WithValue(value))

    [<Extension>]
    static member inline startX(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.StartX.WithValue(value))

    [<Extension>]
    static member inline startY(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.StartY.WithValue(value))

    [<Extension>]
    static member inline headerHeight(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.HeaderHeight.WithValue(value))

    [<Extension>]
    static member inline footerHeight(this: WidgetBuilder<'msg, #IFabSfPopup>, value: int) =
        this.AddScalar(SfPopup.FooterHeight.WithValue(value))

    [<Extension>]
    static member inline animationMode(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupAnimationMode) =
        this.AddScalar(SfPopup.AnimationMode.WithValue(value))

    [<Extension>]
    static member inline animationEasing(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupAnimationEasing) =
        this.AddScalar(SfPopup.AnimationEasing.WithValue(value))

    [<Extension>]
    static member inline appearanceMode(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupButtonAppearanceMode) =
        this.AddScalar(SfPopup.AppearanceMode.WithValue(value))

    [<Extension>]
    static member inline autoSizeMode(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupAutoSizeMode) =
        this.AddScalar(SfPopup.AutoSizeMode.WithValue(value))

    [<Extension>]
    static member inline overlayMode(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupOverlayMode) =
        this.AddScalar(SfPopup.OverlayMode.WithValue(value))

    [<Extension>]
    static member inline relativePosition(this: WidgetBuilder<'msg, #IFabSfPopup>, value: PopupRelativePosition) =
        this.AddScalar(SfPopup.RelativePosition.WithValue(value))

