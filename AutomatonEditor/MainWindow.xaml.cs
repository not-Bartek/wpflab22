using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AutomatonEditor;

public partial class MainWindow : Window
{
    private readonly Automaton _automaton = new();

    private State? _dragState;
    private Point _dragStartPos;
    private Point _dragOffset;
    private bool _isDragging;
    private int _stateCounter;

    private const double DragThreshold = 4.0;
    private const double StateRadius = 25.0;
    private const double StateDiameter = StateRadius * 2;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _automaton;
    }

    private void DrawingArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(DrawingArea);
        var state = FindStateAt(pos);

        if (state == null)
        {
            if (e.ClickCount == 2)
            {
                AddState(pos);
                e.Handled = true;
            }
            else
            {
                SelectState(null);
            }
            return;
        }

        _dragState = state;
        _dragStartPos = pos;
        _dragOffset = new Point(pos.X - state.X, pos.Y - state.Y);
        _isDragging = false;
        Mouse.Capture(DrawingArea);
        e.Handled = true;
    }

    private void DrawingArea_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragState == null || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(DrawingArea);

        if (!_isDragging)
        {
            var delta = pos - _dragStartPos;
            if (delta.Length < DragThreshold) return;
            _isDragging = true;
        }

        _dragState.X = Math.Max(0, Math.Min(DrawingArea.ActualWidth - StateDiameter, pos.X - _dragOffset.X));
        _dragState.Y = Math.Max(0, Math.Min(DrawingArea.ActualHeight - StateDiameter, pos.Y - _dragOffset.Y));
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
        VisualTreeHelper.HitTest(
            StatesControl,
            null,
            result =>
            {
                if (result.VisualHit is FrameworkElement fe && fe.DataContext is State s)
                {
                    found = s;
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
            X = center.X - StateRadius,
            Y = center.Y - StateRadius,
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
        AcceptingCheckbox.IsChecked = state?.IsAccepting ?? false;
        InitialCheckbox.IsChecked = state?.IsInitial ?? false;

        if (state != null)
            state.IsSelected = true;
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

    private void AddTransition_Click(object sender, RoutedEventArgs e)
    {
        var source = SourceStateCombo.SelectedItem as State;
        var target = TargetStateCombo.SelectedItem as State;

        if (source == null || target == null || source == target) return;

        _automaton.Transitions.Add(new Transition { Source = source, Target = target });
    }
}
