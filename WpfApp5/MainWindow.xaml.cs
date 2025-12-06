using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WpfApp5
{
    public partial class MainWindow : Window
    {
        private string _noteFilePath;
        private bool _isDeleting = false;
        private bool _isLocked = false;
        private int _initialX = -1;
        private int _initialY = -1;

        public MainWindow(string filePath, int x = -1, int y = -1)
        {
            InitializeComponent();
            _noteFilePath = filePath;
            
            // Check for saved position first
            var savedPos = PositionManager.GetPosition(filePath);
            if (savedPos != null)
            {
                _initialX = savedPos.X;
                _initialY = savedPos.Y;
            }
            else
            {
                _initialX = x;
                _initialY = y;
            }
            
            this.MouseLeftButtonDown += (s, e) => 
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
            
            this.LocationChanged += MainWindow_LocationChanged;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void LockNote_Checked(object sender, RoutedEventArgs e)
        {
            _isLocked = true;
            this.ResizeMode = ResizeMode.NoResize;
        }

        private void LockNote_Unchecked(object sender, RoutedEventArgs e)
        {
            _isLocked = false;
            this.ResizeMode = ResizeMode.CanResizeWithGrip;
        }

        private void OpenHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWin = new HistoryWindow();
            historyWin.Show();
        }

        // Default constructor for designer, though purely runtime usage will use the other one
        public MainWindow() : this(GetDefaultPath()) { }

        private static string GetDefaultPath()
        {
             // Fallback for designer or no-arg instantiations just in case
             var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
             var noteDir = Path.Combine(appData, "DesktopStickyNote");
             Directory.CreateDirectory(noteDir);
             return Path.Combine(noteDir, "note_default.txt");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load text
            if (File.Exists(_noteFilePath))
            {
                try
                {
                    NoteContent.Text = File.ReadAllText(_noteFilePath);
                }
                catch { }
            }

            // Send to Bottom
            var hwnd = new WindowInteropHelper(this).Handle;
            
            // If we have an initial position (from cursor), set it here using Pixels (bypassing DPI issues)
            // SWP_NOACTIVATE | SWP_NOZORDER (we handle Z separately or together)
            // Actually, we want HWND_BOTTOM, so we do it all in one go or separate.
            // Let's do one SetWindowPos for position if needed, then Z-order.
            // Or combined.
            
            uint flags = SWP_NOSIZE | SWP_NOACTIVATE;
            int x = 0, y = 0;
            
            if (_initialX != -1 && _initialY != -1)
            {
                x = _initialX;
                y = _initialY;
            }
            else
            {
                // If no specific pos, keep current (NOMOVE)
                flags |= SWP_NOMOVE;
            }

            SetWindowPos(hwnd, HWND_BOTTOM, x, y, 0, 0, flags);
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            // Save position when window is moved (debounce could be added here too)
            if (this.IsLoaded && !_isLocked)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                RECT rect;
                if (GetWindowRect(hwnd, out rect))
                {
                    PositionManager.SavePosition(_noteFilePath, rect.Left, rect.Top);
                }
            }
        }

        private System.Windows.Threading.DispatcherTimer _saveTimer;

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ensure pending save is flushed
            if (_saveTimer != null && _saveTimer.IsEnabled)
            {
                 _saveTimer.Stop();
                 SaveNote();
            }
            else 
            {
                SaveNote(); // Just in case
            }
        }

        private void NoteContent_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Threading.DispatcherTimer();
                _saveTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5 sec debounce
                _saveTimer.Tick += (s, args) => 
                {
                    _saveTimer.Stop();
                    SaveNote();
                };
            }

            // Reset timer
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            // Silent delete (Archiving)
            _isDeleting = true;
            try 
            {
                if (File.Exists(_noteFilePath) && !string.IsNullOrWhiteSpace(NoteContent.Text))
                {
                    // Ensure History directory exists
                    var noteDir = Path.GetDirectoryName(_noteFilePath);
                    var historyDir = Path.Combine(noteDir!, "History");
                    if (!Directory.Exists(historyDir))
                    {
                        Directory.CreateDirectory(historyDir);
                    }

                    // Move file to history
                    var fileName = Path.GetFileName(_noteFilePath);
                    var destPath = Path.Combine(historyDir, fileName);
                    
                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Move(_noteFilePath, destPath);
                }
                else if (File.Exists(_noteFilePath))
                {
                    // Empty note -> just delete
                    File.Delete(_noteFilePath);
                }
            }
            catch (Exception ex) 
            { 
                    MessageBox.Show($"Ошибка архивации: {ex.Message}"); // Keep error visible
            }
            
            this.Close();
        }

        private void SaveNote()
        {
            if (_isDeleting) return;

            try
            {
                if (_noteFilePath != null)
                {
                    File.WriteAllText(_noteFilePath, NoteContent.Text);
                }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
    }
}