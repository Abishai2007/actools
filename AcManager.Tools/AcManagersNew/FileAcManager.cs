﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    /// <summary>
    /// AcManager for files (but without watching).
    /// TODO: Combine with AcManagerNew since watchers are required anyway?
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FileAcManager<T> : BaseAcManager<T>, IFileAcManager where T : AcCommonObject {
        protected FileAcManager() {
            SettingsHolder.Content.PropertyChanged += Content_PropertyChanged;
        }

        private void Content_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.ContentSettings.NewContentPeriod)) return;
            foreach (var entry in LoadedOnly) {
                entry.CheckIfNew();
            }
        }

        [NotNull]
        protected virtual string LocationToId(string directory) {
            var name = Path.GetFileName(directory);
            if (name == null) throw new Exception("Cannot get file name from path");
            return name;
        }

        public abstract IAcDirectories Directories { get; }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return Directories.GetSubDirectories().Where(Filter).Select(dir =>
                    CreateAcPlaceholder(LocationToId(dir), Directories.CheckIfEnabled(dir)));
        }

        protected virtual void MoveInner(string id, string newId, string oldLocation, string newLocation, bool newEnabled) {
            FileUtils.Move(oldLocation, newLocation);
            
            var obj = CreateAndLoadAcObject(newId, newEnabled);
            obj.PreviousId = id;
            ReplaceInList(id, new AcItemWrapper(this, obj));
            UpdateList();
        }

        protected virtual void DeleteInner(string id, string location) {
            FileUtils.Recycle(location);
            if (!FileUtils.Exists(location)) {
                RemoveFromList(id);
            }
        }

        public virtual void Rename(string id, string newId, bool newEnabled) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) throw new ArgumentException(@"ID is wrong", nameof(id));

            var currentLocation = ((AcCommonObject)wrapper.Value).Location;
            var path = newEnabled ? Directories.EnabledDirectory : Directories.DisabledDirectory;
            if (path == null) throw new ToggleException("Object can’t be moved");

            var newLocation = Path.Combine(path, newId);
            if (FileUtils.Exists(newLocation)) throw new ToggleException("Place is taken");

            try {
                MoveInner(id, newId, currentLocation, newLocation, newEnabled);
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public void Toggle(string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) {
                throw new ArgumentException(@"ID is wrong", nameof(id));
            }

            Rename(id, id, !wrapper.Value.Enabled);
        }

        public virtual void Delete([NotNull]string id) {
            if (!Directories.Actual) return;
            if (id == null) throw new ArgumentNullException(nameof(id));

            var obj = GetById(id);
            if (obj == null) throw new ArgumentException(@"ID is wrong", nameof(id));
            
            DeleteInner(id, obj.Location);
        }

        public virtual string PrepareForAdditionalContent([NotNull] string id, bool removeExisting) {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var existing = GetById(id);
            var location = existing?.Location ?? Directories.GetLocation(id, true);

            if (removeExisting && FileUtils.Exists(location)) {
                FileUtils.Recycle(location);

                if (FileUtils.Exists(location)) {
                    throw new OperationCanceledException("Can’t remove existing directory");
                }
            }

            return location;
        }
    }
}