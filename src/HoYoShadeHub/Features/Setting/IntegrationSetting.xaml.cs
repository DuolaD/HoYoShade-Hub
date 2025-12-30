using Microsoft.Extensions.Logging;
using HoYoShadeHub.Frameworks;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class IntegrationSetting : PageBase
{

    private readonly ILogger<IntegrationSetting> _logger = AppConfig.GetLogger<IntegrationSetting>();


    public IntegrationSetting()
    {
        this.InitializeComponent();
    }


}
