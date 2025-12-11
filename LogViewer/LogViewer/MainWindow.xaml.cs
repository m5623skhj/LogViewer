using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogViewerApp
{
    public partial class MainWindow : Window
    {
        private readonly List<LogEntry> _allLogs = [];
        private readonly ObservableCollection<LogEntry> _displayedLogs = [];

        public MainWindow()
        {
            InitializeComponent();
            LogListView.ItemsSource = _displayedLogs;
        }

        private void BtnOpenFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            _allLogs.Clear();

            foreach (var file in dlg.FileNames)
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    var buffer = "";
                    var openBraces = 0;

                    foreach (var line in lines)
                    {
                        buffer += line.Trim();
                        openBraces += line.Count(c => c == '{');
                        openBraces -= line.Count(c => c == '}');

                        if (openBraces != 0 || string.IsNullOrWhiteSpace(buffer))
                        {
                            continue;
                        }

                        var entry = ParseLineToEntry(buffer);
                        if (entry != null)
                        {
                            _allLogs.Add(entry);
                        }

                        buffer = "";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일을 읽는 중 오류 발생: {file}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            var sorted = _allLogs.OrderBy(e => e.Timestamp).ToList();
            _allLogs.Clear();
            _allLogs.AddRange(sorted);

            RefreshDisplay(_allLogs);
            StatusText.Text = $"파일 로드 완료: {_allLogs.Count} 라인";
        }

        private LogEntry? ParseLineToEntry(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var time = ExtractTimestamp(line);
            var level = DetectLevel(line);

            return new LogEntry(line, level, time);
        }

        private static DateTime ExtractTimestamp(string line)
        {
            var m = Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})");
            if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            return DateTime.MinValue;
        }

        private static LogLevel DetectLevel(string line)
        {
            if (line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LogLevel.Error;
            }

            if (line.IndexOf("WARN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LogLevel.Warning;
            }

            return line.IndexOf("DEBUG", StringComparison.OrdinalIgnoreCase) >= 0 ? LogLevel.Debug : LogLevel.Info;
        }

        private void RefreshDisplay(IEnumerable<LogEntry> entries, string[]? highlightTerms = null)
        {
            _displayedLogs.Clear();

            var pinned = entries.Where(e => e.IsPinned).OrderByDescending(e => e.Timestamp).ToList();
            var others = entries.Where(e => !e.IsPinned).OrderBy(e => e.Timestamp).ToList();

            foreach (var e in pinned.Concat(others))
            {
                e.IsMatched = highlightTerms is { Length: > 0 } && highlightTerms.Any(t => e.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

                e.UpdateBrushes();
                _displayedLogs.Add(e);
            }

            RefreshStats();
        }

        private void RefreshStats()
        {
            var total = _allLogs.Count;
            var info = _allLogs.Count(x => x.Level == LogLevel.Info);
            var warn = _allLogs.Count(x => x.Level == LogLevel.Warning);
            var error = _allLogs.Count(x => x.Level == LogLevel.Error);

            TxtTotal.Text = $"Total: {total}";
            TxtInfo.Text = $"Info: {info}";
            TxtWarn.Text = $"Warn: {warn}";
            TxtError.Text = $"Error: {error}";
        }

        private void BtnApplyTimeFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!DateTime.TryParseExact(StartBox.Text.Trim(), "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var from))
            {
                MessageBox.Show("시작 시간을 yyyy-MM-dd HH:mm:ss 형식으로 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DateTime.TryParseExact(EndBox.Text.Trim(), "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var to))
            {
                MessageBox.Show("종료 시간을 yyyy-MM-dd HH:mm:ss 형식으로 입력하세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var filtered = _allLogs.Where(l => l.Timestamp >= from && l.Timestamp <= to).ToList();
            RefreshDisplay(filtered);
            StatusText.Text = $"시간필터 적용: {filtered.Count} 라인 표시";
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            var text = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                RefreshDisplay(_allLogs);
                return;
            }

            var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mode = ((ComboBoxItem)SearchModeCombo.SelectedItem).Content?.ToString() ?? "AND";

            var result = mode == "AND" ? _allLogs.Where(l => terms.All(t => l.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) : _allLogs.Where(l => terms.Any(t => l.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0));

            RefreshDisplay(result, terms);
            StatusText.Text = $"검색: {terms.Length}개 키워드, {result.Count()} 라인";
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            StartBox.Text = "";
            EndBox.Text = "";
            RefreshDisplay(_allLogs);
            StatusText.Text = "필터 초기화";
        }

        private void BtnStats_Click(object sender, RoutedEventArgs e)
        {
            var total = _allLogs.Count;
            var info = _allLogs.Count(x => x.Level == LogLevel.Info);
            var warn = _allLogs.Count(x => x.Level == LogLevel.Warning);
            var error = _allLogs.Count(x => x.Level == LogLevel.Error);

            MessageBox.Show($"Total: {total}\nInfo: {info}\nWarn: {warn}\nError: {error}", "통계", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: LogEntry entry })
            {
                return;
            }

            entry.IsPinned = !entry.IsPinned;
            entry.UpdateBrushes();
            RefreshDisplay(_displayedLogs.Union(_allLogs).Distinct());
        }

        private void BtnShowPinnedOnly_Click(object sender, RoutedEventArgs e)
        {
            var pinnedOnly = _allLogs.Where(x => x.IsPinned).ToList();
            RefreshDisplay(pinnedOnly);
            StatusText.Text = $"핀 항목만 표시: {pinnedOnly.Count}";
        }

        private void BtnShowAll_Click(object sender, RoutedEventArgs e)
        {
            RefreshDisplay(_allLogs);
            StatusText.Text = "전체 표시";
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_displayedLogs.Count == 0)
            {
                MessageBox.Show("내보낼 로그가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"exported_logs_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            try
            {
                using (var writer = new StreamWriter(dlg.FileName))
                {
                    writer.WriteLine("=== 로그 내보내기 ===");
                    writer.WriteLine($"내보낸 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"총 로그 수: {_displayedLogs.Count}");
                    writer.WriteLine("===================================");
                    writer.WriteLine();

                    foreach (var log in _displayedLogs)
                    {
                        var pinMark = log.IsPinned ? "[📌] " : "";
                        var levelMark = log.Level switch
                        {
                            LogLevel.Error => "[ERROR] ",
                            LogLevel.Warning => "[WARN]  ",
                            LogLevel.Debug => "[DEBUG] ",
                            _ => "[INFO]  "
                        };
                        writer.WriteLine($"{pinMark}{levelMark}{log.Text}");
                    }
                }

                MessageBox.Show($"로그를 성공적으로 내보냈습니다.\n파일: {dlg.FileName}\n총 {_displayedLogs.Count}개 라인", "내보내기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = $"로그 내보내기 완료: {_displayedLogs.Count}개 라인";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 저장 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class LogEntry
    {
        public string Text { get; }
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public bool IsPinned { get; set; }
        public bool IsMatched { get; set; }

        public string DisplayText => Text;
        public SolidColorBrush ForegroundBrush { get; private set; } = new(Colors.White);
        public SolidColorBrush BackgroundBrush { get; private set; } = new(Colors.Transparent);

        public LogEntry(string text, LogLevel level, DateTime timestamp)
        {
            Text = text;
            Level = level;
            Timestamp = timestamp;
            UpdateBrushes();
        }

        public void UpdateBrushes()
        {
            var fg = Level switch
            {
                LogLevel.Info => Colors.LightGreen,
                LogLevel.Warning => Colors.Khaki,
                LogLevel.Error => Colors.LightCoral,
                LogLevel.Debug => Colors.LightBlue,
                _ => Colors.White
            };
            ForegroundBrush = new SolidColorBrush(fg);

            if (IsPinned)
            {
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(56, 56, 56));
            }
            else if (IsMatched)
            {
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(60, 60, 30));
            }
            else
            {
                BackgroundBrush = new SolidColorBrush(Colors.Transparent);
            }
        }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Debug
    }
}
