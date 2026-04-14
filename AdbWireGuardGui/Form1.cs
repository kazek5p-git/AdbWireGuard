using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json;

namespace AdbWireGuardGui;

public partial class Form1 : Form
{
    private const string MikroTikPrivateKeyFileName = "mikrotik_ed25519";
    private const string MikroTikPublicKeyFileName = "mikrotik_ed25519.pub";
    private static readonly string PackageRoot = AppEnvironment.PackageRoot;
    private static readonly string MikroTikRoot = Path.Combine(PackageRoot, "mikrotik");
    private static readonly string PersistentMikroTikRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ADB-WireGuard",
        "mikrotik-key");
    private static readonly string StateRoot = Path.Combine(PackageRoot, "state");
    private static readonly string WrapperScriptPath = Path.Combine(PackageRoot, "Invoke-ADB-WG-Wrapper.ps1");
    private static readonly string StopScriptPath = Path.Combine(PackageRoot, "3-Stop-ADB-Server-Over-WireGuard.ps1");
    private static readonly string RemoteCommandScriptPath = Path.Combine(PackageRoot, "2-Run-Remote-ADB-Command.ps1");
    private static readonly string MikroTikPrivateKeyPath = Path.Combine(MikroTikRoot, MikroTikPrivateKeyFileName);
    private static readonly string MikroTikPublicKeyPath = Path.Combine(MikroTikRoot, MikroTikPublicKeyFileName);
    private static readonly string PersistentMikroTikPrivateKeyPath = Path.Combine(PersistentMikroTikRoot, MikroTikPrivateKeyFileName);
    private static readonly string PersistentMikroTikPublicKeyPath = Path.Combine(PersistentMikroTikRoot, MikroTikPublicKeyFileName);
    private static readonly string StatusPath = Path.Combine(StateRoot, "last-start-status.txt");
    private static readonly string ReportPath = Path.Combine(StateRoot, "last-start-report.txt");
    private static readonly string ErrorPath = Path.Combine(StateRoot, "last-start-error.txt");
    private static readonly string WrapperReportPath = Path.Combine(StateRoot, "5-wrapper-report.txt");
    private static readonly string MikroTikDiagnosticsPath = Path.Combine(StateRoot, "mikrotik-diagnostics.txt");
    private static readonly string MikroTikCommandHistoryPath = Path.Combine(StateRoot, "mikrotik-command-history.txt");
    private static readonly string MikroTikLastCommandPath = Path.Combine(StateRoot, "mikrotik-last-command.txt");
    private static readonly string ComponentsVersionPath = Path.Combine(StateRoot, "components-version.txt");
    private static readonly string LastActionNamePath = Path.Combine(StateRoot, "gui-last-action-name.txt");
    private static readonly string LastActionReportPath = Path.Combine(StateRoot, "gui-last-action-report.txt");
    private const int DefaultRelaySessionTtlMinutes = 5;

    private static readonly string[] ComponentFiles =
    {
        "1-Start-ADB-Server-Over-WireGuard.ps1",
        "1-Start-ADB-Server-Over-WireGuard.bat",
        "2-Run-Remote-ADB-Command.ps1",
        "2-Run-Remote-ADB-Command.bat",
        "3-Stop-ADB-Server-Over-WireGuard.ps1",
        "3-Stop-ADB-Server-Over-WireGuard.bat",
        "4-Interactive-Remote-ADB.bat",
        "5-Start-ADB-Server-As-Admin.ps1",
        "5-Start-ADB-Server-As-Admin.bat",
        "5-Start-ADB-Server-As-Admin.vbs",
        "6-Remote-ADB-Devices.bat",
        "7-Remote-ADB-Logcat.bat",
        "8-Remote-ADB-Device-Info.bat",
        "9-Remote-ADB-Shell.bat",
        "10-Run-From-Admin-Terminal.bat",
        "Invoke-ADB-WG-Wrapper.ps1",
        "Run-ADB-WG-Task.ps1",
        "Run-ADB-WireGuard-Bootstrap.ps1",
        "Start-ADB-WG-Admin.bat",
        "Start-ADB-WG-Admin.ps1",
        "README-PL.txt"
    };

    private static readonly string[] ComponentDirectories =
    {
        "platform-tools"
    };

    private readonly GitHubUpdateService _updateService = new();
    private readonly RelayApiClient _relayApiClient = new();
    private readonly Version _guiVersion;
    private GuiSettings _settings;
    private bool _isBusy;
    private bool _startupUpdateCheckCompleted;
    private string _lastStatusText = string.Empty;
    private string _lastConnectionTestText = string.Empty;
    private string _lastRemoteLogText = string.Empty;
    private string _lastRemoteUpdatedText = "Brak danych";
    private string _lastRelayLogText = string.Empty;
    private string _lastRelayUpdatedText = "Brak danych";
    private Guid? _relayOwnedSessionId;
    private string _relayOwnedServerUrl = string.Empty;
    private string _relayOwnedHostToken = string.Empty;
    private Guid? _relayLastSessionId;
    private string _relayLastPairCode = string.Empty;
    private string _relayLastRole = string.Empty;
    private string _relayLastServerUrl = string.Empty;
    private string _relayLastName = string.Empty;
    private string _relayLastSessionStatus = string.Empty;
    private string _relayLastExpiresAtText = string.Empty;
    private RelayHostTunnel? _relayHostTunnel;
    private RelayClientProxy? _relayClientProxy;

    public Form1()
    {
        InitializeComponent();

        _guiVersion = GetCurrentVersion();
        _settings = LoadSettings();
        EnsureGenericKeyNames();

        Text = AppEnvironment.AppTitle;
        packagePathTextBox.Text = PackageRoot;
        versionTextBox.Text = _guiVersion.ToString();
        serverHostTextBox.Text = _settings.RemoteServerHost ?? string.Empty;
        adbCommandTextBox.Text = string.IsNullOrWhiteSpace(_settings.RemoteAdbCommand) ? "devices" : _settings.RemoteAdbCommand;
        relayServerTextBox.Text = _settings.RelayServerUrl ?? string.Empty;
        relayNameTextBox.Text = string.IsNullOrWhiteSpace(_settings.RelayName) ? Environment.MachineName : _settings.RelayName;
        relayPairCodeTextBox.Text = _settings.RelayPairCode ?? string.Empty;

        if (string.Equals(_settings.Mode, "relay", StringComparison.OrdinalIgnoreCase))
        {
            relayModeRadioButton.Checked = true;
        }
        else if (string.Equals(_settings.Mode, "remote", StringComparison.OrdinalIgnoreCase))
        {
            remoteModeRadioButton.Checked = true;
        }
        else
        {
            localModeRadioButton.Checked = true;
        }

        Shown += Form1_Shown;
        FormClosing += Form1_FormClosing;

        RefreshModeUi();
        RefreshTimerTick(this, EventArgs.Empty);
        refreshTimer.Start();
    }

    private bool IsDirectRemoteMode => remoteModeRadioButton.Checked;
    private bool IsRelayMode => relayModeRadioButton.Checked;
    private bool IsRelayClientIntent => IsRelayMode && !string.IsNullOrWhiteSpace(relayPairCodeTextBox.Text);

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        if (_startupUpdateCheckCompleted)
        {
            return;
        }

        _startupUpdateCheckCompleted = true;
        await CheckForComponentUpdatesAsync(interactive: false);
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        DisposeRelayTransportAsync().GetAwaiter().GetResult();
        SaveSettings();
    }

    private async void primaryActionButton_Click(object sender, EventArgs e)
    {
        if (IsRelayMode)
        {
            await RunRelayActionAsync();
            return;
        }

        if (IsDirectRemoteMode)
        {
            await RunRemoteCommandAsync();
            return;
        }

        await RunScriptAsync(
            scriptPath: WrapperScriptPath,
            pendingStatus: "Uruchamianie ADB przez WireGuard...",
            actionName: "Uruchomienie serwera");
    }

    private async void stopButton_Click(object sender, EventArgs e)
    {
        if (IsRelayMode)
        {
            await CloseRelaySessionAsync();
            return;
        }

        await RunScriptAsync(
            scriptPath: StopScriptPath,
            pendingStatus: "Zatrzymywanie ADB przez WireGuard...",
            actionName: "Zatrzymanie serwera");
    }

    private void refreshButton_Click(object sender, EventArgs e)
    {
        RefreshTimerTick(this, EventArgs.Empty);
    }

    private async void updateButton_Click(object sender, EventArgs e)
    {
        await CheckForComponentUpdatesAsync(interactive: true);
    }

    private async void testConnectionButton_Click(object sender, EventArgs e)
    {
        await RunConnectionTestAsync();
    }

    private void clearLogsButton_Click(object sender, EventArgs e)
    {
        ClearLogsAndState();
    }

    private void importComponentsButton_Click(object sender, EventArgs e)
    {
        ImportComponentsFromZip();
    }

    private void importKeyButton_Click(object sender, EventArgs e)
    {
        ImportMikroTikKey();
    }

    private void settingsButton_Click(object sender, EventArgs e)
    {
        using var dialog = new SettingsDialog(
            owner: this,
            enableVoiceNotifications: _settings.EnableVoiceNotifications,
            enableSoundNotifications: _settings.EnableSoundNotifications,
            enableRouterAutomation: _settings.EnableRouterAutomation,
            relayHostToken: _settings.RelayHostToken,
            routerHost: _settings.RouterHost,
            routerPort: _settings.RouterPort,
            routerUser: _settings.RouterUser,
            routerWireGuardIp: _settings.RouterWireGuardIp,
            routerWireGuardPrefixLength: _settings.RouterWireGuardPrefixLength,
            onUpdateAsync: () => CheckForComponentUpdatesAsync(interactive: true),
            onClearLogs: ClearLogsAndState);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.EnableVoiceNotifications = dialog.EnableVoiceNotifications;
            _settings.EnableSoundNotifications = dialog.EnableSoundNotifications;
            _settings.EnableRouterAutomation = dialog.EnableRouterAutomation;
            _settings.RelayHostToken = dialog.RelayHostToken;
            _settings.RouterHost = dialog.RouterHost;
            _settings.RouterPort = dialog.RouterPort;
            _settings.RouterUser = dialog.RouterUser;
            _settings.RouterWireGuardIp = dialog.RouterWireGuardIp;
            _settings.RouterWireGuardPrefixLength = dialog.RouterWireGuardPrefixLength;
            SaveSettings();
        }
        RefreshTimerTick(this, EventArgs.Empty);
    }

    private void openStateButton_Click(object sender, EventArgs e)
    {
        OpenFolder(StateRoot);
    }

    private void openPackageButton_Click(object sender, EventArgs e)
    {
        OpenFolder(PackageRoot);
    }

    private void copyStatusButton_Click(object sender, EventArgs e)
    {
        var status = statusTextBox.Text.Trim();
        var log = logTextBox.Text.Trim();
        var payload = string.IsNullOrWhiteSpace(log) ? status : $"{status}{Environment.NewLine}{Environment.NewLine}{log}";

        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = "Brak danych statusu.";
        }

        Clipboard.SetText(payload);
        statusTextBox.Text = "Status skopiowany do schowka.";
    }

    private void ClearLogsAndState()
    {
        var answer = MessageBox.Show(
            this,
            "To usunie raporty, logi i pliki stanu z tego pakietu oraz z lokalnej paczki w AppData. Ustawienia GUI i klucze zostaną zachowane. Czy kontynuować?",
            "Wyczyść logi i stan",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var skippedFiles = new List<string>();
            foreach (var stateDirectory in GetStateDirectoriesForCleanup())
            {
                ClearStateDirectory(stateDirectory, skippedFiles);
            }
            ClearTemporaryWrapperArtifacts(skippedFiles);

            _lastConnectionTestText = string.Empty;
            _lastRemoteLogText = string.Empty;
            _lastRemoteUpdatedText = "Brak danych";
            _lastStatusText = string.Empty;

            RefreshTimerTick(this, EventArgs.Empty);

            var message = skippedFiles.Count == 0
                ? "Wyczyszczono logi i pliki stanu."
                : $"Wyczyszczono logi i pliki stanu. Pominieto zablokowane pliki:{Environment.NewLine}{string.Join(Environment.NewLine, skippedFiles)}";

            MessageBox.Show(
                this,
                message,
                "Wyczyść logi i stan",
                MessageBoxButtons.OK,
                skippedFiles.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Czyszczenie nie powiodło się: {ex.Message}",
                "Błąd czyszczenia",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void modeRadioButton_CheckedChanged(object? sender, EventArgs e)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        RefreshModeUi();
        RefreshTimerTick(this, EventArgs.Empty);
    }

    private void relayPairCodeTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!IsHandleCreated || !IsRelayMode)
        {
            return;
        }

        RefreshModeUi();
    }

    private void RefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshModeUi();

        var packageExists = Directory.Exists(PackageRoot);
        var stateExists = Directory.Exists(StateRoot);

        primaryActionButton.Enabled = !_isBusy && packageExists;
        stopButton.Enabled = !_isBusy && packageExists && (!IsDirectRemoteMode) &&
            (!IsRelayMode || _relayOwnedSessionId.HasValue || _relayClientProxy is not null);
        stopButton.Visible = !IsDirectRemoteMode && (!IsRelayMode || _relayOwnedSessionId.HasValue || _relayClientProxy is not null);
        refreshButton.Enabled = !_isBusy;
        testConnectionButton.Enabled = !_isBusy;
        updateButton.Enabled = !_isBusy && packageExists;
        importComponentsButton.Enabled = !_isBusy && packageExists;
        importKeyButton.Enabled = !_isBusy && packageExists;
        settingsButton.Enabled = !_isBusy;
        openPackageButton.Enabled = packageExists;
        openStateButton.Enabled = stateExists;
        copyStatusButton.Enabled = true;
        clearLogsButton.Enabled = !_isBusy && packageExists;
        updateButton.Visible = false;
        clearLogsButton.Visible = false;
        remoteSettingsLayoutPanel.Enabled = !_isBusy;
        relaySettingsLayoutPanel.Enabled = !_isBusy;

        if (!packageExists)
        {
            SetStatus("Nie znaleziono folderu ADB-WireGuard.");
            updatedTextBox.Text = "Brak pakietu";
            logTextBox.Text = "Nie znaleziono katalogu pakietu ADB przez WireGuard.";
            return;
        }

        if (IsDirectRemoteMode)
        {
            if (string.IsNullOrWhiteSpace(_lastStatusText))
            {
                SetStatus("Gotowe do połączenia.");
            }

            updatedTextBox.Text = _lastRemoteUpdatedText;
            logTextBox.Text = BuildRemoteLogText();
            return;
        }

        if (IsRelayMode)
        {
            if (string.IsNullOrWhiteSpace(_lastStatusText))
            {
                SetStatus(IsRelayClientIntent ? "Gotowe do dołączenia kodem." : "Gotowe do utworzenia kodu połączenia.");
            }

            updatedTextBox.Text = _lastRelayUpdatedText;
            logTextBox.Text = BuildRelayLogText();
            return;
        }

        var statusText = ReadText(StatusPath);
        if (string.IsNullOrWhiteSpace(statusText))
        {
            statusText = "Brak statusu. Kliknij przycisk główny.";
        }

        SetStatus(statusText.Trim());
        updatedTextBox.Text = GetLastUpdatedText();
        logTextBox.Text = BuildLocalLogText();
    }

    private void RefreshModeUi()
    {
        var isDirectRemoteMode = IsDirectRemoteMode;
        var isRelayMode = IsRelayMode;
        var isRelayClientMode = IsRelayClientIntent;

        modeTextBox.Text = isRelayMode
            ? "Połączenie kodem"
            : isDirectRemoteMode
                ? "Klient zdalny"
                : "Serwer lokalny";
        modeHintLabel.Text = isRelayMode
            ? isRelayClientMode
                ? "Wpisz kod połączenia z drugiego komputera i dołącz do sesji."
                : "Utwórz kod połączenia na tym komputerze i przekaż go drugiej stronie."
            : isDirectRemoteMode
                ? "Połącz z komputerem, na którym już działa ADB przez WireGuard."
                : "Włącz udostępnianie ADB na tym komputerze z telefonem podłączonym po USB.";
        primaryActionButton.Text = isRelayMode
            ? isRelayClientMode
                ? "Dołącz kodem"
                : "Utwórz kod połączenia"
            : isDirectRemoteMode
                ? "Połącz z drugim komputerem"
                : "Uruchom serwer na tym komputerze";
        testConnectionButton.Text = isRelayMode
            ? "Sprawdź serwer pośredni"
            : isDirectRemoteMode
                ? "Sprawdź połączenie z serwerem"
                : "Sprawdź serwer na tym komputerze";
        stopButton.Text = isRelayMode ? "Zamknij połączenie kodem" : "Zatrzymaj";
        remoteSettingsLayoutPanel.Visible = isDirectRemoteMode;
        relaySettingsLayoutPanel.Visible = isRelayMode;
        relayHostTokenLabel.Visible = false;
        relayHostTokenTextBox.Visible = false;

        _settings.Mode = isRelayMode ? "relay" : isDirectRemoteMode ? "remote" : "local";
    }

    private async Task RunScriptAsync(string scriptPath, string pendingStatus, string actionName)
    {
        if (!File.Exists(scriptPath))
        {
            SetStatus($"Brak skryptu: {scriptPath}");
            return;
        }

        if (string.Equals(actionName, "Uruchomienie serwera", StringComparison.OrdinalIgnoreCase) &&
            !ValidateLocalServerConfiguration())
        {
            return;
        }

        ClearWrapperArtifacts();
        FocusStatus();
        SetBusy(true);
        SetStatus(pendingStatus);
        FocusStatus();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = PackageRoot
                }
            };

            ApplyNotificationEnvironment(process.StartInfo.EnvironmentVariables);

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            SaveLastActionReport(
                actionName,
                process.ExitCode,
                await stdoutTask,
                await stderrTask);
        }
        catch (Exception ex)
        {
            SaveLastActionReport(
                actionName,
                exitCode: -1,
                standardOutput: string.Empty,
                standardError: ex.ToString());
            SetStatus($"Błąd: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
            FocusStatus();
        }
    }

    private async Task RunRemoteCommandAsync()
    {
        if (!File.Exists(RemoteCommandScriptPath))
        {
            SetStatus("Nie znaleziono skryptu połączenia zdalnego.");
            return;
        }

        var serverHost = serverHostTextBox.Text.Trim();
        var adbCommand = adbCommandTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(serverHost))
        {
            SetStatus("Podaj adres serwera zdalnego.");
            serverHostTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(adbCommand))
        {
            adbCommand = "devices";
            adbCommandTextBox.Text = adbCommand;
        }

        _settings.RemoteServerHost = serverHost;
        _settings.RemoteAdbCommand = adbCommand;
        SaveSettings();

        FocusStatus();
        SetBusy(true);
        SetStatus($"Łączenie z {serverHost}...");
        _lastRemoteLogText = string.Empty;
        _lastRemoteUpdatedText = "Trwa połączenie...";
        RefreshTimerTick(this, EventArgs.Empty);
        FocusStatus();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{RemoteCommandScriptPath}\" -ServerHost \"{serverHost}\" -AdbCommand \"{adbCommand.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = PackageRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            ApplyNotificationEnvironment(process.StartInfo.EnvironmentVariables);

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            _lastRemoteLogText = BuildRemoteResultText(serverHost, adbCommand, stdout, stderr, process.ExitCode);
            _lastRemoteUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (process.ExitCode == 0)
            {
                SetStatus($"Połączono z {serverHost}.");
            }
            else
            {
                SetStatus($"Nie udało się połączyć z {serverHost}.");
            }
        }
        catch (Exception ex)
        {
            _lastRemoteUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _lastRemoteLogText = BuildRemoteResultText(serverHost, adbCommand, string.Empty, ex.ToString(), 1);
            SetStatus($"Nie udało się uruchomić połączenia: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
            FocusStatus();
        }
    }

    private async Task EnsureLocalAdbReadyForRelayHostAsync()
    {
        var startScriptPath = Path.Combine(PackageRoot, "1-Start-ADB-Server-Over-WireGuard.ps1");
        if (!File.Exists(startScriptPath))
        {
            throw new InvalidOperationException("Nie znaleziono skryptu startu lokalnego ADB dla trybu połączenia kodem.");
        }

        var result = await RunPowerShellScriptAsync(
            startScriptPath,
            $"-SkipFirewallRule -SkipMikroTikForward -Port 5037",
            CancellationToken.None);

        if (result.ExitCode != 0)
        {
            var details = BuildRemoteResultText("127.0.0.1", "relay-host-start", result.Stdout, result.Stderr, result.ExitCode);
            throw new InvalidOperationException($"Nie udało się uruchomić lokalnego ADB dla hosta połączenia kodem.{Environment.NewLine}{details}");
        }
    }

    private async Task<CommandExecutionResult> RunRelayClientCommandAsync(int localPort, string adbCommand)
    {
        return await RunPowerShellScriptAsync(
            RemoteCommandScriptPath,
            $"-ServerHost \"127.0.0.1\" -Port {localPort} -AdbCommand \"{adbCommand.Replace("\"", "\\\"")}\"",
            CancellationToken.None);
    }

    private async Task<CommandExecutionResult> RunPowerShellScriptAsync(string scriptPath, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = PackageRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        ApplyNotificationEnvironment(process.StartInfo.EnvironmentVariables);

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        return new CommandExecutionResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private async Task DisposeRelayTransportAsync()
    {
        if (_relayClientProxy is not null)
        {
            await _relayClientProxy.DisposeAsync();
            _relayClientProxy = null;
        }

        if (_relayHostTunnel is not null)
        {
            await _relayHostTunnel.DisposeAsync();
            _relayHostTunnel = null;
        }
    }

    private void AppendRelayRuntimeMessage(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        void Update()
        {
            _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var current = _lastRelayLogText?.TrimEnd() ?? string.Empty;
            _lastRelayLogText = string.IsNullOrWhiteSpace(current)
                ? message.Trim()
                : $"{current}{Environment.NewLine}{Environment.NewLine}{message.Trim()}";
            _relayLastSessionStatus = message;
            RefreshTimerTick(this, EventArgs.Empty);
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(Update));
            return;
        }

        Update();
    }

    private async Task RunRelayActionAsync()
    {
        var relayServerUrl = RelayApiClient.NormalizeServerUrl(relayServerTextBox.Text);
        if (string.IsNullOrWhiteSpace(relayServerUrl))
        {
            SetStatus("Podaj adres serwera pośredniego.");
            relayServerTextBox.Focus();
            return;
        }

        var pairCode = relayPairCodeTextBox.Text.Trim().ToUpperInvariant();
        var relayName = relayNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(relayName))
        {
            relayName = Environment.MachineName;
            relayNameTextBox.Text = relayName;
        }

        relayServerTextBox.Text = relayServerUrl;

        _settings.RelayServerUrl = relayServerUrl;
        _settings.RelayName = relayName;
        _settings.RelayPairCode = pairCode;

        if (string.IsNullOrWhiteSpace(pairCode))
        {
            var hostToken = _settings.RelayHostToken?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(hostToken))
            {
                SetStatus("Brak tokenu tworzenia kodu. Uzupełnij go w ustawieniach.");
                return;
            }

            _settings.RelayHostToken = hostToken;
        }

        SaveSettings();

        FocusStatus();
        SetBusy(true);
        SetStatus(string.IsNullOrWhiteSpace(pairCode)
            ? "Tworzenie kodu połączenia..."
            : $"Dołączanie kodem {pairCode}...");
        _lastRelayUpdatedText = "Trwa połączenie...";
        _lastRelayLogText = string.Empty;
        RefreshTimerTick(this, EventArgs.Empty);
        FocusStatus();

        try
        {
            if (string.IsNullOrWhiteSpace(pairCode))
            {
                await EnsureLocalAdbReadyForRelayHostAsync();

                var hostToken = _settings.RelayHostToken?.Trim() ?? string.Empty;
                var response = await _relayApiClient.CreateSessionAsync(
                    relayServerUrl,
                    hostToken,
                    relayName,
                    DefaultRelaySessionTtlMinutes,
                    CancellationToken.None);

                RelaySessionStatusResponse? status = null;
                try
                {
                    status = await _relayApiClient.GetSessionStatusAsync(
                        relayServerUrl,
                        response.SessionId,
                        hostToken,
                        CancellationToken.None);
                }
                catch
                {
                    // Best effort only.
                }

                await DisposeRelayTransportAsync();
                _relayHostTunnel = new RelayHostTunnel(
                    _relayApiClient,
                    relayServerUrl,
                    response.SessionId,
                    response.HostConnectToken,
                    response.HostResumeToken,
                    response.HeartbeatIntervalSeconds,
                    AppendRelayRuntimeMessage);
                _relayHostTunnel.Start();

                _relayOwnedSessionId = response.SessionId;
                _relayOwnedServerUrl = relayServerUrl;
                _relayOwnedHostToken = hostToken;
                _relayLastSessionId = response.SessionId;
                _relayLastPairCode = response.PairCode;
                _relayLastRole = "host";
                _relayLastServerUrl = relayServerUrl;
                _relayLastName = relayName;
                _relayLastSessionStatus = status?.Status ?? "pending-host";
                _relayLastExpiresAtText = response.ExpiresAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _lastRelayLogText = BuildRelayHostResultText(
                    relayServerUrl,
                    relayName,
                    response,
                    status,
                    "Kanał hosta jest aktywny. Druga strona może dołączyć kodem połączenia.");

                SetStatus($"Kod połączenia gotowy: {response.PairCode}");
            }
            else
            {
                var response = await _relayApiClient.ClaimSessionAsync(
                    relayServerUrl,
                    pairCode,
                    relayName,
                    CancellationToken.None);

                await DisposeRelayTransportAsync();
                var adbCommand = NormalizeRemoteCommand();
                _relayClientProxy = new RelayClientProxy(
                    _relayApiClient,
                    relayServerUrl,
                    response.SessionId,
                    response.ClientConnectToken,
                    response.ClientResumeToken,
                    response.HeartbeatIntervalSeconds,
                    AppendRelayRuntimeMessage);
                _relayClientProxy.Start();
                await Task.Delay(300);

                var commandResult = await RunRelayClientCommandAsync(_relayClientProxy.LocalPort, adbCommand);

                _relayLastSessionId = response.SessionId;
                _relayLastPairCode = pairCode;
                _relayLastRole = "client";
                _relayLastServerUrl = relayServerUrl;
                _relayLastName = relayName;
                _relayLastSessionStatus = "claimed";
                _relayLastExpiresAtText = response.ExpiresAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _lastRelayLogText = BuildRelayClientResultText(
                    relayServerUrl,
                    relayName,
                    pairCode,
                    response,
                    _relayClientProxy.LocalPort,
                    adbCommand,
                    commandResult.Stdout,
                    commandResult.Stderr,
                    commandResult.ExitCode);

                serverHostTextBox.Text = "127.0.0.1";
                adbCommandTextBox.Text = adbCommand;
                _settings.RemoteServerHost = "127.0.0.1";
                _settings.RemoteAdbCommand = adbCommand;
                SaveSettings();

                SetStatus(commandResult.ExitCode == 0
                    ? $"Połączenie kodem działa. Lokalny port: {_relayClientProxy.LocalPort}"
                    : $"Połączenie kodem zostało zestawione, ale komenda ADB zwróciła błąd na porcie {_relayClientProxy.LocalPort}.");
            }
        }
        catch (Exception ex)
        {
            _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _lastRelayLogText = BuildRelayErrorText(
                relayServerUrl,
                relayName,
                pairCode,
                ex);
            SetStatus($"Połączenie kodem: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
            FocusStatus();
        }
    }

    private async Task CloseRelaySessionAsync()
    {
        if (!_relayOwnedSessionId.HasValue &&
            _relayClientProxy is null &&
            _relayHostTunnel is null)
        {
            SetStatus("Brak aktywnego połączenia kodem na tym komputerze.");
            return;
        }

        FocusStatus();
        SetBusy(true);
        SetStatus("Zamykanie połączenia kodem...");
        var serverUrlForError = _relayOwnedServerUrl;

        try
        {
            var sessionId = _relayOwnedSessionId;
            var serverUrl = _relayOwnedServerUrl;

            if (sessionId.HasValue &&
                !string.IsNullOrWhiteSpace(serverUrl) &&
                !string.IsNullOrWhiteSpace(_relayOwnedHostToken))
            {
                await _relayApiClient.CloseSessionAsync(
                    serverUrl,
                    sessionId.Value,
                    _relayOwnedHostToken,
                    CancellationToken.None);
            }

            await DisposeRelayTransportAsync();

            _relayOwnedSessionId = null;
            _relayOwnedHostToken = string.Empty;
            _relayOwnedServerUrl = string.Empty;
            _relayLastSessionStatus = "closed";
            _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _lastRelayLogText = BuildRelayClosedText(sessionId ?? Guid.Empty, serverUrl);
            SetStatus("Połączenie kodem zostało zamknięte.");
        }
        catch (Exception ex)
        {
            _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _lastRelayLogText = BuildRelayErrorText(serverUrlForError, _relayLastName, _relayLastPairCode, ex);
            SetStatus($"Nie udało się zamknąć połączenia kodem: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
            FocusStatus();
        }
    }

    private async Task RunConnectionTestAsync()
    {
        if (IsRelayMode)
        {
            if (_relayClientProxy is not null)
            {
                var localPort = _relayClientProxy.LocalPort;
                SetBusy(true);
                SetStatus($"Sprawdzanie lokalnego portu połączenia kodem {localPort}...");

                try
                {
                    var tcpOk = await TestTcpAsync("127.0.0.1", localPort);
                    _lastConnectionTestText = string.Join(
                        Environment.NewLine,
                        new[]
                        {
                            "Test klienta połączenia kodem",
                            "Adres: 127.0.0.1",
                            $"Port loopback: {localPort}",
                            $"TCP {localPort}: {(tcpOk ? "OK" : "brak połączenia")}"
                        });
                _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus(tcpOk
                    ? $"Lokalny port połączenia kodem odpowiada na porcie {localPort}."
                    : $"Lokalny port połączenia kodem nie odpowiada na porcie {localPort}.");
                }
                catch (Exception ex)
                {
                    _lastConnectionTestText = $"Test klienta połączenia kodem{Environment.NewLine}{ex}";
                    _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    SetStatus($"Błąd testu klienta połączenia kodem: {ex.Message}");
                }
                finally
                {
                    SetBusy(false);
                    RefreshTimerTick(this, EventArgs.Empty);
                    FocusStatus();
                }

                return;
            }

            var relayServerUrl = RelayApiClient.NormalizeServerUrl(relayServerTextBox.Text);
            if (string.IsNullOrWhiteSpace(relayServerUrl))
            {
                SetStatus("Podaj adres serwera pośredniego.");
                relayServerTextBox.Focus();
                return;
            }

            SetBusy(true);
            SetStatus($"Sprawdzanie serwera pośredniego {relayServerUrl}...");

            try
            {
                var response = await _relayApiClient.GetHealthAsync(relayServerUrl, CancellationToken.None);
                _lastConnectionTestText = string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        "Test serwera pośredniego",
                        $"Adres: {relayServerUrl}",
                        $"Service: {response.Service}",
                        $"Health: {(response.Ok ? "OK" : "blad")}",
                        $"Skonfigurowane tokeny hosta: {response.HostTokensConfigured}"
                    });
                _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus(response.Ok
                    ? $"Serwer pośredni {relayServerUrl} odpowiada."
                    : $"Serwer pośredni {relayServerUrl} zwrócił błąd.");
            }
            catch (Exception ex)
            {
                _lastConnectionTestText = $"Test serwera pośredniego{Environment.NewLine}{ex}";
                _lastRelayUpdatedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SetStatus($"Błąd testu serwera pośredniego: {ex.Message}");
            }
            finally
            {
                SetBusy(false);
                RefreshTimerTick(this, EventArgs.Empty);
                FocusStatus();
            }

            return;
        }

        var targetHost = IsDirectRemoteMode ? serverHostTextBox.Text.Trim() : "127.0.0.1";
        var port = 5037;

        if (string.IsNullOrWhiteSpace(targetHost))
        {
            SetStatus("Podaj adres serwera zdalnego.");
            serverHostTextBox.Focus();
            return;
        }

        SetBusy(true);
        SetStatus(IsDirectRemoteMode
            ? $"Sprawdzanie połączenia z {targetHost}:{port}..."
            : $"Sprawdzanie lokalnego serwera na porcie {port}...");

        try
        {
            var pingOk = await TestPingAsync(targetHost);
            var tcpOk = await TestTcpAsync(targetHost, port);
            var title = IsDirectRemoteMode ? "Test połączenia zdalnego" : "Test serwera lokalnego";
            var lines = new List<string>
            {
                title,
                $"Adres: {targetHost}",
                $"Port: {port}",
                $"Ping: {(pingOk ? "OK" : "brak odpowiedzi")}",
                $"TCP {port}: {(tcpOk ? "OK" : "brak połączenia")}"
            };

            _lastConnectionTestText = string.Join(Environment.NewLine, lines);

            if (tcpOk)
            {
                SetStatus(IsDirectRemoteMode
                    ? $"Połączenie z {targetHost}:{port} działa."
                    : $"Lokalny serwer odpowiada na porcie {port}.");
            }
            else
            {
                SetStatus(IsDirectRemoteMode
                    ? $"Brak połączenia z {targetHost}:{port}."
                    : $"Lokalny serwer nie odpowiada na porcie {port}.");
            }
        }
        catch (Exception ex)
        {
            _lastConnectionTestText = $"Test połączenia{Environment.NewLine}{ex}";
            SetStatus($"Błąd testu połączenia: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
            FocusStatus();
        }
    }

    private async Task CheckForComponentUpdatesAsync(bool interactive)
    {
        if (!Directory.Exists(PackageRoot))
        {
            if (interactive)
            {
                MessageBox.Show(
                    this,
                    "Nie znaleziono folderu ADB-WireGuard.",
                    "Aktualizacja komponentów",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        SetBusy(true);

        try
        {
            var releaseInfo = await _updateService.GetLatestComponentsReleaseAsync(CancellationToken.None);
            if (releaseInfo is null)
            {
                if (interactive)
                {
                    MessageBox.Show(
                        this,
                        "Nie znaleziono paczki komponentów w GitHub Releases.",
                        "Aktualizacja komponentów",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            var installedVersion = ReadInstalledComponentsVersion();
            if (installedVersion is not null && releaseInfo.Version <= installedVersion)
            {
                if (interactive)
                {
                    MessageBox.Show(
                        this,
                        $"Komponenty są aktualne ({installedVersion}).",
                        "Aktualizacja komponentów",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            if (!interactive && installedVersion is null)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"Dostępna jest nowa paczka komponentów {releaseInfo.Version}. Zaktualizować?",
                "Aktualizacja komponentów",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            SetStatus($"Pobieranie komponentów {releaseInfo.Version}...");
            var downloadedArchive = await _updateService.DownloadReleaseAssetAsync(releaseInfo, CancellationToken.None);
            ApplyComponentUpdate(downloadedArchive, releaseInfo.Version.ToString());

            MessageBox.Show(
                this,
                $"Komponenty zaktualizowano do wersji {releaseInfo.Version}.",
                "Aktualizacja komponentów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            if (interactive)
            {
                MessageBox.Show(
                    this,
                    $"Aktualizacja nie powiodła się: {ex.Message}",
                    "Błąd aktualizacji",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
        }
    }

    private void ImportComponentsFromZip()
    {
        if (!Directory.Exists(PackageRoot))
        {
            MessageBox.Show(
                this,
                "Nie znaleziono folderu ADB-WireGuard.",
                "Import komponentów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz lokalną paczkę komponentów ADB-WireGuard",
            Filter = "Paczki ZIP (*.zip)|*.zip|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true);

        try
        {
            var versionLabel = $"import-lokalny {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            ApplyComponentUpdate(dialog.FileName, versionLabel);

            MessageBox.Show(
                this,
                "Zaimportowano komponenty z pliku ZIP.",
                "Import komponentów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Import nie powiódł się: {ex.Message}",
                "Błąd importu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshTimerTick(this, EventArgs.Empty);
        }
    }

    private void ImportMikroTikKey()
    {
        if (!Directory.Exists(PackageRoot))
        {
            MessageBox.Show(
                this,
                "Nie znaleziono folderu ADB-WireGuard.",
                "Import klucza",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz klucz prywatny MikroTika",
            Filter = "Klucze prywatne i OpenSSH|*.key;*.pem;*.ppk;*|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var (privateKeySource, publicKeySource) = ResolveKeySources(dialog.FileName);
            Directory.CreateDirectory(MikroTikRoot);
            Directory.CreateDirectory(PersistentMikroTikRoot);

            File.Copy(privateKeySource, MikroTikPrivateKeyPath, overwrite: true);
            File.Copy(privateKeySource, PersistentMikroTikPrivateKeyPath, overwrite: true);

            var importedPublicKey = false;
            if (!string.IsNullOrWhiteSpace(publicKeySource) && File.Exists(publicKeySource))
            {
                File.Copy(publicKeySource, MikroTikPublicKeyPath, overwrite: true);
                File.Copy(publicKeySource, PersistentMikroTikPublicKeyPath, overwrite: true);
                importedPublicKey = true;
            }

            SetStatus(importedPublicKey
                ? "Zaimportowano klucz prywatny i publiczny MikroTika."
                : "Zaimportowano klucz prywatny MikroTika.");

            MessageBox.Show(
                this,
                importedPublicKey
                    ? "Zaimportowano klucz prywatny i publiczny MikroTika."
                    : "Zaimportowano klucz prywatny MikroTika. Pliku .pub nie znaleziono obok wskazanego klucza.",
                "Import klucza",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Import klucza nie powiódł się: {ex.Message}",
                "Błąd importu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            RefreshTimerTick(this, EventArgs.Empty);
        }
    }

    private void ApplyComponentUpdate(string archivePath, string versionLabel)
    {
        var extractRoot = Path.Combine(Path.GetTempPath(), "AdbWireGuardGui", "extract", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(extractRoot))
        {
            Directory.Delete(extractRoot, recursive: true);
        }

        Directory.CreateDirectory(extractRoot);
        ZipFile.ExtractToDirectory(archivePath, extractRoot, overwriteFiles: true);

        var packageSourceRoot = FindPackageSourceRoot(extractRoot);
        if (packageSourceRoot is null)
        {
            throw new InvalidOperationException("Paczka komponentów nie zawiera pliku 1-Start-ADB-Server-Over-WireGuard.ps1.");
        }

        foreach (var fileName in ComponentFiles)
        {
            var sourcePath = Path.Combine(packageSourceRoot, fileName);
            var destinationPath = Path.Combine(PackageRoot, fileName);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        foreach (var directoryName in ComponentDirectories)
        {
            var sourceDirectory = Path.Combine(packageSourceRoot, directoryName);
            var destinationDirectory = Path.Combine(PackageRoot, directoryName);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            CopyDirectory(sourceDirectory, destinationDirectory);
        }

        Directory.CreateDirectory(StateRoot);
        File.WriteAllText(ComponentsVersionPath, versionLabel, Encoding.UTF8);
    }

    private static (string PrivateKeySource, string? PublicKeySource) ResolveKeySources(string selectedPath)
    {
        if (selectedPath.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
        {
            var privateCandidate = selectedPath[..^4];
            if (!File.Exists(privateCandidate))
            {
                throw new InvalidOperationException("Wybrano plik .pub, ale obok nie ma odpowiadającego klucza prywatnego.");
            }

            return (privateCandidate, selectedPath);
        }

        var publicCandidate = $"{selectedPath}.pub";
        return File.Exists(publicCandidate)
            ? (selectedPath, publicCandidate)
            : (selectedPath, null);
    }

    private static string? FindPackageSourceRoot(string extractRoot)
    {
        if (File.Exists(Path.Combine(extractRoot, "1-Start-ADB-Server-Over-WireGuard.ps1")))
        {
            return extractRoot;
        }

        var candidate = Directory
            .EnumerateFiles(extractRoot, "1-Start-ADB-Server-Over-WireGuard.ps1", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        return candidate;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: true);
        }

        foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
        {
            var destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(subDirectory));
            CopyDirectory(subDirectory, destinationSubDirectory);
        }
    }

    private Version? ReadInstalledComponentsVersion()
    {
        var text = ReadText(ComponentsVersionPath).Trim();
        return Version.TryParse(text, out var version) ? version : null;
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        UseWaitCursor = isBusy;
    }

    private void SetStatus(string text)
    {
        statusTextBox.Text = text;
        _lastStatusText = text;
    }

    private void FocusStatus()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || !statusTextBox.CanFocus)
            {
                return;
            }

            ActiveControl = statusTextBox;
            statusTextBox.SelectionLength = 0;
            statusTextBox.SelectionStart = 0;
        }));
    }

    private static Version GetCurrentVersion()
    {
        var versionText = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0];

        return Version.TryParse(versionText, out var version)
            ? version
            : new Version(1, 1, 0);
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> GetStateDirectoriesForCleanup()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        foreach (var candidate in new[]
        {
            StateRoot,
            Path.Combine(AppContext.BaseDirectory, "ADB-WireGuard", "state"),
            string.IsNullOrWhiteSpace(localAppData) ? string.Empty : Path.Combine(localAppData, "ADB-WireGuard", "package", "state")
        })
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
            {
                continue;
            }

            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static void ClearStateDirectory(string stateDirectory, List<string> skippedFiles)
    {
        var keepFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "components-version.txt",
            "mikrotik_known_hosts"
        };

        foreach (var filePath in Directory.GetFiles(stateDirectory))
        {
            var fileName = Path.GetFileName(filePath);
            if (keepFileNames.Contains(fileName))
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
                skippedFiles.Add(filePath);
            }
            catch (UnauthorizedAccessException)
            {
                skippedFiles.Add(filePath);
            }
        }
    }

    private static void ClearWrapperArtifacts()
    {
        var skippedFiles = new List<string>();
        foreach (var stateDirectory in GetStateDirectoriesForCleanup())
        {
            foreach (var fileName in new[]
            {
                "5-wrapper-report.txt",
                "wrapper-fallback-forward-info.txt",
                "mikrotik-last-command.txt"
            })
            {
                var filePath = Path.Combine(stateDirectory, fileName);
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch (IOException)
                {
                    skippedFiles.Add(filePath);
                }
                catch (UnauthorizedAccessException)
                {
                    skippedFiles.Add(filePath);
                }
            }
        }

        ClearTemporaryWrapperArtifacts(skippedFiles);
    }

    private static void ClearTemporaryWrapperArtifacts(List<string> skippedFiles)
    {
        var tempPath = Path.GetTempPath();
        if (string.IsNullOrWhiteSpace(tempPath) || !Directory.Exists(tempPath))
        {
            return;
        }

        foreach (var pattern in new[]
        {
            "adbwg-mt-*-out.txt",
            "adbwg-mt-*-err.txt",
            "adbwg-mikrotik-*"
        })
        {
            foreach (var filePath in Directory.GetFiles(tempPath, pattern))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                    skippedFiles.Add(filePath);
                }
                catch (UnauthorizedAccessException)
                {
                    skippedFiles.Add(filePath);
                }
            }
        }
    }

    private static string GetLastUpdatedText()
    {
        var candidates = new[]
        {
            StatusPath,
            ReportPath,
            ErrorPath,
            WrapperReportPath,
            ComponentsVersionPath,
            LastActionReportPath
        };

        var existing = candidates
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTime)
            .FirstOrDefault();

        return existing is null
            ? "Brak danych"
            : existing.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private string BuildLocalLogText()
    {
        var sections = new List<string>();

        var lastActionName = ReadText(LastActionNamePath).Trim();
        if (string.IsNullOrWhiteSpace(lastActionName))
        {
            lastActionName = "Brak danych";
        }

        AddSection(sections, $"Raport ostatniej akcji: {lastActionName}", ReadText(LastActionReportPath));
        AddSection(sections, "Ostatni raport uruchomienia", ReadText(ReportPath));
        AddSection(sections, "Ostatni błąd uruchomienia", ReadText(ErrorPath));
        AddSection(sections, "Raport wrappera", ReadText(WrapperReportPath));
        AddSection(sections, "Diagnostyka MikroTik", ReadText(MikroTikDiagnosticsPath));
        AddSection(sections, "Ostatnia komenda MikroTik", ReadText(MikroTikLastCommandPath));
        AddSection(sections, "Historia komend MikroTik", ReadText(MikroTikCommandHistoryPath));
        AddSection(sections, "Test połączenia", _lastConnectionTestText);

        var componentsVersion = ReadText(ComponentsVersionPath).Trim();
        if (!string.IsNullOrWhiteSpace(componentsVersion))
        {
            AddSection(sections, "Wersja komponentów", componentsVersion);
        }

        if (sections.Count == 0)
        {
            return "Brak raportów.";
        }

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            sections);
    }

    private static void SaveLastActionReport(string actionName, int exitCode, string standardOutput, string standardError)
    {
        Directory.CreateDirectory(StateRoot);
        File.WriteAllText(LastActionNamePath, actionName, Encoding.UTF8);

        var lines = new List<string>
        {
            $"Akcja: {actionName}",
            $"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Kod zakończenia: {exitCode}"
        };

        var stdout = SanitizeActionText(actionName, standardOutput);
        var stderr = SanitizeActionText(actionName, standardError);

        if (string.Equals(actionName, "Uruchomienie serwera", StringComparison.OrdinalIgnoreCase))
        {
            var startSnapshot = BuildStartActionSnapshot();
            if (!string.IsNullOrWhiteSpace(startSnapshot))
            {
                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    stdout = $"{stdout}{Environment.NewLine}{Environment.NewLine}{startSnapshot}";
                }
                else
                {
                    stdout = startSnapshot;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            lines.Add(string.Empty);
            lines.Add("Standard output");
            lines.Add(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            lines.Add(string.Empty);
            lines.Add("Standard error");
            lines.Add(stderr);
        }

        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            lines.Add(string.Empty);
            lines.Add("Brak tekstowego raportu z tej akcji.");
        }

        File.WriteAllText(LastActionReportPath, string.Join(Environment.NewLine, lines), Encoding.UTF8);
    }

    private static string SanitizeActionText(string actionName, string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!string.Equals(actionName, "Zatrzymanie serwera", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var filteredLines = value
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line =>
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return false;
                }

                return !trimmed.StartsWith("kex_exchange_identification:", StringComparison.OrdinalIgnoreCase) &&
                       !trimmed.StartsWith("Connection reset by ", StringComparison.OrdinalIgnoreCase) &&
                       !trimmed.StartsWith("Connection closed by ", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        return string.Join(Environment.NewLine, filteredLines).Trim();
    }

    private static string BuildStartActionSnapshot()
    {
        var sections = new List<string>();
        AddSection(sections, "Raport uruchomienia", ReadText(ReportPath));
        AddSection(sections, "Błąd uruchomienia", ReadText(ErrorPath));
        AddSection(sections, "Raport wrappera", ReadText(WrapperReportPath));
        return sections.Count == 0
            ? string.Empty
            : string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private string BuildRemoteLogText()
    {
        var sections = new List<string>();

        AddSection(sections, "Test połączenia", _lastConnectionTestText);

        if (!string.IsNullOrWhiteSpace(_lastRemoteLogText))
        {
            AddSection(sections, "Wynik polecenia", _lastRemoteLogText);
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
        }

        AddSection(
            sections,
            "Tryb zdalny",
            string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Serwer: {serverHostTextBox.Text.Trim()}",
                    $"Polecenie: {NormalizeRemoteCommand()}",
                    string.Empty,
                    "Kliknij główny przycisk, aby połączyć się z drugim komputerem."
                }));

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private string BuildRelayLogText()
    {
        var sections = new List<string>();

        AddSection(sections, "Test połączenia", _lastConnectionTestText);

        if (!string.IsNullOrWhiteSpace(_lastRelayLogText))
        {
            AddSection(sections, "Połączenie kodem", _lastRelayLogText);
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
        }

        var intentText = IsRelayClientIntent
            ? "Tryb klienta: wpisz kod połączenia i kliknij główny przycisk."
            : "Tryb hosta: zostaw pole kodu puste i kliknij główny przycisk, aby utworzyć kod połączenia.";

        AddSection(
            sections,
            "Tryb połączenia kodem",
            string.Join(
                Environment.NewLine,
                new[]
                {
                    $"Serwer: {relayServerTextBox.Text.Trim()}",
                    $"Nazwa: {relayNameTextBox.Text.Trim()}",
                    $"Kod połączenia: {relayPairCodeTextBox.Text.Trim()}",
                    $"Polecenie ADB: {NormalizeRemoteCommand()}",
                    $"Kanał hosta: {(_relayHostTunnel is null ? "nieaktywny" : "aktywny")}",
                    $"Lokalny port klienta: {(_relayClientProxy is null ? "nieaktywny" : $"127.0.0.1:{_relayClientProxy.LocalPort}")}",
                    string.Empty,
                    intentText,
                    "Host utrzymuje kanał do lokalnego ADB, a klient wystawia lokalny port loopback i może uruchamiać polecenia ADB przez serwer pośredni."
                }));

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string BuildRemoteResultText(string serverHost, string adbCommand, string stdout, string stderr, int exitCode)
    {
        var sections = new List<string>
        {
            $"Połączenie zdalne{Environment.NewLine}Serwer: {serverHost}{Environment.NewLine}Polecenie: {adbCommand}{Environment.NewLine}Kod zakończenia: {exitCode}"
        };

        AddSection(sections, "Standard output", stdout);
        AddSection(sections, "Standard error", stderr);

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            sections);
    }

    private static string BuildRelayHostResultText(
        string relayServerUrl,
        string relayName,
        RelayCreateSessionResponse response,
        RelaySessionStatusResponse? status,
        string runtimeStatus)
    {
        var sections = new List<string>
        {
            string.Join(
                Environment.NewLine,
                new[]
                {
                    "Kod połączenia został utworzony",
                    $"Serwer: {relayServerUrl}",
                    $"Rola: host",
                    $"Nazwa: {relayName}",
                    $"Kod połączenia: {response.PairCode}",
                    $"SessionId: {response.SessionId}",
                    $"Wygasa: {response.ExpiresAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                    $"Grace reconnect: {response.ReconnectGraceSeconds}s",
                    $"Heartbeat: co {response.HeartbeatIntervalSeconds}s",
                    $"Status API: {status?.Status ?? "brak potwierdzenia"}",
                    $"Host connected: {status?.HostConnected.ToString() ?? "brak danych"}",
                    $"Client connected: {status?.ClientConnected.ToString() ?? "brak danych"}",
                    string.Empty,
                    runtimeStatus,
                    "Przekaż kod połączenia drugiej stronie."
                })
        };

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string BuildRelayClientResultText(
        string relayServerUrl,
        string relayName,
        string pairCode,
        RelayClaimSessionResponse response,
        int localPort,
        string adbCommand,
        string stdout,
        string stderr,
        int exitCode)
    {
        var sections = new List<string>
        {
            string.Join(
                Environment.NewLine,
                new[]
                {
                    "Dołączenie kodem zakończone",
                    $"Serwer: {relayServerUrl}",
                    $"Rola: client",
                    $"Nazwa: {relayName}",
                    $"Kod połączenia: {pairCode}",
                    $"SessionId: {response.SessionId}",
                    $"Wygasa: {response.ExpiresAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                    $"Grace reconnect: {response.ReconnectGraceSeconds}s",
                    $"Heartbeat: co {response.HeartbeatIntervalSeconds}s",
                    $"Lokalny port połączenia: {localPort}",
                    $"Polecenie ADB: {adbCommand}",
                    $"Kod zakonczenia ADB: {exitCode}",
                    string.Empty,
                    "Połączenie kodem zostało zestawione przez lokalny port loopback 127.0.0.1."
                })
        };

        AddSection(sections, "ADB standard output", stdout);
        AddSection(sections, "ADB standard error", stderr);

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string BuildRelayClosedText(Guid sessionId, string relayServerUrl)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "Połączenie kodem zostało zamknięte",
                $"Serwer: {relayServerUrl}",
                $"SessionId: {sessionId}"
            });
    }

    private static string BuildRelayErrorText(string relayServerUrl, string relayName, string pairCode, Exception ex)
    {
        var sections = new List<string>
        {
            string.Join(
                Environment.NewLine,
                new[]
                {
                    "Błąd połączenia kodem",
                    $"Serwer: {relayServerUrl}",
                    $"Nazwa: {relayName}",
                    $"Kod połączenia: {pairCode}"
                })
        };

        AddSection(sections, "Szczegóły", ex.Message);
        AddSection(sections, "Diagnostyka", ex.ToString());
        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private string NormalizeRemoteCommand()
    {
        var command = adbCommandTextBox.Text.Trim();
        return string.IsNullOrWhiteSpace(command) ? "devices" : command;
    }

    private static void AddSection(List<string> sections, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        sections.Add($"{title}{Environment.NewLine}{content.Trim()}");
    }

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static async Task<bool> TestTcpAsync(string host, int port, int timeoutMs = 3000)
    {
        using var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TestPingAsync(string host, int timeoutMs = 1500)
    {
        if (string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static GuiSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(AppEnvironment.SettingsPath))
            {
                return new GuiSettings();
            }

            var json = File.ReadAllText(AppEnvironment.SettingsPath, Encoding.UTF8);
            return NormalizeSettings(JsonSerializer.Deserialize<GuiSettings>(json) ?? new GuiSettings());
        }
        catch
        {
            return new GuiSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Mode = IsRelayMode ? "relay" : IsDirectRemoteMode ? "remote" : "local";
            _settings.RemoteServerHost = serverHostTextBox.Text.Trim();
            _settings.RemoteAdbCommand = NormalizeRemoteCommand();
            _settings.RelayServerUrl = RelayApiClient.NormalizeServerUrl(relayServerTextBox.Text);
            _settings.RelayName = relayNameTextBox.Text.Trim();
            _settings.RelayPairCode = relayPairCodeTextBox.Text.Trim().ToUpperInvariant();

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(AppEnvironment.SettingsPath, json, Encoding.UTF8);
        }
        catch
        {
            // Settings persistence is best-effort only.
        }
    }

    private void ApplyNotificationEnvironment(StringDictionary environmentVariables)
    {
        environmentVariables["ADB_WG_ENABLE_VOICE"] = _settings.EnableVoiceNotifications ? "1" : "0";
        environmentVariables["ADB_WG_ENABLE_SOUND"] = _settings.EnableSoundNotifications ? "1" : "0";
        environmentVariables["ADB_WG_ENABLE_ROUTER_AUTO"] = _settings.EnableRouterAutomation ? "1" : "0";

        SetEnvironmentValue(environmentVariables, "ADB_WG_ROUTER_HOST", _settings.RouterHost);
        SetEnvironmentValue(environmentVariables, "ADB_WG_ROUTER_PORT", _settings.RouterPort > 0 ? _settings.RouterPort.ToString() : string.Empty);
        SetEnvironmentValue(environmentVariables, "ADB_WG_ROUTER_USER", _settings.RouterUser);
        SetEnvironmentValue(environmentVariables, "ADB_WG_ROUTER_WG_IP", _settings.RouterWireGuardIp);
        SetEnvironmentValue(environmentVariables, "ADB_WG_ROUTER_WG_PREFIX", _settings.RouterWireGuardPrefixLength is >= 1 and <= 32
            ? _settings.RouterWireGuardPrefixLength.ToString()
            : string.Empty);
    }

    private sealed class GuiSettings
    {
        public string Mode { get; set; } = "local";
        public string RemoteServerHost { get; set; } = string.Empty;
        public string RemoteAdbCommand { get; set; } = "devices";
        public string RelayServerUrl { get; set; } = string.Empty;
        public string RelayHostToken { get; set; } = string.Empty;
        public string RelayName { get; set; } = string.Empty;
        public string RelayPairCode { get; set; } = string.Empty;
        public bool EnableVoiceNotifications { get; set; } = true;
        public bool EnableSoundNotifications { get; set; } = true;
        public bool EnableRouterAutomation { get; set; }
        public string RouterHost { get; set; } = string.Empty;
        public int RouterPort { get; set; } = 22;
        public string RouterUser { get; set; } = "admin";
        public string RouterWireGuardIp { get; set; } = string.Empty;
        public int RouterWireGuardPrefixLength { get; set; } = 24;
    }

    private sealed record CommandExecutionResult(int ExitCode, string Stdout, string Stderr);

    private static void SetEnvironmentValue(StringDictionary environmentVariables, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            environmentVariables.Remove(key);
            return;
        }

        environmentVariables[key] = value.Trim();
    }

    private static GuiSettings NormalizeSettings(GuiSettings settings)
    {
        settings.RemoteServerHost ??= string.Empty;
        settings.RemoteAdbCommand = string.IsNullOrWhiteSpace(settings.RemoteAdbCommand) ? "devices" : settings.RemoteAdbCommand.Trim();
        settings.RelayServerUrl = RelayApiClient.NormalizeServerUrl(settings.RelayServerUrl ?? string.Empty);
        settings.RelayHostToken ??= string.Empty;
        settings.RelayName = string.IsNullOrWhiteSpace(settings.RelayName) ? Environment.MachineName : settings.RelayName.Trim();
        settings.RelayPairCode = (settings.RelayPairCode ?? string.Empty).Trim().ToUpperInvariant();
        settings.RouterHost ??= string.Empty;
        settings.RouterUser = string.IsNullOrWhiteSpace(settings.RouterUser) ? "admin" : settings.RouterUser.Trim();
        settings.RouterWireGuardIp ??= string.Empty;
        if (settings.RouterPort <= 0)
        {
            settings.RouterPort = 22;
        }

        if (settings.RouterWireGuardPrefixLength is < 1 or > 32)
        {
            settings.RouterWireGuardPrefixLength = 24;
        }

        return settings;
    }

    private static void EnsureGenericKeyNames()
    {
        PromoteLegacyKeyIfNeeded(MikroTikRoot, MikroTikPrivateKeyPath, isPublicKey: false);
        PromoteLegacyKeyIfNeeded(MikroTikRoot, MikroTikPublicKeyPath, isPublicKey: true);
        PromoteLegacyKeyIfNeeded(PersistentMikroTikRoot, PersistentMikroTikPrivateKeyPath, isPublicKey: false);
        PromoteLegacyKeyIfNeeded(PersistentMikroTikRoot, PersistentMikroTikPublicKeyPath, isPublicKey: true);
    }

    private static void PromoteLegacyKeyIfNeeded(string sourceDirectory, string targetPath, bool isPublicKey)
    {
        if (File.Exists(targetPath) || !Directory.Exists(sourceDirectory))
        {
            return;
        }

        var targetName = Path.GetFileName(targetPath);
        var candidate = Directory.EnumerateFiles(sourceDirectory, "mikrotik_*", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), targetName, StringComparison.OrdinalIgnoreCase))
            .Where(path => isPublicKey == path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(candidate, targetPath, overwrite: false);
    }

    private bool ValidateLocalServerConfiguration()
    {
        if (!_settings.EnableRouterAutomation)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_settings.RouterHost) ||
            string.IsNullOrWhiteSpace(_settings.RouterUser) ||
            string.IsNullOrWhiteSpace(_settings.RouterWireGuardIp))
        {
            MessageBox.Show(
                this,
                "Uzupełnij ustawienia routera albo wyłącz automatyczną konfigurację routera w Ustawieniach programu.",
                "Brak ustawień routera",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            SetStatus("Brak ustawień routera.");
            return false;
        }

        if (_settings.RouterPort <= 0 || _settings.RouterWireGuardPrefixLength is < 1 or > 32)
        {
            MessageBox.Show(
                this,
                "Port SSH albo prefiks sieci WireGuard ma nieprawidłową wartość.",
                "Nieprawidłowe ustawienia routera",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            SetStatus("Nieprawidłowe ustawienia routera.");
            return false;
        }

        var hasKey = File.Exists(MikroTikPrivateKeyPath) ||
                     File.Exists(PersistentMikroTikPrivateKeyPath) ||
                     ContainsAnyPrivateKey(MikroTikRoot) ||
                     ContainsAnyPrivateKey(PersistentMikroTikRoot);

        if (!hasKey)
        {
            MessageBox.Show(
                this,
                "Automatyczna konfiguracja routera jest włączona, ale nie ma zaimportowanego klucza SSH. Użyj przycisku 'Importuj klucz'.",
                "Brak klucza routera",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            SetStatus("Brak klucza routera.");
            return false;
        }

        return true;
    }

    private static bool ContainsAnyPrivateKey(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }

        return Directory.EnumerateFiles(directory, "mikrotik_*", SearchOption.TopDirectoryOnly)
            .Any(path => !path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase));
    }
}
