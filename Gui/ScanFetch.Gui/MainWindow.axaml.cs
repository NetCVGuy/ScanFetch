using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScanFetch.Gui;

public partial class MainWindow : Window
{
    private string? _settingsPath;
    private JsonNode? _rootNode;
    private JsonArray? _scannersArray;

    public MainWindow()
    {
        InitializeComponent();

        this.Opened += MainWindow_Opened;
        SaveBtn.Click += SaveBtn_Click;
        PrettyBtn.Click += PrettyBtn_Click;
        AddScannerBtn.Click += AddScannerBtn_Click;
        RemoveScannerBtn.Click += RemoveScannerBtn_Click;
        ScannersList.SelectionChanged += ScannersList_SelectionChanged;
        ScannerNameBox.LostFocus += ScannerField_LostFocus;
        ScannerIpBox.LostFocus += ScannerField_LostFocus;
        ScannerPortBox.LostFocus += ScannerField_LostFocus;
        ScannerRoleBox.SelectionChanged += ScannerRoleBox_SelectionChanged;
        ListenInterfaceBox.LostFocus += ScannerField_LostFocus;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        _settingsPath = FindAppSettings();
        if (_settingsPath == null)
        {
            JsonTextBox.Text = "appsettings.json not found in parent directories.";
            return;
        }

        try
        {
            var content = File.ReadAllText(_settingsPath);
            JsonTextBox.Text = content;
            // parse and populate form
            _rootNode = JsonNode.Parse(content);
            PopulateFormFromJson();
        }
        catch (Exception ex)
        {
            JsonTextBox.Text = "Error reading file: " + ex.Message;
        }
    }

    private void PopulateFormFromJson()
    {
        if (_rootNode == null) return;

        // GoogleSheets section
        var gs = _rootNode["GoogleSheets"] as JsonObject;
        if (gs != null)
        {
            WebhookUrlBox.Text = gs["WebhookUrl"]?.ToString() ?? string.Empty;
            OutputPathBox.Text = gs["OutputPath"]?.ToString() ?? string.Empty;
            FilePrefixBox.Text = gs["FilePrefix"]?.ToString() ?? string.Empty;
            FileSuffixBox.Text = gs["FileSuffix"]?.ToString() ?? string.Empty;
            FileFormatBox.Text = gs["FileFormat"]?.ToString() ?? string.Empty;
            EnableFileOutputBox.IsChecked = gs["EnableFileOutput"]?.GetValue<bool>() ?? true;
            EnableGoogleSheetsBox.IsChecked = gs["EnableGoogleSheets"]?.GetValue<bool>() ?? true;
        }

        // Scanners
        _scannersArray = _rootNode["Scanners"] as JsonArray;
        ScannersList.Items = new List<string>();
        if (_scannersArray != null)
        {
            var names = new List<string>();
            foreach (var item in _scannersArray)
            {
                var name = item? ["Name"]?.ToString() ?? "<unnamed>";
                names.Add(name);
            }
            ScannersList.Items = names;
        }
    }

