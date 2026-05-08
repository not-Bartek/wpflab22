using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutomatonEditor;

public class Automaton : INotifyPropertyChanged
{
    public ObservableCollection<State> States { get; set; } = [];
    public ObservableCollection<Transition> Transitions { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class State : INotifyPropertyChanged
{
    private double _x, _y;
    private bool _isInitial, _isAccepting, _isSelected;
    public string? Name { get; set; }
    public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
    public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    public bool IsInitial { get => _isInitial; set { _isInitial = value; OnPropertyChanged(); } }
    public bool IsAccepting { get => _isAccepting; set { _isAccepting = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Transition : INotifyPropertyChanged
{
    private State _source = null!;
    private State _target = null!;

    public State Source
    {
        get => _source;
        set
        {
            if (_source != null)
                _source.PropertyChanged -= State_PropertyChanged;
            _source = value;
            if (_source != null)
                _source.PropertyChanged += State_PropertyChanged;
            RefreshCoordinates();
        }
    }

    public State Target
    {
        get => _target;
        set
        {
            if (_target != null)
                _target.PropertyChanged -= State_PropertyChanged;
            _target = value;
            if (_target != null)
                _target.PropertyChanged += State_PropertyChanged;
            RefreshCoordinates();
        }
    }

    public double X1 => (Source?.X ?? 0) + 25;
    public double Y1 => (Source?.Y ?? 0) + 25;
    public double X2 => (Target?.X ?? 0) + 25;
    public double Y2 => (Target?.Y ?? 0) + 25;

    private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "X" || e.PropertyName == "Y")
        {
            RefreshCoordinates();
        }
    }

    private void RefreshCoordinates()
    {
        OnPropertyChanged(nameof(X1));
        OnPropertyChanged(nameof(Y1));
        OnPropertyChanged(nameof(X2));
        OnPropertyChanged(nameof(Y2));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}