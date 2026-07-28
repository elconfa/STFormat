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
        new(StringComparer.OrdinalIgnoreCase) { ".tcpou", ".tcgvl", ".tcdut", ".tcio", ".exp", ".st", ".txt" };

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

        // Lingua: iniziale dalla cultura di sistema, poi selezionabile dal menu a tendina.
        Loc.Current = Loc.Detect();
        CbLang.SelectionChanged += (_, _) =>
        {
            Loc.Current = (Lang)CbLang.SelectedIndex;
            ApplyLanguage();
        };
        CbLang.SelectedIndex = (int)Loc.Current;
        ApplyLanguage();
    }

    // ---- Localizzazione ----

    private void ApplyLanguage()
    {
        BtnOpenFile.Content = Loc.T("open_file");
        BtnOpenFolder.Content = Loc.T("open_folder");
        BtnSave.Content = Loc.T("save_current");
        BtnSaveAll.Content = Loc.T("save_all");

        TxtSettings.Text = Loc.T("settings");
        TxtIndentation.Text = Loc.T("indentation");
        RbSpaces.Content = Loc.T("spaces");
        RbTabs.Content = Loc.T("tabs");
        TxtSize.Text = Loc.T("size");
        TxtTabWidth.Text = Loc.T("tab_width");
        TxtKeywords.Text = Loc.T("keywords");
        KwUpper.Content = Loc.T("kw_upper");
        KwLower.Content = Loc.T("kw_lower");
        KwPreserve.Content = Loc.T("kw_preserve");
        TxtAlignHeader.Text = Loc.T("alignment_header");
        ChkAlignDecl.Content = Loc.T("align_decl");
        ChkAlignAssign.Content = Loc.T("align_assign");
        ChkAlignComments.Content = Loc.T("align_comments");
        TxtFilesHeader.Text = Loc.T("files");

        TxtBeforeHeader.Text = Loc.T("before");
        TxtAfterHeader.Text = Loc.T("after");

        if (_currentPath is null) TxtStatus.Text = Loc.T("start_hint");
        else UpdatePreview();
    }

    // ---- Apertura ----

    private async System.Threading.Tasks.Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("pick_file"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Structured Text / TwinCAT")
                {
                    Patterns = new[] { "*.TcPOU", "*.TcGVL", "*.TcDUT", "*.TcIO", "*.exp", "*.st", "*.txt" }
                },
                new FilePickerFileType(Loc.T("filter_all")) { Patterns = new[] { "*" } }
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
            Title = Loc.T("pick_folder"),
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
            TxtStatus.Text = Loc.T("no_files");
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
            TxtStatus.Text = string.Format(Loc.T("read_error"), ex.Message);
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
            ? string.Format(Loc.T("will_change"), name)
            : string.Format(Loc.T("already_ok"), name);
    }

    private FormatOptions BuildOptions()
    {
        return new FormatOptions
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
            TxtStatus.Text = string.Format(Loc.T("saved"), Path.GetFileName(_currentPath));
        }
        catch (Exception ex)
        {
            TxtStatus.Text = string.Format(Loc.T("write_error"), ex.Message);
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

        string msg = string.Format(Loc.T("save_all_result"), changed, unchanged);
        if (errors > 0) msg += string.Format(Loc.T("errors_suffix"), errors);
        TxtStatus.Text = msg;

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
