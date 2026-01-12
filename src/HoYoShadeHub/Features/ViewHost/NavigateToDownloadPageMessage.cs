using System;

namespace HoYoShadeHub.Features.ViewHost;

public class NavigateToDownloadPageMessage
{
    public bool IsUpdateMode { get; set; }

    public NavigateToDownloadPageMessage(bool isUpdateMode = false)
    {
        IsUpdateMode = isUpdateMode;
    }
}
