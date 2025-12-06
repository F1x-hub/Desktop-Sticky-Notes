using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WpfApp5
{
    public class PositionManager
    {
        private static string _configPath;
        private static Dictionary<string, WindowPosition> _positions;

        static PositionManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var noteDir = Path.Combine(appData, "DesktopStickyNote");
            Directory.CreateDirectory(noteDir);
            _configPath = Path.Combine(noteDir, "positions.json");
            LoadPositions();
        }

        private static void LoadPositions()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _positions = JsonSerializer.Deserialize<Dictionary<string, WindowPosition>>(json) 
                                 ?? new Dictionary<string, WindowPosition>();
                }
                else
                {
                    _positions = new Dictionary<string, WindowPosition>();
                }
            }
            catch
            {
                _positions = new Dictionary<string, WindowPosition>();
            }
        }

        private static void SavePositions()
        {
            try
            {
                var json = JsonSerializer.Serialize(_positions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public static WindowPosition GetPosition(string noteFileName)
        {
            var key = Path.GetFileName(noteFileName);
            return _positions.ContainsKey(key) ? _positions[key] : null;
        }

        public static void SavePosition(string noteFileName, double left, double top)
        {
            var key = Path.GetFileName(noteFileName);
            _positions[key] = new WindowPosition { X = (int)left, Y = (int)top };
            SavePositions();
        }

        public static void RemovePosition(string noteFileName)
        {
            var key = Path.GetFileName(noteFileName);
            if (_positions.ContainsKey(key))
            {
                _positions.Remove(key);
                SavePositions();
            }
        }
    }

    public class WindowPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
