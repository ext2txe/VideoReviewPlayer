using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace VideoReviewPlayer
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private bool isDragging = false;
        private string currentVideoPath = "";
        private string currentFolderPath = "";
        private string keepFolderPath = "";
        private const string AppVersion = "0.1.48";

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
            LoadSettings();
            UpdateControlStates(false);
            this.Title = $"Video Review Player v{AppVersion}";
        }

        private void LoadSettings()
        {
            try
            {
                // Load window position and size
                if (Properties.Settings.Default.WindowLeft >= 0 && Properties.Settings.Default.WindowTop >= 0)
                {
                    this.Left = Properties.Settings.Default.WindowLeft;
                    this.Top = Properties.Settings.Default.WindowTop;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                }

                this.Width = Properties.Settings.Default.WindowWidth;
                this.Height = Properties.Settings.Default.WindowHeight;

                if (Properties.Settings.Default.WindowMaximized)
                {
                    this.WindowState = WindowState.Maximized;
                }

                // Load left panel width
                LeftColumn.Width = new GridLength(Properties.Settings.Default.LeftPanelWidth);

                // Load folder paths
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FolderPath) && Directory.Exists(Properties.Settings.Default.FolderPath))
                {
                    currentFolderPath = Properties.Settings.Default.FolderPath;
                    TxtFolderPath.Text = currentFolderPath;
                    LoadVideoFiles();
                }

                if (!string.IsNullOrEmpty(Properties.Settings.Default.KeepFolderPath))
                {
                    keepFolderPath = Properties.Settings.Default.KeepFolderPath;
                    TxtKeepFolderPath.Text = keepFolderPath;
                }

                // Load playback speed
                CmbPlaybackSpeed.SelectedIndex = Properties.Settings.Default.PlaybackSpeed;

                // Load success message preference
                ChkShowSuccessMessages.IsChecked = Properties.Settings.Default.ShowSuccessMessages;

                // Load image format preference
                if (CmbImageFormat != null)
                    CmbImageFormat.SelectedIndex = Properties.Settings.Default.ImageFormat;
            }
            catch (Exception ex)
            {
                // If settings are corrupted, use defaults
                System.Windows.MessageBox.Show($"Error loading settings, using defaults: {ex.Message}",
                              "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveSettings()
        {
            try
            {
                // Don't save settings if UI isn't fully loaded
                if (!this.IsLoaded)
                    return;

                // Save window position and size
                if (this.WindowState == WindowState.Normal)
                {
                    Properties.Settings.Default.WindowLeft = this.Left;
                    Properties.Settings.Default.WindowTop = this.Top;
                    Properties.Settings.Default.WindowWidth = this.Width;
                    Properties.Settings.Default.WindowHeight = this.Height;
                    Properties.Settings.Default.WindowMaximized = false;
                }
                else
                {
                    Properties.Settings.Default.WindowMaximized = this.WindowState == WindowState.Maximized;
                }

                // Save left panel width (with null check)
                if (LeftColumn != null)
                    Properties.Settings.Default.LeftPanelWidth = LeftColumn.Width.Value;

                // Save folder paths
                Properties.Settings.Default.FolderPath = currentFolderPath ?? "";
                Properties.Settings.Default.KeepFolderPath = keepFolderPath ?? "";

                // Save playback speed (with null check)
                if (CmbPlaybackSpeed != null)
                    Properties.Settings.Default.PlaybackSpeed = CmbPlaybackSpeed.SelectedIndex;

                // Save success message preference (with null check)
                if (ChkShowSuccessMessages != null)
                    Properties.Settings.Default.ShowSuccessMessages = ChkShowSuccessMessages.IsChecked ?? true;

                // Save image format preference (with null check)
                if (CmbImageFormat != null)
                    Properties.Settings.Default.ImageFormat = CmbImageFormat.SelectedIndex;

                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}",
                              "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
        }

        private void MainSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            SaveSettings();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                UpdateTimeDisplay();
                UpdateTimeline();
            }
        }

        private void UpdateTimeDisplay()
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                TxtCurrentTime.Text = FormatTime(VideoPlayer.Position);
                TxtTotalTime.Text = FormatTime(VideoPlayer.NaturalDuration.TimeSpan);
            }
        }

        private void UpdateTimeline()
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = VideoPlayer.NaturalDuration.TimeSpan;
                var position = VideoPlayer.Position;

                if (duration.TotalSeconds > 0)
                {
                    var progressPercentage = position.TotalSeconds / duration.TotalSeconds;
                    var timelineWidth = TimelineCanvas.ActualWidth;

                    TimelineProgress.Width = timelineWidth * progressPercentage;

                    // Update playhead position
                    var playheadPosition = timelineWidth * progressPercentage;
                    PlayHead.Margin = new Thickness(playheadPosition - 6, 0, 0, 0);
                }
            }
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"hh\:mm\:ss");
            else
                return time.ToString(@"mm\:ss");
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing video files";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    currentFolderPath = dialog.SelectedPath;
                    TxtFolderPath.Text = currentFolderPath;
                    LoadVideoFiles();
                    SaveSettings(); // Save folder path immediately
                }
            }
        }

        private void BtnBrowseKeepFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select folder to move kept videos to";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    keepFolderPath = dialog.SelectedPath;
                    TxtKeepFolderPath.Text = keepFolderPath;
                    SaveSettings(); // Save keep folder path immediately
                }
            }
        }

        private void LoadVideoFiles()
        {
            if (string.IsNullOrEmpty(currentFolderPath) || !Directory.Exists(currentFolderPath))
            {
                LstVideoFiles.Items.Clear();
                TxtFileCount.Text = "No files";
                return;
            }

            try
            {
                var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".m4v", ".flv", ".webm" };

                var videoFiles = Directory.GetFiles(currentFolderPath)
                    .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => Path.GetFileName(f))
                    .ToList();

                LstVideoFiles.Items.Clear();

                foreach (var file in videoFiles)
                {
                    var fileInfo = new FileInfo(file);
                    var displayName = $"{Path.GetFileName(file)} ({FormatFileSize(fileInfo.Length)})";

                    LstVideoFiles.Items.Add(new VideoFileItem
                    {
                        FullPath = file,
                        DisplayName = displayName,
                        FileName = Path.GetFileName(file)
                    });
                }

                TxtFileCount.Text = $"{videoFiles.Count} video file(s) found";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading files: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFileCount.Text = "Error loading files";
            }
        }

        private void LstVideoFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LstVideoFiles.SelectedItem is VideoFileItem selectedItem)
            {
                LoadVideoFile(selectedItem.FullPath);
            }
        }

        private void LoadVideoFile(string filePath)
        {
            try
            {
                // Stop current video if playing
                VideoPlayer.Stop();
                timer.Stop();

                currentVideoPath = filePath;
                VideoPlayer.Source = new Uri(currentVideoPath);
                this.Title = $"Video Review Player v{AppVersion} - {Path.GetFileName(currentVideoPath)}";

                // Reset UI
                BtnPlay.Content = "Play";
                PlayHead.Visibility = Visibility.Collapsed;
                TimelineProgress.Width = 0;
                TxtCurrentTime.Text = "00:00";
                TxtTotalTime.Text = "00:00";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading video: {ex.Message}", "Load Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes >= 1073741824) // GB
                return $"{bytes / 1073741824.0:F1} GB";
            else if (bytes >= 1048576) // MB
                return $"{bytes / 1048576.0:F1} MB";
            else if (bytes >= 1024) // KB
                return $"{bytes / 1024.0:F1} KB";
            else
                return $"{bytes} bytes";
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mov;*.wmv;*.mkv)|*.mp4;*.avi;*.mov;*.wmv;*.mkv|All files (*.*)|*.*",
                Title = "Select Video File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Stop current video if playing
                    VideoPlayer.Stop();
                    timer.Stop();

                    currentVideoPath = openFileDialog.FileName;
                    VideoPlayer.Source = new Uri(currentVideoPath);
                    this.Title = $"Video Review Player v{AppVersion} - {Path.GetFileName(currentVideoPath)}";

                    // Reset UI
                    BtnPlay.Content = "Play";
                    PlayHead.Visibility = Visibility.Collapsed;
                    TimelineProgress.Width = 0;
                    TxtCurrentTime.Text = "00:00";
                    TxtTotalTime.Text = "00:00";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error loading video: {ex.Message}", "Load Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            UpdateControlStates(true);
            PlayHead.Visibility = Visibility.Visible;
            timer.Start();
            UpdateTimeDisplay();

            // Ensure video is ready to play and apply current speed setting
            VideoPlayer.Position = TimeSpan.Zero;

            // Apply the selected speed ratio
            if (CmbPlaybackSpeed.SelectedIndex >= 0)
            {
                var speeds = new double[] { 1.0, 1.5, 2.0, 4.0, 8.0, 16.0 };
                VideoPlayer.SpeedRatio = speeds[CmbPlaybackSpeed.SelectedIndex];
            }
        }

        private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Windows.MessageBox.Show($"Failed to load video: {e.ErrorException?.Message ?? "Unknown error"}",
                          "Media Error", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateControlStates(false);
            PlayHead.Visibility = Visibility.Collapsed;
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            BtnPlay.Content = "Play";
            VideoPlayer.Position = TimeSpan.Zero;
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source != null)
            {
                try
                {
                    VideoPlayer.Play();

                    // Show current speed in play button
                    var currentSpeed = VideoPlayer.SpeedRatio;
                    if (currentSpeed == 1.0)
                        BtnPlay.Content = "Playing...";
                    else
                        BtnPlay.Content = $"Playing {currentSpeed}x";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error playing video: {ex.Message}", "Playback Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a video file first.", "No Video",
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Pause();
            BtnPlay.Content = "Play";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Stop();
            BtnPlay.Content = "Play";
            VideoPlayer.Position = TimeSpan.Zero;
        }

        private void CmbPlaybackSpeed_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (VideoPlayer.Source != null && CmbPlaybackSpeed.SelectedIndex >= 0)
            {
                var speeds = new double[] { 1.0, 1.5, 2.0, 4.0, 8.0, 16.0 };
                var selectedSpeed = speeds[CmbPlaybackSpeed.SelectedIndex];

                try
                {
                    VideoPlayer.SpeedRatio = selectedSpeed;

                    // Update play button text to show current speed if playing
                    if (BtnPlay.Content.ToString().StartsWith("Playing"))
                    {
                        if (selectedSpeed == 1.0)
                            BtnPlay.Content = "Playing...";
                        else
                            BtnPlay.Content = $"Playing {selectedSpeed}x";
                    }

                    if (this.IsLoaded)
                        SaveSettings(); // Save speed preference immediately
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error setting playback speed: {ex.Message}",
                                  "Speed Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Reset to normal speed
                    CmbPlaybackSpeed.SelectedIndex = 0;
                    VideoPlayer.SpeedRatio = 1.0;
                }
            }
        }

        private void CmbImageFormat_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Only save if the window is fully loaded to avoid startup issues
            if (this.IsLoaded)
                SaveSettings(); // Save image format preference immediately
        }

        private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source == null)
            {
                System.Windows.MessageBox.Show("No video loaded to capture.", "Screenshot Error",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create a render target bitmap from the video player
                var renderTargetBitmap = new RenderTargetBitmap(
                    (int)VideoPlayer.ActualWidth,
                    (int)VideoPlayer.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);

                renderTargetBitmap.Render(VideoPlayer);

                // Determine file extension based on selected format
                var formats = new[] { "jpg", "png", "bmp" };
                var selectedFormat = formats[CmbImageFormat.SelectedIndex];

                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var videoName = Path.GetFileNameWithoutExtension(currentVideoPath);
                var fileName = $"{videoName}_frame_{timestamp}.{selectedFormat}";

                // Determine save location
                string savePath;
                if (!string.IsNullOrEmpty(keepFolderPath) && Directory.Exists(keepFolderPath))
                {
                    savePath = Path.Combine(keepFolderPath, fileName);
                }
                else
                {
                    // Save to same folder as video if no keep folder specified
                    var videoFolder = Path.GetDirectoryName(currentVideoPath);
                    savePath = Path.Combine(videoFolder, fileName);
                }

                // Handle duplicate filenames
                var counter = 1;
                var originalPath = savePath;
                while (File.Exists(savePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
                    var directory = Path.GetDirectoryName(originalPath);
                    var extension = Path.GetExtension(originalPath);
                    savePath = Path.Combine(directory, $"{nameWithoutExt}_{counter}{extension}");
                    counter++;
                }

                // Save the image based on format
                BitmapEncoder encoder;
                switch (selectedFormat.ToLower())
                {
                    case "png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case "bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case "jpg":
                    default:
                        var jpegEncoder = new JpegBitmapEncoder();
                        jpegEncoder.QualityLevel = 95; // High quality JPEG
                        encoder = jpegEncoder;
                        break;
                }

                encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                if (ChkShowSuccessMessages.IsChecked == true)
                {
                    System.Windows.MessageBox.Show($"Screenshot saved:\n{savePath}", "Screenshot Saved",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving screenshot: {ex.Message}", "Screenshot Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var mousePosition = e.GetPosition(TimelineCanvas);
                var percentage = mousePosition.X / TimelineCanvas.ActualWidth;

                // Clamp percentage between 0 and 1
                percentage = Math.Max(0, Math.Min(1, percentage));

                var newPosition = TimeSpan.FromSeconds(
                    VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds * percentage);

                VideoPlayer.Position = newPosition;
                UpdateTimeline();
                UpdateTimeDisplay();
            }
        }

        // Quick jump buttons
        private void BtnJumpStart_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source != null)
            {
                VideoPlayer.Position = TimeSpan.Zero;
            }
        }

        private void BtnJump25_Click(object sender, RoutedEventArgs e)
        {
            JumpToPercentage(0.25);
        }

        private void BtnJump50_Click(object sender, RoutedEventArgs e)
        {
            JumpToPercentage(0.50);
        }

        private void BtnJump75_Click(object sender, RoutedEventArgs e)
        {
            JumpToPercentage(0.75);
        }

        private void BtnJumpEnd_Click(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var endPosition = VideoPlayer.NaturalDuration.TimeSpan.Subtract(TimeSpan.FromSeconds(1));
                VideoPlayer.Position = endPosition;
            }
        }

        private void JumpToPercentage(double percentage)
        {
            if (VideoPlayer.Source != null && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPosition = TimeSpan.FromSeconds(
                    VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds * percentage);
                VideoPlayer.Position = newPosition;
            }
        }

        // Decision buttons
        private void BtnKeep_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentVideoPath))
            {
                var fileName = Path.GetFileName(currentVideoPath);
                var actionText = string.IsNullOrEmpty(keepFolderPath) ?
                    "mark as KEEP" :
                    $"move to keep folder:\n{keepFolderPath}";

                var result = System.Windows.MessageBox.Show(
                    $"Do you want to {actionText}?\n\nFile: {fileName}",
                    "Keep Video",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(keepFolderPath) && Directory.Exists(keepFolderPath))
                        {
                            // Move file to keep folder
                            var destinationPath = Path.Combine(keepFolderPath, fileName);

                            // Handle duplicate file names
                            if (File.Exists(destinationPath))
                            {
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                var extension = Path.GetExtension(fileName);
                                var counter = 1;

                                do
                                {
                                    var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                                    destinationPath = Path.Combine(keepFolderPath, newFileName);
                                    counter++;
                                } while (File.Exists(destinationPath));
                            }

                            VideoPlayer.Source = null; // Release file handle
                            File.Move(currentVideoPath, destinationPath);

                            // Remove from list
                            var itemToRemove = LstVideoFiles.Items.Cast<VideoFileItem>()
                                .FirstOrDefault(item => item.FullPath == currentVideoPath);

                            if (itemToRemove != null)
                            {
                                LstVideoFiles.Items.Remove(itemToRemove);
                                TxtFileCount.Text = $"{LstVideoFiles.Items.Count} video file(s) found";
                            }

                            if (ChkShowSuccessMessages.IsChecked == true)
                            {
                                System.Windows.MessageBox.Show($"Video moved to keep folder:\n{destinationPath}",
                                              "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            // Just mark as keep (no folder specified or folder doesn't exist)
                            if (ChkShowSuccessMessages.IsChecked == true)
                            {
                                System.Windows.MessageBox.Show("Video marked to KEEP!", "Success",
                                              MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }

                        this.Title = $"Video Review Player v{AppVersion}";
                        UpdateControlStates(false);
                        currentVideoPath = "";

                        // Load next video in list
                        LoadNextVideoInList();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error moving file: {ex.Message}", "Error",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentVideoPath))
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to DELETE '{Path.GetFileName(currentVideoPath)}'?\n\nThis action cannot be undone!",
                    "Delete Video",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        VideoPlayer.Source = null;
                        File.Delete(currentVideoPath);

                        if (ChkShowSuccessMessages.IsChecked == true)
                        {
                            System.Windows.MessageBox.Show("Video deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        // Remove from list and refresh
                        var itemToRemove = LstVideoFiles.Items.Cast<VideoFileItem>()
                            .FirstOrDefault(item => item.FullPath == currentVideoPath);

                        if (itemToRemove != null)
                        {
                            LstVideoFiles.Items.Remove(itemToRemove);
                            TxtFileCount.Text = $"{LstVideoFiles.Items.Count} video file(s) found";
                        }

                        this.Title = $"Video Review Player v{AppVersion}";
                        UpdateControlStates(false);
                        currentVideoPath = "";

                        // Load next video if available
                        if (LstVideoFiles.Items.Count > 0)
                        {
                            LstVideoFiles.SelectedIndex = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error deleting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void LoadNextVideoInList()
        {
            if (LstVideoFiles.SelectedIndex < LstVideoFiles.Items.Count - 1)
            {
                LstVideoFiles.SelectedIndex++;
            }
            else if (LstVideoFiles.Items.Count > 0)
            {
                // Loop back to first item
                LstVideoFiles.SelectedIndex = 0;
            }
        }

        private void UpdateControlStates(bool hasVideo)
        {
            BtnPlay.IsEnabled = hasVideo;
            BtnPause.IsEnabled = hasVideo;
            BtnStop.IsEnabled = hasVideo;
            BtnKeep.IsEnabled = hasVideo;
            BtnDelete.IsEnabled = hasVideo;
            BtnScreenshot.IsEnabled = hasVideo;
            CmbPlaybackSpeed.IsEnabled = hasVideo;

            // Quick jump buttons
            BtnJumpStart.IsEnabled = hasVideo;
            BtnJump25.IsEnabled = hasVideo;
            BtnJump50.IsEnabled = hasVideo;
            BtnJump75.IsEnabled = hasVideo;
            BtnJumpEnd.IsEnabled = hasVideo;
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings(); // Save all settings when closing
            timer?.Stop();
            VideoPlayer?.Stop();
            base.OnClosed(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            // Save window state changes
            if (this.IsLoaded)
            {
                SaveSettings();
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            // Save window position changes (but not too frequently)
            if (this.IsLoaded && this.WindowState == WindowState.Normal)
            {
                SaveSettings();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            // Save window size changes
            if (this.IsLoaded && this.WindowState == WindowState.Normal)
            {
                SaveSettings();
            }
        }

        // Keyboard shortcuts
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (VideoPlayer.Source != null)
            {
                switch (e.Key)
                {
                    case Key.Space:
                        if (VideoPlayer.CanPause)
                        {
                            if (BtnPlay.Content.ToString() == "Play")
                                BtnPlay_Click(null, null);
                            else
                                BtnPause_Click(null, null);
                        }
                        e.Handled = true;
                        break;

                    case Key.Left:
                        // Skip back 5 seconds
                        var newPos = VideoPlayer.Position.Subtract(TimeSpan.FromSeconds(5));
                        VideoPlayer.Position = newPos < TimeSpan.Zero ? TimeSpan.Zero : newPos;
                        e.Handled = true;
                        break;

                    case Key.Right:
                        // Skip forward 5 seconds
                        if (VideoPlayer.NaturalDuration.HasTimeSpan)
                        {
                            var skipPos = VideoPlayer.Position.Add(TimeSpan.FromSeconds(5));
                            VideoPlayer.Position = skipPos > VideoPlayer.NaturalDuration.TimeSpan
                                ? VideoPlayer.NaturalDuration.TimeSpan
                                : skipPos;
                        }
                        e.Handled = true;
                        break;

                    case Key.Home:
                        BtnJumpStart_Click(null, null);
                        e.Handled = true;
                        break;

                    case Key.End:
                        BtnJumpEnd_Click(null, null);
                        e.Handled = true;
                        break;
                }
            }

            base.OnKeyDown(e);
        }

        // Helper class for ListBox items
        public class VideoFileItem
        {
            public string FullPath { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string FileName { get; set; } = "";

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}