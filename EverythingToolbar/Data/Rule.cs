using EverythingToolbar.Helpers;

using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace EverythingToolbar.Data
{
    public enum FileType
    {
        Any,
        File,
        Folder
    }

    [Serializable]
    public sealed class Rule : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private FileType _filetype;
        public FileType Type
        {
            get => _filetype;
            set => Set(ref _filetype, value, "FileType");
        }

        private string _expression;
        public string Expression
        {
            get => _expression;
            set
            {
                Set(ref _expression, value);
                OnPropertyChanged("ExpressionValid");
            }
        }

        private string _command;
        public string Command
        {
            get => _command;
            set => Set(ref _command, value);
        }

        public bool ExpressionValid
        {
            get
            {
                try
                {
                    Regex.IsMatch("", Expression);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }
    }
}
