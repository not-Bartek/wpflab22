using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutomatonEditor;

public partial class MainWindow : Window
{
    private bool _czyrusza = false;
    private int _kolka = 0;
    private Ellipse _active = null;
    private Grid _activeGrid = null;
    private Grid _ruszany = null;
    public MainWindow()
    {
        InitializeComponent();
    }
    private void DestroyActive(object sender, RoutedEventArgs e)
    {
        
        if (_active != null)
        {
            Plotno.Children.Remove((UIElement) _active);
            Plotno.Children.Remove((UIElement) _activeGrid);
            _active = null;
            _activeGrid = null;
            usunButton.IsEnabled = false;
        }
    }
    private void PMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {

            //dwuklik - dodanie kółka
            Point pozycja = e.GetPosition(this);
            
            Ellipse noweKolko = new Ellipse
            {
              
                Width = 40,
                Height = 40,
                Fill = new SolidColorBrush(Color.FromArgb(20,150, 150, 150)),
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            TextBlock title = new TextBlock { Text = $"q{_kolka}",
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment =VerticalAlignment.Center
            };

            var grid = new Grid();
            


            grid.MouseLeftButtonDown += (s, args) =>
            {
                args.Handled = true;

                
                if (_active != null)
                {
                    _active.Fill = new SolidColorBrush(Color.FromArgb(20, 150, 150, 150));
                }
                noweKolko.Fill = new SolidColorBrush(Color.FromArgb(80, 66, 144, 245));
                _activeGrid = grid;
                _active = noweKolko;
                usunButton.IsEnabled = true;
            };



            grid.MouseRightButtonDown += (s, args) =>
            {

                if (_active != null)
                {
                    _active.Fill = new SolidColorBrush(Color.FromArgb(20, 150, 150, 150));
                }
                noweKolko.Fill = new SolidColorBrush(Color.FromArgb(80, 66, 144, 245));
                _activeGrid = grid;
                _active = noweKolko;
                usunButton.IsEnabled = true;
                _czyrusza = true;
                _ruszany = _activeGrid;
                //noweKolko.Fill = new SolidColorBrush(Color.FromArgb(100, 227, 66, 245));
            };

            grid.MouseMove += (s, args) =>
            {
                if (!_czyrusza || _active == null || _ruszany == null) return;
                //_active.Fill = new SolidColorBrush(Color.FromArgb(100, 245, 233, 66));

                Point punktAktualny = e.GetPosition(Plotno);

                Canvas.SetLeft(_ruszany, punktAktualny.X-20);
                Canvas.SetTop(_ruszany, punktAktualny.Y-20);
            };
            grid.MouseRightButtonUp += (s, args) =>
            {
                //_active.Fill = new SolidColorBrush(Color.FromArgb(20, 150, 150, 150));
                _ruszany = null;
                _czyrusza = false;
            };


            grid.Children.Add(title);
            grid.Children.Add(noweKolko);


            //Canvas.SetLeft(noweKolko, pozycja.X - 20);
            //Canvas.SetTop(noweKolko, pozycja.Y - 20);

            //Canvas.SetLeft(title, pozycja.X - 20);
            //Canvas.SetTop(title, pozycja.Y - 20);

            Canvas.SetLeft(grid, pozycja.X - 20);
            Canvas.SetTop(grid, pozycja.Y - 20);

            Plotno.Children.Add(grid);

            _kolka++;




        }
        else
        {
            if (_active != null)
            {
                _active.Fill = new SolidColorBrush(Color.FromArgb(20, 150, 150, 150));
            }
            usunButton.IsEnabled = false;
            _activeGrid = null;
            _active = null;
        }
        
    }
}