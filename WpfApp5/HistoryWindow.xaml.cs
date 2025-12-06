using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp5
{
    public partial class HistoryWindow : Window
    {
        public ObservableCollection<HistoryItem> Items { get; set; } = new ObservableCollection<HistoryItem>();

        public HistoryWindow()
        {
            InitializeComponent();
            HistoryList.ItemsSource = Items;
            LoadHistory();
        }

        private void LoadHistory()
        {
            Items.Clear();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var historyDir = Path.Combine(appData, "DesktopStickyNote", "History");

            if (Directory.Exists(historyDir))
            {
                var files = Directory.GetFiles(historyDir, "note_*.txt");
                foreach (var file in files)
                {
                    var text = File.ReadAllText(file);
                    var info = new FileInfo(file);
                    Items.Add(new HistoryItem
                    {
                        FilePath = file,
                        PreviewText = text.Length > 30 ? text.Substring(0, 30) + "..." : text,
                        DateFormatted = info.LastWriteTime.ToString("g")
                    });
                }
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filePath)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var notesDir = Path.Combine(appData, "DesktopStickyNote");
                var fileName = Path.GetFileName(filePath);
                var destPath = Path.Combine(notesDir, fileName);

                try
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Move(filePath, destPath);
                    
                    // Open the restored note
                    var win = new MainWindow(destPath);
                    win.Show();

                    LoadHistory(); // Refresh list
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка восстановления: {ex.Message}");
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filePath)
            {
                 if (MessageBox.Show("Удалить навсегда?", "Удаление", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                 {
                     try
                     {
                         File.Delete(filePath);
                         LoadHistory();
                     }
                     catch { }
                 }
            }
        }
    }

    public class HistoryItem
    {
        public string FilePath { get; set; }
        public string PreviewText { get; set; }
        public string DateFormatted { get; set; }
    }
}
