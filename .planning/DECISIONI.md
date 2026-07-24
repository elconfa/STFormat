# STFormat — Decisioni e roadmap

## Decisioni (2026-07-24)

| Tema | Scelta | Note |
|------|--------|------|
| Linguaggio | C# / .NET | motore condiviso CLI + VSIX |
| Distribuzione | CLI **e** VSIX | |
| Scope v1 | formatter completo | allineamento + indent + spazi + keyword |
| Target IDE | TwinCAT 4026 (VS2022 / TcXaeShell) | VSIX in `Common7\IDE\Extensions` |
| Uso CLI | anche su file CoDeSys | `.TcPOU`/XML e `.exp`/testo ST |
| Sicurezza | safe mode (round-trip / build prima-dopo) | mai cambio di semantica |
| Nome | `STFormat` | provvisorio, rinominabile |

## Vincoli d'ambiente

- Mac: solo `Core` + `Cli` + `Tests` (serve installare .NET SDK).
- Windows + TwinCAT: obbligatorio per `Vsix` + Automation Interface + test in-editor.

## Roadmap proposta

### Fase 0 — Setup ✅
- [x] Cartella progetto + CLAUDE.md + planning
- [x] Installare .NET SDK sul Mac (.NET 10.0.302 via Homebrew)
- [x] Scaffold solution: `STFormat.Core` (netstandard2.0) + `STFormat.Cli` (net10) + `STFormat.Tests` (net10, xUnit)
- [x] `.gitignore` (.NET)
- [ ] Decidere licenza / git init

### Fase 1 — Motore: lexer ST ✅ (base)
- [x] Tokenizer lossless: whitespace, a-capo, `//` e `(* *)` (annidati) e `/* */`, stringhe `'...'`/`"..."`
      con escape `$`, pragma `{...}`, numeri (anche basati `16#FF`), letterali tipizzati (`T#5s`,
      `DT#...`, `E_State#Idle`), indirizzi diretti (`%IX0.0`), operatori multi/single, keyword IEC.
- [x] Confini stringa/commento riconosciuti (il `:=` dentro stringa/commento NON è un operatore).
- [x] Test: round-trip (concat token == sorgente) + classificazione — 35 test verdi.
- [ ] Da valutare più avanti: opzione commenti `(* *)` non annidati; letterali `WSTRING` con `"`.

### Fase 2 — Motore: formattazione base
- [ ] Indentazione per blocchi (`IF/END_IF`, `FOR`, `WHILE`, `CASE`, `VAR/END_VAR`, ...).
- [ ] Spaziatura attorno agli operatori e dopo virgole/`;`.
- [ ] Normalizzazione keyword (maiuscole) e whitespace di riga.
- [ ] Idempotenza: format(format(x)) == format(x).

### Fase 3 — Motore: allineamento a colonne (feature STWEEP)
- [ ] Allineare `:=` in blocchi di assegnazioni consecutive.
- [ ] Allineare `:` e `:=` nelle dichiarazioni `VAR`.
- [ ] Allineare commenti `//` a fine riga.
- [ ] Allineare parametri `:=`/`=>` nelle chiamate FB multi-riga.

### Fase 4 — CLI
- [ ] Input: file singolo, glob, cartella, progetto.
- [ ] Parsing `.TcPOU`/`.TcGVL`/`.TcDUT` (XML) → estrai/reinietta ST in Declaration + Implementation.
- [ ] Parsing export CoDeSys `.exp`.
- [ ] `--check` (exit code per CI), `--diff`, safe mode.

### Fase 5 — VSIX (su Windows)
- [ ] Progetto VSIX per VS2022 / TcXaeShell.
- [ ] Comando "Format Document" + toolbar + keybinding.
- [ ] Lettura/scrittura POU via Automation Interface.
- [ ] Packaging e installazione in TwinCAT 4026.

## Domande aperte

- Configurabilità dello stile (file `.stformat`/EditorConfig) o stile fisso "opinionato"?
- Larghezza massima riga / wrapping delle chiamate lunghe?
- Gestione dei commenti `(* ... *)` multi-riga nell'allineamento.
