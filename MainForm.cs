using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace StemMaker;

public sealed class MainForm : Form
{
    private readonly TextBox _songsText = new();
    private readonly RadioButton _cudaRadio = new();
    private readonly RadioButton _cpuRadio = new();
    private readonly CheckBox _limitCheck = new();
    private readonly NumericUpDown _limitBox = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _log = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly RoundedButton _installButton = new();
    private readonly RoundedButton _scanButton = new();
    private readonly RoundedButton _processButton = new();
    private readonly RoundedButton _cleanButton = new();
    private readonly RoundedButton _cancelButton = new();
    private readonly LinkLabel _selectAllLink = new();
    private readonly LinkLabel _selectNoneLink = new();
    private readonly CheckBox _showLog = new();
    private readonly Label _summary = new();

    private Process? _runningProcess;
    private CancellationTokenSource? _runCts;

    private static readonly Color AppBack = Color.FromArgb(14, 17, 24);
    private static readonly Color CardBack = Color.FromArgb(24, 29, 40);
    private static readonly Color InputBack = Color.FromArgb(17, 21, 30);
    private static readonly Color GridBack = Color.FromArgb(19, 23, 32);
    private static readonly Color Border = Color.FromArgb(54, 62, 78);
    private static readonly Color Primary = Color.FromArgb(50, 104, 255);
    private static readonly Color PrimaryDark = Color.FromArgb(37, 80, 210);
    private static readonly Color Blue = Color.FromArgb(48, 112, 255);
    private static readonly Color BlueDark = Color.FromArgb(32, 86, 220);
    private static readonly Color Success = Color.FromArgb(18, 157, 99);
    private static readonly Color SuccessDark = Color.FromArgb(13, 123, 78);
    private static readonly Color Warning = Color.FromArgb(245, 144, 43);
    private static readonly Color WarningDark = Color.FromArgb(210, 110, 24);
    private static readonly Color Danger = Color.FromArgb(235, 87, 87);
    private static readonly Color DangerDark = Color.FromArgb(199, 60, 60);
    private static readonly Color TextMain = Color.FromArgb(238, 242, 248);
    private static readonly Color TextMuted = Color.FromArgb(154, 166, 184);
    private static readonly Color LinkBlue = Color.FromArgb(118, 168, 255);

