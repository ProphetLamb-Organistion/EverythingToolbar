﻿using EverythingToolbar.Data;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EverythingToolbar.Helpers
{
    internal sealed class FilterLoader : INotifyPropertyChanged
    {
        private readonly ObservableCollection<Filter> defaultFilters = new ObservableCollection<Filter>
        {
            new Filter {
                Name = Properties.Resources.DefaultFilterAll,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "",
                Search = ""
            },
            new Filter {
                Name = Properties.Resources.DefaultFilterFile,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "",
                Search = "file:"
            },
            new Filter {
                Name = Properties.Resources.DefaultFilterFolder,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "",
                Search = "folder:"
            }
        };
        public ObservableCollection<Filter> DefaultFilters
        {
            get
            {
                if (Properties.Settings.Default.isRegExEnabled)
                {
                    return new ObservableCollection<Filter>(defaultFilters.Skip(0).Take(1));
                }
                else
                {
                    return defaultFilters;
                }
            }
        }

        private readonly ObservableCollection<Filter> defaultUserFilters = new ObservableCollection<Filter>()
        {
            new Filter {
                Name = Properties.Resources.UserFilterAudio,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "audio",
                Search = "ext:aac;ac3;aif;aifc;aiff;au;cda;dts;fla;flac;it;m1a;m2a;m3u;m4a;mid;midi;mka;mod;mp2;mp3;mpa;ogg;ra;rmi;spc;rmi;snd;umx;voc;wav;wma;xm"
            },
            new Filter {
                Name = Properties.Resources.UserFilterCompressed,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "zip",
                Search = "ext:7z;ace;arj;bz2;cab;gz;gzip;jar;r00;r01;r02;r03;r04;r05;r06;r07;r08;r09;r10;r11;r12;r13;r14;r15;r16;r17;r18;r19;r20;r21;r22;r23;r24;r25;r26;r27;r28;r29;rar;tar;tgz;z;zip"
            },
            new Filter {
                Name = Properties.Resources.UserFilterDocument,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "doc",
                Search = "ext:c;chm;cpp;csv;cxx;doc;docm;docx;dot;dotm;dotx;h;hpp;htm;html;hxx;ini;java;lua;mht;mhtml;odt;pdf;potx;potm;ppam;ppsm;ppsx;pps;ppt;pptm;pptx;rtf;sldm;sldx;thmx;txt;vsd;wpd;wps;wri;xlam;xls;xlsb;xlsm;xlsx;xltm;xltx;xml"
            },
            new Filter {
                Name = Properties.Resources.UserFilterExecutable,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "exe",
                Search = "ext:bat;cmd;exe;msi;msp;scr"
            },
            new Filter {
                Name = Properties.Resources.UserFilterPicture,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "pic",
                Search = "ext:ani;bmp;gif;ico;jpe;jpeg;jpg;pcx;png;psd;tga;tif;tiff;webp;wmf"
            },
            new Filter {
                Name = Properties.Resources.UserFilterVideo,
                IsMatchCase = false,
                IsMatchWholeWord = false,
                IsMatchPath = false,
                IsRegExEnabled = false,
                Macro = "video",
                Search = "ext:3g2;3gp;3gp2;3gpp;amr;amv;asf;avi;bdmv;bik;d2v;divx;drc;dsa;dsm;dss;dsv;evo;f4v;flc;fli;flic;flv;hdmov;ifo;ivf;m1v;m2p;m2t;m2ts;m2v;m4b;m4p;m4v;mkv;mp2v;mp4;mp4v;mpe;mpeg;mpg;mpls;mpv2;mpv4;mov;mts;ogm;ogv;pss;pva;qt;ram;ratdvd;rm;rmm;rmvb;roq;rpm;smil;smk;swf;tp;tpr;ts;vob;vp6;webm;wm;wmp;wmv"
            }
        };
        public ObservableCollection<Filter> UserFilters
        {
            get
            {
                if (Properties.Settings.Default.isRegExEnabled)
                {
                    return new ObservableCollection<Filter>();
                }
                else
                {
                    if (Properties.Settings.Default.isImportFilters)
                    {
                        return LoadFilters();
                    }
                    else
                    {
                        return defaultUserFilters;
                    }
                }
            }
        }

        public static readonly FilterLoader Instance = new FilterLoader();
        public event PropertyChangedEventHandler PropertyChanged;
        private FileSystemWatcher watcher;

        private FilterLoader()
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.filtersPath))
            {
                Properties.Settings.Default.filtersPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                                                       "Everything",
                                                                       "Filters.csv");
            }
            Properties.Settings.Default.PropertyChanged += OnPropertyChanged;

            RefreshFilters();
            CreateFileWatcher();
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "isRegExEnabled" || e.PropertyName == "isImportFilters")
                RefreshFilters();
        }

        internal void RefreshFilters()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DefaultFilters"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UserFilters"));
        }

        ObservableCollection<Filter> LoadFilters()
        {
            var filters = new ObservableCollection<Filter>();

            if (!File.Exists(Properties.Settings.Default.filtersPath))
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Info("Filters.csv could not be found at " + Properties.Settings.Default.filtersPath);

                MessageBox.Show(Properties.Resources.MessageBoxSelectFiltersCsv,
                                Properties.Resources.MessageBoxSelectFiltersCsvTitle,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.InitialDirectory = Path.Combine(Properties.Settings.Default.filtersPath, "..");
                    openFileDialog.Filter = "Filters.csv|Filters.csv|All files (*.*)|*.*";
                    openFileDialog.FilterIndex = 1;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        Properties.Settings.Default.filtersPath = openFileDialog.FileName;
                        CreateFileWatcher();
                        Properties.Settings.Default.Save();
                    }
                    else
                    {
                        Properties.Settings.Default.isImportFilters = false;
                        Properties.Settings.Default.Save();
                        return defaultUserFilters;
                    }
                }
            }

            try
            {
                using (TextFieldParser csvParser = new TextFieldParser(Properties.Settings.Default.filtersPath))
                {
                    csvParser.CommentTokens = new string[] { "#" };
                    csvParser.SetDelimiters(new string[] { "," });
                    csvParser.HasFieldsEnclosedInQuotes = true;

                    // Skip header row
                    csvParser.ReadLine();

                    while (!csvParser.EndOfData)
                    {
                        string[] fields = csvParser.ReadFields();

                        // Skip default filters
                        string search = fields[6].Trim();
                        if (String.IsNullOrEmpty(search)
                         || search == "file:"
                         || search == "folder:")
                        {
                            continue;
                        }

                        // Everything's default filters are uppercase
                        fields[0] = fields[0].Replace("AUDIO", Properties.Resources.UserFilterAudio);
                        fields[0] = fields[0].Replace("COMPRESSED", Properties.Resources.UserFilterCompressed);
                        fields[0] = fields[0].Replace("DOCUMENT", Properties.Resources.UserFilterDocument);
                        fields[0] = fields[0].Replace("EXECUTABLE", Properties.Resources.UserFilterExecutable);
                        fields[0] = fields[0].Replace("PICTURE", Properties.Resources.UserFilterPicture);
                        fields[0] = fields[0].Replace("VIDEO", Properties.Resources.UserFilterVideo);

                        filters.Add(new Filter()
                        {
                            Name = fields[0],
                            IsMatchCase = fields[1] == "1",
                            IsMatchWholeWord = fields[2] == "1",
                            IsMatchPath = fields[3] == "1",
                            IsRegExEnabled = fields[5] == "1",
                            Search = fields[6],
                            Macro = fields[7]
                        });
                    }
                }
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingToolbar").Error(e, "Parsing Filters.csv failed.");
                return defaultUserFilters;
            }

            return filters;
        }

        public void CreateFileWatcher()
        {
            if (!File.Exists(Properties.Settings.Default.filtersPath))
                return;

            watcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(Properties.Settings.Default.filtersPath),
                Filter = Path.GetFileName(Properties.Settings.Default.filtersPath),
                NotifyFilter = NotifyFilters.FileName
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;

            watcher.EnableRaisingEvents = true;
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            RefreshFilters();
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            RefreshFilters();
        }
    }
}
