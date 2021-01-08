using EverythingToolbar.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace EverythingToolbar.Data
{
    /*
     * 1. IOOps should always be async.
     * 2. A misconfigured Everything also provides file-paths to which we do not have access, e.g. $RECYCLE_BIN\**\. Especially if "Exclude system files and folders" is not checked in Everything.
     * 3. Realtime information about the File is not necessary, obtain such when the FullPathAndFileName changed instead of on demand. In fact Murphy's law suggests that a snapshot is likely more coherent in this case.
     *    Therefore use SetFullPathAndFileName & SetIconSourceFilePath.
     * 4. If one UnauthorizedAccessException is thrown other IOOps will almost certainly do so as-well, so we save precious cycles waiting for IO and Exception handling by canceling after one Exception.
     */
    public sealed class SearchResult : IEquatable<SearchResult>
    {
        public bool IsFile { get; set; }

        private string _fullPathAndFileName;
        public string FullPathAndFileName
        {
            get => _fullPathAndFileName;
            set => SetFullPathAndFileName(value);
        }

        public string Path { get; internal set; }

        public string HighlightedPath { get; set; }

        public string FileName { get; internal set; }

        public string HighlightedFileName { get; set; }

        public string FileSize { get; internal set; }

        public string DateModified { get; internal set; }

        public ImageSource Icon { get; internal set; }

        private string _iconSourceFilePath;
        public string IconSourceFilePath { get => _iconSourceFilePath; set => SetIconSourceFilePath(value); }

        public Task FetchFileInfo { get; private set; } = Task.CompletedTask;

        public bool IsFavorite { get; set; }

        private async void SetFullPathAndFileName(string value)
        {
            await FetchFileInfo;
            FetchFileInfo = Task.Run(() =>
            {
                if (value is null)
                {
                    Path = String.Empty;
                    FileName = String.Empty;
                    FileSize = String.Empty;
                    DateModified = "N/A";
                    Icon = null;
                } 
                else try
                {
                    _fullPathAndFileName = value;
                    Path = System.IO.Path.GetDirectoryName(value);
                    FileName = System.IO.Path.GetFileName(value);
                    FileSize = IsFile ? Utils.GetHumanReadableFileSize(value) : String.Empty;
                    DateModified = File.GetLastWriteTime(value).ToString("g");
                    Icon = Utils.Catch<ImageSource, Exception>(() =>
                        WindowsThumbnailProvider.GetThumbnail(IconSourceFilePath ?? value, 16, 16));
                }
                catch (UnauthorizedAccessException)
                {
                    FileSize = String.Empty;
                    DateModified = "N/A";
                    Icon = null;
                }
            });
        }

        private async void SetIconSourceFilePath(string value)
        {
            await FetchFileInfo;
            FetchFileInfo = Task.Run(() =>
            {
                _iconSourceFilePath = value;
                Icon = Utils.Catch<ImageSource, Exception>(() => 
                    WindowsThumbnailProvider.GetThumbnail(value ?? FullPathAndFileName, 16, 16));
            });
        }

        public void Open()
        {
            try
            {
                Process.Start(FullPathAndFileName);
                EverythingSearch.Instance.IncrementRunCount(FullPathAndFileName);
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Failed to open search result.");
                MessageBox.Show(Properties.Resources.MessageBoxFailedToOpen, Properties.Resources.MessageBoxErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenPath()
        {
            try
            {
                ShellUtils.CreateProcessFromCommandLine("explorer.exe /select,\"" + FullPathAndFileName + "\"");
                EverythingSearch.Instance.IncrementRunCount(FullPathAndFileName);
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Failed to open path.");
                MessageBox.Show(Properties.Resources.MessageBoxFailedToOpenPath, Properties.Resources.MessageBoxErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenWith()
        {
            try
            {
                ShellUtils.OpenWithDialog(FullPathAndFileName);
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Failed to open dialog.");
                MessageBox.Show(Properties.Resources.MessageBoxFailedToOpenDialog, Properties.Resources.MessageBoxErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CopyToClipboard()
        {
            try
            {
                Clipboard.SetFileDropList(new StringCollection { FullPathAndFileName });
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Failed to copy file.");
                MessageBox.Show(Properties.Resources.MessageBoxFailedToCopyFile, Properties.Resources.MessageBoxErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CopyPathToClipboard()
        {
            try
            {
                Clipboard.SetText(FullPathAndFileName);
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Failed to copy path.");
                MessageBox.Show(Properties.Resources.MessageBoxFailedToCopyPath, Properties.Resources.MessageBoxErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ShowProperties()
        {
            ShellUtils.ShowFileProperties(FullPathAndFileName);
        }

        public void ShowInEverything()
        {
            EverythingSearch.Instance.OpenLastSearchInEverything(FullPathAndFileName);
        }

        #region IEquatable members
        public override bool Equals(object obj) => Equals(obj as SearchResult);
        public bool Equals(SearchResult other) => other != null
            && IsFile == other.IsFile
            && FullPathAndFileName == other.FullPathAndFileName
            && HighlightedPath == other.HighlightedPath
            && HighlightedFileName == other.HighlightedFileName
            && FileSize == other.FileSize
            && DateModified == other.DateModified
            && IconSourceFilePath == other.IconSourceFilePath;

        public override int GetHashCode()
        {
            return FullPathAndFileName?.GetHashCode() ?? 0;
        }

        public static bool operator ==(SearchResult left, SearchResult right) => EqualityComparer<SearchResult>.Default.Equals(left, right);
        public static bool operator !=(SearchResult left, SearchResult right) => !(left == right);
        #endregion
    }
}
