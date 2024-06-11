﻿using System.Windows;

namespace seawatcher3000
{
    public partial class MainWindow : Window
    {
        private bool justCheckedToggleHereSoPleaseBreakInfiniteLoop = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnToggledHandler(object sender, System.Windows.RoutedEventArgs e)
        {
            if (justCheckedToggleHereSoPleaseBreakInfiniteLoop)
            {
                justCheckedToggleHereSoPleaseBreakInfiniteLoop = false;
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
