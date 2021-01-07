using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EverythingToolbar
{
    public class Favorites
    {
        internal IList<string> favorites;
        internal IList<int> favoritesHashes;
        private static readonly JsonSerializer s_serializer = JsonSerializer.CreateDefault();

        public Task AddAndWriteAsync(SearchResult item)
        {
            favorites.Add(item.FullPathAndFileName);
            favoritesHashes.Add(item.FullPathAndFileName.GetHashCode());
            return Task.Run(() => WriteToDevice(favorites));
        }

        public Task ReadAsync()
        {
            return Task.Run(async () =>
            {
                var favs = ReadFromDevice();
                CleanDead(favs);
                var t = Task.Run(() => WriteToDevice(favs)).ConfigureAwait(true);
                favoritesHashes = favs.Select(x => x.GetHashCode()).ToList();
                favorites = favs;
                await t;
            });
        }

        public bool Contains(string filePath)
        {
            if (String.IsNullOrEmpty(filePath))
                return false;
            int hash = filePath.GetHashCode();
            for (int i = 0; i < favoritesHashes.Count; i++)
            {
                if (hash == favoritesHashes[i] && favorites[i] == filePath)
                    return true;
            }
            return false;
        }
        
        private static IList<string> ReadFromDevice()
        {
            string path = Properties.Settings.Default.favoritesRelativePathAndFileName ?? "./favorites.json";
            if (!File.Exists(path))
            {
                return new List<string>();
            }
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            using var tr = new JsonTextReader(sr);
            return s_serializer.Deserialize<List<string>>(tr);
        }
        
        private static void WriteToDevice(IList<string> items)
        {
            using var fs = new FileStream(Properties.Settings.Default.favoritesRelativePathAndFileName ?? "./favorites.json", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var sr = new StreamWriter(fs);
            using var tw = new JsonTextWriter(sr);
            s_serializer.Serialize(tw, items);
        }

        private static void CleanDead(IList<string> items)
        {
            for (int i = items.Count-1; i >= 0; i--)
            {
                int index = i;
                try
                {
                    if (!File.Exists(items[index]) && !Directory.Exists(items[index]))
                    {
                        items.RemoveAt(index);
                    }
                }
                catch(Exception ex)
                {
                    throw new TaskCanceledException("Unexpected error occured during task execution.", ex);
                }
            }
        }
    }
}
