using System.Globalization;

namespace IoTSharp.Client.Models;

public sealed record CaptchaSession(string ClientId, string BackgroundBase64, string PieceBase64, double OffsetY);

public sealed record UserContext(
    Guid CustomerId,
    string CustomerName,
    string TenantName,
    string DisplayName,
    string Email,
    string Roles);

public sealed record DeviceSummary(
    Guid Id,
    string Name,
    string DeviceType,
    string IdentityType,
    string IdentityValue,
    bool Active,
    DateTime? LastActivityUtc)
{
    public string ActiveLabel => Active ? "在线" : "离线";

    public string Subtitle => $"{DeviceType} · {IdentityType}";

    public string LastActivityText => LastActivityUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "暂无活动记录";
}

public sealed record DeviceDetail(
    Guid Id,
    string Name,
    string DeviceType,
    string IdentityType,
    string IdentityValue,
    string Owner,
    string CustomerName,
    string TenantName,
    int Timeout,
    bool Active,
    DateTime? LastActivityUtc)
{
    public string ActivityText => Active ? "在线" : "离线";

    public string LastActivityText => LastActivityUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "暂无活动记录";

    public string IdentitySummary => string.IsNullOrWhiteSpace(IdentityValue)
        ? IdentityType
        : $"{IdentityType} / {IdentityValue}";
}

public sealed record DataValueItem(string KeyName, string DataType, string Value, DateTime? TimestampUtc)
{
    public string TimestampText => TimestampUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public bool TryGetNumericValue(out double value)
    {
        return double.TryParse(Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value)
               || double.TryParse(Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value);
    }
}

public sealed record TelemetryQuery(string Keys, DateTime BeginUtc, DateTime EndUtc, TimeSpan Every, string Aggregate);
