using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class IniFileExtension {
        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, [LocalizationRequired(false)] string key, IList<SettingEntry> entries,
                [NotNull] SettingEntry defaultValue) {
            if (defaultValue == null) throw new ArgumentNullException(nameof(defaultValue));
            var value = section.GetNonEmpty(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? defaultValue;
        }

        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, [LocalizationRequired(false)] string key, IList<SettingEntry> entries,
                [LocalizationRequired(false), NotNull] string defaultValueId) {
            if (defaultValueId == null) throw new ArgumentNullException(nameof(defaultValueId));
            var value = section.GetNonEmpty(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.GetByIdOrDefault(defaultValueId) ?? entries.First();
        }

        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, [LocalizationRequired(false)] string key, IList<SettingEntry> entries,
                int defaultValueId) {
            var value = section.GetNonEmpty(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.FirstOrDefault(x => x.IntValue == defaultValueId) ?? entries.First();
        }

        [NotNull]
        public static SettingEntry GetEntry(this IniFileSection section, [LocalizationRequired(false)] string key, IList<SettingEntry> entries) {
            var value = section.GetNonEmpty(key);
            return entries.FirstOrDefault(x => x.Value == value) ?? entries.First();
        }

        public static void Set(this IniFileSection section, [LocalizationRequired(false)] string key, SettingEntry entry) {
            section.Set(key, entry.Value);
        }

        public static Color GetColor(this IniFileSection section, [LocalizationRequired(false)] string key, Color defaultValue) {
            var result = section.GetStrings(key).Select(x => FlexibleParser.ParseInt(x, 0).ClampToByte()).ToArray();
            return result.Length == 3 ? Color.FromRgb(result[0], result[1], result[2]) : defaultValue;
        }

        public static Color GetColor(this IniFileSection section, [LocalizationRequired(false)] string key, Color defaultValue, double defaultMultipler, out double multipler) {
            var strings = section.GetStrings(key);
            var result = strings.Select(x => FlexibleParser.ParseInt(x, 0).ClampToByte()).ToArray();
            if (strings.Length != 4) {
                multipler = 1d;
                return defaultValue;
            }
            
            multipler = FlexibleParser.ParseDouble(strings[3], 1d);
            return Color.FromRgb(result[0], result[1], result[2]);
        }

        public static void Set(this IniFileSection section, [LocalizationRequired(false)] string key, Color entry) {
            section.Set(key, $"{entry.R.ToInvariantString()},{entry.G.ToInvariantString()},{entry.B.ToInvariantString()}");
        }

        public static void Set(this IniFileSection section, [LocalizationRequired(false)] string key, Color entry, double multipler) {
            section.Set(key, $"{entry.R.ToInvariantString()},{entry.G.ToInvariantString()},{entry.B.ToInvariantString()},{multipler.ToInvariantString()}");
        }
    }
}