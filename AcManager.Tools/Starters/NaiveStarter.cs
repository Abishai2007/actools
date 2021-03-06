using System.Diagnostics;
using AcManager.Tools.Managers;

namespace AcManager.Tools.Starters {
    public class NaiveStarter : StarterBase {
        public override void Run() {
            GameProcess = Process.Start(new ProcessStartInfo {
                FileName = AcsFilename,
                WorkingDirectory = AcRootDirectory.Instance.RequireValue
            });
        }
    }
}