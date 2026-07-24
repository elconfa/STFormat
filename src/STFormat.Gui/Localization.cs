using System.Collections.Generic;
using System.Globalization;

namespace STFormat.Gui;

public enum Lang { It = 0, En = 1, De = 2 }

/// <summary>Traduzioni dell'interfaccia (Italiano / English / Deutsch).</summary>
public static class Loc
{
    public static Lang Current { get; set; } = Lang.En;

    // Ogni voce: [ Italiano, English, Deutsch ]
    private static readonly Dictionary<string, string[]> S = new()
    {
        ["open_file"]        = new[] { "📄 Apri file…", "📄 Open file…", "📄 Datei öffnen…" },
        ["open_folder"]      = new[] { "📁 Apri cartella…", "📁 Open folder…", "📁 Ordner öffnen…" },
        ["save_current"]     = new[] { "💾 Formatta e salva", "💾 Format & save", "💾 Formatieren & speichern" },
        ["save_all"]         = new[] { "💾 Formatta tutti…", "💾 Format all…", "💾 Alle formatieren…" },

        ["settings"]         = new[] { "Impostazioni", "Settings", "Einstellungen" },
        ["indentation"]      = new[] { "Indentazione", "Indentation", "Einrückung" },
        ["spaces"]           = new[] { "Spazi", "Spaces", "Leerzeichen" },
        ["tabs"]             = new[] { "Tab", "Tabs", "Tabs" },
        ["size"]             = new[] { "Dimensione", "Size", "Größe" },
        ["tab_width"]        = new[] { "Ampiezza tab", "Tab width", "Tabbreite" },
        ["keywords"]         = new[] { "Keyword", "Keywords", "Schlüsselwörter" },
        ["kw_upper"]         = new[] { "MAIUSCOLE", "UPPERCASE", "GROSSBUCHSTABEN" },
        ["kw_lower"]         = new[] { "minuscole", "lowercase", "kleinbuchstaben" },
        ["kw_preserve"]      = new[] { "invariate", "unchanged", "unverändert" },
        ["alignment_header"] = new[] { "Allineamento a colonne (tab)", "Column alignment (tabs)", "Spaltenausrichtung (Tabs)" },
        ["align_decl"]       = new[] { "Dichiarazioni ( : e := )", "Declarations ( : and := )", "Deklarationen ( : und := )" },
        ["align_assign"]     = new[] { "Assegnazioni ( := )", "Assignments ( := )", "Zuweisungen ( := )" },
        ["align_comments"]   = new[] { "Commenti a fine riga", "End-of-line comments", "Zeilenendkommentare" },
        ["files"]            = new[] { "File", "Files", "Dateien" },

        ["before"]           = new[] { "Prima", "Before", "Vorher" },
        ["after"]            = new[] { "Dopo (formattato)", "After (formatted)", "Nachher (formatiert)" },

        ["start_hint"]       = new[] { "Apri un file o una cartella per iniziare.", "Open a file or folder to get started.", "Öffne eine Datei oder einen Ordner, um zu beginnen." },
        ["will_change"]      = new[] { "{0} — verrà modificato dal salvataggio", "{0} — will be changed on save", "{0} — wird beim Speichern geändert" },
        ["already_ok"]       = new[] { "{0} — già formattato", "{0} — already formatted", "{0} — bereits formatiert" },
        ["saved"]            = new[] { "Salvato: {0}", "Saved: {0}", "Gespeichert: {0}" },
        ["read_error"]       = new[] { "Errore in lettura: {0}", "Read error: {0}", "Lesefehler: {0}" },
        ["write_error"]      = new[] { "Errore in scrittura: {0}", "Write error: {0}", "Schreibfehler: {0}" },
        ["no_files"]         = new[] { "Nessun file .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st trovato nella cartella.", "No .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st file found in the folder.", "Keine .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st-Datei im Ordner gefunden." },
        ["save_all_result"]  = new[] { "Tutti formattati — {0} modificati, {1} già a posto", "All formatted — {0} changed, {1} already fine", "Alle formatiert — {0} geändert, {1} bereits korrekt" },
        ["errors_suffix"]    = new[] { ", {0} errori", ", {0} errors", ", {0} Fehler" },

        ["pick_file"]        = new[] { "Scegli un file da formattare", "Choose a file to format", "Datei zum Formatieren wählen" },
        ["pick_folder"]      = new[] { "Scegli una cartella (ricorsiva)", "Choose a folder (recursive)", "Ordner wählen (rekursiv)" },
        ["filter_all"]       = new[] { "Tutti i file", "All files", "Alle Dateien" },
    };

    public static string T(string key) => S.TryGetValue(key, out var v) ? v[(int)Current] : key;

    /// <summary>Lingua iniziale in base alla cultura del sistema (it/de → altrimenti English).</summary>
    public static Lang Detect()
    {
        string c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return c == "it" ? Lang.It : c == "de" ? Lang.De : Lang.En;
    }
}
