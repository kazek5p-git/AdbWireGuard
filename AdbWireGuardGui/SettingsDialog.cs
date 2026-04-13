using System.Drawing;

namespace AdbWireGuardGui;

internal sealed class SettingsDialog : Form
{
    private readonly Func<Task> _onUpdateAsync;
    private readonly Action _onClearLogs;
    private readonly CheckBox _voiceCheckBox;
    private readonly CheckBox _soundCheckBox;
    private readonly CheckBox _routerAutoCheckBox;
    private readonly TextBox _routerHostTextBox;
    private readonly TextBox _routerPortTextBox;
    private readonly TextBox _routerUserTextBox;
    private readonly TextBox _routerWireGuardIpTextBox;
    private readonly TextBox _routerPrefixTextBox;
    private readonly Button _updateButton;
    private readonly Button _clearLogsButton;
    private readonly Button _closeButton;
    private readonly Label _infoLabel;

    public bool EnableVoiceNotifications => _voiceCheckBox.Checked;
    public bool EnableSoundNotifications => _soundCheckBox.Checked;
    public bool EnableRouterAutomation => _routerAutoCheckBox.Checked;
    public string RouterHost => _routerHostTextBox.Text.Trim();
    public int RouterPort => int.TryParse(_routerPortTextBox.Text.Trim(), out var value) && value > 0 ? value : 22;
    public string RouterUser => _routerUserTextBox.Text.Trim();
    public string RouterWireGuardIp => _routerWireGuardIpTextBox.Text.Trim();
    public int RouterWireGuardPrefixLength => int.TryParse(_routerPrefixTextBox.Text.Trim(), out var value) && value is >= 1 and <= 32 ? value : 24;

    public SettingsDialog(
        Form owner,
        bool enableVoiceNotifications,
        bool enableSoundNotifications,
        bool enableRouterAutomation,
        string routerHost,
        int routerPort,
        string routerUser,
        string routerWireGuardIp,
        int routerWireGuardPrefixLength,
        Func<Task> onUpdateAsync,
        Action onClearLogs)
    {
        _onUpdateAsync = onUpdateAsync;
        _onClearLogs = onClearLogs;

        Text = "Ustawienia";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 470);

