# Stack Trace Explorer Plugin - Development Rules

## Build Configuration
- Always use **Release** configuration: `dotnet build StackTraceExplorer/StackTraceExplorer.csproj -c Release`
- Output goes to: `ILSpy\bin\Release\net10.0-windows\StackTraceExplorer.Plugin.dll`

## Version Management
- Version number is in `StackTraceExplorer.csproj` `<Version>` tag
- Version is also displayed in menu item header in `ShowStackTraceExplorerCommand.cs`
- **Increment version** on every change
- Keep both locations in sync

## Changelog
- Maintain `CHANGELOG.md` with version history
- Format:
  ```markdown
  ## v1.0.X
  - Change description
  ```

## Commits
- Auto-commit after each change (but don't push)
- Use proper summary describing the changes
- Co-Author line: `Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>`

## UI Behavior
- **Single-click**: Navigate to resolved (green checkmark) frames
- **Double-click**: Search for unresolved (orange ?) frames
- **Paste button**: Only UI element in toolbar
- **Persistence**: Last stack trace saved to temp file, auto-loaded on view open

## Stack Trace Formats Supported
1. Standard .NET: `at Namespace.Class.Method(Type param)`
2. WinDbg: `Assembly.dll!Namespace.Class.Method(params) (IL≈0xHEX, Native=...)`
3. Watson Crash: `Module!Namespace.Class.Method+0x0 [source @ line]`
4. WinDbg !clrstack: `HexSP HexIP Namespace.Class.Method(params)`
5. Relaxed parsing for truncated lines (missing closing parens)

## Key Files
- `StackTraceExplorerModel.cs` - Main ViewModel with commands and navigation
- `StackTraceExplorerView.xaml/.cs` - UI with DataTemplate attribute
- `ShowStackTraceExplorerCommand.cs` - Menu command with version
- `Parsers/StandardNetParser.cs` - Relaxed regex parsing
- `Parsers/WinDbgParser.cs` - WinDbg format parsing
- `Parsers/WatsonCrashParser.cs` - Watson crash dump format parsing
- `Parsers/ClrStackParser.cs` - WinDbg !clrstack format parsing
- `MethodResolver.cs` - Method resolution with generic arity matching

## Dependencies
- `TomsToolbox.Wpf.Composition.AttributedModel` - For `[DataTemplate]` attribute
- References to `ILSpy.csproj` and `ICSharpCode.Decompiler.csproj`

## Default Position
- Tool pane should dock at **bottom** (like Analyzer pane)
