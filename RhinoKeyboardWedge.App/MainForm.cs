using System.Diagnostics;
using System.Text.RegularExpressions;
using RhinoKeyboardWedge.App.Configuration;
using RhinoKeyboardWedge.App.Logging;
using RhinoKeyboardWedge.App.Services;

namespace RhinoKeyboardWedge.App;

internal sealed class MainForm : Form
{
    private const string SampleFrame = "WT:   1.420kg\r\nAWP:207.587g\r\nQTY:        7 pcs";

    private static readonly Color AppBackground = Color.FromArgb(246, 244, 241);
    private static readonly Color SurfaceBackground = Color.White;
    private static readonly Color SurfaceBorder = Color.FromArgb(223, 218, 212);
    private static readonly Color HeaderText = Color.FromArgb(28, 33, 40);
    private static readonly Color MutedText = Color.FromArgb(108, 113, 122);
    private static readonly Color Accent = Color.FromArgb(188, 67, 57);
    private static readonly Color AccentSoft = Color.FromArgb(249, 236, 233);
    private static readonly Color Good = Color.FromArgb(46, 125, 91);
    private static readonly Color GoodSoft = Color.FromArgb(229, 243, 236);
    private static readonly Color Error = Color.FromArgb(168, 58, 51);
    private static readonly Color ErrorSoft = Color.FromArgb(251, 235, 232);

    private readonly ConfigService _configService;
    private readonly DailyFileLogger _logger;
    private readonly ScaleSerialService _serialService;
    private readonly StartupManager _startupManager = new();
    private readonly WedgeProcessor _processor;
    private readonly AppSettings _settings;
    private readonly bool _startHiddenToTray;

    private ComboBox _portCombo = null!;
    private ComboBox _baudCombo = null!;
    private ComboBox _dataBitsCombo = null!;
    private ComboBox _parityCombo = null!;
    private ComboBox _stopBitsCombo = null!;
    private ComboBox _flowControlCombo = null!;
    private ComboBox _postActionCombo = null!;
    private TextBox _regexTextBox = null!;
    private NumericUpDown _dedupeNumeric = null!;
    private CheckBox _dtrEnableCheck = null!;
    private CheckBox _rtsEnableCheck = null!;
    private CheckBox _startMinimizedCheck = null!;
    private CheckBox _minimizeToTrayCheck = null!;
    private CheckBox _startWithWindowsCheck = null!;
    private Label _connectionLabel = null!;
    private Label _heroStatusLabel = null!;
    private TextBox _rawTextBox = null!;
    private Label _qtyValueLabel = null!;
    private Label _timestampValueLabel = null!;
    private Label _resultValueLabel = null!;
    private Label _portValueLabel = null!;
    private ListBox _activityList = null!;
    private Button _connectButton = null!;
    private Button _disconnectButton = null!;
    private ToolStripMenuItem _connectMenuItem = null!;
    private ToolStripMenuItem _disconnectMenuItem = null!;
    private NotifyIcon _notifyIcon = null!;

    private bool _allowExit;
    private bool _hasShownTrayBalloon;

    public MainForm(AppSettings settings, ConfigService configService, DailyFileLogger logger)
    {
        _settings = settings;
        _configService = configService;
        _logger = logger;
        _serialService = new ScaleSerialService(_logger);
        _processor = new WedgeProcessor(_settings, new QuantityParser(), new KeyboardSender(), _logger);
        _startHiddenToTray = settings.StartMinimized && settings.MinimizeToTray;

        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "LINHER Keyboard Wedge";
        Icon = LoadApplicationIcon();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1080, 760);
        Size = new Size(1240, 860);
        BackColor = AppBackground;

