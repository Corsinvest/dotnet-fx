; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION001 | Generator | Error | Union generation failed

## Release 2.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION004 | Design | Warning | Switch does not handle all union variants
UNION008 | Design | Error | Union case names collide
UNION009 | Design | Warning | Implicit conversions omitted for duplicate CLR case types
UNION012 | Design | Error | Union case type cannot be an interface
UNION013 | Design | Error | Union root implements more than one IUnion<...>
UNION014 | Design | Error | Union root must be declared abstract partial
