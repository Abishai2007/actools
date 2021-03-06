﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FirstFloor.ModernUI.Helpers {
    public class KillerOrder<T> : KillerOrder {
        public new T Victim => (T)base.Victim;

        public KillerOrder(T victim, TimeSpan timeout) : base(victim, timeout) { }
    }

    public class KillerOrder : IDisposable {
        public static bool OptionUseDispatcher = false;
        public static TimeSpan OptionInterval = TimeSpan.FromMilliseconds(100d);

        public readonly object Victim;
        public readonly TimeSpan Timeout;
        public DateTime KillAfter;
        public bool Killed;

        public static KillerOrder<T> Create<T>(T victim, long timeout) {
            return new KillerOrder<T>(victim, TimeSpan.FromMilliseconds(timeout));
        }

        public static KillerOrder<T> Create<T>(T victim, TimeSpan timeout) {
            return new KillerOrder<T>(victim, timeout);
        }

        protected KillerOrder(object victim, TimeSpan timeout) {
            Victim = victim;
            Timeout = timeout;
            KillAfter = DateTime.Now + timeout;
            Register(this);
        }

        public void Delay() {
            KillAfter = DateTime.Now + Timeout;
        }

        public void Dispose() {
            lock (StaticLock) {
                _sockets?.Remove(this);
            }

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }

        private void Kill() {
            Killed = true;

            var socket = Victim as Socket;
            if (socket != null) {
                socket.Close();
                return;
            }

            var webClient = Victim as WebClient;
            if (webClient != null) {
                webClient.CancelAsync();
                return;
            }

            var webRequest = Victim as WebRequest;
            if (webRequest != null) {
                webRequest.Abort();
                return;
            }

            var disposable = Victim as IDisposable;
            disposable?.Dispose();
        }
        
        private static readonly object StaticLock = new object();

        private static List<KillerOrder> _sockets;
        private static DispatcherTimer _dispatcherTimer;
        private static Timer _timer;

        private static void Register(KillerOrder order) {
            lock (StaticLock) {
                if (_sockets == null) {
                    _sockets = new List<KillerOrder>();

                    if (OptionUseDispatcher) {
                        _dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher) {
                            Interval = OptionInterval,
                            IsEnabled = true
                        };

                        _dispatcherTimer.Tick += OnDispatcherTick;
                    } else {
                        _timer = new Timer(OnTick, null, OptionInterval, System.Threading.Timeout.InfiniteTimeSpan);
                    }
                }

                _sockets.Add(order);
            }
        }

        private static void InvokeKill(bool sync) {
            lock (StaticLock) {
                if (_sockets.Count == 0) return;

                List<KillerOrder> list = null;
                var now = DateTime.Now;
                foreach (var pair in _sockets) {
                    if (now > pair.KillAfter) {
                        if (list == null) {
                            list = new List<KillerOrder> { pair };
                        } else {
                            list.Add(pair);
                            if (list.Count > 50) break;
                        }
                    }
                }

                if (list != null) {
                    foreach (var tuple in list) {
                        _sockets.Remove(tuple);
                        if (sync) {
                            (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher).BeginInvoke(DispatcherPriority.Background, (Action)tuple.Kill);
                        } else {
                            tuple.Kill();
                        }
                    }
                }
            }
        }

        private static void OnTick(object state) {
            InvokeKill(true);
            _timer.Change(OptionInterval, System.Threading.Timeout.InfiniteTimeSpan);
        }

        private static void OnDispatcherTick(object sender, EventArgs e) {
            InvokeKill(false);
        }
    }
}