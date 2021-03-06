using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Helpers {
    public static class LimitedSpace {
        public const string SelectedSkin = ".SelectedSkin";
        public const string SelectedLayout = ".SelectedLayout";
        public const string SelectedEntry = ".SelectedEntry";
        public const string OnlineQuickFilter = ".QuickFilter";
        public const string OnlineSelected = ".OnlineSelected";
        public const string OnlineSelectedCar = ".OnlineSelectedCar";
        public const string OnlineSorting = ".OnlineSorting";
        public const string LapTimesSortingColumn = ".LapTimesSortingColumn";
        public const string LapTimesSortingDescending = ".LapTimesSortingDescending";

        public static void Initialize() {
            LimitedStorage.RegisterSpace(SelectedSkin, 25);
            LimitedStorage.RegisterSpace(SelectedLayout, 25);
            LimitedStorage.RegisterSpace(SelectedEntry, 25);
            LimitedStorage.RegisterSpace(OnlineQuickFilter, 25);
            LimitedStorage.RegisterSpace(OnlineSelected, 25);
            LimitedStorage.RegisterSpace(OnlineSelectedCar, 25);
            LimitedStorage.RegisterSpace(OnlineSorting, 25);
            LimitedStorage.RegisterSpace(LapTimesSortingColumn, 25);
            LimitedStorage.RegisterSpace(LapTimesSortingDescending, 25);
        }
    }
}