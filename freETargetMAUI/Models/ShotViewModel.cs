using freETarget;
using Microsoft.Maui.Graphics;
using System.ComponentModel;

namespace freETargetMAUI.Models
{
    public class ShotViewModel : INotifyPropertyChanged
    {
        private bool _isLatest;
        private bool _isSum;
        private string _displayText;

        public Shot Shot { get; set; }

        public bool IsSum 
        { 
            get => _isSum; 
            set { _isSum = value; OnPropertyChanged(nameof(IsSum)); OnPropertyChanged(nameof(BackgroundColor)); OnPropertyChanged(nameof(TextColor)); }
        }
        
        public bool IsLatest 
        { 
            get => _isLatest; 
            set { _isLatest = value; OnPropertyChanged(nameof(IsLatest)); OnPropertyChanged(nameof(BackgroundColor)); OnPropertyChanged(nameof(TextColor)); }
        }

        public string DisplayText 
        { 
            get => _displayText; 
            set { _displayText = value; OnPropertyChanged(nameof(DisplayText)); }
        }

        public Color BackgroundColor => IsSum ? Color.FromArgb("#F1F5F9") : (IsLatest ? Colors.Red : Colors.White);
        public Color TextColor => IsSum ? Colors.Black : (IsLatest ? Colors.White : Color.FromArgb("#1E293B"));

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
