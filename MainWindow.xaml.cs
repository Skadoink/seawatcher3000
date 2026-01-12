using System.Windows;
using System.Windows.Navigation;

namespace seawatcher3000
{
    public partial class MainWindow : Window
    {
        private bool justCheckedToggleHereSoPleaseBreakInfiniteLoop = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        object? GetDataContext(object sender)
        {
            if (sender is FrameworkElement element)
            {
                return element.DataContext;
            }

            return null;
        }

        private void OnToggledHandler(object sender, RoutedEventArgs e)
        {
            if (GetDataContext(sender) is not Seawatcher sw)
            {
                return;
            }
            if (toggleButton1.IsChecked == true)
            {
                
                try
                {
                    sw.StartLiveView();
                    toggleButton1.Content = "Stop Live View";
                }
                catch
                {
                    toggleButton1.Checked -= OnToggledHandler;
                    toggleButton1.Unchecked -= OnToggledHandler; // Unsubscribe to avoid recursion
                    toggleButton1.IsChecked = false;
                    toggleButton1.Checked += OnToggledHandler;
                    toggleButton1.Unchecked += OnToggledHandler;
                }
            }
            else
            {
                try
                {
                    sw.StopLiveView();
                    toggleButton1.Content = "Start Live View";
                }
                catch
                {
                    toggleButton1.Checked -= OnToggledHandler;
                    toggleButton1.Unchecked -= OnToggledHandler; // Unsubscribe to avoid recursion
                    toggleButton1.IsChecked = true;
                    toggleButton1.Checked += OnToggledHandler;
                    toggleButton1.Unchecked += OnToggledHandler;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (GetDataContext(sender) is not Seawatcher sw)
            {
                return;
            }
            sw.StopTimer();
            sw.StopManager();
        }
    }
}
