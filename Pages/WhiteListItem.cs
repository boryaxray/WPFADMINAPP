using System.Linq;
using System.Runtime.Serialization;

namespace WPFAPP.Pages
{
    [DataContract]
    public class WhiteListItem
    {
        [DataMember(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "Hash")]
        public string Hash { get; set; } = string.Empty;

        [IgnoreDataMember]
        public bool IsSelected { get; set; }

        public WhiteListItem() { }

        public WhiteListItem(string name, string hash)
        {
            Name = name;
            Hash = hash;
        }

        public string FullHash => Hash;

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
    }
}