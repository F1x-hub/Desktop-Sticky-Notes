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
            this.SizeChanged += MainWindow_SizeChanged;
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

            var hwnd = new WindowInteropHelper(this).Handle;
            HideFromAltTab(hwnd);
            
            uint flags = SWP_NOACTIVATE;
            int x = 0, y = 0;
            int width = 0, height = 0;
            
            var savedPos = PositionManager.GetPosition(_noteFilePath);
            
            if (savedPos != null)
            {
                // Restore both position and size
                x = savedPos.X;
                y = savedPos.Y;
                width = (int)savedPos.Width;
                height = (int)savedPos.Height;
            }
            else if (_initialX != -1 && _initialY != -1)
            {
                // New note with cursor position
                x = _initialX;
                y = _initialY;
                flags |= SWP_NOSIZE;  // Keep default size for new notes
            }
            else
            {
                flags |= SWP_NOMOVE | SWP_NOSIZE;
            }
            
            SetWindowPos(hwnd, HWND_BOTTOM, x, y, width, height, flags);
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            SavePositionAndSize();
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.IsLoaded && !_isLocked)
            {
                SavePositionAndSize();
            }
        }

        private void SavePositionAndSize()
        {
            if (this.IsLoaded && !_isLocked)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                RECT rect;
                if (GetWindowRect(hwnd, out rect))
                {
                    double width = rect.Right - rect.Left;
                    double height = rect.Bottom - rect.Top;
                    PositionManager.SavePosition(_noteFilePath, rect.Left, rect.Top, width, height);
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

        private void HideFromAltTab(IntPtr hwnd)
        {
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
    }
}