    private void ScannersList_SelectionChanged(object? sender, EventArgs e)
    {
        var idx = ScannersList.SelectedIndex;
        if (_scannersArray == null || idx < 0 || idx >= _scannersArray.Count)
        {
            ScannerNameBox.Text = string.Empty;
            ScannerIpBox.Text = string.Empty;
            ScannerPortBox.Text = string.Empty;
            ScannerRoleBox.SelectedIndex = 0;
            ListenInterfaceBox.Text = string.Empty;
            return;
        }

        var node = _scannersArray[idx] as JsonObject;
        if (node == null) return;
        ScannerNameBox.Text = node["Name"]?.ToString() ?? string.Empty;
        ScannerIpBox.Text = node["Ip"]?.ToString() ?? string.Empty;
        ScannerPortBox.Text = node["Port"]?.ToString() ?? string.Empty;
        var role = node["Role"]?.ToString() ?? "Client";
        ScannerRoleBox.SelectedIndex = role.Equals("Server", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ListenInterfaceBox.Text = node["ListenInterface"]?.ToString() ?? string.Empty;
    }

    private void ScannerField_LostFocus(object? sender, EventArgs e)
    {
        SaveScannerFieldsToJson();
    }

    private void ScannerRoleBox_SelectionChanged(object? sender, EventArgs e)
    {
        SaveScannerFieldsToJson();
    }

    private void SaveScannerFieldsToJson()
    {
        var idx = ScannersList.SelectedIndex;
        if (_scannersArray == null || idx < 0 || idx >= _scannersArray.Count) return;
        var node = _scannersArray[idx] as JsonObject;
        if (node == null) return;
        node["Name"] = ScannerNameBox.Text ?? string.Empty;
        node["Ip"] = ScannerIpBox.Text ?? string.Empty;
        if (int.TryParse(ScannerPortBox.Text, out var p)) node["Port"] = p; else node["Port"] = 0;
        node["Role"] = (ScannerRoleBox.SelectedIndex == 1) ? "Server" : "Client";
        node["ListenInterface"] = ListenInterfaceBox.Text ?? string.Empty;

        // Update list display
        var names = new List<string>();
        foreach (var item in _scannersArray)
        {
            names.Add(item?["Name"]?.ToString() ?? "<unnamed>");
        }
        ScannersList.Items = names;
        ScannersList.SelectedIndex = idx;
        JsonTextBox.Text = _rootNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? JsonTextBox.Text;
    }

    private void AddScannerBtn_Click(object? sender, RoutedEventArgs e)
    {
        var newObj = new JsonObject
        {
            ["Name"] = "NewScanner",
            ["Ip"] = "0.0.0.0",
            ["Port"] = 0,
            ["Enabled"] = true,
            ["Role"] = "Client",
            ["ListenInterface"] = string.Empty
        };
        if (_scannersArray == null)
        {
            _scannersArray = new JsonArray();
            if (_rootNode is JsonObject ro) ro["Scanners"] = _scannersArray;
        }
        _scannersArray.Add(newObj);
        PopulateFormFromJson();
        ScannersList.SelectedIndex = _scannersArray.Count - 1;
    }

    private void RemoveScannerBtn_Click(object? sender, RoutedEventArgs e)
    {
        var idx = ScannersList.SelectedIndex;
        if (_scannersArray == null || idx < 0 || idx >= _scannersArray.Count) return;
        _scannersArray.RemoveAt(idx);
        PopulateFormFromJson();
    }

    private void PrettyBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var node = JsonNode.Parse(JsonTextBox.Text);
            JsonTextBox.Text = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            // refresh internal model
            _rootNode = node;
            PopulateFormFromJson();
        }
        catch { /* ignore parse errors */ }
    }

    private void SaveBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_settingsPath == null)
        {
            return;
        }

        try
        {
            // Update model from form fields
            if (_rootNode is JsonObject ro)
            {
                var gs = ro["GoogleSheets"] as JsonObject ?? new JsonObject();
                gs["WebhookUrl"] = WebhookUrlBox.Text ?? string.Empty;
                gs["OutputPath"] = OutputPathBox.Text ?? string.Empty;
                gs["FilePrefix"] = FilePrefixBox.Text ?? string.Empty;
                gs["FileSuffix"] = FileSuffixBox.Text ?? string.Empty;
                gs["FileFormat"] = FileFormatBox.Text ?? string.Empty;
                gs["EnableFileOutput"] = EnableFileOutputBox.IsChecked ?? true;
                gs["EnableGoogleSheets"] = EnableGoogleSheetsBox.IsChecked ?? true;
                ro["GoogleSheets"] = gs;
            }

            var outText = _rootNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? JsonTextBox.Text;
            File.WriteAllText(_settingsPath, outText);
            var dlg = new Window { Width = 300, Height = 120, Title = "Saved" };
            var tb = new TextBlock { Text = "Saved appsettings.json", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            dlg.Content = tb;
            dlg.ShowDialog(this);
        }
        catch (Exception ex)
        {
            var dlg = new Window { Width = 400, Height = 160, Title = "Error" };
            var tb = new TextBlock { Text = "Error saving file: " + ex.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            dlg.Content = tb;
            dlg.ShowDialog(this);
        }
    }

    private string? FindAppSettings()
    {
        var dir = AppContext.BaseDirectory;
        var di = new DirectoryInfo(dir);
        for (int i = 0; i < 8 && di != null; i++)
        {
            var candidate = Path.Combine(di.FullName, "appsettings.json");
            if (File.Exists(candidate)) return candidate;
            di = di.Parent!;
        }
        // Try current working directory
        var cwd = Directory.GetCurrentDirectory();
        var cwdCandidate = Path.Combine(cwd, "appsettings.json");
        if (File.Exists(cwdCandidate)) return cwdCandidate;
        return null;
    }
}
