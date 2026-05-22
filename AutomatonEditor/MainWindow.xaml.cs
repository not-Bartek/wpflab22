using System.Globalization;
using System.IO;
using System.Linq;
using Path = System.Windows.Shapes.Path;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

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
    private class AutomatonDto
    {
        public MetaDto? Meta { get; set; }
        public List<StateDto>? States { get; set; }
        public List<TransitionDto>? Transitions { get; set; }
    }
    private class MetaDto
    {
        public string? Description { get; set; }
        public List<string>? Alphabet { get; set; }
        public string? Created { get; set; }
    }
    private class StateDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsStart { get; set; }
        public bool IsAccepting { get; set; }
        public PositionDto? Position { get; set; }
        public AppearanceDto? Appearance { get; set; }
    }
    private class PositionDto { public double X { get; set; } public double Y { get; set; } }
    private class AppearanceDto
    {
        public double Radius { get; set; } = 25;
        public string FillColor { get; set; } = "#FFFFFF";
        public string StrokeColor { get; set; } = "#222222";
        public double StrokeThickness { get; set; } = 2;
    }
    private class TransitionDto
    {
        public int FromStateId { get; set; }
        public int ToStateId { get; set; }
        public string? Symbol { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly Automaton _automaton = new();

    private State? _dragState;
    private Point _dragStartPos;
    private Point _dragOffset;
    private bool _isDragging;
    private int _stateCounter;
    private bool _updatingControls;
    private bool _isEditorMode = true;

    private const double DragThreshold = 4.0;
    private const double ArrowSize = 11.0;
    private static readonly Brush DefaultStroke = new SolidColorBrush(Color.FromRgb(50, 50, 50));
    private static readonly Brush SelectedStroke = Brushes.DodgerBlue;
    private static readonly Brush ActiveStroke = new SolidColorBrush(Color.FromRgb(0, 160, 50));

    private int _simStep = -1;
    private State? _simCurrentState;
    private string _inputWord = "";
    private readonly List<(State From, string Symbol, Transition Trans, State To)> _simHistory = new();
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _historyItems = new();
    private readonly DispatcherTimer _animTimer = new();
    private Transition? _simActiveTransition;

    private enum StepResult { Success, WordComplete, NoTransition }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _automaton;
        HistoryList.ItemsSource = _historyItems;

        _animTimer.Tick += AnimTimer_Tick;

        _automaton.States.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (State s in args.NewItems)
                    s.PropertyChanged += State_PropertyChanged;
            if (args.OldItems != null)
                foreach (State s in args.OldItems)
                    s.PropertyChanged -= State_PropertyChanged;
            ResetSimulationState();
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
            ResetSimulationState();
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
        if (e.PropertyName is not "IsActive")
            RedrawTransitions();
        if (e.PropertyName == "Label")
            UpdateAlphabetDisplay();
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not TabControl) return;
        _isEditorMode = EditorTab.IsSelected;

        if (!_isEditorMode)
        {
            if (_automaton.States.Count == 0 || !_automaton.States.Any(s => s.IsInitial))
            {
                MessageBox.Show("Automat nie ma stanu początkowego. Najpierw utwórz automat w edytorze.",
                    "Symulacja", MessageBoxButton.OK, MessageBoxImage.Information);
                EditorTab.IsSelected = true;
                return;
            }
            SelectState(null);
            SelectTransition(null);
        }
        else
        {
            ClearSimHighlights();
            RedrawTransitions();
        }
    }

    private void DrawingArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditorMode) return;

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
        if (!_isEditorMode || _dragState == null || e.LeftButton != MouseButtonState.Pressed) return;

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
        if (!_isEditorMode || _dragState == null) return;
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
                { found = s; return HitTestResultBehavior.Stop; }
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
                { found = t; return HitTestResultBehavior.Stop; }
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

        _updatingControls = true;
        try
        {
            AcceptingCheckbox.IsChecked = state?.IsAccepting ?? false;
            InitialCheckbox.IsChecked = state?.IsInitial ?? false;
            FillColorBox.Text = state?.FillColor ?? "#FFFFFF";
            StrokeColorBox.Text = state?.StrokeColor ?? "#222222";
            RadiusSlider.Value = state?.Radius ?? 25;
            ThicknessSlider.Value = state?.StrokeThickness ?? 2;
        }
        finally
        {
            _updatingControls = false;
        }

        UpdateColorPreview(FillPreview, FillColorBox.Text);
        UpdateColorPreview(StrokePreview, StrokeColorBox.Text);

        if (state != null)
            state.IsSelected = true;
    }

    private void SelectTransition(Transition? transition)
    {
        if (_automaton.SelectedTransition != null)
            _automaton.SelectedTransition.IsSelected = false;
        _automaton.SelectedTransition = transition;
        DeleteTransitionButton.IsEnabled = transition != null;
        if (transition != null)
            transition.IsSelected = true;
        RedrawTransitions();
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
            foreach (var s in _automaton.States) s.IsInitial = false;
            _automaton.SelectedState.IsInitial = true;
        }
        else
        {
            _automaton.SelectedState.IsInitial = false;
        }
    }

    private void FillColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingControls) return;
        UpdateColorPreview(FillPreview, FillColorBox.Text);
        if (_automaton.SelectedState != null)
            _automaton.SelectedState.FillColor = FillColorBox.Text;
    }

    private void StrokeColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingControls) return;
        UpdateColorPreview(StrokePreview, StrokeColorBox.Text);
        if (_automaton.SelectedState != null)
            _automaton.SelectedState.StrokeColor = StrokeColorBox.Text;
    }

    private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RadiusValue != null) RadiusValue.Text = $"{e.NewValue:0}";
        if (_updatingControls) return;
        if (_automaton.SelectedState != null) _automaton.SelectedState.Radius = e.NewValue;
    }

    private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThicknessValue != null) ThicknessValue.Text = $"{e.NewValue:0.#}";
        if (_updatingControls) return;
        if (_automaton.SelectedState != null) _automaton.SelectedState.StrokeThickness = e.NewValue;
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

        var label = LabelBox.Text.Trim();
        if (string.IsNullOrEmpty(label))
        {
            MessageBox.Show("Etykieta przejścia nie może być pusta.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newSymbols = label.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim()).ToList();

        var usedSymbols = _automaton.Transitions
            .Where(t => t.Source == source)
            .SelectMany(t => (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.Trim()))
            .ToHashSet();

        var conflicts = newSymbols.Where(s => usedSymbols.Contains(s)).ToList();
        if (conflicts.Count > 0)
        {
            MessageBox.Show(
                $"Ze stanu {source.Name} istnieje już przejście dla symboli: {string.Join(", ", conflicts)}.",
                "Konflikt DFA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = _automaton.Transitions.FirstOrDefault(t => t.Source == source && t.Target == target);
        if (existing != null)
        {
            var merged = (existing.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(newSymbols).Distinct().OrderBy(s => s);
            existing.Label = string.Join(",", merged);
        }
        else
        {
            _automaton.Transitions.Add(new Transition { Source = source, Target = target, Label = label });
        }
        LabelBox.Clear();
    }

    private void DeleteTransition_Click(object sender, RoutedEventArgs e)
    {
        var t = _automaton.SelectedTransition;
        if (t == null) return;
        SelectTransition(null);
        _automaton.Transitions.Remove(t);
    }

    private void UpdateAlphabetDisplay()
    {
        var symbols = _automaton.Transitions
            .SelectMany(t => (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct().OrderBy(s => s);
        var joined = string.Join(", ", symbols);
        AlphabetText.Text = string.IsNullOrEmpty(joined) ? "∅" : "{ " + joined + " }";
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

    private Brush GetTransitionBrush(Transition t) =>
        t.IsActive ? ActiveStroke : t.IsSelected ? SelectedStroke : DefaultStroke;

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
        Brush stroke = GetTransitionBrush(t);

        double x1, y1, x2, y2;

        if (hasReverse)
        {
            double bowAmount = Math.Min(len * 0.25, 40.0);
            double midX = (scx + tcx) / 2 + px * bowAmount;
            double midY = (scy + tcy) / 2 + py * bowAmount;

            x1 = scx + nx * t.Source.Radius + px * 4;
            y1 = scy + ny * t.Source.Radius + py * 4;
            x2 = tcx - nx * t.Target.Radius + px * 4;
            y2 = tcy - ny * t.Target.Radius + py * 4;

            var figure = new PathFigure { StartPoint = new Point(x1, y1) };
            figure.Segments.Add(new QuadraticBezierSegment(new Point(midX, midY), new Point(x2, y2), true));
            var geo = new PathGeometry();
            geo.Figures.Add(figure);

            TransitionsCanvas.Children.Add(new Path
            {
                Data = geo, Stroke = Brushes.Transparent, StrokeThickness = 10,
                Fill = Brushes.Transparent, Tag = t
            });
            TransitionsCanvas.Children.Add(new Path
            {
                Data = geo, Stroke = stroke, StrokeThickness = 2,
                Fill = Brushes.Transparent, IsHitTestVisible = false
            });

            double tanDx = x2 - midX, tanDy = y2 - midY;
            double tanLen = Math.Sqrt(tanDx * tanDx + tanDy * tanDy);
            if (tanLen > 0) AddArrowHead(x2, y2, tanDx / tanLen, tanDy / tanLen, stroke);

            if (!string.IsNullOrEmpty(t.Label))
                PlaceLabel(t.Label, midX + px * 10, midY + py * 10, stroke);
        }
        else
        {
            x1 = scx + nx * t.Source.Radius;
            y1 = scy + ny * t.Source.Radius;
            x2 = tcx - nx * t.Target.Radius;
            y2 = tcy - ny * t.Target.Radius;

            TransitionsCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = Brushes.Transparent, StrokeThickness = 10, Tag = t
            });
            TransitionsCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = stroke, StrokeThickness = 2, IsHitTestVisible = false
            });

            AddArrowHead(x2, y2, nx, ny, stroke);

            if (!string.IsNullOrEmpty(t.Label))
                PlaceLabel(t.Label, (x1 + x2) / 2 + px * 14, (y1 + y2) / 2 + py * 14, stroke);
        }
    }

    private void DrawSelfLoop(Transition t)
    {
        double cx = t.Source.X + t.Source.Radius;
        double cy = t.Source.Y + t.Source.Radius;
        double r = t.Source.Radius;
        double s45 = Math.Sqrt(2) / 2;

        double startX = cx - r * s45, startY = cy - r * s45;
        double endX = cx + r * s45, endY = cy - r * s45;
        double cp1x = cx - r * 1.4, cp1y = cy - r * 2.8;
        double cp2x = cx + r * 1.4, cp2y = cy - r * 2.8;

        var figure = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false };
        figure.Segments.Add(new BezierSegment(
            new Point(cp1x, cp1y), new Point(cp2x, cp2y), new Point(endX, endY), true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);

        Brush stroke = GetTransitionBrush(t);

        TransitionsCanvas.Children.Add(new Path
        {
            Data = geo, Stroke = Brushes.Transparent, StrokeThickness = 10,
            Fill = Brushes.Transparent, Tag = t
        });
        TransitionsCanvas.Children.Add(new Path
        {
            Data = geo, Stroke = stroke, StrokeThickness = 2,
            Fill = Brushes.Transparent, IsHitTestVisible = false
        });

        double aDx = endX - cp2x, aDy = endY - cp2y;
        double aLen = Math.Sqrt(aDx * aDx + aDy * aDy);
        if (aLen > 0) AddArrowHead(endX, endY, aDx / aLen, aDy / aLen, stroke);

        if (!string.IsNullOrEmpty(t.Label))
        {
            var lbl = new TextBlock { Text = t.Label, Foreground = stroke, FontSize = 11 };
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lbl, cx - lbl.DesiredSize.Width / 2);
            Canvas.SetTop(lbl, cy - r * 3.2 - lbl.DesiredSize.Height);
            TransitionsCanvas.Children.Add(lbl);
        }
    }

    private void AddArrowHead(double x, double y, double nx, double ny, Brush fill)
    {
        double cos = Math.Cos(Math.PI / 6), sin = Math.Sin(Math.PI / 6);
        double lx = x + (-nx * cos + ny * sin) * ArrowSize;
        double ly = y + (-ny * cos - nx * sin) * ArrowSize;
        double rx = x + (-nx * cos - ny * sin) * ArrowSize;
        double ry = y + (-ny * cos + nx * sin) * ArrowSize;
        TransitionsCanvas.Children.Add(new Polygon
        {
            Points = new PointCollection { new(x, y), new(lx, ly), new(rx, ry) },
            Fill = fill, IsHitTestVisible = false
        });
    }

    private void PlaceLabel(string text, double cx, double cy, Brush stroke)
    {
        var lbl = new TextBlock
        {
            Text = text, Foreground = stroke, FontSize = 11,
            Background = new SolidColorBrush(Color.FromArgb(200, 245, 245, 245))
        };
        lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(lbl, cx - lbl.DesiredSize.Width / 2);
        Canvas.SetTop(lbl, cy - lbl.DesiredSize.Height / 2);
        TransitionsCanvas.Children.Add(lbl);
    }

    private void MenuImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json|Wszystkie pliki (*.*)|*.*",
            Title = "Importuj automat",
            InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
        };
        if (dlg.ShowDialog() != true) return;
        ImportFromJson(dlg.FileName);
    }

    private void MenuExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_automaton.States.Count == 0)
        {
            MessageBox.Show("Brak stanów do wyeksportowania.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            Title = "Eksportuj automat",
            FileName = "automaton.json"
        };
        if (dlg.ShowDialog() == true) ExportToJson(dlg.FileName);
    }

    private void MenuExportImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg",
            Title = "Eksportuj jako obraz",
            FileName = "automaton.png"
        };
        if (dlg.ShowDialog() == true) ExportAsImage(dlg.FileName);
    }

    private void ImportFromJson(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<AutomatonDto>(json, JsonOpts)
                ?? throw new InvalidDataException("Plik jest pusty lub nieprawidłowy.");

            if (data.States == null || data.States.Count == 0)
                throw new InvalidDataException("Brak stanów w pliku.");

            var stateMap = new Dictionary<int, State>();
            foreach (var s in data.States)
            {
                var state = new State
                {
                    Name = s.Name ?? $"q{s.Id}",
                    X = s.Position?.X ?? 50,
                    Y = s.Position?.Y ?? 50,
                    IsInitial = s.IsStart,
                    IsAccepting = s.IsAccepting,
                    Radius = s.Appearance?.Radius > 0 ? s.Appearance.Radius : 25,
                    FillColor = s.Appearance?.FillColor ?? "#FFFFFF",
                    StrokeColor = s.Appearance?.StrokeColor ?? "#222222",
                    StrokeThickness = s.Appearance?.StrokeThickness > 0 ? s.Appearance.StrokeThickness : 2
                };
                stateMap[s.Id] = state;
            }

            var groups = new Dictionary<(int, int), List<string>>();
            if (data.Transitions != null)
            {
                foreach (var t in data.Transitions)
                {
                    if (!stateMap.ContainsKey(t.FromStateId) || !stateMap.ContainsKey(t.ToStateId))
                        throw new InvalidDataException($"Przejście odwołuje się do nieistniejącego stanu ({t.FromStateId} lub {t.ToStateId}).");
                    var key = (t.FromStateId, t.ToStateId);
                    if (!groups.ContainsKey(key)) groups[key] = new List<string>();
                    if (!string.IsNullOrEmpty(t.Symbol)) groups[key].Add(t.Symbol);
                }
            }

            _animTimer.Stop();
            SelectState(null);
            SelectTransition(null);

            _automaton.Transitions.Clear();
            _automaton.States.Clear();

            foreach (var s in stateMap.Values) _automaton.States.Add(s);
            foreach (var (key, symbols) in groups)
                _automaton.Transitions.Add(new Transition
                {
                    Source = stateMap[key.Item1],
                    Target = stateMap[key.Item2],
                    Label = string.Join(",", symbols)
                });

            _stateCounter = stateMap.Values
                .Select(s => { int.TryParse(s.Name?.TrimStart('q'), out int n); return n + 1; })
                .DefaultIfEmpty(0).Max();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd importu:\n{ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportToJson(string filePath)
    {
        try
        {
            var stateList = _automaton.States.ToList();
            var states = stateList.Select((s, i) => new StateDto
            {
                Id = i, Name = s.Name ?? $"q{i}",
                IsStart = s.IsInitial, IsAccepting = s.IsAccepting,
                Position = new PositionDto { X = s.X, Y = s.Y },
                Appearance = new AppearanceDto
                {
                    Radius = s.Radius, FillColor = s.FillColor,
                    StrokeColor = s.StrokeColor, StrokeThickness = s.StrokeThickness
                }
            }).ToList();

            var alphabet = _automaton.Transitions
                .SelectMany(t => (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(x => x).ToList();

            var transitions = _automaton.Transitions
                .SelectMany(t =>
                {
                    int fromId = stateList.IndexOf(t.Source);
                    int toId = stateList.IndexOf(t.Target);
                    var syms = (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return syms.Length > 0
                        ? syms.Select(sym => new TransitionDto { FromStateId = fromId, ToStateId = toId, Symbol = sym.Trim() })
                        : new[] { new TransitionDto { FromStateId = fromId, ToStateId = toId, Symbol = "" } };
                }).ToList();

            var data = new AutomatonDto
            {
                Meta = new MetaDto { Description = "", Alphabet = alphabet, Created = DateTime.UtcNow.ToString("o") },
                States = states,
                Transitions = transitions
            };

            File.WriteAllText(filePath, JsonSerializer.Serialize(data, JsonOpts));
            MessageBox.Show("Eksport zakończony.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Eksport", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportAsImage(string filePath)
    {
        try
        {
            DrawingArea.UpdateLayout();
            var rtb = new RenderTargetBitmap(
                (int)DrawingArea.ActualWidth, (int)DrawingArea.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(DrawingArea);

            BitmapEncoder enc = filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? new JpegBitmapEncoder() : new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var stream = File.OpenWrite(filePath);
            enc.Save(stream);
            MessageBox.Show("Obraz zapisany.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd eksportu obrazu:\n{ex.Message}", "Eksport", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private HashSet<string> GetAlphabet() =>
        _automaton.Transitions
            .SelectMany(t => (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet();

    private Transition? FindTransitionForSymbol(State state, string symbol) =>
        _automaton.Transitions.FirstOrDefault(t =>
            t.Source == state &&
            (t.Label ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(s => s.Trim() == symbol));

    private void WordInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ResetSimulationState();
    }

    private void StartSim_Click(object sender, RoutedEventArgs e)
    {
        var word = WordInputBox.Text;
        var alphabet = GetAlphabet();

        if (alphabet.Count == 0 && word.Length > 0)
        {
            MessageBox.Show("Automat nie ma zdefiniowanych przejść.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (char c in word)
        {
            if (!alphabet.Contains(c.ToString()))
            {
                MessageBox.Show($"Symbol '{c}' nie należy do alfabetu {{ {string.Join(", ", alphabet)} }}.",
                    "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var initial = _automaton.States.FirstOrDefault(s => s.IsInitial);
        if (initial == null)
        {
            MessageBox.Show("Automat nie ma stanu początkowego.", "Symulacja", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _animTimer.Stop();
        _simHistory.Clear();
        _historyItems.Clear();
        _inputWord = word;
        _simStep = 0;
        _simCurrentState = initial;
        ResultText.Text = "";

        WordInputBox.IsEnabled = false;
        ClearSimHighlights();
        _simCurrentState.IsActive = true;
        RedrawTransitions();
        UpdateWordDisplay();
        UpdateSimButtons();

        if (word.Length == 0)
        {
            bool accepted = _simCurrentState.IsAccepting;
            ResultText.Text = accepted ? "✓ Zaakceptowane" : "✗ Odrzucone";
            ResultText.Foreground = accepted ? Brushes.Green : Brushes.Red;
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e) => StepForward();

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_simStep <= 0 || _simHistory.Count == 0) return;

        var last = _simHistory[^1];
        _simHistory.RemoveAt(_simHistory.Count - 1);
        if (_historyItems.Count > 0) _historyItems.RemoveAt(_historyItems.Count - 1);

        if (_simCurrentState != null) _simCurrentState.IsActive = false;
        if (_simActiveTransition != null) _simActiveTransition.IsActive = false;

        _simCurrentState = last.From;
        _simStep--;
        _simCurrentState.IsActive = true;

        _simActiveTransition = _simHistory.Count > 0 ? _simHistory[^1].Trans : null;
        if (_simActiveTransition != null) _simActiveTransition.IsActive = true;

        ResultText.Text = "";
        RedrawTransitions();
        UpdateWordDisplay();
        UpdateSimButtons();
    }

    private void StartAnim_Click(object sender, RoutedEventArgs e)
    {
        _animTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(0.1, AnimSpeedSlider.Value));
        _animTimer.Start();
        UpdateSimButtons();
    }

    private void StopAnim_Click(object sender, RoutedEventArgs e)
    {
        _animTimer.Stop();
        UpdateSimButtons();
    }

    private void ResetSim_Click(object sender, RoutedEventArgs e)
    {
        _animTimer.Stop();
        var initial = _automaton.States.FirstOrDefault(s => s.IsInitial);
        if (initial == null) return;

        _simHistory.Clear();
        _historyItems.Clear();
        ClearSimHighlights();
        _simStep = 0;
        _simCurrentState = initial;
        _simCurrentState.IsActive = true;
        ResultText.Text = "";
        WordInputBox.IsEnabled = false;
        RedrawTransitions();
        UpdateWordDisplay();
        UpdateSimButtons();
    }

    private void AnimSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AnimSpeedValue != null) AnimSpeedValue.Text = $"{e.NewValue:0.#}";
        if (_animTimer.IsEnabled)
            _animTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(0.1, e.NewValue));
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        var result = StepForward();
        if (result != StepResult.Success)
        {
            _animTimer.Stop();
            UpdateSimButtons();
        }
    }

    private StepResult StepForward()
    {
        if (_simStep < 0 || _simCurrentState == null) return StepResult.NoTransition;
        if (_simStep >= _inputWord.Length) return StepResult.WordComplete;

        string symbol = _inputWord[_simStep].ToString();
        var transition = FindTransitionForSymbol(_simCurrentState, symbol);

        if (transition == null)
        {
            ResultText.Text = $"✗ Odrzucone (brak przejścia ze stanu {_simCurrentState.Name} dla '{symbol}')";
            ResultText.Foreground = Brushes.Red;
            return StepResult.NoTransition;
        }

        var prevActive = _simCurrentState;
        _simHistory.Add((_simCurrentState, symbol, transition, transition.Target));
        _historyItems.Add($"{_simCurrentState.Name} →[{symbol}]→ {transition.Target.Name}");

        prevActive.IsActive = false;
        if (_simActiveTransition != null) _simActiveTransition.IsActive = false;
        transition.IsActive = true;
        _simActiveTransition = transition;
        _simCurrentState = transition.Target;
        _simCurrentState.IsActive = true;
        _simStep++;

        RedrawTransitions();
        UpdateWordDisplay();

        if (_simStep >= _inputWord.Length)
        {
            bool accepted = _simCurrentState.IsAccepting;
            ResultText.Text = accepted ? "✓ Zaakceptowane" : "✗ Odrzucone";
            ResultText.Foreground = accepted ? Brushes.Green : Brushes.Red;
            UpdateSimButtons();
            return StepResult.WordComplete;
        }

        ResultText.Text = "";
        UpdateSimButtons();
        return StepResult.Success;
    }

    private void ClearSimHighlights()
    {
        foreach (var s in _automaton.States) s.IsActive = false;
        foreach (var t in _automaton.Transitions) t.IsActive = false;
        _simActiveTransition = null;
    }

    private void ResetSimulationState()
    {
        _animTimer.Stop();
        _simStep = -1;
        _simCurrentState = null;
        _simHistory.Clear();
        _historyItems.Clear();
        ClearSimHighlights();
        ResultText.Text = "";
        if (WordInputBox != null) WordInputBox.IsEnabled = true;
        RedrawTransitions();
        UpdateWordDisplay();
        UpdateSimButtons();
    }

    private void UpdateWordDisplay()
    {
        WordDisplay.Inlines.Clear();
        if (_inputWord.Length == 0 || _simStep < 0) return;

        for (int i = 0; i < _inputWord.Length; i++)
        {
            var run = new Run(_inputWord[i].ToString());
            if (_simStep < _inputWord.Length && i == _simStep)
            {
                run.Background = new SolidColorBrush(Colors.Yellow);
                run.FontWeight = FontWeights.Bold;
            }
            else if (i < _simStep)
            {
                run.Foreground = Brushes.Gray;
            }
            WordDisplay.Inlines.Add(run);
        }
    }

    private void UpdateSimButtons()
    {
        bool isReady = _simStep >= 0;
        bool atEnd = _simStep >= _inputWord.Length;
        bool animating = _animTimer.IsEnabled;

        PrevButton.IsEnabled = _simStep > 0 && !animating;
        NextButton.IsEnabled = isReady && !atEnd && !animating;
        StartAnimButton.IsEnabled = isReady && !atEnd && !animating;
        StopAnimButton.IsEnabled = animating;
        ResetSimButton.IsEnabled = isReady && !animating;
    }
}
