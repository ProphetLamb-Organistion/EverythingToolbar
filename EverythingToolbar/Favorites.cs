using CsvHelper;
using NLog;
using EverythingToolbar.Data;
using EverythingToolbar.Helpers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace EverythingToolbar
{
    internal class Favorites
    {
        internal IList<FileListRecord> favorites;
        internal IList<int> favoritesHashes;
        private readonly ILogger logger;
        private static readonly SemaphoreSlim fileLock = new SemaphoreSlim(1, 1);

        static Favorites() { }

        public Favorites()
        {
            logger = ToolbarLogger.GetLogger("EverythingToolbar");
        }

        internal string FilePath => Properties.Settings.Default.favoritesRelativePathAndFileName ?? "./favorites.efu";

        public async Task AddAndWriteAsync(SearchResult item)
        {
            favorites.Add(await item.ToFileListRecord());
            favoritesHashes.Add(item.FullPathAndFileName.GetHashCode());
            if (!await WriteToDevice(favorites, FilePath))
                logger.Error("Failed to write favorites to device. filename: \"{0}\"", Path.GetFullPath(FilePath));
        }

        public async Task ReadAsync()
        {
            var favs = await ReadFromDevice(FilePath);
            if (favs is null)
                logger.Error("Failed to read favorites from device. filename: \"{0}\"", Path.GetFullPath(FilePath));
            await UpdateFileListData(favs);
            var write = WriteToDevice(favs, FilePath);
            favoritesHashes = favs.Select(x => x.GetHashCode()).ToList();
            favorites = favs;
            if (!await write)
                logger.Error("Failed to write favorites to device. filename: \"{0}\"", Path.GetFullPath(FilePath));
        }

        public bool Contains(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
                return false;
            int hash = fileName.GetHashCode();
            for (int i = 0; i < favoritesHashes.Count; i++)
            {
                if (hash == favoritesHashes[i] && favorites[i].Filename == fileName)
                    return true;
            }
            return false;
        }

        private static async Task<IList<FileListRecord>> ReadFromDevice(string path)
        {
            if (!File.Exists(path))
            {
                return new List<FileListRecord>();
            }
            await fileLock.WaitAsync();
            try
            {
                IList<FileListRecord> records = new List<FileListRecord>();
                FileListRecord record = new FileListRecord();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                using (var tr = new CsvReader(sr, System.Globalization.CultureInfo.InvariantCulture))
                {
                    var en =  tr.EnumerateRecordsAsync<FileListRecord>(record).GetAsyncEnumerator();
                    while(await en.MoveNextAsync())
                        records.Add(en.Current);
                    await en.DisposeAsync();
                }
                return records;
            }
            catch
            {
                return null;
            }
            finally
            {
                fileLock.Release();
            }
        }

        private static async Task<bool> WriteToDevice(IList<FileListRecord> items, string path)
        {
            await fileLock.WaitAsync();
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var sr = new StreamWriter(fs))
                using (var tw = new CsvWriter(sr, System.Globalization.CultureInfo.InvariantCulture))
                {
                    await tw.WriteRecordsAsync(items);
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                fileLock.Release();
            }
        }

        private static async Task UpdateFileListData(IList<FileListRecord> items)
        {
            for (int i = items.Count-1; i >= 0; i--)
            {
                FileListRecord item = items[i];
                FileSystemInfo fsi = null;
                bool isFile = false;
                if (isFile = File.Exists(item.Filename))
                    fsi = await Utils.CatchAsync<FileInfo, UnauthorizedAccessException>(() => new FileInfo(item.Filename));
                else if (Directory.Exists(item.Filename))
                    fsi = await Utils.CatchAsync<DirectoryInfo, UnauthorizedAccessException>(() => new DirectoryInfo(item.Filename));
                if (fsi is null)
                {
                    items.RemoveAt(i);
                }
                else
                {
                    if (isFile)
                        item.Size = ((FileInfo)fsi).Length;
                    item.DateModified = fsi.LastWriteTime.Ticks;
                    item.DateCreated = fsi.CreationTime.Ticks;
                    item.Attributes = (int?)fsi.Attributes;
                }
            }
        }
    }
}
