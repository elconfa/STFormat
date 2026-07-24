# STFormat

Formatter di codice per **Structured Text (IEC 61131-3)**, ispirato a
[STWEEP](https://www.stweep.com/) e [TcBlack](https://github.com/Roald87/TcBlack).
Pensato per essere usato sia come **CLI** (anche su file CoDeSys) sia come **plugin (VSIX)**
per **TwinCAT XAE 4026** (VS2022 / TcXaeShell). Feature di punta: **allineamento a colonne**
delle assegnazioni, delle dichiarazioni e dei commenti, oltre a indentazione e spaziatura consistenti.

> ⚠️ Progetto in fase iniziale. Al momento è implementato e testato il **lexer** del motore.

## Struttura

| Progetto | Target | Ruolo |
|----------|--------|-------|
| `STFormat.Core` | netstandard2.0 | motore: lexer + formattazione (condivisibile con la VSIX) |
| `STFormat.Cli` | net10.0 | tool a riga di comando |
| `STFormat.Gui` | net10.0 | GUI desktop (Avalonia, multipiattaforma) |
| `STFormat.Tests` | net10.0 | test (xUnit) |

## Principio di sicurezza

Il motore parte da un **lexer lossless**: riconcatenando i token si riottiene il sorgente
identico. La formattazione può modificare solo la *trivia* (spazi, a-capo), mai i token di
codice — così lo stile cambia ma la semantica resta invariata.

## Uso (CLI)

```bash
# Formatta in-place file e/o cartelle (ricorsione su .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st)
stformat src/

# Anteprima senza scrivere
stformat --diff MyFb.TcPOU
stformat --stdout MyFb.TcPOU

# CI: esce con codice 1 se qualcosa cambierebbe
stformat --check src/

# Da stdin (ST puro, es. file CoDeSys)
cat code.st | stformat --stdin --use-tabs

# Opzioni stile: --use-tabs  --indent-size N  --tab-width N  --keywords upper|lower|preserve
```

Nei file XML TwinCAT viene formattato **solo** il codice ST dentro le CDATA di `<Declaration>`
e `<ST>`; il resto dell'XML resta invariato (diff minimi, CRLF preservati).

## GUI

App desktop con scelta file/cartella, impostazioni e anteprima **Prima / Dopo** in tempo reale:

```bash
dotnet run --project src/STFormat.Gui
```

## Sviluppo

```bash
dotnet build STFormat.slnx
dotnet test  STFormat.slnx
dotnet run --project src/STFormat.Cli -- --help
```

La **VSIX** e l'integrazione con la TwinCAT Automation Interface vanno compilate e testate
su **Windows** (VS SDK + COM + TwinCAT sono Windows-only).

## Licenza

[MIT](LICENSE) — come TcBlack.
