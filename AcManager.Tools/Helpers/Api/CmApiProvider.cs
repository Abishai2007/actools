﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    [Localizable(false)]
    public static class CmApiProvider {
        #region Initialization
        public static string UserAgent { get; private set; }

        public static void OverrideUserAgent(string newValue) {
            UserAgent = newValue;
        }

        static CmApiProvider() {
            var windows = $"Windows NT {Environment.OSVersion.Version};{(Environment.Is64BitOperatingSystem ? @" WOW64;" : "")}";
            UserAgent = $"ContentManager/{BuildInformation.AppVersion} ({windows})";
        }
        #endregion

        [CanBeNull]
        public static string GetString(string url) {
            try {
                var result = InternalUtils.CmGetData(url, UserAgent);
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning($"Cannot read as UTF8 from {url}: " + e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<string> GetStringAsync(string url, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var result = await InternalUtils.CmGetDataAsync(url, UserAgent, cancellation: cancellation);
                if (cancellation.IsCancellationRequested) return null;
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [CanBeNull]
        public static T Get<T>(string url) {
            try {
                var json = GetString(url);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning(e);
                return default(T);
            }
        }

        [ItemCanBeNull]
        public static async Task<T> GetAsync<T>(string url, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var json = await GetStringAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return default(T);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning(e);
                return default(T);
            }
        }

        [CanBeNull]
        public static byte[] GetData(string url) {
            return InternalUtils.CmGetData(url, UserAgent);
        }

        [ItemCanBeNull]
        public static Task<byte[]> GetDataAsync(string url, IProgress<double?> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            return InternalUtils.CmGetDataAsync(url, UserAgent, progress, cancellation);
        }

        private static readonly List<string> JustLoadedStaticData = new List<string>();

        /// <summary>
        /// Load piece of static data, either from CM API, or from cache.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="progress"></param>
        /// <param name="cancellation"></param>
        /// <returns>Cached filename and if data is just loaded or not.</returns>
        [ItemCanBeNull]
        public static async Task<Tuple<string, bool>> GetStaticDataAsync(string id, IProgress<double?> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var file = new FileInfo(FilesStorage.Instance.GetTemporaryFilename("Static", $"{id}.zip"));

            if (JustLoadedStaticData.Contains(id) && file.Exists) {
                return Tuple.Create(file.FullName, false);
            }

            var result = await InternalUtils.CmGetDataAsync($"static/get/{id}", UserAgent,
                    file.Exists ? file.LastWriteTime : (DateTime?)null, progress, cancellation).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested) return null;

            if (result != null && result.Item1.Length != 0) {
                Logging.Debug($"Fresh version of {id} loaded, from {result.Item2?.ToString() ?? "UNKNOWN"}");
                var lastWriteTime = result.Item2 ?? DateTime.Now;
                await FileUtils.WriteAllBytesAsync(file.FullName, result.Item1, cancellation).ConfigureAwait(false);
                file.Refresh();
                file.LastWriteTime = lastWriteTime;
                JustLoadedStaticData.Add(id);
                return Tuple.Create(file.FullName, true);
            }

            if (!file.Exists) {
                return null;
            }

            Logging.Debug($"Cached {id} used");
            JustLoadedStaticData.Add(id);
            return Tuple.Create(file.FullName, false);
        }

        [ItemCanBeNull]
        public static async Task<byte[]> GetStaticDataBytesAsync(string id, IProgress<double?> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var t = await GetStaticDataAsync(id, progress, cancellation);
            return t == null ? null : await FileUtils.ReadAllBytesAsync(t.Item1);
        }

        [ItemCanBeNull]
        public static async Task<string> GetContentStringAsync(string url, CancellationToken cancellation = default(CancellationToken)) {
            try {
                var result = await InternalUtils.CmGetContentDataAsync(url, UserAgent, null, cancellation);
                if (cancellation.IsCancellationRequested) return null;
                return result == null ? null : Encoding.UTF8.GetString(result);
            } catch (Exception e) {
                Logging.Warning(e);
                return null;
            }
        }

        [ItemCanBeNull]
        public static async Task<T> GetContentAsync<T>(string url = "", CancellationToken cancellation = default(CancellationToken)) {
            try {
                var json = await GetContentStringAsync(url, cancellation);
                if (cancellation.IsCancellationRequested) return default(T);
                return json == null ? default(T) : JsonConvert.DeserializeObject<T>(json);
            } catch (Exception e) {
                Logging.Warning(e);
                return default(T);
            }
        }
    }
}
