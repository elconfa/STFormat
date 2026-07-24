using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using STFormat.Core.Formatting;

namespace STFormat.Gui;

public partial class MainWindow : Window
{
    private static readonly HashSet<string> XmlExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".tcpou", ".tcgvl", ".tcdut", ".tcio" };

    private static readonly HashSet<string> ScanExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".tcpou", ".tcgvl", ".tcdut", ".tcio", ".exp", ".st" };

    private string? _currentPath;
    private string _currentOriginal = "";
    private string _currentAfter = "";
    private bool _currentBom;

    public MainWindow()
    {
        InitializeComponent();

        BtnOpenFile.Click += async (_, _) => await OpenFileAsync();
        BtnOpenFolder.Click += async (_, _) => await OpenFolderAsync();
        BtnSave.Click += (_, _) => SaveCurrent();
        BtnSaveAll.Click += (_, _) => SaveAll();

        LstFiles.SelectionChanged += (_, _) => LoadSelected();

        RbSpaces.IsCheckedChanged += (_, _) => UpdatePreview();
        RbTabs.IsCheckedChanged += (_, _) => UpdatePreview();
        NudIndentSize.ValueChanged += (_, _) => UpdatePreview();
        NudTabWidth.ValueChanged += (_, _) => UpdatePreview();
        CbKeywords.SelectionChanged += (_, _) => UpdatePreview();
        ChkAlignDecl.IsCheckedChanged += (_, _) => UpdatePreview();
        ChkAlignAssign.IsCheckedChanged += (_, _) => UpdatePreview();
        ChkAlignComments.IsCheckedChanged += (_, _) => UpdatePreview();
    }

    // ---- Apertura ----

    private async System.Threading.Tasks.Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Scegli un file da formattare",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Structured Text / TwinCAT")
                {
                    Patterns = new[] { "*.TcPOU", "*.TcGVL", "*.TcDUT", "*.TcIO", "*.exp", "*.st" }
                },
                new FilePickerFileType("Tutti i file") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (path is null) return;

        SetFileList(new List<string> { path });
    }

    private async System.Threading.Tasks.Task OpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Scegli una cartella (ricorsiva)",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        string? dir = folders[0].TryGetLocalPath();
        if (dir is null || !Directory.Exists(dir)) return;

        var found = new List<string>();
        foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            if (ScanExtensions.Contains(Path.GetExtension(f)))
                found.Add(f);
        found.Sort(StringComparer.OrdinalIgnoreCase);

        if (found.Count == 0)
        {
            TxtStatus.Text = "Nessun file .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st trovato nella cartella.";
            SetFileList(found);
            return;
        }
        SetFileList(found);
    }

    private void SetFileList(List<string> paths)
    {
        var entries = new List<FileEntry>(paths.Count);
        foreach (string p in paths) entries.Add(new FileEntry(p));

        LstFiles.ItemsSource = entries;
        BtnSaveAll.IsEnabled = entries.Count > 1;

        if (entries.Count > 0)
            LstFiles.SelectedIndex = 0;
        else
        {
            _currentPath = null;
            TxtBefore.Text = "";
            TxtAfter.Text = "";
            BtnSave.IsEnabled = false;
        }
    }

    private void LoadSelected()
    {
        if (LstFiles.SelectedItem is not FileEntry entry)
            return;
        try
        {
            _currentPath = entry.Path;
            _currentOriginal = ReadText(entry.Path, out _currentBom);
            UpdatePreview();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Errore in lettura: " + ex.Message;
        }
    }

    // ---- Anteprima ----

    private void UpdatePreview()
    {
        if (_currentPath is null) return;

        FormatOptions opts = BuildOptions();
        _currentAfter = FormatText(_currentOriginal, _currentPath, opts);

        TxtBefore.Text = _currentOriginal;
        TxtAfter.Text = _currentAfter;
        BtnSave.IsEnabled = true;

        string name = Path.GetFileName(_currentPath);
        TxtStatus.Text = _currentAfter != _currentOriginal
            ? name + " — verrà modificato dal salvataggio"
            : name + " — già formattato";
    }

    private FormatOptions BuildOptions()
    {
        var o = new FormatOptions
        {
            IndentUnit = RbTabs.IsChecked == true
                ? "\t"
                : new string(' ', ToInt(NudIndentSize.Value, 4)),
            TabWidth = ToInt(NudTabWidth.Value, 4),
            KeywordCasing = CbKeywords.SelectedIndex switch
            {
                1 => KeywordCasing.Lower,
                2 => KeywordCasing.Preserve,
                _ => KeywordCasing.Upper
            },
            AlignDeclarations = ChkAlignDecl.IsChecked == true,
            AlignAssignments = ChkAlignAssign.IsChecked == true,
            AlignTrailingComments = ChkAlignComments.IsChecked == true
        };
        return o;
    }

    private static string FormatText(string text, string path, FormatOptions opts)
    {
        bool isXml = XmlExtensions.Contains(Path.GetExtension(path))
                     || TcPouFormatter.LooksLikeTwinCatXml(text);
        return isXml ? TcPouFormatter.FormatDocument(text, opts) : StFormatter.Format(text, opts);
    }

    // ---- Salvataggio ----

    private void SaveCurrent()
    {
        if (_currentPath is null) return;
        try
        {
            WriteText(_currentPath, _currentAfter, _currentBom);
            _currentOriginal = _currentAfter;
            TxtStatus.Text = "Salvato: " + Path.GetFileName(_currentPath);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = "Errore in scrittura: " + ex.Message;
        }
    }

    private void SaveAll()
    {
        if (LstFiles.ItemsSource is not IEnumerable<FileEntry> entries) return;

        FormatOptions opts = BuildOptions();
        int changed = 0, unchanged = 0, errors = 0;
        foreach (FileEntry e in entries)
        {
            try
            {
                string original = ReadText(e.Path, out bool bom);
                string formatted = FormatText(original, e.Path, opts);
                if (formatted != original)
                {
                    WriteText(e.Path, formatted, bom);
                    changed++;
                }
                else unchanged++;
            }
            catch { errors++; }
        }

        TxtStatus.Text = $"Tutti formattati — {changed} modificati, {unchanged} già a posto"
                         + (errors > 0 ? $", {errors} errori" : "");

        // Ricarica l'anteprima del file corrente (ora salvato).
        LoadSelected();
    }

    // ---- I/O con gestione BOM UTF-8 ----

    private static string ReadText(string path, out bool bom)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        int start = bom ? 3 : 0;
        return new UTF8Encoding(false).GetString(bytes, start, bytes.Length - start);
    }

    private static void WriteText(string path, string text, bool bom)
    {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom);
        File.WriteAllText(path, text, enc);
    }

    private static int ToInt(decimal? value, int fallback)
        => value.HasValue ? (int)value.Value : fallback;

    private sealed class FileEntry
    {
        public string Path { get; }
        public FileEntry(string path) { Path = path; }
        public override string ToString() => System.IO.Path.GetFileName(Path);
    }
}
