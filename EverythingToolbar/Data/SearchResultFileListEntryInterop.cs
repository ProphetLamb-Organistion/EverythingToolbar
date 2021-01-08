using CsvHelper;
using EverythingToolbar.Helpers;

using System;
using System.IO;
using System.Threading.Tasks;

namespace EverythingToolbar.Data
{
    public static class SearchResultFileListRecordInterop
    {
        public static async Task<SearchResult> ToSearchResult(this FileListRecord self)
        {
            var result = new SearchResult
            {
                FullPathAndFileName = self.Filename,
                IsFile = self.Size != null
            };
            await result.FetchFileInfo;
            return result;
        }

        public static async Task<FileListRecord> ToFileListRecord(this SearchResult self)
        {
            var fi = self.IsFile ? await Utils.CatchAsync<FileInfo, UnauthorizedAccessException>(() => new FileInfo(self.FullPathAndFileName)) : null;
            return new FileListRecord
            {
                Filename = self.FullPathAndFileName,
                Size = fi?.Length,
                DateModified = fi?.LastWriteTime.Ticks,
                DateCreated = fi?.CreationTime.Ticks,
                Attributes = (int?)fi?.Attributes
            };
        }
    }
}