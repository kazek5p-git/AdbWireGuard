namespace AdbWireGuardGui;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootLayoutPanel;
    private GroupBox modeGroupBox;
    private FlowLayoutPanel modeFlowLayoutPanel;
    private RadioButton localModeRadioButton;
    private RadioButton remoteModeRadioButton;
    private RadioButton relayModeRadioButton;
    private Label modeHintLabel;
    private Label packageLabel;
    private TextBox packagePathTextBox;
    private TableLayoutPanel remoteSettingsLayoutPanel;
    private TableLayoutPanel relaySettingsLayoutPanel;
    private Label serverHostLabel;
    private TextBox serverHostTextBox;
    private Label adbCommandLabel;
    private TextBox adbCommandTextBox;
    private Label relayServerLabel;
    private TextBox relayServerTextBox;
    private Label relayHostTokenLabel;
    private TextBox relayHostTokenTextBox;
    private Label relayNameLabel;
    private TextBox relayNameTextBox;
    private Label relayPairCodeLabel;
    private TextBox relayPairCodeTextBox;
    private FlowLayoutPanel actionsPanel;
    private Button primaryActionButton;
    private Button stopButton;
    private Button refreshButton;
    private Button testConnectionButton;
    private Button updateButton;
    private Button importComponentsButton;
    private Button importKeyButton;
    private Button settingsButton;
    private Button openStateButton;
    private Button openPackageButton;
    private Button copyStatusButton;
    private Button clearLogsButton;
    private TableLayoutPanel summaryLayoutPanel;
    private Label modeLabel;
    private TextBox modeTextBox;
    private Label statusLabel;
    private TextBox statusTextBox;
    private Label updatedLabel;
    private TextBox updatedTextBox;
    private Label versionLabel;
    private TextBox versionTextBox;
    private Label logLabel;
    private TextBox logTextBox;
    private System.Windows.Forms.Timer refreshTimer;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayoutPanel = new TableLayoutPanel();
        modeGroupBox = new GroupBox();
        modeHintLabel = new Label();
        modeFlowLayoutPanel = new FlowLayoutPanel();
        localModeRadioButton = new RadioButton();
        remoteModeRadioButton = new RadioButton();
        relayModeRadioButton = new RadioButton();
        packageLabel = new Label();
        packagePathTextBox = new TextBox();
        remoteSettingsLayoutPanel = new TableLayoutPanel();
        relaySettingsLayoutPanel = new TableLayoutPanel();
        serverHostLabel = new Label();
        serverHostTextBox = new TextBox();
        adbCommandLabel = new Label();
        adbCommandTextBox = new TextBox();
        relayServerLabel = new Label();
        relayServerTextBox = new TextBox();
        relayHostTokenLabel = new Label();
        relayHostTokenTextBox = new TextBox();
        relayNameLabel = new Label();
        relayNameTextBox = new TextBox();
        relayPairCodeLabel = new Label();
        relayPairCodeTextBox = new TextBox();
        actionsPanel = new FlowLayoutPanel();
        primaryActionButton = new Button();
        stopButton = new Button();
        refreshButton = new Button();
        testConnectionButton = new Button();
        updateButton = new Button();
        importComponentsButton = new Button();
        importKeyButton = new Button();
        settingsButton = new Button();
        openStateButton = new Button();
        openPackageButton = new Button();
        copyStatusButton = new Button();
        clearLogsButton = new Button();
        summaryLayoutPanel = new TableLayoutPanel();
        modeLabel = new Label();
        modeTextBox = new TextBox();
        statusLabel = new Label();
        statusTextBox = new TextBox();
        updatedLabel = new Label();
        updatedTextBox = new TextBox();
        versionLabel = new Label();
        versionTextBox = new TextBox();
        logLabel = new Label();
        logTextBox = new TextBox();
        refreshTimer = new System.Windows.Forms.Timer(components);
        rootLayoutPanel.SuspendLayout();
        modeGroupBox.SuspendLayout();
        modeFlowLayoutPanel.SuspendLayout();
        remoteSettingsLayoutPanel.SuspendLayout();
        relaySettingsLayoutPanel.SuspendLayout();
        actionsPanel.SuspendLayout();
        summaryLayoutPanel.SuspendLayout();
        SuspendLayout();
        //
        // rootLayoutPanel
        //
        rootLayoutPanel.ColumnCount = 1;
        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayoutPanel.Controls.Add(modeGroupBox, 0, 0);
        rootLayoutPanel.Controls.Add(packageLabel, 0, 1);
        rootLayoutPanel.Controls.Add(packagePathTextBox, 0, 2);
        rootLayoutPanel.Controls.Add(remoteSettingsLayoutPanel, 0, 3);
        rootLayoutPanel.Controls.Add(relaySettingsLayoutPanel, 0, 4);
        rootLayoutPanel.Controls.Add(actionsPanel, 0, 5);
        rootLayoutPanel.Controls.Add(summaryLayoutPanel, 0, 6);
        rootLayoutPanel.Controls.Add(logLabel, 0, 7);
        rootLayoutPanel.Controls.Add(logTextBox, 0, 8);
        rootLayoutPanel.Dock = DockStyle.Fill;
        rootLayoutPanel.Location = new Point(0, 0);
        rootLayoutPanel.Name = "rootLayoutPanel";
        rootLayoutPanel.Padding = new Padding(12);
        rootLayoutPanel.RowCount = 9;
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle());
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayoutPanel.Size = new Size(964, 684);
        rootLayoutPanel.TabIndex = 0;
        //
        // modeGroupBox
        //
        modeGroupBox.Controls.Add(modeHintLabel);
        modeGroupBox.Controls.Add(modeFlowLayoutPanel);
        modeGroupBox.Dock = DockStyle.Fill;
        modeGroupBox.Location = new Point(15, 15);
        modeGroupBox.Name = "modeGroupBox";
        modeGroupBox.Padding = new Padding(12, 10, 12, 12);
        modeGroupBox.Size = new Size(934, 88);
        modeGroupBox.TabIndex = 0;
        modeGroupBox.TabStop = false;
        modeGroupBox.Text = "Tryb pracy";
        //
        // modeHintLabel
        //
        modeHintLabel.AutoSize = true;
        modeHintLabel.Dock = DockStyle.Top;
        modeHintLabel.Location = new Point(12, 49);
        modeHintLabel.Margin = new Padding(3, 10, 3, 0);
        modeHintLabel.Name = "modeHintLabel";
        modeHintLabel.Size = new Size(287, 15);
        modeHintLabel.TabIndex = 1;
        modeHintLabel.Text = "Uruchom ADB na komputerze, do którego jest wpięty telefon.";
        //
        // modeFlowLayoutPanel
        //
        modeFlowLayoutPanel.AutoSize = true;
        modeFlowLayoutPanel.Controls.Add(localModeRadioButton);
        modeFlowLayoutPanel.Controls.Add(remoteModeRadioButton);
        modeFlowLayoutPanel.Controls.Add(relayModeRadioButton);
        modeFlowLayoutPanel.Dock = DockStyle.Top;
        modeFlowLayoutPanel.Location = new Point(12, 26);
        modeFlowLayoutPanel.Name = "modeFlowLayoutPanel";
        modeFlowLayoutPanel.Size = new Size(910, 23);
        modeFlowLayoutPanel.TabIndex = 0;
        //
        // localModeRadioButton
        //
        localModeRadioButton.AutoSize = true;
        localModeRadioButton.Checked = true;
        localModeRadioButton.Location = new Point(3, 3);
        localModeRadioButton.Name = "localModeRadioButton";
        localModeRadioButton.Size = new Size(164, 19);
        localModeRadioButton.TabIndex = 0;
        localModeRadioButton.TabStop = true;
        localModeRadioButton.Text = "Uruchom serwer na tym komputerze";
        localModeRadioButton.UseVisualStyleBackColor = true;
        localModeRadioButton.CheckedChanged += modeRadioButton_CheckedChanged;
        //
        // remoteModeRadioButton
        //
        remoteModeRadioButton.AutoSize = true;
        remoteModeRadioButton.Location = new Point(173, 3);
        remoteModeRadioButton.Name = "remoteModeRadioButton";
        remoteModeRadioButton.Size = new Size(173, 19);
        remoteModeRadioButton.TabIndex = 1;
        remoteModeRadioButton.Text = "Połącz z drugim komputerem";
        remoteModeRadioButton.UseVisualStyleBackColor = true;
        remoteModeRadioButton.CheckedChanged += modeRadioButton_CheckedChanged;
        //
        // relayModeRadioButton
        //
        relayModeRadioButton.AutoSize = true;
        relayModeRadioButton.Location = new Point(352, 3);
        relayModeRadioButton.Name = "relayModeRadioButton";
        relayModeRadioButton.Size = new Size(128, 19);
        relayModeRadioButton.TabIndex = 2;
        relayModeRadioButton.Text = "Połącz przez serwer";
        relayModeRadioButton.UseVisualStyleBackColor = true;
        relayModeRadioButton.CheckedChanged += modeRadioButton_CheckedChanged;
        //
        // packageLabel
        //
        packageLabel.Anchor = AnchorStyles.Left;
        packageLabel.AutoSize = true;
        packageLabel.Location = new Point(15, 118);
        packageLabel.Name = "packageLabel";
        packageLabel.Size = new Size(126, 15);
        packageLabel.TabIndex = 1;
        packageLabel.Text = "Pakiet ADB przez WireGuard";
        //
        // packagePathTextBox
        //
        packagePathTextBox.Dock = DockStyle.Fill;
        packagePathTextBox.Location = new Point(15, 139);
        packagePathTextBox.Name = "packagePathTextBox";
        packagePathTextBox.ReadOnly = true;
        packagePathTextBox.Size = new Size(934, 23);
        packagePathTextBox.TabIndex = 2;
        //
        // remoteSettingsLayoutPanel
        //
        remoteSettingsLayoutPanel.ColumnCount = 2;
        remoteSettingsLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        remoteSettingsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        remoteSettingsLayoutPanel.Controls.Add(serverHostLabel, 0, 0);
        remoteSettingsLayoutPanel.Controls.Add(serverHostTextBox, 1, 0);
        remoteSettingsLayoutPanel.Controls.Add(adbCommandLabel, 0, 1);
        remoteSettingsLayoutPanel.Controls.Add(adbCommandTextBox, 1, 1);
        remoteSettingsLayoutPanel.Dock = DockStyle.Fill;
        remoteSettingsLayoutPanel.Location = new Point(15, 168);
        remoteSettingsLayoutPanel.Name = "remoteSettingsLayoutPanel";
        remoteSettingsLayoutPanel.RowCount = 2;
        remoteSettingsLayoutPanel.RowStyles.Add(new RowStyle());
        remoteSettingsLayoutPanel.RowStyles.Add(new RowStyle());
        remoteSettingsLayoutPanel.Size = new Size(934, 64);
        remoteSettingsLayoutPanel.TabIndex = 3;
        //
        // serverHostLabel
        //
        serverHostLabel.Anchor = AnchorStyles.Left;
        serverHostLabel.AutoSize = true;
        serverHostLabel.Location = new Point(3, 6);
        serverHostLabel.Name = "serverHostLabel";
        serverHostLabel.Size = new Size(79, 15);
        serverHostLabel.TabIndex = 0;
        serverHostLabel.Text = "Adres serwera";
        //
        // serverHostTextBox
        //
        serverHostTextBox.Dock = DockStyle.Fill;
        serverHostTextBox.Location = new Point(107, 3);
        serverHostTextBox.Name = "serverHostTextBox";
        serverHostTextBox.Size = new Size(824, 23);
        serverHostTextBox.TabIndex = 1;
        //
        // adbCommandLabel
        //
        adbCommandLabel.Anchor = AnchorStyles.Left;
        adbCommandLabel.AutoSize = true;
        adbCommandLabel.Location = new Point(3, 38);
        adbCommandLabel.Name = "adbCommandLabel";
        adbCommandLabel.Size = new Size(77, 15);
        adbCommandLabel.TabIndex = 2;
        adbCommandLabel.Text = "Polecenie ADB";
        //
        // adbCommandTextBox
        //
        adbCommandTextBox.Dock = DockStyle.Fill;
        adbCommandTextBox.Location = new Point(107, 35);
        adbCommandTextBox.Name = "adbCommandTextBox";
        adbCommandTextBox.Size = new Size(824, 23);
        adbCommandTextBox.TabIndex = 3;
        //
        // relaySettingsLayoutPanel
        //
        relaySettingsLayoutPanel.ColumnCount = 2;
        relaySettingsLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        relaySettingsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        relaySettingsLayoutPanel.Controls.Add(relayServerLabel, 0, 0);
        relaySettingsLayoutPanel.Controls.Add(relayServerTextBox, 1, 0);
        relaySettingsLayoutPanel.Controls.Add(relayHostTokenLabel, 0, 1);
        relaySettingsLayoutPanel.Controls.Add(relayHostTokenTextBox, 1, 1);
        relaySettingsLayoutPanel.Controls.Add(relayNameLabel, 0, 2);
        relaySettingsLayoutPanel.Controls.Add(relayNameTextBox, 1, 2);
        relaySettingsLayoutPanel.Controls.Add(relayPairCodeLabel, 0, 3);
        relaySettingsLayoutPanel.Controls.Add(relayPairCodeTextBox, 1, 3);
        relaySettingsLayoutPanel.Dock = DockStyle.Fill;
        relaySettingsLayoutPanel.Location = new Point(15, 238);
        relaySettingsLayoutPanel.Name = "relaySettingsLayoutPanel";
        relaySettingsLayoutPanel.RowCount = 4;
        relaySettingsLayoutPanel.RowStyles.Add(new RowStyle());
        relaySettingsLayoutPanel.RowStyles.Add(new RowStyle());
        relaySettingsLayoutPanel.RowStyles.Add(new RowStyle());
        relaySettingsLayoutPanel.RowStyles.Add(new RowStyle());
        relaySettingsLayoutPanel.Size = new Size(934, 128);
        relaySettingsLayoutPanel.TabIndex = 4;
        //
        // relayServerLabel
        //
        relayServerLabel.Anchor = AnchorStyles.Left;
        relayServerLabel.AutoSize = true;
        relayServerLabel.Location = new Point(3, 6);
        relayServerLabel.Name = "relayServerLabel";
        relayServerLabel.Size = new Size(69, 15);
        relayServerLabel.TabIndex = 0;
        relayServerLabel.Text = "Adres relay";
        //
        // relayServerTextBox
        //
        relayServerTextBox.Dock = DockStyle.Fill;
        relayServerTextBox.Location = new Point(147, 3);
        relayServerTextBox.Name = "relayServerTextBox";
        relayServerTextBox.PlaceholderText = "https://relay.example.com";
        relayServerTextBox.Size = new Size(784, 23);
        relayServerTextBox.TabIndex = 1;
        //
        // relayHostTokenLabel
        //
        relayHostTokenLabel.Anchor = AnchorStyles.Left;
        relayHostTokenLabel.AutoSize = true;
        relayHostTokenLabel.Location = new Point(3, 38);
        relayHostTokenLabel.Name = "relayHostTokenLabel";
        relayHostTokenLabel.Size = new Size(70, 15);
        relayHostTokenLabel.TabIndex = 2;
        relayHostTokenLabel.Text = "Token hosta";
        //
        // relayHostTokenTextBox
        //
        relayHostTokenTextBox.Dock = DockStyle.Fill;
        relayHostTokenTextBox.Location = new Point(147, 35);
        relayHostTokenTextBox.Name = "relayHostTokenTextBox";
        relayHostTokenTextBox.Size = new Size(784, 23);
        relayHostTokenTextBox.TabIndex = 3;
        //
        // relayNameLabel
        //
        relayNameLabel.Anchor = AnchorStyles.Left;
        relayNameLabel.AutoSize = true;
        relayNameLabel.Location = new Point(3, 70);
        relayNameLabel.Name = "relayNameLabel";
        relayNameLabel.Size = new Size(112, 15);
        relayNameLabel.TabIndex = 4;
        relayNameLabel.Text = "Nazwa hosta / klienta";
        //
        // relayNameTextBox
        //
        relayNameTextBox.Dock = DockStyle.Fill;
        relayNameTextBox.Location = new Point(147, 67);
        relayNameTextBox.Name = "relayNameTextBox";
        relayNameTextBox.Size = new Size(784, 23);
        relayNameTextBox.TabIndex = 5;
        //
        // relayPairCodeLabel
        //
        relayPairCodeLabel.Anchor = AnchorStyles.Left;
        relayPairCodeLabel.AutoSize = true;
        relayPairCodeLabel.Location = new Point(3, 102);
        relayPairCodeLabel.Name = "relayPairCodeLabel";
        relayPairCodeLabel.Size = new Size(132, 15);
        relayPairCodeLabel.TabIndex = 6;
        relayPairCodeLabel.Text = "Kod sesji (dla klienta)";
        //
        // relayPairCodeTextBox
        //
        relayPairCodeTextBox.CharacterCasing = CharacterCasing.Upper;
        relayPairCodeTextBox.Dock = DockStyle.Fill;
        relayPairCodeTextBox.Location = new Point(147, 99);
        relayPairCodeTextBox.Name = "relayPairCodeTextBox";
        relayPairCodeTextBox.PlaceholderText = "Wpisz kod, aby dołączyć do sesji";
        relayPairCodeTextBox.Size = new Size(784, 23);
        relayPairCodeTextBox.TabIndex = 7;
        relayPairCodeTextBox.TextChanged += relayPairCodeTextBox_TextChanged;
        //
        // actionsPanel
        //
        actionsPanel.AutoSize = true;
        actionsPanel.Controls.Add(primaryActionButton);
        actionsPanel.Controls.Add(stopButton);
        actionsPanel.Controls.Add(refreshButton);
        actionsPanel.Controls.Add(testConnectionButton);
        actionsPanel.Controls.Add(updateButton);
        actionsPanel.Controls.Add(importComponentsButton);
        actionsPanel.Controls.Add(importKeyButton);
        actionsPanel.Controls.Add(settingsButton);
        actionsPanel.Controls.Add(openStateButton);
        actionsPanel.Controls.Add(openPackageButton);
        actionsPanel.Controls.Add(copyStatusButton);
        actionsPanel.Controls.Add(clearLogsButton);
        actionsPanel.Dock = DockStyle.Fill;
        actionsPanel.Location = new Point(15, 372);
        actionsPanel.Margin = new Padding(3, 6, 3, 12);
        actionsPanel.Name = "actionsPanel";
        actionsPanel.Size = new Size(934, 39);
        actionsPanel.TabIndex = 5;
        //
        // primaryActionButton
        //
        primaryActionButton.AutoSize = true;
        primaryActionButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        primaryActionButton.Location = new Point(3, 3);
        primaryActionButton.Name = "primaryActionButton";
        primaryActionButton.Padding = new Padding(12, 4, 12, 4);
        primaryActionButton.Size = new Size(164, 33);
        primaryActionButton.TabIndex = 0;
        primaryActionButton.Text = "Uruchom serwer na tym komputerze";
        primaryActionButton.UseVisualStyleBackColor = true;
        primaryActionButton.Click += primaryActionButton_Click;
        //
        // stopButton
        //
        stopButton.AutoSize = true;
        stopButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        stopButton.Location = new Point(173, 3);
        stopButton.Name = "stopButton";
        stopButton.Padding = new Padding(12, 4, 12, 4);
        stopButton.Size = new Size(95, 33);
        stopButton.TabIndex = 1;
        stopButton.Text = "Zatrzymaj";
        stopButton.UseVisualStyleBackColor = true;
        stopButton.Click += stopButton_Click;
        //
        // refreshButton
        //
        refreshButton.AutoSize = true;
        refreshButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        refreshButton.Location = new Point(274, 3);
        refreshButton.Name = "refreshButton";
        refreshButton.Padding = new Padding(12, 4, 12, 4);
        refreshButton.Size = new Size(90, 33);
        refreshButton.TabIndex = 2;
        refreshButton.Text = "Odśwież";
        refreshButton.UseVisualStyleBackColor = true;
        refreshButton.Click += refreshButton_Click;
        //
        // testConnectionButton
        //
        testConnectionButton.AutoSize = true;
        testConnectionButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        testConnectionButton.Location = new Point(370, 3);
        testConnectionButton.Name = "testConnectionButton";
        testConnectionButton.Padding = new Padding(12, 4, 12, 4);
        testConnectionButton.Size = new Size(126, 33);
        testConnectionButton.TabIndex = 3;
        testConnectionButton.Text = "Sprawdź połączenie";
        testConnectionButton.UseVisualStyleBackColor = true;
        testConnectionButton.Click += testConnectionButton_Click;
        //
        // updateButton
        //
        updateButton.AutoSize = true;
        updateButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        updateButton.Location = new Point(502, 3);
        updateButton.Name = "updateButton";
        updateButton.Padding = new Padding(12, 4, 12, 4);
        updateButton.Size = new Size(157, 33);
        updateButton.TabIndex = 4;
        updateButton.Text = "Aktualizuj komponenty";
        updateButton.UseVisualStyleBackColor = true;
        updateButton.Click += updateButton_Click;
        //
        // importComponentsButton
        //
        importComponentsButton.AutoSize = true;
        importComponentsButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        importComponentsButton.Location = new Point(665, 3);
        importComponentsButton.Name = "importComponentsButton";
        importComponentsButton.Padding = new Padding(12, 4, 12, 4);
        importComponentsButton.Size = new Size(164, 33);
        importComponentsButton.TabIndex = 5;
        importComponentsButton.Text = "Importuj komponenty ZIP";
        importComponentsButton.UseVisualStyleBackColor = true;
        importComponentsButton.Click += importComponentsButton_Click;
        //
        // importKeyButton
        //
        importKeyButton.AutoSize = true;
        importKeyButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        importKeyButton.Location = new Point(3, 42);
        importKeyButton.Name = "importKeyButton";
        importKeyButton.Padding = new Padding(12, 4, 12, 4);
        importKeyButton.Size = new Size(111, 33);
        importKeyButton.TabIndex = 6;
        importKeyButton.Text = "Importuj klucz";
        importKeyButton.UseVisualStyleBackColor = true;
        importKeyButton.Click += importKeyButton_Click;
        //
        // settingsButton
        //
        settingsButton.AutoSize = true;
        settingsButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        settingsButton.Location = new Point(120, 42);
        settingsButton.Name = "settingsButton";
        settingsButton.Padding = new Padding(12, 4, 12, 4);
        settingsButton.Size = new Size(91, 33);
        settingsButton.TabIndex = 7;
        settingsButton.Text = "Ustawienia";
        settingsButton.UseVisualStyleBackColor = true;
        settingsButton.Click += settingsButton_Click;
        //
        // openStateButton
        //
        openStateButton.AutoSize = true;
        openStateButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        openStateButton.Location = new Point(217, 42);
        openStateButton.Name = "openStateButton";
        openStateButton.Padding = new Padding(12, 4, 12, 4);
        openStateButton.Size = new Size(125, 33);
        openStateButton.TabIndex = 8;
        openStateButton.Text = "Otwórz folder stanu";
        openStateButton.UseVisualStyleBackColor = true;
        openStateButton.Click += openStateButton_Click;
        //
        // openPackageButton
        //
        openPackageButton.AutoSize = true;
        openPackageButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        openPackageButton.Location = new Point(348, 42);
        openPackageButton.Name = "openPackageButton";
        openPackageButton.Padding = new Padding(12, 4, 12, 4);
        openPackageButton.Size = new Size(131, 33);
        openPackageButton.TabIndex = 9;
        openPackageButton.Text = "Otwórz folder pakietu";
        openPackageButton.UseVisualStyleBackColor = true;
        openPackageButton.Click += openPackageButton_Click;
        //
        // copyStatusButton
        //
        copyStatusButton.AutoSize = true;
        copyStatusButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        copyStatusButton.Location = new Point(485, 42);
        copyStatusButton.Name = "copyStatusButton";
        copyStatusButton.Padding = new Padding(12, 4, 12, 4);
        copyStatusButton.Size = new Size(105, 33);
        copyStatusButton.TabIndex = 10;
        copyStatusButton.Text = "Kopiuj status";
        copyStatusButton.UseVisualStyleBackColor = true;
        copyStatusButton.Click += copyStatusButton_Click;
        //
        // clearLogsButton
        //
        clearLogsButton.AutoSize = true;
        clearLogsButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        clearLogsButton.Location = new Point(499, 42);
        clearLogsButton.Name = "clearLogsButton";
        clearLogsButton.Padding = new Padding(12, 4, 12, 4);
        clearLogsButton.Size = new Size(147, 33);
        clearLogsButton.TabIndex = 11;
        clearLogsButton.Text = "Wyczyść logi i stan";
        clearLogsButton.UseVisualStyleBackColor = true;
        clearLogsButton.Click += clearLogsButton_Click;
        //
        // summaryLayoutPanel
        //
        summaryLayoutPanel.ColumnCount = 2;
        summaryLayoutPanel.ColumnStyles.Add(new ColumnStyle());
        summaryLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        summaryLayoutPanel.Controls.Add(modeLabel, 0, 0);
        summaryLayoutPanel.Controls.Add(modeTextBox, 1, 0);
        summaryLayoutPanel.Controls.Add(statusLabel, 0, 1);
        summaryLayoutPanel.Controls.Add(statusTextBox, 1, 1);
        summaryLayoutPanel.Controls.Add(updatedLabel, 0, 2);
        summaryLayoutPanel.Controls.Add(updatedTextBox, 1, 2);
        summaryLayoutPanel.Controls.Add(versionLabel, 0, 3);
        summaryLayoutPanel.Controls.Add(versionTextBox, 1, 3);
        summaryLayoutPanel.Dock = DockStyle.Fill;
        summaryLayoutPanel.Location = new Point(15, 426);
        summaryLayoutPanel.Name = "summaryLayoutPanel";
        summaryLayoutPanel.RowCount = 4;
        summaryLayoutPanel.RowStyles.Add(new RowStyle());
        summaryLayoutPanel.RowStyles.Add(new RowStyle());
        summaryLayoutPanel.RowStyles.Add(new RowStyle());
        summaryLayoutPanel.RowStyles.Add(new RowStyle());
        summaryLayoutPanel.Size = new Size(934, 128);
        summaryLayoutPanel.TabIndex = 6;
        //
        // modeLabel
        //
        modeLabel.Anchor = AnchorStyles.Left;
        modeLabel.AutoSize = true;
        modeLabel.Location = new Point(3, 6);
        modeLabel.Name = "modeLabel";
        modeLabel.Size = new Size(32, 15);
        modeLabel.TabIndex = 0;
        modeLabel.Text = "Tryb";
        //
        // modeTextBox
        //
        modeTextBox.Dock = DockStyle.Fill;
        modeTextBox.Location = new Point(120, 3);
        modeTextBox.Name = "modeTextBox";
        modeTextBox.ReadOnly = true;
        modeTextBox.Size = new Size(811, 23);
        modeTextBox.TabIndex = 1;
        //
        // statusLabel
        //
        statusLabel.Anchor = AnchorStyles.Left;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(3, 38);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(38, 15);
        statusLabel.TabIndex = 2;
        statusLabel.Text = "Status";
        //
        // statusTextBox
        //
        statusTextBox.Dock = DockStyle.Fill;
        statusTextBox.Location = new Point(120, 35);
        statusTextBox.Name = "statusTextBox";
        statusTextBox.ReadOnly = true;
        statusTextBox.Size = new Size(811, 23);
        statusTextBox.TabIndex = 3;
        //
        // updatedLabel
        //
        updatedLabel.Anchor = AnchorStyles.Left;
        updatedLabel.AutoSize = true;
        updatedLabel.Location = new Point(3, 70);
        updatedLabel.Name = "updatedLabel";
        updatedLabel.Size = new Size(104, 15);
        updatedLabel.TabIndex = 4;
        updatedLabel.Text = "Ostatnia aktualizacja";
        //
        // updatedTextBox
        //
        updatedTextBox.Dock = DockStyle.Fill;
        updatedTextBox.Location = new Point(120, 67);
        updatedTextBox.Name = "updatedTextBox";
        updatedTextBox.ReadOnly = true;
        updatedTextBox.Size = new Size(811, 23);
        updatedTextBox.TabIndex = 5;
        //
        // versionLabel
        //
        versionLabel.Anchor = AnchorStyles.Left;
        versionLabel.AutoSize = true;
        versionLabel.Location = new Point(3, 102);
        versionLabel.Name = "versionLabel";
        versionLabel.Size = new Size(40, 15);
        versionLabel.TabIndex = 6;
        versionLabel.Text = "Wersja";
        //
        // versionTextBox
        //
        versionTextBox.Dock = DockStyle.Fill;
        versionTextBox.Location = new Point(120, 99);
        versionTextBox.Name = "versionTextBox";
        versionTextBox.ReadOnly = true;
        versionTextBox.Size = new Size(811, 23);
        versionTextBox.TabIndex = 7;
        //
        // logLabel
        //
        logLabel.AutoSize = true;
        logLabel.Location = new Point(15, 569);
        logLabel.Margin = new Padding(3, 12, 3, 6);
        logLabel.Name = "logLabel";
        logLabel.Size = new Size(40, 15);
        logLabel.TabIndex = 7;
        logLabel.Text = "Raport";
        //
        // logTextBox
        //
        logTextBox.Dock = DockStyle.Fill;
        logTextBox.Location = new Point(15, 590);
        logTextBox.Multiline = true;
        logTextBox.Name = "logTextBox";
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Both;
        logTextBox.Size = new Size(934, 79);
        logTextBox.TabIndex = 8;
        logTextBox.WordWrap = false;
        //
        // refreshTimer
        //
        refreshTimer.Interval = 2000;
        refreshTimer.Tick += RefreshTimerTick;
        //
        // Form1
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(964, 820);
        Controls.Add(rootLayoutPanel);
        MinimumSize = new Size(980, 856);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ADB przez WireGuard";
        rootLayoutPanel.ResumeLayout(false);
        rootLayoutPanel.PerformLayout();
        modeGroupBox.ResumeLayout(false);
        modeGroupBox.PerformLayout();
        modeFlowLayoutPanel.ResumeLayout(false);
        modeFlowLayoutPanel.PerformLayout();
        remoteSettingsLayoutPanel.ResumeLayout(false);
        remoteSettingsLayoutPanel.PerformLayout();
        relaySettingsLayoutPanel.ResumeLayout(false);
        relaySettingsLayoutPanel.PerformLayout();
        actionsPanel.ResumeLayout(false);
        actionsPanel.PerformLayout();
        summaryLayoutPanel.ResumeLayout(false);
        summaryLayoutPanel.PerformLayout();
        ResumeLayout(false);
    }
}
