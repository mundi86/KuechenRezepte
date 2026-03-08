using Microsoft.Extensions.Logging;

namespace KuechenRezepte.Services;

public class ApiAuditLogger
{
    private readonly ILogger<ApiAuditLogger> _logger;

    public ApiAuditLogger(ILogger<ApiAuditLogger> logger)
    {
        _logger = logger;
    }

    public void LogMealPlanAccess(string endpoint, string? clientIp, bool success, string reason)
    {
        _logger.LogInformation(
            "audit_type=mealplan endpoint={Endpoint} client_ip={ClientIp} success={Success} reason={Reason}",
            endpoint,
            clientIp ?? "unknown",
            success,
            reason);
    }

    public void LogAlexaAccess(string? intent, string? appId, string? clientIp, bool success, string reason)
    {
        _logger.LogInformation(
            "audit_type=alexa intent={Intent} app_id={AppId} client_ip={ClientIp} success={Success} reason={Reason}",
            intent ?? "unknown",
            appId ?? "unknown",
            clientIp ?? "unknown",
            success,
            reason);
    }
}
