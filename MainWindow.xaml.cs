using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfProgressSpinner
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            var T = Type.GetType("System.Windows.Controls.Grid+GridLinesRenderer," +
                " PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");

            var GLR = Activator.CreateInstance(T);
            GLR.GetType().GetField("s_oddDashPen", BindingFlags.Static | BindingFlags.NonPublic).SetValue(GLR, new Pen(Brushes.Yellow, 1.0));
            GLR.GetType().GetField("s_evenDashPen", BindingFlags.Static | BindingFlags.NonPublic).SetValue(GLR, new Pen(Brushes.Yellow, 1.0));

            DataContext = new ViewModel();
            InitializeComponent();
        }

        private void ProgressStateButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                switch (vm.ProgressState)
                {
                    case ProgressState.None:
                        vm.ProgressState = ProgressState.Indeterminate;
                        break;
                    case ProgressState.Indeterminate:
                        vm.ProgressState = ProgressState.Normal;
                        break;
                    case ProgressState.Normal:
                        vm.ProgressState = ProgressState.Completed;
                        break;
                    default:
                        vm.ProgressState = ProgressState.None;
                        break;
                }
            }
        }

        private bool _progress = false;
        private async void ProgressButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_progress && DataContext is ViewModel vm)
            {
                _progress = true;
                vm.Progress = 0.0;
                vm.ProgressState = ProgressState.Normal;

                for (int progress = 0; progress <= 100; progress += 10)
                {
                    await Task.Delay(500);

                    vm.Progress = progress;
                }
                _progress = false;
            }
        }

        private void VisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                switch (vm.Visibility)
                {
                    case Visibility.Visible:
                        vm.Visibility = Visibility.Collapsed;
                        break;
                    case Visibility.Collapsed:
                        vm.Visibility = Visibility.Visible;
                        break;
                }
            }
        }
    }
}
