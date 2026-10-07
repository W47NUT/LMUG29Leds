using LMUG29Leds.Core;
using System.Windows;
using System.Windows.Media;

namespace LMUG29Leds;

public partial class MainWindow : Window
{
    private static readonly Color Green = Color.FromRgb(63, 185, 80);
    private static readonly Color Amber = Color.FromRgb(210, 153, 34);
    private static readonly Color Red = Color.FromRgb(248, 81, 73);

    private readonly G29LedController _wheel = new();
    private readonly LmuTelemetryReader _lmu = new();
    private readonly CancellationTokenSource _shutdown = new();

    private byte _lastMask = 0xFF;
    private int _testing;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;

        TestRpmButton.Click += async (_, _) =>
            await RunTestAsync("RPM / Shift", TestRpmAsync);

        TestPitButton.Click += async (_, _) =>
            await RunTestAsync("Pit Limiter", TestPitLimiterAsync);

        TestShiftButton.Click += async (_, _) =>
            await RunTestAsync("Shift Warning", TestShiftWarningAsync);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = Task.Run(() => RunWorkerAsync(_shutdown.Token));
    }

    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        long nextWheelConnect = 0;
        long nextLmuConnect = 0;
        long nextUiUpdate = 0;

        TelemetrySnapshot snapshot = default;

        while (!cancellationToken.IsCancellationRequested)
        {
            long now = Environment.TickCount64;

            if (!_wheel.IsConnected && now >= nextWheelConnect)
            {
                _wheel.TryConnect();
                nextWheelConnect = now + 1_000;

                if (_wheel.IsConnected)
                {
                    await RunStartupSweepAsync(cancellationToken);
                    _lastMask = 0xFF;
                }
            }

            if (!_lmu.IsConnected && now >= nextLmuConnect)
            {
                _lmu.TryConnect();
                nextLmuConnect = now + 1_000;
            }

            bool haveSnapshot = _lmu.IsConnected && _lmu.TryRead(out snapshot);
            bool testing = Volatile.Read(ref _testing) != 0;

            string mode = "Waiting";
            string behavior = "Idle";
            byte desiredMask = 0x00;

            if (haveSnapshot)
            {
                if (!snapshot.HasVehicle)
                {
                    mode = "Garage";
                }
                else if (snapshot.SpeedLimiter)
                {
                    mode = "Pit Limiter";
                    behavior = "Pit Limiter";
                    desiredMask = LedPatterns.GetPitLimiterMask(now);
                }
                else
                {
                    mode = "Racing";
                    behavior = "RPM / Shift";

                    if (snapshot.MaxRpm > 0)
                    {
                        desiredMask = LedPatterns.GetRpmMask(
                            snapshot.Rpm / snapshot.MaxRpm,
                            now);
                    }
                }
            }

            if (!testing && _wheel.IsConnected && desiredMask != _lastMask)
            {
                _lastMask = _wheel.TrySetMask(desiredMask)
                    ? desiredMask
                    : (byte)0xFF;
            }

            if (now >= nextUiUpdate)
            {
                nextUiUpdate = now + 100;

                TelemetrySnapshot displaySnapshot = snapshot;
                bool displayHaveSnapshot = haveSnapshot;
                string displayMode = mode;
                string displayBehavior = behavior;
                bool displayTesting = testing;

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateConnectionUi();

                    RpmText.Text = displayHaveSnapshot && displaySnapshot.HasVehicle
                        ? $"{displaySnapshot.Rpm:F0} / {displaySnapshot.MaxRpm:F0}"
                        : "0 / 0";

                    ThrottleText.Text = displayHaveSnapshot && displaySnapshot.HasVehicle
                        ? $"{displaySnapshot.Throttle:P0}"
                        : "0%";

                    if (!displayTesting)
                    {
                        ModeText.Text = displayMode;
                        LedModeText.Text = displayBehavior;
                    }
                });
            }

            try
            {
                await Task.Delay(20, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunTestAsync(
        string testName,
        Func<CancellationToken, Task> test)
    {
        if (Interlocked.CompareExchange(ref _testing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (!_wheel.IsConnected && !_wheel.TryConnect())
            {
                MessageBox.Show(
                    this,
                    "The Logitech G29 LED interface could not be opened. " +
                    "Make sure the wheel is connected and try again.",
                    "LMUG29Leds",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SetButtonsEnabled(false);
            ModeText.Text = "Testing";
            LedModeText.Text = testName;

            await test(_shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // Application is closing.
        }
        finally
        {
            _wheel.TrySetMask(0x00);
            _lastMask = 0xFF;
            Interlocked.Exchange(ref _testing, 0);
            UpdateConnectionUi();
        }
    }

    private async Task TestRpmAsync(CancellationToken cancellationToken)
    {
        byte[] up = [0x00, 0x01, 0x03, 0x07, 0x0F, 0x1F];

        foreach (byte mask in up)
        {
            if (!_wheel.TrySetMask(mask)) return;
            await Task.Delay(220, cancellationToken);
        }

        await Task.Delay(300, cancellationToken);

        for (int index = up.Length - 2; index >= 0; index--)
        {
            if (!_wheel.TrySetMask(up[index])) return;
            await Task.Delay(110, cancellationToken);
        }
    }

    private async Task TestPitLimiterAsync(CancellationToken cancellationToken)
    {
        byte[] sequence = [0x01, 0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02];

        for (int loop = 0; loop < 3; loop++)
        {
            foreach (byte mask in sequence)
            {
                if (!_wheel.TrySetMask(mask)) return;
                await Task.Delay(90, cancellationToken);
            }
        }
    }

    private async Task TestShiftWarningAsync(CancellationToken cancellationToken)
    {
        for (int flash = 0; flash < 8; flash++)
        {
            byte mask = flash % 2 == 0 ? (byte)0x1F : (byte)0x00;

            if (!_wheel.TrySetMask(mask)) return;
            await Task.Delay(120, cancellationToken);
        }
    }

    private async Task RunStartupSweepAsync(CancellationToken cancellationToken)
    {
        byte[] sequence = [0x01, 0x03, 0x07, 0x0F, 0x1F, 0x0F, 0x07, 0x03, 0x01, 0x00];

        foreach (byte mask in sequence)
        {
            if (!_wheel.TrySetMask(mask)) return;

            try
            {
                await Task.Delay(70, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void UpdateConnectionUi()
    {
        bool wheelConnected = _wheel.IsConnected;
        bool lmuConnected = _lmu.IsConnected;
        bool testing = Volatile.Read(ref _testing) != 0;

        WheelStatusText.Text = wheelConnected ? "G29 Connected" : "G29 Not Found";
        WheelStatusDot.Fill = Brush(wheelConnected ? Green : Red);

        LmuStatusText.Text = lmuConnected ? "Connected" : "Waiting for LMU";
        LmuStatusDot.Fill = Brush(lmuConnected ? Green : Amber);

        if (wheelConnected && lmuConnected)
        {
            AppStatusText.Text = "Ready";
            AppStatusText.Foreground = Brush(Color.FromRgb(126, 231, 135));
            AppStatusDot.Fill = Brush(Green);
            AppStatusBorder.Background = Brush(Color.FromRgb(23, 53, 31));
            AppStatusBorder.BorderBrush = Brush(Color.FromRgb(40, 108, 54));
        }
        else
        {
            AppStatusText.Text = "Waiting";
            AppStatusText.Foreground = Brush(Color.FromRgb(227, 179, 65));
            AppStatusDot.Fill = Brush(Amber);
            AppStatusBorder.Background = Brush(Color.FromRgb(56, 47, 22));
            AppStatusBorder.BorderBrush = Brush(Color.FromRgb(110, 91, 30));
        }

        SetButtonsEnabled(wheelConnected && !testing);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        TestRpmButton.IsEnabled = enabled;
        TestPitButton.IsEnabled = enabled;
        TestShiftButton.IsEnabled = enabled;
    }

    private static SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _shutdown.Cancel();
        _wheel.Dispose();
        _lmu.Dispose();
        _shutdown.Dispose();
    }
}
