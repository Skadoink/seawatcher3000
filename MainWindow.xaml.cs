using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace seawatcher3000
{
    public partial class MainWindow
    {
        private Seawatcher sw;
        public MainWindow()
        {
            InitializeComponent();
            sw = new Seawatcher();
        }
        void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                textBlock1.Text = "You Entered: " + textBox1.Text;
                //end live view
                sw.StopLiveView();
            }
        }
    }
}
