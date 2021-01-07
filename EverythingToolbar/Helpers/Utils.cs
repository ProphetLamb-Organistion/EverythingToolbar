using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace EverythingToolbar.Helpers
{
    internal static class Utils
    {
        // Taken from: https://stackoverflow.com/a/11124118/1477251
        public static string GetHumanReadableFileSize(string path)
        {
            // Get file length
            long length = Math.Abs(new FileInfo(path).Length);

            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (length >= 0x1000000000000000L) // Exabyte
            {
                suffix = "EB";
                readable = (length >> 50);
            }
            else if (length >= 0x4000000000000L) // Petabyte
            {
                suffix = "PB";
                readable = (length >> 40);
            }
            else if (length >= 0x10000000000L) // Terabyte
            {
                suffix = "TB";
                readable = (length >> 30);
            }
            else if (length >= 0x40000000L) // Gigabyte
            {
                suffix = "GB";
                readable = (length >> 20);
            }
            else if (length >= 0x100000L) // Megabyte
            {
                suffix = "MB";
                readable = (length >> 10);
            }
            else if (length >= 0x400L) // Kilobyte
            {
                suffix = "KB";
                readable = length;
            }
            else
            {
                return length.ToString("0 B"); // Byte
            }

            // Divide by 1024 to get fractional value
            readable /= 1024;

            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }
        
        public static TReturn Catch<TReturn, TException>(Func<TReturn> function, TReturn fallback = default) where TException : Exception
        {
            try
            {
                return function();
            }
            catch (TException)
            {
                return fallback;
            }
        }
        public static TReturn Catch<TReturn, TException1, TException2>(Func<TReturn> function, TReturn fallback = default)
            where TException1 : Exception where TException2 : Exception
        {
            try
            {
                return function();
            }
            catch (Exception ex)
            {
                if (ex is TException1 || ex is TException2)
                    return fallback;
                throw;
            }
        }
        public static TReturn Catch<TReturn, TException1, TException2, TException3>(Func<TReturn> function, TReturn fallback = default)
            where TException1 : Exception where TException2 : Exception where TException3 : Exception
        {
            try
            {
                return function();
            }
            catch (Exception ex)
            {
                if (ex is TException1 || ex is TException2 || ex is TException3)
                    return fallback;
                throw;
            }
        }
        
        public static void InsertRange<T>(this Collection<T> self, IEnumerable<T> itemSource)
        {
            using IEnumerator<T> en = itemSource.GetEnumerator();
            for (int i = 0; en.MoveNext(); i++)
            {
                self.Insert(i, en.Current);
            }
        }

        public static void InsertRange<T>(this IList<T> self, IEnumerable<T> itemSource)
        {
            using IEnumerator<T> en = itemSource.GetEnumerator();
            for (int i = 0; en.MoveNext(); i++)
            {
                self.Insert(i, en.Current);
            }
        }
    }
}
