using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RowSenseWindows.Controls;

public sealed class AthleteCard : Border
{
    private readonly TextBlock _currentPower;
    private readonly TextBlock _averagePower;
    private readonly TextBlock _tempo;
    private readonly TextBlock _fmax;
    private readonly ForcePlotControl _plot;
    private double _powerSum;
    private int _powerCount;

    public int AthleteNumber { get; }

    public AthleteCard(int athleteNumber)
    {
        AthleteNumber = athleteNumber;
        Margin = new Thickness(6);
        Padding = new Thickness(10);
        BorderThickness = new Thickness(1);
        BorderBrush = Brushes.LightGray;
        CornerRadius = new CornerRadius(8);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var title = new TextBlock
        {
            Text = $"Спортсмен {athleteNumber}",
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(title);

        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 8) };
        _currentPower = Metric(metrics, "Мощность", "0 W");
        _averagePower = Metric(metrics, "Средняя", "0 W");
        _tempo = Metric(metrics, "Темп", "0");
        _fmax = Metric(metrics, "Fmax", "0 N");
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        _plot = new ForcePlotControl { MinHeight = 150 };
        Grid.SetRow(_plot, 2);
        root.Children.Add(_plot);
        Child = root;
    }

    private static TextBlock Metric(Panel parent, string label, string initial)
    {
        var box = new StackPanel { Margin = new Thickness(4) };
        box.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, FontSize = 12 });
        var value = new TextBlock { Text = initial, FontWeight = FontWeights.SemiBold, FontSize = 18 };
        box.Children.Add(value);
        parent.Children.Add(box);
        return value;
    }

    public void ResetAverage()
    {
        _powerSum = 0;
        _powerCount = 0;
        _averagePower.Text = "0 W";
    }

    public void UpdateStroke(float[] forces, float power, ushort tempo)
    {
        _currentPower.Text = $"{power:F0} W";
        _tempo.Text = tempo.ToString();
        _fmax.Text = $"{(forces.Length == 0 ? 0 : forces.Max()):F0} N";
        _powerSum += power;
        _powerCount++;
        _averagePower.Text = $"{(_powerSum / Math.Max(1, _powerCount)):F0} W";
        _plot.SetForces(forces);
    }
}
