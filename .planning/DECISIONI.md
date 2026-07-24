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

### Fase 2 — Motore: formattazione base ✅
- [x] Indentazione per blocchi (`IndentEngine` a stack: `IF`/`ELSE`/`ELSIF`, `FOR`, `WHILE`,
      `REPEAT`/`UNTIL`, `CASE` con etichette+`ELSE`, `VAR*/END_VAR`, `STRUCT`). POU e `TYPE` non
      indentano (convenzione TwinCAT). Indent configurabile (default 4 spazi).
- [x] Spaziatura attorno agli operatori, chiamate/indici/membri incollati, unario, commenti (`SpacingRules`).
- [x] Normalizzazione keyword (`Upper`/`Lower`/`Preserve`, default Upper), trim whitespace, righe vuote
      collassate, a-capo rilevato e preservato, singolo newline finale.
- [x] Idempotenza + invarianza dei token significativi (mod case keyword) — test dedicati.
- [x] 20 test formatter verdi (55 totali col lexer).
- [ ] Limiti noti (Fase 3+): continuazioni multi-riga rese al livello del blocco; etichetta+statement
      sulla stessa riga (`1: x:=1;`) non porta il doppio indent.

### Fase 3 — Motore: allineamento a colonne (feature STWEEP) ✅
- [x] **Riempimento con TAB, mai spazi** (requisito): anchor portati su tab stop (multipli di `TabWidth`,
      default 4). Gli spazi "normali" fra token restano spazi.
- [x] Allineare `:` e `:=` nelle dichiarazioni `VAR`/`STRUCT` consecutive (`Aligner`).
- [x] Allineare `:=` in blocchi di assegnazioni consecutive (livello parentesi 0; i `:=` dei
      parametri nelle chiamate FB NON vengono trattati come assegnazioni).
- [x] Allineare commenti a fine riga nei gruppi.
- [x] Gruppi di 1 riga non vengono tab-allineati; allineamento disattivabile via `FormatOptions`.
- [x] Idempotenza mantenuta; 10 test allineamento (65 totali).
- [ ] Allineare parametri `:=`/`=>` nelle chiamate FB **multi-riga** (rimandato).

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
