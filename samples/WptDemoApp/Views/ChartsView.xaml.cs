using ScottPlot;
using ScottPlot.WPF;
using Ason;
using System.Windows;
using System.Windows.Controls;
using WpfSampleApp.AI;
using WpfSampleApp.ViewModels;

namespace WpfSampleApp.Views; 
/// <summary>
/// Interaction logic for AnalyticsView.xaml
/// </summary>
public partial class ChartsView : UserControl {
    ScottPlot.Color accentPlotColor = new ScottPlot.Color(SystemColors.AccentColor.R, SystemColors.AccentColor.G, SystemColors.AccentColor.B, SystemColors.AccentColor.A);
    RootOperator? rootOperator;
    public ChartsView() {
        InitializeComponent();
    }

    WpfPlot? plot;
    public void AddBarChart(BarValue[] barValues, string xAxisCaption, string yAxisCaption) {
        plot = new WpfPlot();

        plot.Plot.Axes.Bottom.MajorTickStyle.Length = 2;
        plot.Plot.HideGrid();

        plot.Plot.Axes.Right.FrameLineStyle.Width = 0;
        plot.Plot.Axes.Top.FrameLineStyle.Width = 0;
        plot.Plot.Axes.Left.FrameLineStyle.Width = 0;


        plot.Plot.Axes.Left.TickLabelStyle.IsVisible = false;
        plot.Plot.Axes.Left.MajorTickStyle.Length = 0;
        plot.Plot.Axes.Left.MinorTickStyle.Length = 0;

        AssignBarValues(barValues, xAxisCaption, yAxisCaption);

        chartArea.Content = plot;

        plot.Refresh();
    }

    public void AssignBarValues(BarValue[] barValues, string xAxisCaption, string yAxisCaption) {
        if (plot == null) return;
        var barPlot = plot.Plot.Add.Bars(barValues.Select(bv => bv.Value).ToArray());

        barPlot.ValueLabelStyle.FontSize = 18;

        // Axis labels
        plot.Plot.XLabel(xAxisCaption);
        plot.Plot.YLabel(yAxisCaption);

        foreach (var bar in barPlot.Bars) {
            bar.ValueLabel = bar.Value.ToString();
            bar.FillColor = accentPlotColor;
            bar.LineColor = ScottPlot.Colors.Transparent;
        }

        Tick[] ticks = new Tick[barValues.Length];
        for (int i = 0; i < barValues.Length; i++) {
            ticks[i] = new Tick(i, barValues[i].Caption);
        }

        plot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        rootOperator = ((ChartsViewModel)DataContext).RootOperator;
        rootOperator.AttachChildOperator<ChartsViewOperator>(this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        rootOperator?.DetachChildOperator<ChartsViewOperator>();
    }
}

[AsonModel("ALWAYS convert Value to double explicitly")]
public class BarValue {
    public string Caption { get; set; } = string.Empty;
    public double Value { get; set; }
}
