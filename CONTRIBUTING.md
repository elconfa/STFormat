# Contributing to STFormat

Thanks for taking the time to contribute! STFormat is a formatter for Structured Text
(IEC 61131-3) for TwinCAT and CoDeSys. Contributions of all sizes are welcome.

## Ways to contribute

- **Report a formatting issue** — some code is formatted sub-optimally or incorrectly.
  A small **before/after snippet** is the fastest way to get a fix and a regression test.
- **Report a bug** — a crash, an error, or wrong handling of a file.
- **Suggest a feature** — an option or a construct that isn't handled yet.
- **Open a pull request** — bug fixes, new alignment cases, tests, docs.

Use the [issue templates](../../issues/new/choose) when opening an issue.

## Development setup

Requires the [.NET SDK 10](https://dotnet.microsoft.com/download). Works on Windows, macOS and Linux.

```bash
git clone https://github.com/elconfa/STFormat.git
cd STFormat
dotnet build STFormat.slnx
dotnet test  STFormat.slnx      # run the test suite
dotnet run --project src/STFormat.Cli -- --help
dotnet run --project src/STFormat.Gui   # the GUI
```

> Note: the **VSIX** plugin and the TwinCAT Automation Interface (roadmap) can only be built and
> tested on Windows. The engine (`STFormat.Core`), the CLI and the tests are cross-platform.

## Project layout

| Project | Target | Role |
|---------|--------|------|
| `STFormat.Core` | netstandard2.0 | engine: lexer + formatting + alignment |
| `STFormat.Cli` | net10.0 | command-line tool |
| `STFormat.Gui` | net10.0 | desktop GUI (Avalonia) |
| `STFormat.Tests` | net10.0 | tests (xUnit) |

`STFormat.Core` targets **netstandard2.0** on purpose (so it can be shared with a future VSIX). Keep it
free of newer runtime-only APIs.

## The rules the formatter must always respect

Any change to formatting behaviour must preserve these invariants (they are what makes STFormat safe):

1. **Lossless lexing** — re-concatenating the tokens must reproduce the source exactly.
2. **Significant-token invariance** — formatting may only change *trivia* (spaces, tabs, newlines) and
   keyword casing. It must **never** add, remove, reorder or alter code tokens (no semantic change).
3. **Idempotence** — `format(format(x)) == format(x)`.
4. **Alignment uses tabs**, never spaces, for the column padding.

There are tests that check all of these — please keep them green.

## Adding a fix or a new case

1. Add a **regression test** first. For formatting behaviour, the tests live in
   `tests/STFormat.Tests/Formatting/` (see `StFormatterTests.cs` and `AlignmentTests.cs`); for the
   lexer, `tests/STFormat.Tests/Lexing/`. A test is usually just an input string and the expected output.
2. Implement the change in `STFormat.Core`.
3. Run `dotnet test STFormat.slnx` and make sure everything passes.

## Code style

- Match the style of the surrounding code (naming, comments, structure).
- Keep changes focused on the issue at hand; avoid unrelated refactors in the same PR.
- Prefer clear, small methods over cleverness — this is a tool people trust with their source.

## Pull requests

- Keep PRs small and focused; describe the problem and the fix.
- Make sure `dotnet test STFormat.slnx` passes.
- Reference the issue it addresses (e.g. `Fixes #12`).

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
