; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
UNION004 | Design | Warning | Switch does not handle all union variants
UNION008 | Design | Error | Union case names collide
UNION009 | Design | Warning | Implicit conversions omitted for duplicate CLR case types
UNION012 | Design | Error | Union case type cannot be an interface

; UNION002, UNION003, and UNION010 were added and retired within this same unshipped release,
; without ever appearing in AnalyzerReleases.Shipped.md. RS2007 rejects a "Removed Rules" entry
; for a rule that has no prior shipped entry ("Aggiungere invece una voce 'Removed' distinta per
; la regola nel file di versione non distribuito" / "the file must first record the rule as
; shipped before it can be removed"), so - same as UNION002/UNION003 before them (see
; c5f2ce2923214088da4a450b26c1abd65efa7c8a) - UNION010 is dropped from "New Rules" outright rather
; than moved to a "Removed Rules" section here.
;
; UNION002 | Design | Error | Union type must be partial
; UNION003 | Design | Error | Union variant must be partial
; UNION010 | Design | Warning | Generic union root needs distinct case names