        var rootPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5
        };
        rootPanel.RowStyles.Add(new RowStyle());
        rootPanel.RowStyles.Add(new RowStyle());
        rootPanel.RowStyles.Add(new RowStyle());
        rootPanel.RowStyles.Add(new RowStyle());
        rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _infoLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Text = "Tutaj ustawisz komunikaty programu oraz opcjonalną automatyczną konfigurację routera. Publiczna paczka nie ma już żadnych zaszytych adresów ani kluczy."
        };

        var notificationsPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 8, 0, 8)
        };

        _voiceCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = enableVoiceNotifications,
            Text = "Komunikaty głosowe"
        };

        _soundCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = enableSoundNotifications,
            Text = "Dźwięki systemowe"
        };

        notificationsPanel.Controls.Add(_voiceCheckBox, 0, 0);
        notificationsPanel.Controls.Add(_soundCheckBox, 0, 1);

        var routerPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Margin = new Padding(0, 8, 0, 8)
        };
        routerPanel.ColumnStyles.Add(new ColumnStyle());
        routerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _routerAutoCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = enableRouterAutomation,
            Text = "Automatycznie konfiguruj router MikroTik"
        };
        _routerAutoCheckBox.CheckedChanged += routerAutoCheckBox_CheckedChanged;

        var routerHintLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 0, 3, 8),
            Text = "Włącz to tylko wtedy, gdy chcesz aby program sam ustawiał forward TCP 5037 na Twoim routerze. Klucz SSH importujesz osobno przyciskiem w głównym oknie."
        };

        _routerHostTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = routerHost
        };
        _routerPortTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = routerPort.ToString()
        };
        _routerUserTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = routerUser
        };
        _routerWireGuardIpTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = routerWireGuardIp
        };
        _routerPrefixTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = routerWireGuardPrefixLength.ToString()
        };

        routerPanel.Controls.Add(_routerAutoCheckBox, 0, 0);
        routerPanel.SetColumnSpan(_routerAutoCheckBox, 2);
        routerPanel.Controls.Add(routerHintLabel, 0, 1);
        routerPanel.SetColumnSpan(routerHintLabel, 2);
        routerPanel.Controls.Add(new Label { AutoSize = true, Text = "Adres routera", Margin = new Padding(0, 6, 12, 0) }, 0, 2);
        routerPanel.Controls.Add(_routerHostTextBox, 1, 2);
        routerPanel.Controls.Add(new Label { AutoSize = true, Text = "Port SSH", Margin = new Padding(0, 6, 12, 0) }, 0, 3);
        routerPanel.Controls.Add(_routerPortTextBox, 1, 3);
        routerPanel.Controls.Add(new Label { AutoSize = true, Text = "Użytkownik SSH", Margin = new Padding(0, 6, 12, 0) }, 0, 4);
        routerPanel.Controls.Add(_routerUserTextBox, 1, 4);
        routerPanel.Controls.Add(new Label { AutoSize = true, Text = "Adres WireGuard routera", Margin = new Padding(0, 6, 12, 0) }, 0, 5);
        routerPanel.Controls.Add(_routerWireGuardIpTextBox, 1, 5);
        routerPanel.Controls.Add(new Label { AutoSize = true, Text = "Prefiks sieci WG", Margin = new Padding(0, 6, 12, 0) }, 0, 6);
        routerPanel.Controls.Add(_routerPrefixTextBox, 1, 6);

        var actionsPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill
        };

        _updateButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 4, 12, 4),
            Text = "Aktualizuj komponenty"
        };
        _updateButton.Click += updateButton_Click;

        _clearLogsButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 4, 12, 4),
            Text = "Wyczyść logi i stan"
        };
        _clearLogsButton.Click += clearLogsButton_Click;

        _closeButton = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 4, 12, 4),
            Text = "Zamknij",
            DialogResult = DialogResult.OK
        };

        actionsPanel.Controls.Add(_updateButton);
        actionsPanel.Controls.Add(_clearLogsButton);
        actionsPanel.Controls.Add(_closeButton);

        rootPanel.Controls.Add(_infoLabel, 0, 0);
        rootPanel.Controls.Add(notificationsPanel, 0, 1);
        rootPanel.Controls.Add(routerPanel, 0, 2);
        rootPanel.Controls.Add(actionsPanel, 0, 3);

        Controls.Add(rootPanel);
        AcceptButton = _closeButton;
        CancelButton = _closeButton;

        FormClosing += SettingsDialog_FormClosing;
        RefreshRouterUi();
    }

    private async void updateButton_Click(object? sender, EventArgs e)
    {
        await RunBusyActionAsync(_onUpdateAsync);
    }

    private void clearLogsButton_Click(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            _onClearLogs();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunBusyActionAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _voiceCheckBox.Enabled = !isBusy;
        _soundCheckBox.Enabled = !isBusy;
        _routerAutoCheckBox.Enabled = !isBusy;
        _routerHostTextBox.Enabled = !isBusy && _routerAutoCheckBox.Checked;
        _routerPortTextBox.Enabled = !isBusy && _routerAutoCheckBox.Checked;
        _routerUserTextBox.Enabled = !isBusy && _routerAutoCheckBox.Checked;
        _routerWireGuardIpTextBox.Enabled = !isBusy && _routerAutoCheckBox.Checked;
        _routerPrefixTextBox.Enabled = !isBusy && _routerAutoCheckBox.Checked;
        _updateButton.Enabled = !isBusy;
        _clearLogsButton.Enabled = !isBusy;
        _closeButton.Enabled = !isBusy;
    }

    private void routerAutoCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        RefreshRouterUi();
    }

    private void RefreshRouterUi()
    {
        var enabled = _routerAutoCheckBox.Checked && !_updateButton.Focused && !_clearLogsButton.Focused;
        _routerHostTextBox.Enabled = enabled;
        _routerPortTextBox.Enabled = enabled;
        _routerUserTextBox.Enabled = enabled;
        _routerWireGuardIpTextBox.Enabled = enabled;
        _routerPrefixTextBox.Enabled = enabled;
    }

    private void SettingsDialog_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK || !_routerAutoCheckBox.Checked)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RouterHost) ||
            string.IsNullOrWhiteSpace(RouterUser) ||
            string.IsNullOrWhiteSpace(RouterWireGuardIp))
        {
            MessageBox.Show(
                this,
                "Jeśli chcesz automatycznie konfigurować router, podaj adres routera, użytkownika SSH i adres WireGuard routera.",
                "Brak ustawień routera",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            e.Cancel = true;
            return;
        }

        if (RouterPort <= 0 || RouterWireGuardPrefixLength is < 1 or > 32)
        {
            MessageBox.Show(
                this,
                "Port SSH musi być większy od zera, a prefiks sieci WireGuard musi mieścić się w zakresie 1-32.",
                "Nieprawidłowe ustawienia routera",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            e.Cancel = true;
        }
    }
}
