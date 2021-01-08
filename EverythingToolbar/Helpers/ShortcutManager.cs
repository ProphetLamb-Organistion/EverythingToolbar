﻿using NHotkey;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace EverythingToolbar.Helpers
{
    internal sealed class ShortcutManager
    {
        public static readonly ShortcutManager Instance = new ShortcutManager();

        private ShortcutManager() { }

        private readonly Dictionary<string, EventHandler<HotkeyEventArgs>> shortcuts = new Dictionary<string, EventHandler<HotkeyEventArgs>>();

        public bool AddOrReplace(string name, Key key, ModifierKeys modifiers, EventHandler<HotkeyEventArgs> handler)
        {
            try
            {
                shortcuts[name] = handler;
                HotkeyManager.Current.AddOrReplace(name, key, modifiers, handler);
                return true;
            }
            catch (Exception e)
            {
                ToolbarLogger.GetLogger("EverythingInstance").Error(e, "Failed to register hotkey.");
                return false;
            }
        }

        public bool AddOrReplace(string name, Key key, ModifierKeys modifiers)
        {
            return AddOrReplace(name, key, modifiers, shortcuts[name]);
        }
    }
}
