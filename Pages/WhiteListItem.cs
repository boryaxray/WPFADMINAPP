using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace WPFAPP.Pages
{
    [DataContract]
    public class WhiteListItem : INotifyPropertyChanged
    {
        private string _name;
        private string _hash;
        private bool _isSelected;
        private string _status;
        private string _statusColor;
        private bool _hashChanged;
        private string _newHash;

        [DataMember(Name = "Name")]
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        [DataMember(Name = "Hash")]
        public string Hash
        {
            get => _hash;
            set { _hash = value; OnPropertyChanged(); OnPropertyChanged(nameof(FullHash)); }
        }

        [IgnoreDataMember]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        [IgnoreDataMember]
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        [IgnoreDataMember]
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        [IgnoreDataMember]
        public bool HashChanged
        {
            get => _hashChanged;
            set { _hashChanged = value; OnPropertyChanged(); }
        }

        [IgnoreDataMember]
        public string NewHash
        {
            get => _newHash;
            set { _newHash = value; OnPropertyChanged(); }
        }

        [IgnoreDataMember]
        public string FullHash => Hash;

        public WhiteListItem()
        {
            _name = string.Empty;
            _hash = string.Empty;
            _status = string.Empty;
            _statusColor = "Transparent";
        }

        public WhiteListItem(string name, string hash)
        {
            _name = name;
            _hash = hash;
            _status = string.Empty;
            _statusColor = "Transparent";
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Hash) || Hash.Length < 8)
                return Name;

            return $"{Name} ({Hash.Substring(0, 8)}...)";
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Hash))
                return false;

            if (Hash.Length != 64)
                return false;

            foreach (var c in Hash)
            {
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'f') ||
                      (c >= 'A' && c <= 'F')))
                    return false;
            }

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}