# Pipe Extensions - Functional Pipeline Composition

**Universal pipe operator for fluent data transformation in C#**

## Overview

Pipe extensions enable F#-style forward piping in C#, allowing you to chain transformations in a readable left-to-right, top-to-bottom manner.

## The Problem

Traditional function composition in C# nests functions inside-out:

```csharp
// ❌ Hard to read
var result = FormatOutput(AddPrefix(ToUpper(Trim(input))));
```

## The Solution

```csharp
// ✅ Easy to read  
var result = input
    .Pipe(Trim)
    .Pipe(ToUpper)
    .Pipe(AddPrefix)
    .Pipe(FormatOutput);
```

---

**See full documentation at [docs/Pipe.md](./Pipe.md)**
