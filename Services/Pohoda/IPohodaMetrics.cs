using System;

namespace SysJaky_N.Services.Pohoda;

public interface IPohodaMetrics
{
    void ObserveExportSuccess(TimeSpan duration);

    void ObserveExportFailure(TimeSpan duration);
}
