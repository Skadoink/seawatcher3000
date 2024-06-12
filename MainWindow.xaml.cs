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

        object GetDataContext(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;

            if (element != null)
            {
                return element.DataContext;
            }

            return null;
        }

        private void OnToggledHandler(object sender, RoutedEventArgs e)
        {
            if (justCheckedToggleHereSoPleaseBreakInfiniteLoop)
            {
                justCheckedToggleHereSoPleaseBreakInfiniteLoop = false; 
                return;
            }
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
                    justCheckedToggleHereSoPleaseBreakInfiniteLoop = true;
                    toggleButton1.IsChecked = false;
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
                    justCheckedToggleHereSoPleaseBreakInfiniteLoop = true;
                    toggleButton1.IsChecked = true;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Seawatcher._timer.Stop();
            Seawatcher.manager.Shutdown();
        }
    }
}
