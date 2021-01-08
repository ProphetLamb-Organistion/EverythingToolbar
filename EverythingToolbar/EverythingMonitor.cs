using EverythingToolbar.Helpers;
using NLog;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EverythingToolbar
{
    public sealed class EverythingMonitor
    {
        private static readonly Lazy<EverythingMonitor> s_lazy_everythingMonitor = new Lazy<EverythingMonitor>(() => new EverythingMonitor());

        public static EverythingMonitor Instance => s_lazy_everythingMonitor.Value;

        private EverythingMonitor()
        {
            EnsureEverythingServiceStarted().ConfigureAwait(false);
        }

        public Task EnsureEverythingServiceStarted()
        {
           return Task.Run(() => {
                Process[] processes = Process.GetProcessesByName("Everything");
                if (processes.Length == 0)
                {
                    if (!File.Exists(Properties.Settings.Default.everythingPath) && !EverythingSearch.SelectEverythingBinaries())
                    {
                        ToolbarLogger.GetLogger("EverythingToolbar").Warn("Everything binaries could not be located. OpenFileDialog canceled.");
                    }
                    else
                    {
                        try
                        {
                            Process.Start(Properties.Settings.Default.everythingPath, "-startup -first-instance");
                        }
                        catch (Exception ex)
                        {
                            ToolbarLogger.GetLogger("EverythingToolbar").Error(ex);
                        }
                    }
                }
            });
        }
    }
}