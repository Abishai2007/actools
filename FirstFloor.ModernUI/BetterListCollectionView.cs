﻿using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class BetterListCollectionView : ListCollectionView, IWeakEventListener {
        private bool _compatible;

        public BetterListCollectionView([NotNull] IList list)
                : base(list) {
            var changed = list as INotifyCollectionChanged;
            if (changed == null) return;

            changed.CollectionChanged -= OnCollectionChanged;
            CollectionChangedEventManager.AddListener(changed, this);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            var notify = e as NotifyCollectionChangedEventArgs;
            if (notify == null) return false;

            if (_compatible) {
                Refresh();
                return true;
            }

            try {
                ActionExtension.InvokeInMainThread(() => {
                    OnCollectionChanged(sender, notify);
                });
            } catch (ArgumentOutOfRangeException ex) {
                _compatible = true;

                Logging.Debug("That weird ListCollectionView crash, switching to the compatible mode…");
                Logging.Debug(notify.Action);
                Logging.Debug($"Old: {notify.OldStartingIndex}; new: {notify.NewStartingIndex}");
                Logging.Debug($"Old: {string.Join(", ", notify.OldItems.OfType<object>())}; new: {string.Join(", ", notify.NewItems.OfType<object>())}");
                Logging.Debug(ex);

                Refresh();
            } catch (Exception ex) {
                Logging.Warning(ex);
                Refresh();
            }

            return true;
        }

        public void Refresh([NotNull] object obj) {
            EditItem(obj);
            CommitEdit();
        }
    }
}
