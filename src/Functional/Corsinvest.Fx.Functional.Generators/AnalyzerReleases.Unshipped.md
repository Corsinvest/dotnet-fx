; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION002 | Design | Error | Union type must be partial
UNION003 | Design | Error | Union variant must be partial
UNION004 | Design | Warning | Switch does not handle all union variants
UNION008 | Design | Error | Union case names collide
UNION009 | Design | Warning | Implicit conversions omitted for duplicate CLR case types
UNION010 | Design | Warning | Generic union root needs distinct case names
UNION012 | Design | Error | Union case type cannot be an interface