    private string AppFolder => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private string RuntimeFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Stem Maker");
    private string VenvPython => Path.Combine(RuntimeFolder, ".venv", "Scripts", "python.exe");
    private string BatcherScript => Path.Combine(RuntimeFolder, "ch_stem_batcher.py");
    private const string BatcherResourceName = "StemMaker.ch_stem_batcher.py";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainForm()
    {
        Text = "Stem Maker";
        Width = 1120;
        Height = 780;
        MinimumSize = new Size(940, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBack;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 9.5f);
        TrySetIcon();
        ExtractBatcherScript();

        BuildUi();
        SetBusy(false);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22, 18, 22, 18),
            BackColor = AppBack,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
            BackColor = AppBack,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            BackColor = AppBack,
        };
        titleStack.Controls.Add(new Label
        {
            Text = "Stem Maker",
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 21, FontStyle.Bold),
            Margin = new Padding(0),
        });
        titleStack.Controls.Add(new Label
        {
            Text = "Turn single-audio Clone Hero songs into guitar, rhythm, and backing stems.",
            AutoSize = true,
            ForeColor = TextMuted,
            Margin = new Padding(1, 4, 0, 0),
        });
        header.Controls.Add(titleStack, 0, 0);

        _installButton.Text = "Setup runtime";
        StyleButton(_installButton, ButtonRole.Blue);
        _installButton.AutoSize = false;
        _installButton.Size = new Size(138, 40);
        _installButton.Margin = new Padding(0);
        _installButton.Click += async (_, _) => await RunInstallAsync();
        header.Controls.Add(_installButton, 1, 0);
        root.Controls.Add(header, 0, 0);

        root.Controls.Add(BuildPathPanel(), 0, 1);
        root.Controls.Add(BuildControlsPanel(), 0, 2);
        root.Controls.Add(BuildResultsPanel(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnableDarkTitleBar();
    }

    private void TrySetIcon()
    {
        try
        {
            var embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (embeddedIcon is not null)
                Icon = embeddedIcon;
        }
        catch
        {
            // App can still run if Windows cannot extract the embedded icon.
        }
    }

    private void EnableDarkTitleBar()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        }
        catch
        {
            // Older Windows builds may not support dark title bars.
        }
    }

    private Control BuildPathPanel()
    {
        var card = CreateCard("Library");
        card.MinimumSize = new Size(0, 102);
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CardBack,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _songsText.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _songsText.Text = "";
        _songsText.BorderStyle = BorderStyle.FixedSingle;
        _songsText.BackColor = InputBack;
        _songsText.ForeColor = TextMain;
        _songsText.Margin = new Padding(0, 6, 12, 0);
        panel.Controls.Add(_songsText, 0, 0);

        var browse = new RoundedButton { Text = "Browse", AutoSize = false, Anchor = AnchorStyles.Right };
        StyleButton(browse, ButtonRole.Blue);
        browse.MinimumSize = new Size(82, 30);
        browse.Size = new Size(82, 30);
        browse.Padding = new Padding(8, 3, 8, 3);
        browse.CornerRadius = 8;
        browse.Margin = new Padding(0, 2, 0, 0);
        browse.Click += (_, _) => BrowseSongs();
        panel.Controls.Add(browse, 1, 0);

        card.Controls.Add(panel, 0, 1);
        return card;
    }

    private Control BuildControlsPanel()
    {
        var card = CreateCard("Processing");
        card.MinimumSize = new Size(0, 132);
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = CardBack,
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var hint = new Label
        {
            Text = "Stem Maker will scan every supported Clone Hero song folder inside the selected folder.",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = TextMuted,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = CardBack,
        };
        panel.Controls.Add(hint, 0, 0);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CardBack,
            Margin = new Padding(0),
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var options = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0, 6, 0, 0),
            BackColor = CardBack,
            WrapContents = false,
        };

        var devicePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            BackColor = CardBack,
            Margin = new Padding(0, 0, 26, 0),
            WrapContents = false,
        };
        devicePanel.Controls.Add(FieldLabel("Mode"));
        _cudaRadio.Text = "GPU / CUDA";
        _cudaRadio.Checked = true;
        _cudaRadio.AutoSize = true;
        _cudaRadio.ForeColor = TextMain;
        _cudaRadio.BackColor = CardBack;
        _cudaRadio.Margin = new Padding(8, 4, 14, 0);
        _cudaRadio.CheckedChanged += (_, _) => ShowCudaWarningIfNeeded();
        _cpuRadio.Text = "CPU";
        _cpuRadio.AutoSize = true;
        _cpuRadio.ForeColor = TextMain;
        _cpuRadio.BackColor = CardBack;
        _cpuRadio.Margin = new Padding(0, 4, 0, 0);
        devicePanel.Controls.Add(_cudaRadio);
        devicePanel.Controls.Add(_cpuRadio);
        options.Controls.Add(devicePanel);

        var limitPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            BackColor = CardBack,
            Margin = new Padding(0),
            WrapContents = false,
        };
        _limitCheck.Text = "Limit";
        _limitCheck.AutoSize = true;
        _limitCheck.Checked = true;
        _limitCheck.ForeColor = TextMain;
        _limitCheck.BackColor = CardBack;
        _limitCheck.Margin = new Padding(0, 4, 8, 0);
        _limitBox.Minimum = 1;
        _limitBox.Maximum = 9999;
        _limitBox.Value = 20;
        _limitBox.Width = 64;
        _limitBox.Height = 26;
        _limitBox.BackColor = InputBack;
        _limitBox.ForeColor = TextMain;
        _limitBox.Margin = new Padding(0, 0, 0, 0);
        limitPanel.Controls.Add(_limitCheck);
        limitPanel.Controls.Add(_limitBox);
        options.Controls.Add(limitPanel);
        bar.Controls.Add(options, 0, 0);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 2, 0, 0),
            BackColor = CardBack,
            WrapContents = false,
        };
        _scanButton.Text = "Scan folder";
        StyleButton(_scanButton, ButtonRole.Blue);
        _scanButton.Click += async (_, _) => await RunScanAsync();
        actions.Controls.Add(_scanButton);

        _cancelButton.Text = "Cancel";
        StyleButton(_cancelButton, ButtonRole.Danger);
        _cancelButton.Click += (_, _) => CancelRunningProcess();
        actions.Controls.Add(_cancelButton);

        bar.Controls.Add(actions, 1, 0);
        panel.Controls.Add(bar, 0, 1);

        card.Controls.Add(panel, 0, 1);
        return card;
    }

    private Control BuildResultsPanel()
    {
        var card = CreateCard("Songs to process");

        var wrapper = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = CardBack };
        wrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wrapper.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

        var summaryRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CardBack,
            Height = 44,
            Margin = new Padding(0, 0, 0, 10),
        };
        summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        summaryRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _summary.Text = "Scan a folder to see songs.";
        _summary.AutoSize = true;
        _summary.Anchor = AnchorStyles.Left;
        _summary.Margin = new Padding(6, 0, 20, 0);
        _summary.ForeColor = TextMuted;
        summaryRow.Controls.Add(_summary, 0, 0);

        var toolbarActions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            BackColor = CardBack,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false,
        };

        _selectAllLink.Text = "Select all";
        _selectAllLink.AutoSize = true;
        _selectAllLink.Margin = new Padding(6, 10, 18, 0);
        _selectAllLink.LinkColor = LinkBlue;
        _selectAllLink.ActiveLinkColor = Color.White;
        _selectAllLink.VisitedLinkColor = LinkBlue;
        _selectAllLink.BackColor = CardBack;
        _selectAllLink.LinkClicked += (_, _) => SetAllRows(true);
        toolbarActions.Controls.Add(_selectAllLink);

        _selectNoneLink.Text = "Select none";
        _selectNoneLink.AutoSize = true;
        _selectNoneLink.Margin = new Padding(0, 10, 22, 0);
        _selectNoneLink.LinkColor = LinkBlue;
        _selectNoneLink.ActiveLinkColor = Color.White;
        _selectNoneLink.VisitedLinkColor = LinkBlue;
        _selectNoneLink.BackColor = CardBack;
        _selectNoneLink.LinkClicked += (_, _) => SetAllRows(false);
        toolbarActions.Controls.Add(_selectNoneLink);

        _processButton.Text = "Make stems";
        StyleButton(_processButton, ButtonRole.Primary);
        _processButton.AutoSize = false;
        _processButton.Size = new Size(132, 38);
        _processButton.Margin = new Padding(0, 0, 14, 0);
        _processButton.Click += async (_, _) => await ProcessSelectedAsync();
        toolbarActions.Controls.Add(_processButton);

        _cleanButton.Text = "Clean backups";
        StyleButton(_cleanButton, ButtonRole.Warning);
        _cleanButton.AutoSize = false;
        _cleanButton.Size = new Size(132, 38);
        _cleanButton.Margin = new Padding(0, 0, 14, 0);
        _cleanButton.Click += async (_, _) => await CleanBackupsAsync();
        toolbarActions.Controls.Add(_cleanButton);

        _showLog.Text = "Details";
        _showLog.AutoSize = true;
        _showLog.Margin = new Padding(0, 10, 0, 0);
        _showLog.ForeColor = TextMain;
        _showLog.BackColor = CardBack;
        _showLog.Checked = false;
        _showLog.CheckedChanged += (_, _) =>
        {
            _log.Visible = _showLog.Checked;
            wrapper.RowStyles[2].Height = _showLog.Checked ? 150 : 0;
        };
        toolbarActions.Controls.Add(_showLog);

        summaryRow.Controls.Add(toolbarActions, 1, 0);

        wrapper.Controls.Add(summaryRow, 0, 0);
        wrapper.Controls.Add(BuildGrid(), 0, 1);
        wrapper.Controls.Add(BuildLog(), 0, 2);
        _log.Visible = false;

        card.Controls.Add(wrapper, 0, 1);
        return card;
    }

    private Control BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = GridBack;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Border;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 37, 50);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _grid.DefaultCellStyle.BackColor = GridBack;
        _grid.DefaultCellStyle.ForeColor = TextMain;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 104, 255);
        _grid.DefaultCellStyle.SelectionForeColor = TextMain;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(22, 27, 37);
        _grid.RowTemplate.Height = 30;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, _) => UpdateSummary();

        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "", Width = 42, FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Song", FillWeight = 58, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", FillWeight = 17, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", FillWeight = 25, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Path", Visible = false, ReadOnly = true });

        return _grid;
    }

    private Control BuildLog()
    {
        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.Font = new Font("Consolas", 9);
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.BackColor = InputBack;
        _log.ForeColor = TextMain;
        return _log;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0),
            BackColor = AppBack,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

        _status.Text = "Ready";
        _status.AutoSize = true;
        _status.ForeColor = TextMuted;
        footer.Controls.Add(_status, 0, 0);

        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 0;
        _progress.Visible = false;
        footer.Controls.Add(_progress, 1, 0);

        return footer;
    }

    private static TableLayoutPanel CreateCard(string title)
    {
        var card = new RoundedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = CardBack,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 14),
            CornerRadius = 14,
            BorderColor = Border,
        };
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(new Label
        {
            Text = title,
            AutoSize = true,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
        }, 0, 0);
        return card;
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        ForeColor = TextMuted,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
    };

    private enum ButtonRole
    {
        Primary,
        Blue,
        Secondary,
        Warning,
        Danger,
    }

    private static void StyleButton(RoundedButton button, ButtonRole role)
    {
        button.AutoSize = true;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.Padding = new Padding(16, 7, 16, 7);
        button.Margin = new Padding(0, 0, 8, 0);
        button.MinimumSize = new Size(92, 36);
        button.CornerRadius = 10;

        switch (role)
        {
            case ButtonRole.Primary:
                button.FillColor = Success;
                button.HoverColor = SuccessDark;
                button.PressedColor = Color.FromArgb(10, 100, 63);
                button.BorderColor = Success;
                button.ForeColor = Color.White;
                break;
            case ButtonRole.Blue:
                button.FillColor = Blue;
                button.HoverColor = BlueDark;
                button.PressedColor = Color.FromArgb(20, 70, 190);
                button.BorderColor = Blue;
                button.ForeColor = Color.White;
                break;
            case ButtonRole.Warning:
                button.FillColor = Warning;
                button.HoverColor = WarningDark;
                button.PressedColor = Color.FromArgb(178, 87, 18);
                button.BorderColor = Warning;
                button.ForeColor = Color.White;
                break;
            case ButtonRole.Danger:
                button.FillColor = Danger;
                button.HoverColor = DangerDark;
                button.PressedColor = Color.FromArgb(170, 45, 45);
                button.BorderColor = Danger;
                button.ForeColor = Color.White;
                break;
            default:
                button.FillColor = Color.FromArgb(36, 43, 58);
                button.HoverColor = Color.FromArgb(45, 53, 70);
                button.PressedColor = Color.FromArgb(31, 37, 50);
                button.BorderColor = Border;
                button.ForeColor = TextMain;
                break;
        }
    }

    private string Device => _cudaRadio.Checked ? "cuda" : "cpu";

    private void BrowseSongs()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Clone Hero Songs folder",
            SelectedPath = Directory.Exists(_songsText.Text) ? _songsText.Text : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _songsText.Text = dialog.SelectedPath;
    }

    private void ShowCudaWarningIfNeeded()
    {
        if (_cudaRadio.Checked)
        {
            MessageBox.Show(
                this,
                "GPU/CUDA mode only works with supported NVIDIA GPUs and compatible drivers. If it fails, switch to CPU mode.",
                "CUDA Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task RunInstallAsync()
    {
        if (_cudaRadio.Checked)
        {
            var ok = MessageBox.Show(
                this,
                "CUDA install is recommended for supported NVIDIA GPUs. It may not work on every graphics card.\n\nContinue with CUDA setup?",
                "CUDA Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes)
                return;
        }

        await RunExclusiveAsync("Checking/installing requirements...", async token =>
        {
            AppendLog("Checking Python 3.12...\r\n");
            var python = await EnsurePython312Async(token);

            AppendLog("Checking FFmpeg...\r\n");
            await EnsureFfmpegAsync(token);

            if (!File.Exists(VenvPython))
            {
                Directory.CreateDirectory(RuntimeFolder);
                AppendLog($"Creating runtime in {RuntimeFolder}...\r\n");
                await RunProcessAsync(python.FileName, python.Args($"-m venv {Quote(Path.Combine(RuntimeFolder, ".venv"))}"), token);
            }

            AppendLog("Installing Python package basics...\r\n");
            await RunProcessAsync(VenvPython, "-m pip install -U pip", token);
            await RunProcessAsync(VenvPython, "-m pip install \"setuptools<82\" wheel packaging", token);

            if (_cudaRadio.Checked)
            {
                AppendLog("Installing CUDA PyTorch...\r\n");
                await RunProcessAsync(
                    VenvPython,
                    "-m pip install torch==2.4.1+cu121 torchaudio==2.4.1+cu121 --index-url https://download.pytorch.org/whl/cu121",
                    token);
            }
            else
            {
                AppendLog("Installing CPU PyTorch...\r\n");
                await RunProcessAsync(
                    VenvPython,
                    "-m pip install torch==2.4.1 torchaudio==2.4.1 --index-url https://download.pytorch.org/whl/cpu",
                    token);
            }

            AppendLog("Installing Demucs and audio backend...\r\n");
            await RunProcessAsync(VenvPython, "-m pip install demucs==4.0.1 soundfile", token);

            AppendLog("Verifying runtime...\r\n");
            await RunProcessAsync(
                VenvPython,
                "-c \"import torch, torchaudio, soundfile; print('torch', torch.__version__); print('torchaudio', torchaudio.__version__); print('cuda', torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'CUDA unavailable')\"",
                token);
        });
    }

    private async Task<PythonCommand> EnsurePython312Async(CancellationToken token)
    {
        var python = await FindPython312Async(token);
        if (python is not null)
        {
            AppendLog($"Python 3.12 found: {python.DisplayName}\r\n");
            return python;
        }

        AppendLog("Python 3.12 not found. Installing with winget...\r\n");
        var code = await RunProcessAsync(
            "winget",
            "install -e --id Python.Python.3.12 --accept-source-agreements --accept-package-agreements",
            token,
            throwOnError: false);

        python = await FindPython312Async(token);
        if (python is not null)
        {
            AppendLog($"Python 3.12 ready: {python.DisplayName}\r\n");
            return python;
        }

        throw new InvalidOperationException(
            $"Python 3.12 could not be found after winget finished with code {code}. Install Python 3.12 from python.org, then reopen Stem Maker.");
    }

    private async Task<PythonCommand?> FindPython312Async(CancellationToken token)
    {
        var candidates = new List<PythonCommand>
        {
            new("py", "-3.12", "Python Launcher 3.12"),
            new("python", "", "python on PATH"),
            new("python3.12", "", "python3.12 on PATH"),
        };

        AddPythonPathCandidate(candidates, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe");
        AddPythonPathCandidate(candidates, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python312", "python.exe");
        AddPythonPathCandidate(candidates, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python312", "python.exe");

        foreach (var candidate in candidates)
        {
            if (await CommandSucceedsAsync(
                    candidate.FileName,
                    candidate.Args("-c \"import sys; raise SystemExit(0 if sys.version_info[:2] == (3, 12) else 1)\""),
                    token))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void AddPythonPathCandidate(List<PythonCommand> candidates, params string[] parts)
    {
        if (parts.Any(string.IsNullOrWhiteSpace))
            return;

        var path = Path.Combine(parts);
        if (File.Exists(path))
            candidates.Add(new PythonCommand(path, "", path));
    }

    private async Task EnsureFfmpegAsync(CancellationToken token)
    {
        if (await CommandSucceedsAsync("ffmpeg", "-version", token))
        {
            AppendLog("FFmpeg found.\r\n");
            return;
        }

        AppendLog("FFmpeg not found. Installing with winget...\r\n");
        var code = await RunProcessAsync(
            "winget",
            "install -e --id Gyan.FFmpeg --accept-source-agreements --accept-package-agreements",
            token,
            throwOnError: false);

        if (await CommandSucceedsAsync("ffmpeg", "-version", token))
        {
            AppendLog("FFmpeg ready.\r\n");
            return;
        }

        throw new InvalidOperationException(
            $"FFmpeg could not be found after winget finished with code {code}. Install FFmpeg, reopen Stem Maker, then run Setup again.");
    }

    private async Task RunScanAsync()
    {
        if (!ValidateReadyForBatcher())
            return;

        var planPath = Path.Combine(Path.GetTempPath(), $"stem-maker-plan-{Guid.NewGuid():N}.json");
        var args = BuildScanArgs(planPath);
        if (args is null)
            return;

        await RunExclusiveAsync("Scanning...", async token =>
        {
            _grid.Rows.Clear();
            await RunProcessAsync(VenvPython, args, token);
            LoadPlan(planPath);
        });
    }

    private async Task ProcessSelectedAsync()
    {
        if (!ValidateReadyForBatcher())
            return;

        var selected = SelectedSongPaths();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select at least one song folder first.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_cudaRadio.Checked)
        {
            var ok = MessageBox.Show(
                this,
                "Processing with GPU/CUDA. If your GPU or driver is unsupported, the run may fail.\n\nContinue?",
                "CUDA Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (ok != DialogResult.Yes)
                return;
        }

        var selectedFile = Path.Combine(Path.GetTempPath(), $"stem-maker-selected-{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(selectedFile, selected);

        await RunExclusiveAsync("Processing selected songs...", async token =>
        {
            var songs = _songsText.Text.Trim();
            var args = $"{Quote(BatcherScript)} --songs {Quote(songs)} --song-dir-file {Quote(selectedFile)} --apply --device {Device} --keep-going --verbose";
            await RunProcessAsync(VenvPython, args, token);
        });
    }

    private async Task CleanBackupsAsync()
    {
        var songs = _songsText.Text.Trim();
        if (!Directory.Exists(songs))
        {
            MessageBox.Show(this, "Select a valid Songs folder first.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var backups = Directory.EnumerateDirectories(songs, "_stem_batcher_backup", SearchOption.AllDirectories).ToList();
        if (backups.Count == 0)
        {
            MessageBox.Show(this, "No _stem_batcher_backup folders found.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        long totalBytes = 0;
        foreach (var backup in backups)
        {
            try
            {
                totalBytes += Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
            }
            catch
            {
                // Best-effort size estimate only.
            }
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete {backups.Count} backup folder(s)?\n\nApprox size: {FormatSize(totalBytes)}\n\nOnly do this after testing the generated stems.",
            "Clean Backups",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        await RunExclusiveAsync("Deleting backups...", _ =>
        {
            var deleted = 0;
            foreach (var backup in backups)
            {
                try
                {
                    Directory.Delete(backup, recursive: true);
                    deleted++;
                }
                catch (Exception ex)
                {
                    AppendLog($"Could not delete {backup}: {ex.Message}\r\n");
                }
            }
            AppendLog($"Deleted {deleted} backup folder(s), freed about {FormatSize(totalBytes)}.\r\n");
            return Task.CompletedTask;
        });
    }

    private string? BuildScanArgs(string planPath)
    {
        var songs = _songsText.Text.Trim();
        if (!Directory.Exists(songs))
        {
            MessageBox.Show(this, "Select a valid Songs folder first.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var args = $"{Quote(BatcherScript)} --songs {Quote(songs)} --all --dry-run --plan-json {Quote(planPath)} --device {Device}";

        if (_limitCheck.Checked)
            args += $" --limit {(int)_limitBox.Value}";

        return args;
    }

    private bool ValidateReadyForBatcher()
    {
        ExtractBatcherScript();

        if (!File.Exists(BatcherScript))
        {
            MessageBox.Show(this, $"Could not prepare ch_stem_batcher.py:\n{BatcherScript}", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (!File.Exists(VenvPython))
        {
            MessageBox.Show(this, "Runtime is not installed yet. Click Setup first.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        return true;
    }

    private void ExtractBatcherScript()
    {
        Directory.CreateDirectory(RuntimeFolder);

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(BatcherResourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource not found: {BatcherResourceName}");

        using var file = File.Create(BatcherScript);
        stream.CopyTo(file);
    }

    private void LoadPlan(string planPath)
    {
        var json = File.ReadAllText(planPath);
        var plan = JsonSerializer.Deserialize<PlanFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        _grid.Rows.Clear();

        foreach (var row in plan?.Rows ?? new List<PlanRow>())
        {
            if (!string.Equals(row.Status, "process", StringComparison.OrdinalIgnoreCase))
                continue;

            _grid.Rows.Add(true, row.Label, row.Source, row.Reason, row.Path);
        }

        UpdateSummary();
        AppendLog($"Loaded {_grid.Rows.Count} processable song(s).\r\n");
    }

    private List<string> SelectedSongPaths()
    {
        var selected = new List<string>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var isChecked = row.Cells[0].Value is bool value && value;
            if (!isChecked)
                continue;
            var path = row.Cells[4].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(path))
                selected.Add(path);
        }
        return selected;
    }

    private void SetAllRows(bool selected)
    {
        foreach (DataGridViewRow row in _grid.Rows)
            row.Cells[0].Value = selected;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var total = _grid.Rows.Count;
        var selected = SelectedSongPaths().Count;
        _summary.Text = total == 0
            ? "Scan a folder to see songs."
            : $"{selected} of {total} songs selected";
    }

    private async Task RunExclusiveAsync(string label, Func<CancellationToken, Task> action)
    {
        if (_runCts is not null)
        {
            MessageBox.Show(this, "Another operation is already running.", "Stem Maker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _runCts = new CancellationTokenSource();
        SetBusy(true);
        _status.Text = label;
        ShowDetails();
        AppendLog($"\r\n== {label} ==\r\n");

        try
        {
            await action(_runCts.Token);
            AppendLog("Done.\r\n");
            _status.Text = "Ready";
        }
        catch (OperationCanceledException)
        {
            AppendLog("Cancelled.\r\n");
            _status.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}\r\n");
            _status.Text = "Error";
            MessageBox.Show(this, ex.Message, "Stem Maker Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runCts.Dispose();
            _runCts = null;
            _runningProcess = null;
            SetBusy(false);
        }
    }

    private async Task<bool> CommandSucceedsAsync(string fileName, string arguments, CancellationToken token)
    {
        try
        {
            var code = await RunProcessAsync(fileName, arguments, token, throwOnError: false, echoCommand: false);
            return code == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> RunProcessAsync(string fileName, string arguments, CancellationToken token, bool throwOnError = true, bool echoCommand = true)
    {
        if (echoCommand)
            AppendLog($"> {fileName} {arguments}\r\n");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Directory.Exists(RuntimeFolder) ? RuntimeFolder : AppFolder,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _runningProcess = process;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                AppendLog(e.Data + "\r\n");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                AppendLog(e.Data + "\r\n");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(token);
        _runningProcess = null;

        if (throwOnError && process.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}.");

        return process.ExitCode;
    }

    private void CancelRunningProcess()
    {
        _runCts?.Cancel();
        try
        {
            if (_runningProcess is { HasExited: false })
                _runningProcess.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may already be gone.
        }
    }

    private void SetBusy(bool busy)
    {
        _installButton.Enabled = !busy;
        _scanButton.Enabled = !busy;
        _processButton.Enabled = !busy;
        _cleanButton.Enabled = !busy;
        _cancelButton.Enabled = busy;
        _progress.Visible = busy;
        _progress.MarqueeAnimationSpeed = busy ? 35 : 0;
    }

    private void ShowDetails()
    {
        if (!_showLog.Checked)
            _showLog.Checked = true;
    }

    private void AppendLog(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(text));
            return;
        }

        _log.AppendText(text);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string FormatSize(long bytes)
    {
        double value = bytes;
        foreach (var unit in new[] { "B", "KB", "MB", "GB", "TB" })
        {
            if (value < 1024 || unit == "TB")
                return $"{value:0.00} {unit}";
            value /= 1024;
        }
        return $"{value:0.00} TB";
    }

    private sealed record PythonCommand(string FileName, string PrefixArgs, string DisplayName)
    {
        public string Args(string arguments) =>
            string.IsNullOrWhiteSpace(PrefixArgs) ? arguments : $"{PrefixArgs} {arguments}";
    }

    private sealed class PlanFile
    {
        public List<PlanRow> Rows { get; set; } = new();
    }

    private sealed class PlanRow
    {
        public string Label { get; set; } = "";
        public string Path { get; set; } = "";
        public string Source { get; set; } = "";
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
    }
}

internal sealed class RoundedButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public int CornerRadius { get; set; } = 10;
    public Color FillColor { get; set; } = Color.White;
    public Color HoverColor { get; set; } = Color.FromArgb(248, 250, 253);
    public Color PressedColor { get; set; } = Color.FromArgb(238, 242, 248);
    public Color BorderColor { get; set; } = Color.FromArgb(226, 229, 235);

    public RoundedButton()
    {
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        BackColor = Color.Transparent;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        var fill = !Enabled
            ? Color.FromArgb(45, 50, 61)
            : _pressed
                ? PressedColor
                : _hovered
                    ? HoverColor
                    : FillColor;

        using var path = RoundedRect(rect, CornerRadius);
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(!Enabled ? Color.FromArgb(67, 73, 86) : BorderColor);
        pevent.Graphics.FillPath(brush, path);
        pevent.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            ClientRectangle,
            Enabled ? ForeColor : Color.FromArgb(129, 139, 154),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class RoundedTableLayoutPanel : TableLayoutPanel
{
    public int CornerRadius { get; set; } = 14;
    public Color BorderColor { get; set; } = Color.FromArgb(226, 229, 235);

    public RoundedTableLayoutPanel()
    {
        BackColor = Color.FromArgb(24, 29, 40);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        using var path = RoundedRect(rect, CornerRadius);
        using var brush = new SolidBrush(BackColor);
        using var pen = new Pen(BorderColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);

        base.OnPaint(e);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
