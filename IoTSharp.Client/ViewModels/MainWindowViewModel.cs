using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
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

    private void SetCaptchaImages(Bitmap? background, Bitmap? piece)
    {
        var oldBackground = CaptchaBackgroundImage;
        var oldPiece = CaptchaPieceImage;
        CaptchaBackgroundImage = background;
        CaptchaPieceImage = piece;
        oldBackground?.Dispose();
        oldPiece?.Dispose();
    }
}
