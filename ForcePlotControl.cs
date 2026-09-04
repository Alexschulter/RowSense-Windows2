using System.Windows;
using System.Windows.Media;

namespace RowSenseWindows.Controls;

public sealed class ForcePlotControl : FrameworkElement
{
    private float[] _forces = Array.Empty<float>();

    public void SetForces(float[] forces)
    {
        _forces = forces?.ToArray() ?? Array.Empty<float>();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(Brushes.WhiteSmoke, new Pen(Brushes.LightGray, 1), rect);
        if (_forces.Length < 2 || ActualWidth <= 10 || ActualHeight <= 10) return;

        float max = Math.Max(1f, _forces.Max());
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < _forces.Length; i++)
            {
                double x = 6 + i * (ActualWidth - 12) / (_forces.Length - 1);
                double y = ActualHeight - 6 - Math.Max(0, _forces[i]) / max * (ActualHeight - 12);
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(Brushes.Black, 2), geometry);
    }
}
