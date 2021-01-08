using CsvHelper.Configuration.Attributes;

using System;

namespace EverythingToolbar.Data
{
    public sealed class FileListRecord
    {
        [Name("Filename")]
        public string Filename { get; set; }
        [Name("Size")]
        public long? Size { get; set; }
        [Name("Date Modified")]
        public long? DateModified { get; set; }
        [Name("Date Created")]
        public long? DateCreated { get; set; }
        [Name("Attributes")]
        public int? Attributes { get; set; }
    }
}