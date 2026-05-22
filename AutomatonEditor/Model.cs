using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutomatonEditor;

public class Automaton : INotifyPropertyChanged
{
    private State? _selectedState;
    private Transition? _selectedTransition;

    public ObservableCollection<State> States { get; set; } = [];
    public ObservableCollection<Transition> Transitions { get; set; } = [];

    public State? SelectedState
    {
        get => _selectedState;
        set { _selectedState = value; OnPropertyChanged(); }
    }

    public Transition? SelectedTransition
    {
        get => _selectedTransition;
        set { _selectedTransition = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class State : INotifyPropertyChanged
{
    private double _x, _y;
    private double _radius = 25.0;
    private double _strokeThickness = 2.0;
    private bool _isInitial, _isAccepting, _isSelected, _isActive;
    private string _fillColor = "#FFFFFF";
    private string _strokeColor = "#222222";

    public string? Name { get; set; }

    public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
    public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
    public bool IsInitial { get => _isInitial; set { _isInitial = value; OnPropertyChanged(); } }
    public bool IsAccepting { get => _isAccepting; set { _isAccepting = value; OnPropertyChanged(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

    public double Radius
    {
        get => _radius;
        set { _radius = value; OnPropertyChanged(); OnPropertyChanged(nameof(Diameter)); OnPropertyChanged(nameof(InnerDiameter)); }
    }

    public double Diameter => _radius * 2;
    public double InnerDiameter => Math.Max(0, _radius * 2 - 12);

    public double StrokeThickness
    {
        get => _strokeThickness;
        set { _strokeThickness = value; OnPropertyChanged(); }
    }

    public string FillColor
    {
        get => _fillColor;
        set { _fillColor = value; OnPropertyChanged(); }
    }

    public string StrokeColor
    {
        get => _strokeColor;
        set { _strokeColor = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class Transition : INotifyPropertyChanged
{
    private State _source = null!;
    private State _target = null!;
    private string _label = "";
    private bool _isSelected, _isActive;

    public State Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    public State Target
    {
        get => _target;
        set { _target = value; OnPropertyChanged(); }
    }

    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
