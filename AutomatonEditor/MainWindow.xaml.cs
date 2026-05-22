using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomatonEditor;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return (Brush)new BrushConverter().ConvertFrom(hex)!; }
            catch { }
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public partial class MainWindow : Window
{
    private readonly Automaton _automaton = new();

    private State? _dragState;
    private Point _dragStartPos;
    private Point _dragOffset;
    private bool _isDragging;
    private int _stateCounter;

    private const double DragThreshold = 4.0;
    private const double ArrowSize = 11.0;
    private static readonly Brush DefaultStroke = new SolidColorBrush(Color.FromRgb(50, 50, 50));
    private static readonly Brush SelectedStroke = Brushes.DodgerBlue;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _automaton;

        _automaton.States.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (State s in args.NewItems)
                    s.PropertyChanged += State_PropertyChanged;
            if (args.OldItems != null)
                foreach (State s in args.OldItems)
                    s.PropertyChanged -= State_PropertyChanged;
            RedrawTransitions();
        };

        _automaton.Transitions.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (Transition t in args.NewItems)
                    t.PropertyChanged += Transition_PropertyChanged;
            if (args.OldItems != null)
                foreach (Transition t in args.OldItems)
                    t.PropertyChanged -= Transition_PropertyChanged;
            RedrawTransitions();
            UpdateAlphabetDisplay();
        };
    }

    private void State_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "X" or "Y" or "Radius")
            RedrawTransitions();
    }

    private void Transition_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RedrawTransitions();
        if (e.PropertyName == "Label")
            UpdateAlphabetDisplay();
    }

    private void DrawingArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DrawingArea);
        var state = FindStateAt(pos);

        if (state != null)
        {
            SelectTransition(null);
            _dragState = state;
            _dragStartPos = pos;
            _dragOffset = new Point(pos.X - state.X, pos.Y - state.Y);
            _isDragging = false;
            Mouse.Capture(DrawingArea);
            e.Handled = true;
            return;
        }

        var transition = FindTransitionAt(pos);
        if (transition != null)
        {
            SelectState(null);
            SelectTransition(transition);
            e.Handled = true;
            return;
        }

        SelectState(null);
        SelectTransition(null);

        if (e.ClickCount == 2)
        {
            AddState(pos);
            e.Handled = true;
        }
    }

    private void DrawingArea_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragState == null || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(DrawingArea);
        if (!_isDragging)
        {
            if ((pos - _dragStartPos).Length < DragThreshold) return;
            _isDragging = true;
        }

        _dragState.X = Math.Max(0, Math.Min(DrawingArea.ActualWidth - _dragState.Diameter, pos.X - _dragOffset.X));
        _dragState.Y = Math.Max(0, Math.Min(DrawingArea.ActualHeight - _dragState.Diameter, pos.Y - _dragOffset.Y));
    }

    private void DrawingArea_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragState == null) return;
        Mouse.Capture(null);
        if (!_isDragging)
            SelectState(_dragState);
        _dragState = null;
        _isDragging = false;
        e.Handled = true;
    }

    private State? FindStateAt(Point point)
    {
        State? found = null;
        VisualTreeHelper.HitTest(StatesControl, null,
            r =>
            {
                if (r.VisualHit is FrameworkElement fe && fe.DataContext is State s)
                {
                    found = s;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(point));
        return found;
    }

    private Transition? FindTransitionAt(Point point)
    {
        Transition? found = null;
        VisualTreeHelper.HitTest(TransitionsCanvas, null,
            r =>
            {
                if (r.VisualHit is FrameworkElement fe && fe.Tag is Transition t)
                {
                    found = t;
                    return HitTestResultBehavior.Stop;
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(point));
        return found;
    }

    private void AddState(Point center)
    {
        var state = new State
        {
            Name = $"q{_stateCounter++}",
            X = center.X - 25,
            Y = center.Y - 25,
            IsInitial = _automaton.States.Count == 0
        };
        _automaton.States.Add(state);
        SelectState(state);
    }

    private void SelectState(State? state)
    {
        if (_automaton.SelectedState != null)
            _automaton.SelectedState.IsSelected = false;

        _automaton.SelectedState = state;

        bool enabled = state != null;
        DeleteButton.IsEnabled = enabled;
        AcceptingCheckbox.IsEnabled = enabled;
        InitialCheckbox.IsEnabled = enabled;
        StateAppearancePanel.IsEnabled = enabled;

        AcceptingCheckbox.IsChecked = state?.IsAccepting ?? false;
        InitialCheckbox.IsChecked = state?.IsInitial ?? false;

        if (state != null)
        {
            state.IsSelected = true;
            FillColorBox.Text = state.FillColor;
            StrokeColorBox.Text = state.StrokeColor;
            RadiusSlider.Value = state.Radius;
            ThicknessSlider.Value = state.StrokeThickness;
        }
        else
        {
            FillColorBox.Text = "#FFFFFF";
            StrokeColorBox.Text = "#222222";
            RadiusSlider.Value = 25;
            ThicknessSlider.Value = 2;
        }
    }

    private void SelectTransition(Transition? transition)
    {
        if (_automaton.SelectedTransition != null)
            _automaton.SelectedTransition.IsSelected = false;
        _automaton.SelectedTransition = transition;
        DeleteTransitionButton.IsEnabled = transition != null;
        if (transition != null)
            transition.IsSelected = true;
    }

    private void DeleteState_Click(object sender, RoutedEventArgs e)
    {
        var state = _automaton.SelectedState;
        if (state == null) return;
        foreach (var t in _automaton.Transitions.Where(t => t.Source == state || t.Target == state).ToList())
            _automaton.Transitions.Remove(t);
        SelectState(null);
        _automaton.States.Remove(state);
    }

    private void Accepting_Click(object sender, RoutedEventArgs e)
    {
        if (_automaton.SelectedState != null)
            _automaton.SelectedState.IsAccepting = AcceptingCheckbox.IsChecked == true;
    }

    private void Initial_Click(object sender, RoutedEventArgs e)
    {
        if (_automaton.SelectedState == null) return;
        if (InitialCheckbox.IsChecked == true)
        {
            foreach (var s in _automaton.States)
                s.IsInitial = false;
            _automaton.SelectedState.IsInitial = true;
        }
        else
        {
            _automaton.SelectedState.IsInitial = false;
        }
    }

    private void FillColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_automaton.SelectedState == null) return;
        _automaton.SelectedState.FillColor = FillColorBox.Text;
        UpdateColorPreview(FillPreview, FillColorBox.Text);
    }

    private void StrokeColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_automaton.SelectedState == null) return;
        _automaton.SelectedState.StrokeColor = StrokeColorBox.Text;
        UpdateColorPreview(StrokePreview, StrokeColorBox.Text);
    }

    private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RadiusValue != null)
            RadiusValue.Text = $"{e.NewValue:0}";
        if (_automaton.SelectedState == null) return;
        _automaton.SelectedState.Radius = e.NewValue;
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThicknessValue != null)
            ThicknessValue.Text = $"{e.NewValue:0.#}";
        if (_automaton.SelectedState == null) return;
        _automaton.SelectedState.StrokeThickness = e.NewValue;
    }

    private void UpdateColorPreview(Rectangle preview, string hex)
    {
        try { preview.Fill = (Brush)new BrushConverter().ConvertFrom(hex)!; }
        catch { }
    }

    private void AddTransition_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceStateCombo.SelectedItem as State;
        var target = TargetStateCombo.SelectedItem as State;
        if (source == null || target == null) return;
        _automaton.Transitions.Add(new Transition
        {
            Source = source,
            Target = target,
            Label = LabelBox.Text.Trim()
        });
        LabelBox.Clear();
    }

    private void DeleteTransition_Click(object sender, RoutedEventArgs e)
    {
        var t = _automaton.SelectedTransition;
        if (t == null) return;
        SelectTransition(null);
        _automaton.Transitions.Remove(t);
    }

    private void RedrawTransitions()
    {
        TransitionsCanvas.Children.Clear();
        foreach (var t in _automaton.Transitions)
            DrawTransition(t);
    }

    private void DrawTransition(Transition t)
    {
        if (t.Source == t.Target)
            DrawSelfLoop(t);
        else
            DrawDirectedArrow(t);
    }

    private void DrawDirectedArrow(Transition t)
    {
        double scx = t.Source.X + t.Source.Radius;
        double scy = t.Source.Y + t.Source.Radius;
        double tcx = t.Target.X + t.Target.Radius;
        double tcy = t.Target.Y + t.Target.Radius;

        double dx = tcx - scx, dy = tcy - scy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;

        double nx = dx / len, ny = dy / len;
        double px = -ny, py = nx;

        bool hasReverse = _automaton.Transitions.Any(o => o != t && o.Source == t.Target && o.Target == t.Source);
        double offset = hasReverse ? 12.0 : 0.0;
        double ox = px * offset, oy = py * offset;

        double x1 = scx + nx * t.Source.Radius + ox;
        double y1 = scy + ny * t.Source.Radius + oy;
        double x2 = tcx - nx * t.Target.Radius + ox;
        double y2 = tcy - ny * t.Target.Radius + oy;

        Brush stroke = t.IsSelected ? SelectedStroke : DefaultStroke;

        var hitLine = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = Brushes.Transparent, StrokeThickness = 10,
            Tag = t
        };
        TransitionsCanvas.Children.Add(hitLine);

        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = 2,
            IsHitTestVisible = false
        };
        TransitionsCanvas.Children.Add(line);

        AddArrowHead(x2, y2, nx, ny, stroke);

        if (!string.IsNullOrEmpty(t.Label))
        {
            var lbl = new TextBlock
            {
                Text = t.Label,
                Foreground = stroke,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromArgb(200, 245, 245, 245))
            };
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lbl, (x1 + x2) / 2 + px * 14 - lbl.DesiredSize.Width / 2);
            Canvas.SetTop(lbl, (y1 + y2) / 2 + py * 14 - lbl.DesiredSize.Height / 2);
            TransitionsCanvas.Children.Add(lbl);
        }
    }

    private void DrawSelfLoop(Transition t)
    {
        double cx = t.Source.X + t.Source.Radius;
        double cy = t.Source.Y + t.Source.Radius;
        double r = t.Source.Radius;

        double sin45 = Math.Sqrt(2) / 2;
        double startX = cx - r * sin45;
        double startY = cy - r * sin45;
        double endX = cx + r * sin45;
        double endY = cy - r * sin45;

        double cp1x = cx - r * 1.4;
        double cp1y = cy - r * 2.8;
        double cp2x = cx + r * 1.4;
        double cp2y = cy - r * 2.8;

        var figure = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false };
        figure.Segments.Add(new BezierSegment(
            new Point(cp1x, cp1y),
            new Point(cp2x, cp2y),
            new Point(endX, endY), true));

        var geo = new PathGeometry();
        geo.Figures.Add(figure);

        Brush stroke = t.IsSelected ? SelectedStroke : DefaultStroke;

        var hitPath = new Path
        {
            Data = geo,
            Stroke = Brushes.Transparent,
            StrokeThickness = 10,
            Fill = Brushes.Transparent,
            Tag = t
        };
        TransitionsCanvas.Children.Add(hitPath);

        var visPath = new Path
        {
            Data = geo,
            Stroke = stroke,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        TransitionsCanvas.Children.Add(visPath);

        double aDx = endX - cp2x;
        double aDy = endY - cp2y;
        double aLen = Math.Sqrt(aDx * aDx + aDy * aDy);
        if (aLen > 0)
            AddArrowHead(endX, endY, aDx / aLen, aDy / aLen, stroke);

        if (!string.IsNullOrEmpty(t.Label))
        {
            var lbl = new TextBlock { Text = t.Label, Foreground = stroke, FontSize = 11 };
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lbl, cx - lbl.DesiredSize.Width / 2);
            Canvas.SetTop(lbl, cy - r * 3.0 - lbl.DesiredSize.Height);
            TransitionsCanvas.Children.Add(lbl);
        }
    }

    private void AddArrowHead(double x, double y, double nx, double ny, Brush fill)
    {
        double cos = Math.Cos(Math.PI / 6);
        double sin = Math.Sin(Math.PI / 6);
        double lx = x + (-nx * cos + ny * sin) * ArrowSize;
        double ly = y + (-ny * cos - nx * sin) * ArrowSize;
        double rx = x + (-nx * cos - ny * sin) * ArrowSize;
        double ry = y + (-ny * cos + nx * sin) * ArrowSize;

        TransitionsCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection { new(x, y), new(lx, ly), new(rx, ry) },
            Fill = fill,
            IsHitTestVisible = false
        });
    }

    private void UpdateAlphabetDisplay()
    {
        var symbols = _automaton.Transitions
            .SelectMany(t => (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s);
        var joined = string.Join(", ", symbols);
        AlphabetText.Text = string.IsNullOrEmpty(joined) ? "∅" : "{ " + joined + " }";
    }
}
