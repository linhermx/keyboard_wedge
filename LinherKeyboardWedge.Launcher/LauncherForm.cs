namespace LinherKeyboardWedge.Launcher;

internal sealed class LauncherForm : Form
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;

    public LauncherForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Text = LauncherConstants.DisplayName;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(420, 144);
        BackColor = Color.White;
        Icon = LoadApplicationIcon();

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(28, 33, 40),
            Text = LauncherConstants.DisplayName,
            Location = new Point(20, 18)
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(380, 34),
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(108, 113, 122),
            Text = "Preparando...",
            Location = new Point(20, 54)
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 96),
            Size = new Size(380, 14),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 24
        };

        Controls.Add(titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_progressBar);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RunAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        base.OnFormClosed(e);
    }

    private async Task RunAsync()
    {
        try
        {
            IProgress<string> progress = new Progress<string>(message => _statusLabel.Text = message);
            var service = new LauncherService();
            var app = await service.EnsureLatestInstalledAsync(progress, _cancellationTokenSource.Token);
            progress.Report("Abriendo aplicación...");
            service.Launch(app);
            Close();
        }
        catch (OperationCanceledException)
        {
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Error de inicio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            using var extractedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            return extractedIcon is null ? SystemIcons.Application : (Icon)extractedIcon.Clone();
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}
