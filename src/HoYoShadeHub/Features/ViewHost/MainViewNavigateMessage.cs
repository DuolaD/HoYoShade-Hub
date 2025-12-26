using System;

namespace HoYoShadeHub.Features.ViewHost;

internal class MainViewNavigateMessage
{

    public Type Page { get; set; }

    public MainViewNavigateMessage(Type page)
    {
        Page = page;
    }

}
