using EverythingToolbar.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace EverythingToolbar.Data
{
    [Serializable]
    public sealed class QuickCommand : ViewModelBase, System.Windows.Input.ICommand // Lets combine Data, Business and Visual layer, yay!
    {
        private string _command;
        public string Command
        {
            get => _command;
            set => Set(ref _command, value);
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set => Set(ref _fileName, value);
        }

        private string _format;
        public string ArgumentsFormat
        {
            get => _format;
            set
            {
                _argumentsCount = -1;
                Set(ref _format, value);
                OnPropertyChanged("ArgumentCount");
            }
        }

        private int _argumentsCount = -1;
        public int ArgumentsCount
        {
            get
            {
                if (_argumentsCount < 0)
                    _argumentsCount = CountFormatArguments(ArgumentsFormat);
                return _argumentsCount;
            }
        }

        [field: NonSerialized]
        private bool _canExecute;

        [field: NonSerialized]
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            bool canExecute = parameter is string p
             && p.StartsWith(Properties.Settings.Default.quickCommandPrefix + Command)
             && p.Length >= Properties.Settings.Default.quickCommandPrefix.Length + Command.Length + ArgumentsCount;
             if (canExecute != _canExecute)
                CanExecuteChanged.Invoke(this, EventArgs.Empty);
            return _canExecute = canExecute;
        }

        public void Execute(object parameter)
        {
            // Remove prefix and trim excess whitespace
            string source = (parameter as string)?.Substring(Properties.Settings.Default.quickCommandPrefix.Length + Command.Length).Trim();
            if (source is null)
                throw new ArgumentException(nameof(parameter));
            Task.Run(() => InternalExecute(source)).ConfigureAwait(false);
        }

        private void InternalExecute(string source)
        {
            IList<string> arguments = new List<string>();
            foreach(var arg in ArgumentParser.Parse(source))
            {
                if (arguments.Count < ArgumentsCount)
                    arguments.Add(arg);
                else
                    arguments[arguments.Count-1] += arg;
            }
            if (arguments.Count != ArgumentsCount)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error("The source string must contain ArgumentCount or more arguments. Tailing arguments will be concatenated.");
                return;
            }
            try
            {
                Process.Start(FileName, String.Format(ArgumentsFormat, arguments));
            }
            catch (Exception ex)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(ex);
            }
        }

        public static int CountFormatArguments(string format)
        {
            return Regex.Matches(format, "{(.*?)}").OfType<Match>().Select(m => m.Value.GetHashCode()).Distinct().Count();
        }
    }
}