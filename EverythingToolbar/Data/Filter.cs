using System;

namespace EverythingToolbar.Data
{
    internal class Filter : IEquatable<Filter>
    {
        public string Name { get; set; }
        public bool IsMatchCase { get; set; }
        public bool IsMatchWholeWord { get; set; }
        public bool IsMatchPath { get; set; }
        public bool IsRegExEnabled { get; set; }
        public string Search { get; set; }
        public string Macro { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Filter) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public bool Equals(Filter other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name;
        }

        public static bool operator ==(Filter left, Filter right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Filter left, Filter right)
        {
            return !Equals(left, right);
        }
    }
}
