[English](README.md) | **Italiano**

# STFormat

**Formatter di codice per Structured Text (IEC 61131-3)** — per **TwinCAT** e **CoDeSys**.
Indenta, spazia, normalizza le keyword e **allinea a colonne** (con tab) dichiarazioni, assegnazioni,
membri di enum e commenti. Disponibile come **riga di comando** e come **interfaccia grafica**.

Ispirato a [STWEEP](https://www.stweep.com/) e [TcBlack](https://github.com/Roald87/TcBlack); gratuito e open-source (MIT).

![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/OS-Windows%20%7C%20macOS%20%7C%20Linux-informational)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)

## Prima / Dopo

Un enum reale, prima e dopo `stformat` (allineamento di `:=` e dei commenti a colonne, con tab):

| Prima | Dopo |
|-------|------|
| ![Prima](docs/images/enum-prima.jpg) | ![Dopo](docs/images/enum-dopo.jpg) |

## Cosa fa

- **Indentazione** per blocchi (`IF/ELSE/ELSIF`, `FOR`, `WHILE`, `REPEAT/UNTIL`, `CASE` con etichette, `VAR*`, `STRUCT`, enum `TYPE … ( … )`).
- **Spaziatura** consistente attorno a operatori, chiamate, indici, membri, segno unario.
- **Normalizzazione keyword** (MAIUSCOLE / minuscole / invariate).
- **Allineamento a colonne con TAB** di `:` e `:=` nelle dichiarazioni, `:=` nelle assegnazioni e nei membri di enum, dei parametri `:=`/`=>` nelle chiamate FB multi-riga, e dei commenti a fine riga.
- Lavora direttamente sui file **`.TcPOU` / `.TcGVL` / `.TcDUT`** di TwinCAT (formatta solo il codice ST dentro le CDATA — diff minimi, CRLF preservati) e sugli **export CoDeSys** / ST puro.
- Modalità **`--check`** per CI/pre-commit, **`--diff`**, elaborazione ricorsiva di cartelle.

## Sicurezza: non altera mai la semantica

Il motore parte da un **lexer lossless** (riconcatenando i token si riottiene il sorgente identico).
La formattazione modifica solo la *trivia* (spazi, tab, a-capo) e il case delle keyword: la sequenza dei
token di codice resta invariata. È inoltre **idempotente** — riformattare due volte dà lo stesso risultato.
Coperto da una suite di test (lexer, formattazione, allineamento, round-trip `.TcPOU`).

## Installazione

### Windows (consigliato)
Scarica l'eseguibile **autonomo** dall'ultima [Release](../../releases/latest) — non serve installare .NET:
- `stformat.exe` (riga di comando)
- `STFormat-GUI.exe` (interfaccia grafica)

### Da sorgente (Windows / macOS / Linux)
Serve il [.NET SDK 10](https://dotnet.microsoft.com/download).
```bash
git clone https://github.com/elconfa/STFormat.git
cd STFormat
dotnet build -c Release
```

## Uso — riga di comando

```bash
# Formatta in-place file e/o cartelle (ricorsione su .TcPOU/.TcGVL/.TcDUT/.TcIO/.exp/.st)
stformat src/

# Anteprima senza scrivere
stformat --diff  MyFb.TcPOU
stformat --stdout MyFb.TcPOU

# CI / pre-commit: esce con codice 1 se qualcosa cambierebbe
stformat --check src/

# Da stdin (ST puro, es. export CoDeSys)
cat code.st | stformat --stdin

# Opzioni stile
#   --use-tabs           indenta con tab invece che con spazi
#   --indent-size N      indentazione con N spazi (default 4)
#   --tab-width N        ampiezza del tab per l'allineamento (default 4)
#   --keywords MODE      upper | lower | preserve
```

## Uso — interfaccia grafica

Scelta file/cartella, pannello impostazioni e anteprima **Prima / Dopo** in tempo reale.
L'interfaccia è disponibile in **italiano, inglese e tedesco** (selettore in alto).
Dall'eseguibile `STFormat-GUI.exe`, oppure da sorgente:
```bash
dotnet run --project src/STFormat.Gui
```

## Confronto

| | STFormat | STWEEP | TcBlack |
|---|:---:|:---:|:---:|
| Licenza | MIT (gratis) | commerciale | MIT (gratis) |
| Riga di comando | ✅ | ✅ | ✅ |
| Interfaccia grafica | ✅ | ✅ | — |
| Allineamento a colonne | ✅ (tab) | ✅ | parziale |
| File `.TcPOU` TwinCAT | ✅ | ✅ | ✅ |
| Export CoDeSys / ST puro | ✅ | ✅ | — |
| Plugin nell'editor TwinCAT | in sviluppo | ✅ | ✅ |

## Struttura del progetto

| Progetto | Target | Ruolo |
|----------|--------|-------|
| `STFormat.Core` | netstandard2.0 | motore: lexer + formattazione + allineamento |
| `STFormat.Cli` | net10.0 | tool a riga di comando (`stformat`) |
| `STFormat.Gui` | net10.0 | GUI desktop (Avalonia, multipiattaforma) |
| `STFormat.Tests` | net10.0 | test (xUnit) |

```bash
dotnet test STFormat.slnx      # esegue la suite di test
```

## Roadmap

- Plugin **VSIX** per l'editor di TwinCAT XAE 4026 (comando "Format Document"), riusando `STFormat.Core`.
- Distribuzione via `dotnet tool` e winget.

## Contribuire

Segnalazioni e richieste sono benvenute tramite le [Issues](../../issues). Se un costrutto viene
formattato in modo non ideale, allega un piccolo esempio "prima/dopo": è il modo più veloce per
aggiungere il caso e un test di regressione. Vedi [CONTRIBUTING.md](CONTRIBUTING.md) per come
compilare, testare e proporre modifiche.

## Licenza e crediti

[MIT](LICENSE). Ispirato a **STWEEP** e a **TcBlack** di Roald Nefs.
