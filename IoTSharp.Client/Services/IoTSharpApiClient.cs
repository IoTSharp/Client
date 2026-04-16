using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IoTSharp.Client.Models;

namespace IoTSharp.Client.Services;

public sealed class IoTSharpApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private string? _accessToken;
    private Guid _customerId;

    public Guid CustomerId => _customerId;

    public void Logout()
    {
        _accessToken = null;
        _customerId = Guid.Empty;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<CaptchaSession> CreateCaptchaAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        ConfigureBaseAddress(serverUrl);
        var clientId = Guid.NewGuid().ToString("N");
        var result = await GetAsync<ApiEnvelope<CaptchaResponse>>($"api/Captcha/Index?clientid={Uri.EscapeDataString(clientId)}", false, cancellationToken);
        var data = EnsureData(result, "获取验证码失败。");
        return new CaptchaSession(clientId, data.BigImage ?? string.Empty, data.SmallImage ?? string.Empty, data.Yheight);
    }

    public async Task<UserContext> LoginAsync(
        string serverUrl,
        string userName,
        string password,
        string captchaClientId,
        double captchaMove,
        CancellationToken cancellationToken = default)
    {
        ConfigureBaseAddress(serverUrl);

        var loginEnvelope = await PostAsync<ApiEnvelope<LoginResponse>>(
            "api/Account/Login",
            new
            {
                userName,
                password,
                captchaClientId,
                captchaMove = (int)Math.Round(captchaMove)
            },
            false,
            cancellationToken);

        var loginData = EnsureData(loginEnvelope, "登录失败。");
        var accessToken = loginData.Token?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("服务端未返回访问令牌。");
        }

        _accessToken = accessToken;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var user = await GetCurrentUserAsync(cancellationToken);
        _customerId = user.CustomerId;
        return user;
    }

    public async Task<UserContext> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ApiEnvelope<MyInfoResponse>>("api/Account/MyInfo", true, cancellationToken);
        var data = EnsureData(result, "获取当前用户失败。");
        var customerId = data.Customer?.Id ?? Guid.Empty;
        if (customerId == Guid.Empty)
        {
            throw new InvalidOperationException("当前账号未分配客户，无法查询设备。");
        }

        return new UserContext(
            customerId,
            data.Customer?.Name ?? "未命名客户",
            data.Tenant?.Name ?? "未命名租户",
            data.Name ?? data.Email ?? "未知用户",
            data.Email ?? string.Empty,
            data.Roles ?? string.Empty);
    }

    public async Task<IReadOnlyList<DeviceSummary>> GetDevicesAsync(
        Guid customerId,
        string? name,
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"customerId={customerId:D}",
            "offset=0",
            "limit=100",
            $"onlyActive={onlyActive.ToString().ToLowerInvariant()}"
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            query.Add($"name={Uri.EscapeDataString(name)}");
        }

        var result = await GetAsync<ApiEnvelope<PagedResponse<DeviceSummaryDto>>>($"api/Devices/Customers?{string.Join("&", query)}", true, cancellationToken);
        var data = EnsureData(result, "获取设备列表失败。");
        return data.Rows?
            .Select(device => new DeviceSummary(
                device.Id,
                device.Name ?? "未命名设备",
                ToDisplayString(device.DeviceType),
                ToDisplayString(device.IdentityType),
                device.IdentityValue ?? string.Empty,
                device.Active,
                device.LastActivityDateTime))
            .ToArray() ?? Array.Empty<DeviceSummary>();
    }

    public async Task<DeviceDetail> GetDeviceDetailAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ApiEnvelope<DeviceDetailDto>>($"api/Devices/{deviceId:D}", true, cancellationToken);
        var data = EnsureData(result, "获取设备详情失败。");
        return new DeviceDetail(
            data.Id,
            data.Name ?? "未命名设备",
            ToDisplayString(data.DeviceType),
            ToDisplayString(data.IdentityType),
            data.IdentityValue ?? string.Empty,
            data.Owner ?? string.Empty,
            data.CustomerName ?? string.Empty,
            data.TenantName ?? string.Empty,
            data.Timeout,
            data.Active,
            data.LastActivityDateTime);
    }

    public async Task<IReadOnlyList<DataValueItem>> GetAttributeLatestAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ApiEnvelope<List<DataPointDto>>>($"api/Devices/{deviceId:D}/AttributeLatest", true, cancellationToken);
        var data = EnsureData(result, "获取设备属性失败。");
        return data.Select(MapDataPoint).ToArray();
    }

    public async Task<IReadOnlyList<DataValueItem>> GetTelemetryLatestAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<ApiEnvelope<List<DataPointDto>>>($"api/Devices/{deviceId:D}/TelemetryLatest", true, cancellationToken);
        var data = EnsureData(result, "获取最新遥测失败。");
        return data.Select(MapDataPoint).ToArray();
    }

    public async Task<IReadOnlyList<DataValueItem>> QueryTelemetryAsync(
        Guid deviceId,
        TelemetryQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<ApiEnvelope<List<DataPointDto>>>(
            $"api/Devices/{deviceId:D}/TelemetryData",
            new
            {
                keys = query.Keys,
                begin = query.BeginUtc,
                end = query.EndUtc,
                every = query.Every,
                aggregate = query.Aggregate
            },
            true,
            cancellationToken);

        var data = EnsureData(result, "查询遥测数据失败。");
        return data.Select(MapDataPoint).ToArray();
    }

    private void ConfigureBaseAddress(string serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException("请输入 IoTSharp 服务地址。");
        }

        var normalized = serverUrl.Trim();
        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"http://{normalized}";
        }

        normalized = normalized.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(normalized, UriKind.Absolute);
    }

    private async Task<T> GetAsync<T>(string url, bool authorized, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        PrepareAuthorization(request, authorized);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T> PostAsync<T>(string url, object body, bool authorized, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        PrepareAuthorization(request, authorized);
        return await SendAsync<T>(request, cancellationToken);
    }

    private void PrepareAuthorization(HttpRequestMessage request, bool authorized)
    {
        if (authorized)
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                throw new InvalidOperationException("当前尚未登录。");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"请求失败: {(int)response.StatusCode} {response.ReasonPhrase} {content}".Trim());
        }

        var data = JsonSerializer.Deserialize<T>(content, JsonOptions);
        return data ?? throw new InvalidOperationException("服务端返回了无法解析的数据。");
    }

    private static T EnsureData<T>(ApiEnvelope<T> envelope, string defaultMessage)
    {
        if (envelope.Code is not 0 && envelope.Code is not 200)
        {
            throw new InvalidOperationException(envelope.Message ?? defaultMessage);
        }

        return envelope.Data ?? throw new InvalidOperationException(envelope.Message ?? defaultMessage);
    }

    private static DataValueItem MapDataPoint(DataPointDto item)
        => new(
            item.KeyName ?? "未命名键",
            ToDisplayString(item.DataType),
            ToDisplayString(item.Value),
            item.DateTime);

    private static string ToDisplayString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed class ApiEnvelope<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("msg")]
        public string? LegacyMessage
        {
            set
            {
                if (string.IsNullOrWhiteSpace(Message))
                {
                    Message = value;
                }
            }
        }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class CaptchaResponse
    {
        [JsonPropertyName("bigImage")]
        public string? BigImage { get; set; }

        [JsonPropertyName("smallImage")]
        public string? SmallImage { get; set; }

        [JsonPropertyName("yheight")]
        public double Yheight { get; set; }
    }

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public TokenResponse? Token { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class MyInfoResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("roles")]
        public string? Roles { get; set; }

        [JsonPropertyName("customer")]
        public NamedEntityDto? Customer { get; set; }

        [JsonPropertyName("tenant")]
        public NamedEntityDto? Tenant { get; set; }
    }

    private sealed class NamedEntityDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class PagedResponse<T>
    {
        [JsonPropertyName("rows")]
        public List<T>? Rows { get; set; }
    }

    private sealed class DeviceSummaryDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("identityValue")]
        public string? IdentityValue { get; set; }

        [JsonPropertyName("deviceType")]
        public JsonElement DeviceType { get; set; }

        [JsonPropertyName("identityType")]
        public JsonElement IdentityType { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("lastActivityDateTime")]
        public DateTime? LastActivityDateTime { get; set; }
    }

    private sealed class DeviceDetailDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("deviceType")]
        public JsonElement DeviceType { get; set; }

        [JsonPropertyName("identityType")]
        public JsonElement IdentityType { get; set; }

        [JsonPropertyName("identityValue")]
        public string? IdentityValue { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("customerName")]
        public string? CustomerName { get; set; }

        [JsonPropertyName("tenantName")]
        public string? TenantName { get; set; }

        [JsonPropertyName("timeout")]
        public int Timeout { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("lastActivityDateTime")]
        public DateTime? LastActivityDateTime { get; set; }
    }

    private sealed class DataPointDto
    {
        [JsonPropertyName("keyName")]
        public string? KeyName { get; set; }

        [JsonPropertyName("dataType")]
        public JsonElement DataType { get; set; }

        [JsonPropertyName("dateTime")]
        public DateTime? DateTime { get; set; }

        [JsonPropertyName("value")]
        public JsonElement Value { get; set; }
    }
}
