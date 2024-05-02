using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfProgressSpinner
{
    public class ViewModel : ViewModelBase
    {
        private double _progress = 75;
        public double Progress
        {
            get { return _progress; }
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        private ProgressState _progressState = ProgressState.Indeterminate;
        public ProgressState ProgressState
        {
            get { return _progressState; }
            set
            {
                SetProperty(ref _progressState, value);
            }
        }

        private Visibility _visibility = Visibility.Visible;
        public Visibility Visibility
        {
            get { return _visibility; }
            set { SetProperty(ref _visibility, value); }
        }

        private double _minimum = 0;
        public double Minimum
        {
            get { return _minimum; }
            set
            {
                SetProperty(ref _minimum, value);
            }
        }

        private double _maximum = 100;
        public double Maximum
        {
            get { return _maximum; }
            set
            {
                SetProperty(ref _maximum, value);
            }
        }

        public ViewModel() { }
    }


    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
