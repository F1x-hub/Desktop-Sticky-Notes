using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace WpfApp5
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                EnsureContextMenuRegistration();
                EnsureAutostart();
            }
            catch { /* Ignore registry errors (permissions etc) */ }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var noteDir = Path.Combine(appData, "DesktopStickyNote");
            if (!Directory.Exists(noteDir))
            {
                Directory.CreateDirectory(noteDir);
            }

            // Check if /new argument is passed
            bool createNew = e.Args.Contains("/new");

            if (createNew)
            {
                CreateNewNote(noteDir);
                return;
            }

            // Otherwise load existing notes
            var files = Directory.GetFiles(noteDir, "note_*.txt");
            if (files.Length == 0)
            {
                // If no notes exist, create a default one
                CreateNewNote(noteDir);
            }
            else
            {
                foreach (var file in files)
                {
                    var window = new MainWindow(file);
                    window.Show();
                }
            }
        }

        private void CreateNewNote(string noteDir)
        {
            string newFileName = $"note_{Guid.NewGuid()}.txt";
            string newFilePath = Path.Combine(noteDir, newFileName);
            // Create empty file to reserve it
            File.WriteAllText(newFilePath, ""); 
            
            // Get cursor position
            int x = -1, y = -1;
            if (GetCursorPos(out POINT lpPoint))
            {
                x = lpPoint.X;
                y = lpPoint.Y;
            }
            
            var window = new MainWindow(newFilePath, x, y);
            window.Show();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private void EnsureContextMenuRegistration()
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            string keyPath = @"Software\Classes\Directory\Background\shell\StickyNote";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            if (key != null)
            {
                key.SetValue("", "Создать заметку");
                // Icon format: "path\to\file.exe,0" where 0 is the icon index
                key.SetValue("Icon", $"\"{exePath}\",0");

                using var commandKey = key.CreateSubKey("command");
                commandKey?.SetValue("", $"\"{exePath}\" /new");
            }
        }

        private void EnsureAutostart()
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key != null)
            {
                key.SetValue("DesktopStickyNotes", $"\"{exePath}\"");
            }
        }
    }
}