        if (_startHiddenToTray)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;
        }

        BuildUi();
        BuildTrayIcon();
        WireEvents();
        LoadSettingsIntoControls();
        RefreshPorts(_settings.PortName);
        SetConnectedState(connected: false);

        _logger.LogInfo("APP", "Application started");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_startHiddenToTray)
        {
            BeginInvoke(() =>
            {
                HideToTray();
                Opacity = 1;
            });
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized && _settings.MinimizeToTray)
        {
            HideToTray();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit &&
            e.CloseReason == CloseReason.UserClosing &&
            _settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        TrySaveSettings(showSuccess: false);
        _serialService.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _logger.LogInfo("APP", "Application closed");

        base.OnFormClosing(e);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBackground,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var configGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        configGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        configGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        configGrid.Controls.Add(BuildSectionCard("Conexión serial", "Puerto, parámetros y estado del enlace."), 0, 0);
        configGrid.Controls.Add(BuildSectionCard("Captura y envío", "Extracción, movimiento y comportamiento posterior."), 1, 0);

        var middleGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14)
        };
        middleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69));
        middleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        middleGrid.Controls.Add(BuildReadingCard(), 0, 0);
        middleGrid.Controls.Add(BuildSummaryCard(), 1, 0);

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(configGrid, 0, 1);
        root.Controls.Add(middleGrid, 0, 2);
        root.Controls.Add(BuildActivityCard(), 0, 3);
        root.Controls.Add(BuildButtonBar(), 0, 4);

        Controls.Add(root);
    }

    private Control BuildHeaderPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18, 14, 18, 14)
        };
        panel.Paint += PaintSurfaceBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleWrap = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };

        titleWrap.Controls.Add(new Label
        {
            Text = "LINHER Keyboard Wedge",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 0, 4)
        });

        titleWrap.Controls.Add(new Label
        {
            Text = "Captura cantidades desde una báscula RHINO y las envía como entrada de teclado al campo activo.",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedText,
            Margin = new Padding(0)
        });

        _heroStatusLabel = new Label
        {
            AutoSize = true,
            Padding = new Padding(14, 8, 14, 8),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 6, 0, 0)
        };

        layout.Controls.Add(titleWrap, 0, 0);
        layout.Controls.Add(_heroStatusLabel, 1, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSectionCard(string title, string subtitle)
    {
        return title switch
        {
            "Conexión serial" => BuildCard(title, subtitle, BuildSerialContent()),
            _ => BuildCard(title, subtitle, BuildBehaviorContent())
        };
    }

    private Control BuildCard(string title, string subtitle, Control content)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 14, 0),
            Padding = new Padding(18, 16, 18, 16)
        };
        panel.Paint += PaintSurfaceBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = subtitle,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.75F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedText,
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 1);

        content.Dock = DockStyle.Fill;
        layout.Controls.Add(content, 0, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSerialContent()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _portCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
        var refreshButton = CreateActionButton("Actualizar", AccentSoft, Accent);
        refreshButton.Click += (_, _) => RefreshPorts(_portCombo.Text);

        var portPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0)
        };
        portPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        portPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        portPanel.Controls.Add(_portCombo, 0, 0);
        portPanel.Controls.Add(refreshButton, 1, 0);

        _baudCombo = BuildDropDown("9600", "19200", "38400", "57600", "115200");
        _dataBitsCombo = BuildDropDown("7", "8");
        _parityCombo = BuildDropDown("None", "Odd", "Even", "Mark", "Space");
        _stopBitsCombo = BuildDropDown("1", "1.5", "2");
        _flowControlCombo = BuildDropDown("None", "XOnXOff", "RequestToSend", "RequestToSendXOnXOff");
        _dtrEnableCheck = CreateCheckBox("DTR activo");
        _rtsEnableCheck = CreateCheckBox("RTS activo");
        _connectionLabel = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold) };

        AddLabeledControl(table, 0, 0, "Puerto", portPanel);
        AddLabeledControl(table, 0, 2, "Baud", _baudCombo);
        AddLabeledControl(table, 1, 0, "Data bits", _dataBitsCombo);
        AddLabeledControl(table, 1, 2, "Parity", _parityCombo);
        AddLabeledControl(table, 2, 0, "Stop bits", _stopBitsCombo);
        AddLabeledControl(table, 2, 2, "Flow", _flowControlCombo);
        AddLabeledControl(table, 3, 0, "Control", BuildInlinePanel(_dtrEnableCheck, _rtsEnableCheck));
        AddLabeledControl(table, 4, 0, "Estado", _connectionLabel);

        return table;
    }

    private Control BuildBehaviorContent()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _regexTextBox = new TextBox();
        _postActionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _postActionCombo.Items.Add(new ComboOption<PostSendAction>("Enter", PostSendAction.Enter));
        _postActionCombo.Items.Add(new ComboOption<PostSendAction>("Tab", PostSendAction.Tab));
        _postActionCombo.Items.Add(new ComboOption<PostSendAction>("Ninguna", PostSendAction.None));

        _dedupeNumeric = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 60000,
            Increment = 100
        };

        _startMinimizedCheck = CreateCheckBox("Iniciar minimizada");
        _minimizeToTrayCheck = CreateCheckBox("Minimizar a bandeja");
        _startWithWindowsCheck = CreateCheckBox("Iniciar con Windows");

        AddLabeledControl(table, 0, 0, "Regex QTY", _regexTextBox);
        AddLabeledControl(table, 1, 0, "Después de enviar", _postActionCombo);
        AddLabeledControl(table, 2, 0, "Anti-duplicado ms", _dedupeNumeric);
        AddLabeledControl(table, 3, 0, "Arranque", BuildStackPanel(_startMinimizedCheck, _minimizeToTrayCheck, _startWithWindowsCheck));

        return table;
    }

    private Control BuildReadingCard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 14, 14),
            Padding = new Padding(18, 16, 18, 16)
        };
        panel.Paint += PaintSurfaceBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Última lectura",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Trama completa recibida desde la báscula.",
            AutoSize = true,
            ForeColor = MutedText,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        _rawTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 11F, FontStyle.Regular, GraphicsUnit.Point)
        };

        layout.Controls.Add(_rawTextBox, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSummaryCard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18, 16, 18, 16)
        };
        panel.Paint += PaintSurfaceBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Resumen",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true
        };

        _qtyValueLabel = BuildMetricValueLabel();
        _timestampValueLabel = BuildMetricValueLabel();
        _resultValueLabel = BuildMetricValueLabel();
        _portValueLabel = BuildMetricValueLabel();
        var dataFolderLabel = BuildMetricValueLabel();
        dataFolderLabel.Text = AppPaths.DataDirectory;

        metrics.Controls.Add(BuildMetricRow("Ultimo QTY", _qtyValueLabel), 0, 0);
        metrics.Controls.Add(BuildMetricRow("Fecha y hora", _timestampValueLabel), 0, 1);
        metrics.Controls.Add(BuildMetricRow("Resultado", _resultValueLabel), 0, 2);
        metrics.Controls.Add(BuildMetricRow("Puerto activo", _portValueLabel), 0, 3);
        metrics.Controls.Add(BuildMetricRow("Carpeta datos", dataFolderLabel), 0, 4);

        layout.Controls.Add(metrics, 0, 1);

        var openLogsButton = CreateActionButton("Abrir logs", AccentSoft, Accent);
        openLogsButton.Anchor = AnchorStyles.Left;
        openLogsButton.Click += (_, _) => OpenLogsFolder();
        layout.Controls.Add(openLogsButton, 0, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildActivityCard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceBackground,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(18, 16, 18, 16)
        };
        panel.Paint += PaintSurfaceBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "Actividad",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11.5F, FontStyle.Bold),
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "Eventos recientes de conexión, lectura y envío.",
            AutoSize = true,
            ForeColor = MutedText,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 1);

        _activityList = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point),
            HorizontalScrollbar = true
        };

        layout.Controls.Add(_activityList, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildButtonBar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            Padding = new Padding(0, 0, 0, 0)
        };

        var exitButton = CreateActionButton("Salir", Color.FromArgb(241, 245, 249), HeaderText);
        exitButton.Click += (_, _) =>
        {
            _allowExit = true;
            Close();
        };

        var saveButton = CreateActionButton("Guardar", Color.FromArgb(224, 242, 254), Accent);
        saveButton.Click += (_, _) => TrySaveSettings(showSuccess: true);

        var testButton = CreateActionButton("Probar trama", AccentSoft, Accent);
        testButton.Click += (_, _) => RunTestFrame();

        _disconnectButton = CreateActionButton("Desconectar", ErrorSoft, Error);
        _disconnectButton.Click += (_, _) => Disconnect();

        _connectButton = CreateActionButton("Conectar", GoodSoft, Good);
        _connectButton.Click += (_, _) => Connect();

        panel.Controls.Add(exitButton);
        panel.Controls.Add(saveButton);
        panel.Controls.Add(testButton);
        panel.Controls.Add(_disconnectButton);
        panel.Controls.Add(_connectButton);

        return panel;
    }

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point)
        };
        menu.Items.Add("Abrir", null, (_, _) => ShowFromTray());
        _connectMenuItem = new ToolStripMenuItem("Conectar", null, (_, _) => Connect());
        _disconnectMenuItem = new ToolStripMenuItem("Desconectar", null, (_, _) => Disconnect());
        menu.Items.Add(_connectMenuItem);
        menu.Items.Add(_disconnectMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) =>
        {
            _allowExit = true;
            Close();
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "LINHER Keyboard Wedge",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private void WireEvents()
    {
        _serialService.FrameReceived += (_, frame) => _processor.ProcessFrame(frame);
        _serialService.StatusChanged += (_, status) =>
        {
            RunOnUiThread(() =>
            {
                _connectionLabel.Text = status;
                AppendActivity(status);
            });
        };

        _processor.ReadingProcessed += (_, args) => RunOnUiThread(() => UpdateReading(args));
    }

    private void LoadSettingsIntoControls()
    {
        _baudCombo.Text = _settings.BaudRate.ToString();
        _dataBitsCombo.Text = _settings.DataBits.ToString();
        _parityCombo.Text = _settings.Parity;
        _stopBitsCombo.Text = _settings.StopBits;
        _flowControlCombo.Text = _settings.FlowControl;
        _dtrEnableCheck.Checked = _settings.DtrEnable;
        _rtsEnableCheck.Checked = _settings.RtsEnable;
        _regexTextBox.Text = _settings.QuantityRegex;
        _dedupeNumeric.Value = Math.Clamp(_settings.DuplicateWindowMs, 0, 60000);
        _startMinimizedCheck.Checked = _settings.StartMinimized;
        _minimizeToTrayCheck.Checked = _settings.MinimizeToTray;
        _startWithWindowsCheck.Checked = _settings.StartWithWindows;
        SelectPostAction(_settings.PostSendAction);
    }

    private void RefreshPorts(string? selectedPort)
    {
        var current = string.IsNullOrWhiteSpace(selectedPort) ? _settings.PortName : selectedPort;
        var ports = ScaleSerialService.GetAvailablePorts();

        _portCombo.Items.Clear();
        foreach (var port in ports)
        {
            _portCombo.Items.Add(port);
        }

        if (!string.IsNullOrWhiteSpace(current) && !_portCombo.Items.Contains(current))
        {
            _portCombo.Items.Add(current);
        }

        if (ports.Length > 0)
        {
            _portCombo.Text = ports.Contains(current, StringComparer.OrdinalIgnoreCase) ? current : ports[0];
            return;
        }

        _portCombo.Text = string.IsNullOrWhiteSpace(current) ? string.Empty : current;
    }

    private void Connect()
    {
        if (!TrySaveSettings(showSuccess: false))
        {
            return;
        }

        try
        {
            _serialService.Connect(_settings);
            SetConnectedState(connected: true);
            AppendActivity($"Conectado a {_settings.PortName}");
        }
        catch (Exception exception)
        {
            _logger.LogError("SERIAL_CONNECT", exception);
            SetConnectedState(connected: false);
            MessageBox.Show(
                this,
                $"No se pudo conectar al puerto {_settings.PortName}.\r\n\r\n{exception.Message}",
                "Error de conexión",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void Disconnect()
    {
        _serialService.Disconnect();
        SetConnectedState(connected: false);
        AppendActivity("Desconectado");
    }

    private bool TrySaveSettings(bool showSuccess)
    {
        try
        {
            CollectSettingsFromControls();
            ValidateRegex(_settings.QuantityRegex);
            _processor.UpdateSettings(_settings);
            _configService.Save(_settings);
            _startupManager.SetEnabled(_settings.StartWithWindows);

            if (showSuccess)
            {
                AppendActivity("Configuración guardada");
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError("CONFIG_SAVE", exception);
            MessageBox.Show(
                this,
                exception.Message,
                "No se pudo guardar la configuración",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private void CollectSettingsFromControls()
    {
        var port = _portCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(port))
        {
            throw new InvalidOperationException("Selecciona o escribe un puerto COM.");
        }

        if (!int.TryParse(_baudCombo.Text.Trim(), out var baudRate) || baudRate <= 0)
        {
            throw new InvalidOperationException("Baud rate invalido.");
        }

        if (!int.TryParse(_dataBitsCombo.Text.Trim(), out var dataBits) || dataBits <= 0)
        {
            throw new InvalidOperationException("Data bits invalido.");
        }

        _settings.PortName = port;
        _settings.BaudRate = baudRate;
        _settings.DataBits = dataBits;
        _settings.Parity = _parityCombo.Text.Trim();
        _settings.StopBits = _stopBitsCombo.Text.Trim();
        _settings.FlowControl = _flowControlCombo.Text.Trim();
        _settings.DtrEnable = _dtrEnableCheck.Checked;
        _settings.RtsEnable = _rtsEnableCheck.Checked;
        _settings.QuantityRegex = _regexTextBox.Text.Trim();
        _settings.DuplicateWindowMs = (int)_dedupeNumeric.Value;
        _settings.StartMinimized = _startMinimizedCheck.Checked;
        _settings.MinimizeToTray = _minimizeToTrayCheck.Checked;
        _settings.StartWithWindows = _startWithWindowsCheck.Checked;

        if (_postActionCombo.SelectedItem is ComboOption<PostSendAction> action)
        {
            _settings.PostSendAction = action.Value;
        }
    }

    private static void ValidateRegex(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new InvalidOperationException("El regex no puede estar vacio.");
        }

        _ = Regex.Match(string.Empty, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    }

    private void RunTestFrame()
    {
        if (!TrySaveSettings(showSuccess: false))
        {
            return;
        }

        _processor.ProcessFrame(SampleFrame);
    }

    private void UpdateReading(ProcessedReadingEventArgs args)
    {
        _rawTextBox.Text = args.Raw;
        _qtyValueLabel.Text = string.IsNullOrWhiteSpace(args.Quantity) ? "-" : args.Quantity;
        _timestampValueLabel.Text = args.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        _resultValueLabel.Text = args.Result switch
        {
            ReadingResult.Sent => "Enviado",
            ReadingResult.IgnoredDuplicate => "Duplicado ignorado",
            _ => "Error"
        };
        _portValueLabel.Text = _settings.PortName;

        AppendActivity($"{args.Timestamp:HH:mm:ss} | {_resultValueLabel.Text} | QTY={_qtyValueLabel.Text} | {args.Message}");
    }

    private void SetConnectedState(bool connected)
    {
        var activePort = _serialService.IsConnected ? _portCombo.Text.Trim() : _settings.PortName;
        _settings.PortName = string.IsNullOrWhiteSpace(activePort) ? _settings.PortName : activePort;
        _connectionLabel.Text = connected ? $"Conectado a {_settings.PortName}" : "Desconectado";
        _connectionLabel.ForeColor = connected ? Good : Error;
        _heroStatusLabel.Text = connected ? $"Conectado a {_settings.PortName}" : "Desconectado";
        _heroStatusLabel.ForeColor = connected ? Good : Error;
        _heroStatusLabel.BackColor = connected ? GoodSoft : ErrorSoft;
        _portValueLabel.Text = string.IsNullOrWhiteSpace(_settings.PortName) ? "-" : _settings.PortName;

        _connectButton.Enabled = !connected;
        _disconnectButton.Enabled = connected;
        _connectMenuItem.Enabled = !connected;
        _disconnectMenuItem.Enabled = connected;
        _portCombo.Enabled = !connected;
        _baudCombo.Enabled = !connected;
        _dataBitsCombo.Enabled = !connected;
        _parityCombo.Enabled = !connected;
        _stopBitsCombo.Enabled = !connected;
        _flowControlCombo.Enabled = !connected;
        _dtrEnableCheck.Enabled = !connected;
        _rtsEnableCheck.Enabled = !connected;
    }

    private void AppendActivity(string message)
    {
        if (_activityList.Items.Count >= 200)
        {
            _activityList.Items.RemoveAt(0);
        }

        _activityList.Items.Add($"{DateTime.Now:HH:mm:ss} | {message}");
        _activityList.SelectedIndex = _activityList.Items.Count - 1;
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();

        if (!_hasShownTrayBalloon)
        {
            _notifyIcon.ShowBalloonTip(
                2500,
                "LINHER Keyboard Wedge",
                "La aplicación sigue activa en la bandeja del sistema.",
                ToolTipIcon.Info);
            _hasShownTrayBalloon = true;
        }
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        Opacity = 1;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(AppPaths.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.LogDirectory}\"")
        {
            UseShellExecute = true
        });
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void SelectPostAction(PostSendAction action)
    {
        foreach (var item in _postActionCombo.Items)
        {
            if (item is ComboOption<PostSendAction> option && EqualityComparer<PostSendAction>.Default.Equals(option.Value, action))
            {
                _postActionCombo.SelectedItem = option;
                return;
            }
        }

        _postActionCombo.SelectedIndex = 0;
    }

    private static ComboBox BuildDropDown(params string[] values)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            FlatStyle = FlatStyle.Standard,
            Margin = new Padding(0, 0, 0, 10)
        };

        combo.Items.AddRange(values);
        if (values.Length > 0)
        {
            combo.SelectedIndex = 0;
        }

        return combo;
    }

    private static CheckBox CreateCheckBox(string text)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            ForeColor = HeaderText,
            Margin = new Padding(0, 0, 14, 8)
        };
    }

    private static Button CreateActionButton(string text, Color backColor, Color foreColor)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(14, 8, 14, 8)
        };
    }

    private static Panel BuildMetricRow(string title, Label valueLabel)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 10, 12, 10),
            BackColor = Color.FromArgb(248, 250, 252)
        };
        panel.Paint += PaintInlineBorder;

        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 8.75F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedText,
            Dock = DockStyle.Top
        };

        valueLabel.Dock = DockStyle.Bottom;
        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static Label BuildMetricValueLabel()
    {
        return new Label
        {
            Text = "-",
            AutoEllipsis = true,
            AutoSize = false,
            Height = 22,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = HeaderText,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Control BuildInlinePanel(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 6)
        };

        foreach (var control in controls)
        {
            panel.Controls.Add(control);
        }

        return panel;
    }

    private static Control BuildStackPanel(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };

        foreach (var control in controls)
        {
            panel.Controls.Add(control);
        }

        return panel;
    }

    private static void AddLabeledControl(TableLayoutPanel table, int row, int column, string labelText, Control control)
    {
        while (table.RowStyles.Count <= row)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 8.75F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = MutedText,
            Margin = new Padding(0, 6, 10, 12)
        };

        control.Dock = DockStyle.Fill;
        if (control.Margin == Padding.Empty)
        {
            control.Margin = new Padding(0, 0, 0, 10);
        }

        table.Controls.Add(label, column, row);
        table.Controls.Add(control, column + 1, row);
    }

    private static void PaintSurfaceBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
        {
            return;
        }

        using var pen = new Pen(SurfaceBorder);
        var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
    }

    private static void PaintInlineBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(228, 232, 238));
        var rect = new Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
        e.Graphics.DrawRectangle(pen, rect);
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

    private sealed class ComboOption<T>
    {
        public ComboOption(string text, T value)
        {
            Text = text;
            Value = value;
        }

        public string Text { get; }

        public T Value { get; }

        public override string ToString()
        {
            return Text;
        }
    }
}
