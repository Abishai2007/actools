﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Dialogs {
    public partial class WaitingDialog : INotifyPropertyChanged, IProgress<string>, IProgress<double?>, IProgress<Tuple<string, double?>>,
            IProgress<AsyncProgressEntry>, IDisposable, IProgress<double> {
        public static WaitingDialog Create(string reportValue) {
            var w = new WaitingDialog();
            w.Report(reportValue);
            return w;
        }

        private string _message;

        public string Message {
            get => _message;
            private set {
                if (Equals(value, _message)) return;
                _message = value;
                OnPropertyChanged();
            }
        }

        private string _details;

        public string Details {
            get => _details;
            private set {
                if (Equals(value, _details)) return;
                _details = value;
                OnPropertyChanged();
            }
        }

        public void SetMultiline(bool multiline) {
            lock (_lock) {
                Dispatcher.Invoke(() => {
                    MessageBlock.MinHeight = multiline ? 40d : 0d;
                });
            }
        }

        public void SetDetails([CanBeNull] IEnumerable<string> details) {
            lock (_lock) {
                Dispatcher.Invoke(() => {
                    Details = details == null ? null : string.Join(Environment.NewLine, details);
                });
            }
        }

        public void SetDetails([CanBeNull] params string[] details) {
            SetDetails((IEnumerable<string>)details);
        }

        public void SetImage(string imageFilename) {
            lock (_lock) {
                Dispatcher.Invoke(() => {
                    Image.Visibility = Visibility.Visible;
                    Image.Filename = imageFilename;
                });
            }
        }

        private double? _progress;

        public double? Progress {
            get => _progress;
            private set {
                if (Equals(value, _progress)) return;
                _progress = value;
                OnPropertyChanged();
            }
        }

        private bool _progressIndetermitate;

        public bool ProgressIndetermitate {
            get => _progressIndetermitate;
            private set {
                if (Equals(value, _progressIndetermitate)) return;
                _progressIndetermitate = value;
                OnPropertyChanged();
            }
        }

        private bool _isCancelled;

        public bool IsCancelled {
            get => _isCancelled;
            private set {
                if (Equals(value, _isCancelled)) return;
                _isCancelled = value;
                OnPropertyChanged();
            }
        }

        public WaitingDialog(string title = null) {
            Title = title;

            if (title == null) {
                ShowTitle = false;
                ShowTopBlob = false;
            }

            DataContext = this;
            InitializeComponent();
            Padding = new Thickness(24, ShowTopBlob ? 20 : 0, 24, 0);
            Buttons = new Button[] { };
        }

        private TaskbarProgress _taskbarProgress;

        public new string Title {
            get => base.Title;
            set => base.Title = value ?? UiStrings.Common_PleaseWait;
        }

        private string _cancellationText = UiStrings.Cancel;

        public string CancellationText {
            get => _cancellationText;
            set {
                if (Equals(value, _cancellationText)) return;
                _cancellationText = value;
                OnPropertyChanged();

                var c = Buttons.OfType<Button>().FirstOrDefault();
                if (c != null) {
                    c.Content = value;
                }
            }
        }

        private CancellationTokenSource _cancellationTokenSource;

        public CancellationToken CancellationToken {
            get {
                if (_cancellationTokenSource != null) return _cancellationTokenSource.Token;

                lock (_lock) {
                    return Dispatcher.Invoke(() => {
                        _cancellationTokenSource = new CancellationTokenSource();
                        Buttons = new[] { CreateCloseDialogButton(CancellationText, false, true, MessageBoxResult.Cancel) };
                        Padding = new Thickness(24, ShowTopBlob ? 20 : 0, 24, 20);
                        Closing += WaitingDialog_Closing;
                        return _cancellationTokenSource.Token;
                    });
                }
            }
        }

        private bool _closeWithoutCancellation;

        private void WaitingDialog_Closing(object sender, CancelEventArgs e) {
            if (!_closeWithoutCancellation && _cancellationTokenSource != null) {
                _cancellationTokenSource.Cancel();
                IsCancelled = true;
            }

            if (_taskbarProgress != null) {
                _taskbarProgress.Dispose();
                _taskbarProgress = null;
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, EventArgs eventArgs) {
            if (_disposed) {
                EnsureClosed();
            }
        }

        private bool _shown, _closed, _disposed;

        private async void EnsureShown() {
            if (_disposed) return;

            _loaded = true;
            if (IsVisible || _shown) return;

            _shown = true;

            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(500);
            if (_closed || _disposed) return;

            var app = Application.Current;
            if (app == null) {
                ShowDialog();
                return;
            }

            await app.Dispatcher.BeginInvoke((Action)(() => {
                if (_closed || _disposed) return;

                try {
                    ShowDialog();
                } catch (InvalidOperationException e) {
                    Logging.Error(e);
                }
            }));
        }

        /// <summary>
        /// Ensures window is closed, without cancelling task.
        /// </summary>
        private void EnsureClosed() {
            if (_loaded && !_closed) {
                _closed = true;
                _closeWithoutCancellation = true;
                Close();
            }
        }

        private readonly object _lock = new object();

        public void Report(string value) {
            lock (_lock) {
                if (Message == value) return;
                Dispatcher.Invoke(() => {
                    if (value != null) {
                        EnsureShown();
                        Message = value;
                    } else {
                        EnsureClosed();
                    }
                });
            }
        }

        private void SetProgress(double value) {
            Progress = value;
            ProgressIndetermitate = Equals(value, 0d);

            if (_taskbarProgress == null) {
                _taskbarProgress = new TaskbarProgress(this);
            }

            if (ProgressIndetermitate) {
                _taskbarProgress.Set(TaskbarState.Indeterminate);
            } else {
                _taskbarProgress.Set(TaskbarState.Normal);
                _taskbarProgress.Set(value);
            }
        }

        public void Report(double? value) {
            lock (_lock) {
                if (Progress.HasValue && value.HasValue && Math.Abs(Progress.Value - value.Value) < 0.0001) return;
                Dispatcher.Invoke(() => {
                    if (value.HasValue) {
                        EnsureShown();
                        SetProgress(value.Value);
                    } else {
                        EnsureClosed();
                    }
                });
            }
        }

        public void Report() {
            Report(0d);
        }

        public void Report(double value) {
            lock (_lock) {
                if (Progress.HasValue && Math.Abs(Progress.Value - value) < 0.0001) return;
                Dispatcher.Invoke(() => {
                    EnsureShown();
                    SetProgress(value);
                });
            }
        }

        public void Report(int n, int total) {
            var v = (double)n / total + 0.000001;
            Report(v < 0d ? 0d : v > 1d ? 1d : v);
        }

        public void Report(AsyncProgressEntry value) {
            lock (_lock) {
                Dispatcher.Invoke(() => {
                    if (value.Message != null) {
                        EnsureShown();
                        Message = value.Message;
                        SetProgress(value.Progress ?? 0d);
                    } else {
                        EnsureClosed();
                    }
                });
            }
        }

        public void Report(Tuple<string, double?> value) {
            lock (_lock) {
                Dispatcher.Invoke(() => {
                    if (value.Item1 != null) {
                        EnsureShown();
                        Message = value.Item1;
                        SetProgress(value.Item2 ?? 0d);
                    } else {
                        EnsureClosed();
                    }
                });
            }
        }

        public void Dispose() {
            _disposed = true;
            if (_cancellationTokenSource != null) {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            Dispatcher.Invoke(EnsureClosed);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
