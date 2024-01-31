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
        public MainWindow()
        {
            InitializeComponent();
        }
        void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                textBlock1.Text = "You Entered: " + textBox1.Text;
            }
        }
    }
}
