﻿using EverythingToolbar.Data;
using EverythingToolbar.Helpers;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Forms;
using EverythingToolbar.Properties;

namespace EverythingToolbar
{
    internal sealed class EverythingSearch : ViewModelBase
    {
        private enum ErrorCode
        {
            EVERYTHING_OK,
            EVERYTHING_ERROR_MEMORY,
            EVERYTHING_ERROR_IPC,
            EVERYTHING_ERROR_REGISTERCLASSEX,
            EVERYTHING_ERROR_CREATEWINDOW,
            EVERYTHING_ERROR_CREATETHREAD,
            EVERYTHING_ERROR_INVALIDINDEX,
            EVERYTHING_ERROR_INVALIDCALL
        }

        private const int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        private const int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
        private const int EVERYTHING_REQUEST_HIGHLIGHTED_PATH = 0x00004000;

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern uint Everything_SetSearchW(string lpSearchString);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMatchPath(bool bEnable);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMatchCase(bool bEnable);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMatchWholeWord(bool bEnable);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetRegex(bool bEnable);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetMax(uint dwMax);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetOffset(uint dwOffset);
        [DllImport("Everything64.dll")]
        private static extern bool Everything_QueryW(bool bWait);
        [DllImport("Everything64.dll")]
        private static extern uint Everything_GetNumResults();
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetSort(uint dwSortType);
        [DllImport("Everything64.dll")]
        private static extern void Everything_SetRequestFlags(uint dwRequestFlags);
        [DllImport("Everything64.dll")]
        private static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr Everything_GetResultHighlightedFileName(uint nIndex);
        [DllImport("Everything64.dll")]
        private static extern uint Everything_IncRunCountFromFileName(string lpFileName);
        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr Everything_GetResultHighlightedPath(uint nIndex);
        [DllImport("Everything64.dll")]
        private static extern bool Everything_IsFileResult(uint nIndex);
        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetLastError();
        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetMajorVersion();
        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetMinorVersion();
        [DllImport("Everything64.dll")]
        public static extern uint Everything_GetRevision();
        [DllImport("Everything64.dll")]
        public static extern bool Everything_IsFastSort(uint sortType);

