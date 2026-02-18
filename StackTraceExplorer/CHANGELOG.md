# Stack Trace Explorer Plugin - Changelog

## v1.0.20
- **Watson crash stack trace support**: Added `WatsonCrashParser` for Watson crash dump format
  - Format: `Module!Namespace.Class.Method+0x0 [source @ line]`
  - Handles module names with underscores (e.g., `mscorlib_ni`, `Microsoft_Crm_Core`)
  - Strips `_ni` suffix and converts underscores to dots for assembly name matching
  - Recognizes native modules (clr, ntdll, kernel32, etc.) as non-navigable frames
  - Handles generic type notation like `Task`1[[System.__Canon, mscorlib]]`

## v1.0.18
- **Clipboard emoji**: Use Unicode clipboard glyph (📋) for Paste button instead of Copy icon
- **Smaller buttons**: Reduced button padding for more compact toolbar

## v1.0.17
- **Icon buttons**: Replaced text labels with icons for Paste and Open buttons (uses ILSpy's standard icons)

## v1.0.16
- **Open button**: Added Open button to load stack trace files from disk (supports multiple file selection, each file opens in a new tab)

## v1.0.15
- **Display original text**: Show original un-parsed stack trace text in the list instead of reconstructed method signatures (preserves generic type args like `<bool>`)

## v1.0.14
- **Fixed generic type argument parsing**: Generic args like `<System.Guid>` now correctly parsed (handles dots inside angle brackets)
- **Improved regex pattern**: Uses non-greedy matching for better handling of nested parentheses
- **Added unit test project**: `StackTraceExplorer.Tests` with comprehensive parser tests

## v1.0.13
- **Fixed generic method overload selection for WinDbg format**: WinDbgParser was missing generic method arity extraction, causing `Execute<bool>` to navigate to non-generic `Execute` instead of `Execute<TResult>`

## v1.0.12
- **Fixed generic method overload selection**: When multiple overloads exist (generic and non-generic), now correctly selects the generic version based on type parameter count

## v1.0.11
- **Fixed generic method resolution**: Strip concrete type arguments like `<bool>` from method names (e.g., `Execute<bool>` -> `Execute`)
- **Fixed search for generic methods**: Search now uses method name without generic type arguments

## v1.0.10
- **Fixed generic type resolution**: Handle apostrophe notation (`'1`) in addition to backtick (`` `1 ``) for generic arity
- **Fixed generic type arguments**: Strip type arguments like `<System.Guid>` and `<object>` from type names before resolution
- **Fixed generic closure types**: Handle lambda/closure types like `<>c__DisplayClass3_0'1<object>`

## v1.0.9
- **Multiple tabs support**: Paste button now creates a new tab for each stack trace
- Compact layout: Paste button and tab headers on the same line
- Close tabs with X button or middle-click
- Each tab shows its own title based on the first method in the stack trace

## v1.0.8
- **Performance fix improved**: Use `LoadedAssembly.GetTypeSystemOrNull()` instead of our own cache
- This reuses ILSpy's existing cached type systems, avoiding duplication and ensuring consistency with Search/Analyzer

## v1.0.7
- **Major performance fix**: Cache `DecompilerTypeSystem` instances instead of creating new ones for every frame/assembly combination
- This eliminates the ~36% CPU overhead seen when resolving stack traces with many frames

## v1.0.6
- Fixed constructor resolution: methods like `ClassName.ClassName()` are now correctly resolved to `.ctor`

## v1.0.5
- Double-click on unresolved frames now triggers search
- Single-click behavior unchanged for resolved frames (navigates directly)
- Enter key now works for both resolved (navigate) and unresolved (search) frames
- Hand cursor shown for unresolved method frames with "Double-click to search" tooltip
- Added claude.md for development conventions

## v1.0.4
- More relaxed parsing for truncated stack trace lines
- Fallback to Search pane when clicking on unresolved frames
- Fixed parsing for lines without closing parentheses

## v1.0.3
- Simplified UI: removed Clear/Copy/Input buttons and text editor
- Single-click navigation to decompiled code
- Generic method handling with arity matching
- Persistence: last pasted stack trace saved to temp file and auto-loaded

## v1.0.2
- Added DataTemplate attribute to connect View to ViewModel
- Added TomsToolbox.Wpf.Composition.AttributedModel package reference
- Fixed plugin not showing proper UI

## v1.0.1
- Added Paste button to toolbar
- Added version number display in menu item

## v1.0.0
- Initial implementation
- Parse standard .NET and WinDbg stack trace formats
- Resolve methods against loaded assemblies
- Navigate to decompiled code on click
- Visual indicators for navigable vs non-navigable frames
- Support for async state machines and lambda expressions
