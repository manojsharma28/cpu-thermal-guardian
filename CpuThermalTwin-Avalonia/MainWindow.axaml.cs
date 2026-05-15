using Avalonia.Controls;

namespace CpuThermalTwinConfigurator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void OnOsMetricsButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsOsMetricsOpen = !vm.IsOsMetricsOpen;
            }
        }

        public void OnSystemMetricsButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsSystemMetricsOpen = !vm.IsSystemMetricsOpen;
            }
        }

        public void OnCpuMetricsButtonClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsCpuMetricsOpen = !vm.IsCpuMetricsOpen;
            }
        }
    }
}
