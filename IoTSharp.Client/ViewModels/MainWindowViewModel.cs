using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IoTSharp.Client.Models;
using IoTSharp.Client.Services;

namespace IoTSharp.Client.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IoTSharpApiClient _apiClient;

    public ObservableCollection<DeviceSummary> Devices { get; } = [];

    public ObservableCollection<DataValueItem> Attributes { get; } = [];

    public ObservableCollection<DataValueItem> LatestTelemetry { get; } = [];

    public ObservableCollection<DataValueItem> QueryTelemetryResults { get; } = [];

    public IReadOnlyList<string> AggregateOptions { get; } = ["None", "Mean", "Max", "Min", "Sum", "Count", "First", "Last"];

    public AsyncRelayCommand RefreshCaptchaCommand { get; }

    public AsyncRelayCommand LoginCommand { get; }

    public AsyncRelayCommand LoadDevicesCommand { get; }

    public AsyncRelayCommand QueryTelemetryCommand { get; }

    public RelayCommand LogoutCommand { get; }

    public RelayCommand<string> ApplyTimeRangeCommand { get; }

    [ObservableProperty]
    private string serverUrl = "http://localhost:5000";

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isAuthenticated;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "请输入 IoTSharp 服务地址并获取验证码。";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string currentUserDisplayName = string.Empty;

    [ObservableProperty]
    private string currentCustomerDisplayName = string.Empty;

    [ObservableProperty]
    private Bitmap? captchaBackgroundImage;

    [ObservableProperty]
    private Bitmap? captchaPieceImage;

    [ObservableProperty]
    private string captchaClientId = string.Empty;

    [ObservableProperty]
    private double captchaMove;

    [ObservableProperty]
    private double captchaYOffset;

    [ObservableProperty]
    private double captchaCanvasWidth = 320;

    [ObservableProperty]
    private double captchaCanvasHeight = 180;

    [ObservableProperty]
    private double captchaMaxOffset = 240;

    [ObservableProperty]
    private string deviceSearchText = string.Empty;

    [ObservableProperty]
    private bool onlyActiveDevices = true;

    [ObservableProperty]
    private DeviceSummary? selectedDevice;

    [ObservableProperty]
    private DeviceDetail? selectedDeviceDetail;

    [ObservableProperty]
    private string telemetryKeys = string.Empty;

    [ObservableProperty]
    private string telemetryBeginText = DateTime.UtcNow.AddHours(-24).ToString("yyyy-MM-ddTHH:mm:ss");

    [ObservableProperty]
    private string telemetryEndText = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

    [ObservableProperty]
    private string telemetryEveryText = "00:05:00";

    [ObservableProperty]
    private string selectedAggregate = "Mean";

    [ObservableProperty]
    private string telemetrySummary = "选择设备后可查询原始遥测或聚合数据。";

    [ObservableProperty]
    private string deviceInventorySummary = "登录后加载设备库存。";

    [ObservableProperty]
    private string latestTelemetryInsights = "选择设备后显示最新遥测快照。";

    [ObservableProperty]
    private string queryTelemetryInsights = "查询后将在这里显示聚合趋势摘要。";

    [ObservableProperty]
    private string latestTelemetryKeySummary = "暂无数值型遥测键。";

    [ObservableProperty]
    private string queryTelemetryKeySummary = "暂无聚合曲线。";

    [ObservableProperty]
    private Points latestTelemetryChartPoints = new();

    [ObservableProperty]
    private Points queryTelemetryChartPoints = new();

    public double TelemetryChartWidth => 360;

    public double TelemetryChartHeight => 130;

    public MainWindowViewModel() : this(new IoTSharpApiClient(), false)
    {
    }

    public MainWindowViewModel(IoTSharpApiClient apiClient)
        : this(apiClient, true)
    {
    }

    private MainWindowViewModel(IoTSharpApiClient apiClient, bool loadCaptcha)
    {
        _apiClient = apiClient;
        RefreshCaptchaCommand = new AsyncRelayCommand(RefreshCaptchaAsync, CanPrepareLogin);
        LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        LoadDevicesCommand = new AsyncRelayCommand(LoadDevicesAsync, CanLoadDevices);
        QueryTelemetryCommand = new AsyncRelayCommand(QueryTelemetryAsync, CanQueryTelemetry);
        LogoutCommand = new RelayCommand(Logout);
        ApplyTimeRangeCommand = new RelayCommand<string>(ApplyTimeRange);
        if (loadCaptcha)
        {
            _ = RefreshCaptchaAsync();
        }
    }

    public bool ShowLoginPanel => !IsAuthenticated;

    public bool ShowWorkspace => IsAuthenticated;

    public bool HasSelectedDevice => SelectedDeviceDetail is not null;

    public string CurrentSessionText => string.IsNullOrWhiteSpace(CurrentUserDisplayName)
        ? string.Empty
        : $"{CurrentUserDisplayName} · {CurrentCustomerDisplayName}";

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCaptchaCommand.NotifyCanExecuteChanged();
        LoginCommand.NotifyCanExecuteChanged();
        LoadDevicesCommand.NotifyCanExecuteChanged();
        QueryTelemetryCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLoginPanel));
        OnPropertyChanged(nameof(ShowWorkspace));
        OnPropertyChanged(nameof(CurrentSessionText));
        LoadDevicesCommand.NotifyCanExecuteChanged();
        QueryTelemetryCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentUserDisplayNameChanged(string value) => OnPropertyChanged(nameof(CurrentSessionText));

    partial void OnCurrentCustomerDisplayNameChanged(string value) => OnPropertyChanged(nameof(CurrentSessionText));

    partial void OnUserNameChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnServerUrlChanged(string value)
    {
        RefreshCaptchaCommand.NotifyCanExecuteChanged();
        LoginCommand.NotifyCanExecuteChanged();
    }

    partial void OnCaptchaClientIdChanged(string value) => LoginCommand.NotifyCanExecuteChanged();

    partial void OnSelectedDeviceChanged(DeviceSummary? value)
    {
        QueryTelemetryCommand.NotifyCanExecuteChanged();
        if (value is null)
        {
            SelectedDeviceDetail = null;
            Attributes.Clear();
            LatestTelemetry.Clear();
            QueryTelemetryResults.Clear();
            ClearTelemetryVisuals();
            TelemetrySummary = "请选择设备。";
            return;
        }

        _ = LoadSelectedDeviceAsync(value);
    }

    partial void OnSelectedDeviceDetailChanged(DeviceDetail? value) => OnPropertyChanged(nameof(HasSelectedDevice));

    private bool CanPrepareLogin() => !IsBusy && !string.IsNullOrWhiteSpace(ServerUrl);

    private bool CanLogin() => !IsBusy
                               && !string.IsNullOrWhiteSpace(ServerUrl)
                               && !string.IsNullOrWhiteSpace(UserName)
                               && !string.IsNullOrWhiteSpace(Password)
                               && !string.IsNullOrWhiteSpace(CaptchaClientId);

    private bool CanLoadDevices() => IsAuthenticated && !IsBusy;

    private bool CanQueryTelemetry() => IsAuthenticated && SelectedDevice is not null && !IsBusy;

    private async Task RefreshCaptchaAsync()
    {
        await ExecuteBusyAsync("正在获取验证码...", async () =>
        {
            ClearError();
            var captcha = await _apiClient.CreateCaptchaAsync(ServerUrl);
            CaptchaClientId = captcha.ClientId;
            CaptchaYOffset = captcha.OffsetY;
            CaptchaMove = 0;

            SetCaptchaImages(Base64BitmapFactory.Create(captcha.BackgroundBase64), Base64BitmapFactory.Create(captcha.PieceBase64));

            if (CaptchaBackgroundImage is not null)
            {
                CaptchaCanvasWidth = CaptchaBackgroundImage.PixelSize.Width;
                CaptchaCanvasHeight = CaptchaBackgroundImage.PixelSize.Height;
            }

            CaptchaMaxOffset = CaptchaBackgroundImage is not null && CaptchaPieceImage is not null
                ? Math.Max(0, CaptchaBackgroundImage.PixelSize.Width - CaptchaPieceImage.PixelSize.Width)
                : 240;

            StatusMessage = "验证码已更新，请拖动拼图后登录。";
        });
    }

    private async Task LoginAsync()
    {
        await ExecuteBusyAsync("正在登录 IoTSharp...", async () =>
        {
            ClearError();
            var user = await _apiClient.LoginAsync(ServerUrl, UserName, Password, CaptchaClientId, CaptchaMove);
            CurrentUserDisplayName = user.DisplayName;
            CurrentCustomerDisplayName = $"{user.CustomerName} / {user.TenantName}";
            IsAuthenticated = true;
            StatusMessage = "登录成功，正在加载设备列表。";
            await LoadDevicesCoreAsync();
        }, refreshCaptchaOnFailure: true);
    }

    private async Task LoadDevicesAsync()
    {
        await ExecuteBusyAsync("正在加载设备列表...", LoadDevicesCoreAsync);
    }

    private async Task LoadDevicesCoreAsync()
    {
        var devices = await _apiClient.GetDevicesAsync(_apiClient.CustomerId, DeviceSearchText, OnlyActiveDevices);
        ReplaceCollection(Devices, devices);
        DeviceInventorySummary = BuildDeviceInventorySummary();

        StatusMessage = Devices.Count == 0 ? "未查询到匹配设备。" : $"已加载 {Devices.Count} 台设备。";

        if (Devices.Count == 0)
        {
            SelectedDevice = null;
            return;
        }

        if (SelectedDevice is null || Devices.All(device => device.Id != SelectedDevice.Id))
        {
            SelectedDevice = Devices[0];
        }
        else
        {
            SelectedDevice = Devices.First(device => device.Id == SelectedDevice.Id);
        }
    }

    private async Task LoadSelectedDeviceAsync(DeviceSummary device)
    {
        await ExecuteBusyAsync($"正在加载 {device.Name} ...", async () =>
        {
            var detailTask = _apiClient.GetDeviceDetailAsync(device.Id);
            var attributeTask = _apiClient.GetAttributeLatestAsync(device.Id);
            var telemetryTask = _apiClient.GetTelemetryLatestAsync(device.Id);
            await Task.WhenAll(detailTask, attributeTask, telemetryTask);

            if (SelectedDevice?.Id != device.Id)
            {
                return;
            }

            SelectedDeviceDetail = await detailTask;
            ReplaceCollection(Attributes, await attributeTask);
            ReplaceCollection(LatestTelemetry, await telemetryTask);
            UpdateLatestTelemetryVisuals();
            ReplaceCollection(QueryTelemetryResults, Array.Empty<DataValueItem>());
            QueryTelemetryInsights = "重新选择设备后，请再次执行聚合查询。";
            QueryTelemetryKeySummary = "暂无聚合曲线。";
            QueryTelemetryChartPoints = new Points();

            if (string.IsNullOrWhiteSpace(TelemetryKeys))
            {
                TelemetryKeys = string.Join(',', LatestTelemetry.Select(item => item.KeyName).Take(3));
            }

            TelemetrySummary = $"已加载 {LatestTelemetry.Count} 条最新遥测，支持继续查询聚合数据。";
        });
    }

    private async Task QueryTelemetryAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        await ExecuteBusyAsync("正在查询遥测数据...", async () =>
        {
            ClearError();
            var beginUtc = ParseDateTime(TelemetryBeginText, "开始时间");
            var endUtc = ParseDateTime(TelemetryEndText, "结束时间");
            if (endUtc <= beginUtc)
            {
                throw new InvalidOperationException("结束时间必须晚于开始时间。");
            }

            var every = string.IsNullOrWhiteSpace(TelemetryEveryText)
                ? TimeSpan.Zero
                : TimeSpan.Parse(TelemetryEveryText);

            var results = await _apiClient.QueryTelemetryAsync(
                SelectedDevice.Id,
                new TelemetryQuery(TelemetryKeys, beginUtc, endUtc, every, SelectedAggregate));

            ReplaceCollection(QueryTelemetryResults, results);
            UpdateQueryTelemetryVisuals();
            TelemetrySummary = $"查询完成：返回 {QueryTelemetryResults.Count} 条 {SelectedAggregate} 数据。";
        });
    }

    private void Logout()
    {
        _apiClient.Logout();
        IsAuthenticated = false;
        CurrentUserDisplayName = string.Empty;
        CurrentCustomerDisplayName = string.Empty;
        Password = string.Empty;
        ReplaceCollection(Devices, Array.Empty<DeviceSummary>());
        ReplaceCollection(Attributes, Array.Empty<DataValueItem>());
        ReplaceCollection(LatestTelemetry, Array.Empty<DataValueItem>());
        ReplaceCollection(QueryTelemetryResults, Array.Empty<DataValueItem>());
        SelectedDevice = null;
        SelectedDeviceDetail = null;
        DeviceInventorySummary = "登录后加载设备库存。";
        ClearTelemetryVisuals();
        TelemetrySummary = "已退出登录。";
        StatusMessage = "已退出登录，请重新获取验证码。";
        _ = RefreshCaptchaAsync();
    }

    private async Task ExecuteBusyAsync(string busyMessage, Func<Task> action, bool refreshCaptchaOnFailure = false)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = busyMessage;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "操作失败，请检查服务地址、验证码或接口返回。";
            if (refreshCaptchaOnFailure && ShowLoginPanel)
            {
                await RefreshCaptchaAfterFailureAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCaptchaAfterFailureAsync()
    {
        try
        {
            var captcha = await _apiClient.CreateCaptchaAsync(ServerUrl);
            CaptchaClientId = captcha.ClientId;
            CaptchaYOffset = captcha.OffsetY;
            CaptchaMove = 0;
            SetCaptchaImages(Base64BitmapFactory.Create(captcha.BackgroundBase64), Base64BitmapFactory.Create(captcha.PieceBase64));
        }
        catch
        {
            // Ignore secondary captcha failures so the original error remains visible.
        }
    }

    private void ClearError() => ErrorMessage = string.Empty;

    private static DateTime ParseDateTime(string text, string fieldName)
    {
        if (!DateTime.TryParse(text, out var value))
        {
            throw new InvalidOperationException($"{fieldName}格式无效，请使用 ISO 时间，例如 2026-04-15T08:00:00。");
        }

        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private void ApplyTimeRange(string? preset)
    {
        var end = DateTime.UtcNow;
        var begin = preset switch
        {
            "1h" => end.AddHours(-1),
            "24h" => end.AddHours(-24),
            "7d" => end.AddDays(-7),
            _ => end.AddHours(-24)
        };

        TelemetryBeginText = begin.ToString("yyyy-MM-ddTHH:mm:ss");
        TelemetryEndText = end.ToString("yyyy-MM-ddTHH:mm:ss");
        TelemetrySummary = preset switch
        {
            "1h" => "已切换到最近 1 小时查询窗口。",
            "7d" => "已切换到最近 7 天查询窗口。",
            _ => "已切换到最近 24 小时查询窗口。"
        };
    }

    private string BuildDeviceInventorySummary()
    {
        if (Devices.Count == 0)
        {
            return "当前客户暂无可展示设备。";
        }

        var onlineCount = Devices.Count(device => device.Active);
        return $"共 {Devices.Count} 台设备，其中 {onlineCount} 台在线，{Devices.Count - onlineCount} 台离线。";
    }

    private void UpdateLatestTelemetryVisuals()
    {
        var visualization = BuildVisualization(
            LatestTelemetry,
            item => item.KeyName,
            "暂无数值型遥测快照。",
            "最近快照");

        LatestTelemetryChartPoints = visualization.Points;
        LatestTelemetryInsights = visualization.Insights;
        LatestTelemetryKeySummary = visualization.Keys;
    }

    private void UpdateQueryTelemetryVisuals()
    {
        var visualization = BuildVisualization(
            QueryTelemetryResults,
            item => item.TimestampUtc?.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) ?? item.KeyName,
            "当前查询未返回可绘制的数值结果。",
            SelectedAggregate);

        QueryTelemetryChartPoints = visualization.Points;
        QueryTelemetryInsights = visualization.Insights;
        QueryTelemetryKeySummary = visualization.Keys;
    }

    private void ClearTelemetryVisuals()
    {
        LatestTelemetryChartPoints = new Points();
        QueryTelemetryChartPoints = new Points();
        LatestTelemetryInsights = "选择设备后显示最新遥测快照。";
        QueryTelemetryInsights = "查询后将在这里显示聚合趋势摘要。";
        LatestTelemetryKeySummary = "暂无数值型遥测键。";
        QueryTelemetryKeySummary = "暂无聚合曲线。";
    }

    private TelemetryVisualization BuildVisualization(
        IEnumerable<DataValueItem> source,
        Func<DataValueItem, string> labelSelector,
        string emptyMessage,
        string seriesName)
    {
        var numericItems = source
            .Select(item => new { Item = item, HasValue = item.TryGetNumericValue(out var value), Value = value })
            .Where(entry => entry.HasValue)
            .ToList();

        if (numericItems.Count == 0)
        {
            return new TelemetryVisualization(new Points(), emptyMessage, "暂无可用键。");
        }

        const double padding = 10;
        var width = TelemetryChartWidth;
        var height = TelemetryChartHeight;
        var min = numericItems.Min(entry => entry.Value);
        var max = numericItems.Max(entry => entry.Value);
        var range = Math.Abs(max - min) < 0.0001 ? 1 : max - min;
        var xStep = numericItems.Count == 1 ? 0 : (width - (padding * 2)) / (numericItems.Count - 1);
        var points = new Points();

        for (var index = 0; index < numericItems.Count; index++)
        {
            var x = padding + (xStep * index);
            var normalized = (numericItems[index].Value - min) / range;
            var y = height - padding - (normalized * (height - (padding * 2)));
            points.Add(new Point(x, y));
        }

        var latest = numericItems[^1].Value;
        var average = numericItems.Average(entry => entry.Value);
        var keySummary = string.Join(" · ", numericItems.Select(entry => labelSelector(entry.Item)).Take(4));
        if (numericItems.Count > 4)
        {
            keySummary += $" · +{numericItems.Count - 4}";
        }

        var insights = $"{seriesName}：最新 {latest:0.##}，最小 {min:0.##}，最大 {max:0.##}，平均 {average:0.##}。";
        return new TelemetryVisualization(points, insights, keySummary);
    }

    private void SetCaptchaImages(Bitmap? background, Bitmap? piece)
    {
        var oldBackground = CaptchaBackgroundImage;
        var oldPiece = CaptchaPieceImage;
        CaptchaBackgroundImage = background;
        CaptchaPieceImage = piece;
        oldBackground?.Dispose();
        oldPiece?.Dispose();
    }

    private sealed record TelemetryVisualization(Points Points, string Insights, string Keys);
}
