; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------

; UNION002, UNION003, and UNION010 were added and retired before ever shipping - none of them
; ever appeared in a "## Release" section of AnalyzerReleases.Shipped.md. RS2007 rejects a
; "Removed Rules" entry for a rule that has no prior shipped entry (the file must first record
; the rule as shipped before it can be removed), so - same as UNION002/UNION003 before them (see
; c5f2ce2923214088da4a450b26c1abd65efa7c8a) - UNION010 is dropped from "New Rules" outright
; rather than moved to a "Removed Rules" section here. Every rule that does ship is listed under
; "## Release 1.0.0" in AnalyzerReleases.Shipped.md, the first public release, which is why
; "New Rules" above is empty.
;
; UNION002 | Design | Error | Union type must be partial
; UNION003 | Design | Error | Union variant must be partial
; UNION010 | Design | Warning | Generic union root needs distinct case names
