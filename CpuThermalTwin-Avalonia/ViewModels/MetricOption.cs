using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CpuThermalTwinConfigurator.ViewModels
{
    public class MetricOption : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
