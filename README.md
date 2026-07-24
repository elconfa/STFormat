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
| `STFormat.Tests` | net10.0 | test (xUnit) |

## Principio di sicurezza

Il motore parte da un **lexer lossless**: riconcatenando i token si riottiene il sorgente
identico. La formattazione può modificare solo la *trivia* (spazi, a-capo), mai i token di
codice — così lo stile cambia ma la semantica resta invariata.

## Sviluppo

```bash
dotnet build STFormat.slnx
dotnet test  STFormat.slnx
```

La **VSIX** e l'integrazione con la TwinCAT Automation Interface vanno compilate e testate
su **Windows** (VS SDK + COM + TwinCAT sono Windows-only).

## Licenza

[MIT](LICENSE) — come TcBlack.