        private string _searchTerm;
        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                Set(ref _searchTerm, value);
                lock (_searchResultsLock)
                    SearchResults.Clear();
                QueryBatch();
            }
        }

        private Filter _currentFilter;
        public Filter CurrentFilter
        {
            get => _currentFilter ?? FilterLoader.Instance.DefaultFilters[0];
            set
            {
                Set(ref _currentFilter, value);
                lock (_searchResultsLock)
                    SearchResults.Clear();
                QueryBatch();
            }
        }

        public ObservableCollection<SearchResult> SearchResults = new ObservableCollection<SearchResult>();
        public int BatchSize = 100;
        internal static readonly Favorites Favorites = new Favorites();
        public static readonly EverythingSearch Instance = new EverythingSearch();
        private readonly object _searchResultsLock = new object();
        private readonly ILogger logger;
        private CancellationTokenSource cancellationTokenSource;

        private EverythingSearch()
        {
            logger = ToolbarLogger.GetLogger("EverythingToolbar");

            var readFavs = Favorites.ReadAsync();

            try
            {
                uint major = Everything_GetMajorVersion();
                uint minor = Everything_GetMinorVersion();
                uint revision = Everything_GetRevision();

                if ((major > 1) || ((major == 1) && (minor > 4)) || ((major == 1) && (minor == 4) && (revision >= 1)))
                {
                    logger.Info("Everything version: {major}.{minor}.{revision}", major, minor, revision);
                }
                else if (major == 0 && minor == 0 && revision == 0 && (ErrorCode)Everything_GetLastError() == ErrorCode.EVERYTHING_ERROR_IPC)
                {
                    ErrorCode errorCode = (ErrorCode)Everything_GetLastError();
                    HandleError(errorCode);
                    logger.Error("Failed to get Everything version number. Is Everything running?");
                }
                else
                {
                    logger.Error("Everything version {major}.{minor}.{revision} is not supported.", major, minor, revision);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Everything64.dll could not be opened.");
            }

            Settings.Default.PropertyChanged += OnSettingChanged;
            BindingOperations.EnableCollectionSynchronization(SearchResults, _searchResultsLock);

            // Lock until favorites are read
            readFavs.Wait();
        }

        private void OnSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "isRegExEnabled")
                CurrentFilter = FilterLoader.Instance.DefaultFilters[0];

            if (e.PropertyName == "isMatchCase" ||
                e.PropertyName == "isRegExEnabled" ||
                e.PropertyName == "isMatchPath" ||
                e.PropertyName == "isMatchWholeWord" ||
                e.PropertyName == "isHideEmptySearchResults" ||
                e.PropertyName == "sortBy")
            {
                lock (_searchResultsLock)
                    SearchResults.Clear();
                QueryBatch();
            }
        }

        public void QueryBatch()
        {
            cancellationTokenSource?.Cancel();

            if (SearchTerm == null)
                return;

            if (SearchTerm.Length == 0 && Settings.Default.isHideEmptySearchResults)
                return;

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            IList<SearchResult> favoriteResultBuffer = new List<SearchResult>();

            Task.Run(() =>
            {
                try
                {
                    uint flags = EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME;
                    flags |= EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME;
                    flags |= EVERYTHING_REQUEST_HIGHLIGHTED_PATH;

                    Everything_SetSearchW(CurrentFilter.Search + (CurrentFilter.Search.Length > 0 ? " " : "") + SearchTerm);
                    Everything_SetRequestFlags(flags);
                    Everything_SetSort((uint)Settings.Default.sortBy);
                    Everything_SetMatchCase(Settings.Default.isMatchCase);
                    Everything_SetMatchPath(Settings.Default.isMatchPath);
                    Everything_SetMatchWholeWord(Settings.Default.isMatchWholeWord);
                    Everything_SetRegex(Settings.Default.isRegExEnabled);
                    Everything_SetMax((uint)BatchSize);
                    lock (_searchResultsLock)
                        Everything_SetOffset((uint)SearchResults.Count);

                    if (!Everything_QueryW(true))
                    {
                        HandleError((ErrorCode)Everything_GetLastError());
                        return;
                    }

                    uint resultsCount = Everything_GetNumResults();

                    for (uint i = 0; i < resultsCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IntPtr highlightedPathPtr = Everything_GetResultHighlightedPath(i);
                        IntPtr highlightedNamePtr = Everything_GetResultHighlightedFileName(i);
                        // Validate return pointer from external lib functions: Prevent possible NullPointerException
                        if (highlightedPathPtr != IntPtr.Zero || highlightedNamePtr != IntPtr.Zero)
                        {
                            string path = Marshal.PtrToStringUni(highlightedPathPtr);
                            string filename = Marshal.PtrToStringUni(highlightedNamePtr);
                            bool isFile = Everything_IsFileResult(i);
                            StringBuilder fullPath = new StringBuilder(4096);
                            Everything_GetResultFullPathNameW(i, fullPath, 4096);

                            string filePath = fullPath.ToString();
                            SearchResult searchResult = new SearchResult
                            {
                                HighlightedPath = path,
                                FullPathAndFileName = filePath,
                                HighlightedFileName = filename,
                                IsFile = isFile
                            };
                            if (Favorites.Contains(filePath))
                            {
                                searchResult.IsFavorite = true;
                                favoriteResultBuffer.Add(searchResult);
                            }
                            else
                            {
                                lock (_searchResultsLock)
                                    SearchResults.Add(searchResult);
                            }
                        }
                    }
                    // Insert favorites in order to the top
                    lock (_searchResultsLock)
                        SearchResults.InsertRange(favoriteResultBuffer);
                }
                catch (OperationCanceledException) { }
            }, cancellationToken);
        }

        public void Reset()
        {
            SearchTerm = null;
            CurrentFilter = FilterLoader.Instance.DefaultFilters[0];
        }

        public void CycleFilters(int offset = 1)
        {
            int defaultSize = FilterLoader.Instance.DefaultFilters.Count;
            int userSize = FilterLoader.Instance.UserFilters.Count;
            int defaultIndex = FilterLoader.Instance.DefaultFilters.IndexOf(CurrentFilter);
            int userIndex = FilterLoader.Instance.UserFilters.IndexOf(CurrentFilter);

            int d = defaultIndex >= 0 ? defaultIndex : defaultSize;
            int u = userIndex >= 0 ? userIndex : 0;
            int i = (d + u + offset + defaultSize + userSize) % (defaultSize + userSize);

            if (i < defaultSize)
                CurrentFilter = FilterLoader.Instance.DefaultFilters[i];
            else
                CurrentFilter = FilterLoader.Instance.UserFilters[i - defaultSize];
        }

        public void OpenLastSearchInEverything(string highlighted_file = "")
        {
            if(!File.Exists(Settings.Default.everythingPath) && !SelectEverythingBinaries())
            {
                logger.Warn("Everything binaries could not be located. OpenFileDialog canceled.");
                return;
            }
            string args = "";
            if (Settings.Default.sortBy <= 2) args += " -sort \"Name\"";
            else if (Settings.Default.sortBy <= 4) args += " -sort \"Path\"";
            else if (Settings.Default.sortBy <= 6) args += " -sort \"Size\"";
            else if (Settings.Default.sortBy <= 8) args += " -sort \"Extension\"";
            else if (Settings.Default.sortBy <= 10) args += " -sort \"Type name\"";
            else if (Settings.Default.sortBy <= 12) args += " -sort \"Date created\"";
            else if (Settings.Default.sortBy <= 14) args += " -sort \"Date modified\"";
            else if (Settings.Default.sortBy <= 16) args += " -sort \"Attributes\"";
            else if (Settings.Default.sortBy <= 18) args += " -sort \"File list filename\"";
            else if (Settings.Default.sortBy <= 20) args += " -sort \"Run count\"";
            else if (Settings.Default.sortBy <= 22) args += " -sort \"Date recently changed\"";
            else if (Settings.Default.sortBy <= 24) args += " -sort \"Date accessed\"";
            else if (Settings.Default.sortBy <= 26) args += " -sort \"Date run\"";
            if (Settings.Default.sortBy % 2 > 0) args += " -sort-ascending";
            else args += " -sort-descending";
            if (highlighted_file != "") args += " -select \"" + highlighted_file + "\"";
            args += Settings.Default.isMatchCase ? " -case" : " -nocase";
            args += Settings.Default.isMatchPath ? " -matchpath" : " -nomatchpath";
            args += Settings.Default.isMatchWholeWord ? " -ww" : " -noww";
            args += Settings.Default.isRegExEnabled ? " -regex" : " -noregex";
            args += " -s \"" + (CurrentFilter.Search + " " + SearchTerm).Replace("\"", "\"\"") + "\"";

            Process.Start(Settings.Default.everythingPath, args);
        }

        public void IncrementRunCount(string path)
        {
            Everything_IncRunCountFromFileName(path);
        }

        public bool GetIsFastSort(uint sortBy)
        {
            return Everything_IsFastSort(sortBy);
        }

        public static bool SelectEverythingBinaries()
        {
            MessageBox.Show(Resources.MessageBoxSelectEverythingExe);
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "Everything.exe|Everything.exe|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Settings.Default.everythingPath = openFileDialog.FileName;
                    Settings.Default.Save();
                    return true;
                }
                return false;
            }
        }

        private void HandleError(ErrorCode code)
        {
            switch(code)
            {
                case ErrorCode.EVERYTHING_ERROR_MEMORY:
                    logger.Error("Failed to allocate memory for the search query.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_IPC:
                    logger.Error("IPC is not available.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_REGISTERCLASSEX:
                    logger.Error("Failed to register the search query window class.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_CREATEWINDOW:
                    logger.Error("Failed to create the search query window.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_CREATETHREAD:
                    logger.Error("Failed to create the search query thread.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_INVALIDINDEX:
                    logger.Error("Invalid index.");
                    break;
                case ErrorCode.EVERYTHING_ERROR_INVALIDCALL:
                    logger.Error("Invalid call.");
                    break;
            }
        }
    }
}
