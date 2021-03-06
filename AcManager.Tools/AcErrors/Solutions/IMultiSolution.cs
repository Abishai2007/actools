using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors.Solutions {
    public interface IMultiSolution : ISolution {
        Task Run(IEnumerable<IAcError> errors, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);
    }
}