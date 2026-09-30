using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LumChems.Helpers
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
                    // Force the UI to re-evaluate if the Save button should be enabled
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _isTracking = false;

        public void StartTracking()
        {
            _isTracking = true;
            IsDirty = false;
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (_isTracking && propertyName != nameof(IsDirty))
            {
                IsDirty = true;
            }
        }
    }
}