# Proposal: Conditional Pipeline Extensions

**Status**: 📋 Proposal
**Target Version**: v1.1 (Post-v1.0)
**Created**: 2025-10-15
**Decision**: ✅ **Raccomandato `PipeIf` per v1.1**

---

## 🎯 Executive Summary

**Problema**: Dynamic query building in LINQ richiede codice verboso con variabili mutabili.

**Soluzione**: Aggiungere `PipeIf` a `Corsinvest.Fx.Functional` per conditional transformations.

**Motivazione**: Pattern universale, filosoficamente aligned, zero nuovo package.

**Effort**: ~1 ora (2 metodi, 15 test, docs).

---

## 📋 Il Problema

### Scenario: Dynamic Query Building

Quando costruisci query LINQ dinamiche basate su user input, il codice diventa non-fluent:

```csharp
// ❌ PROBLEMA: Verbose, mutabile, non fluent
var users = dbContext.Users.AsQueryable();

if (!string.IsNullOrEmpty(nameFilter))
    users = users.Where(u => u.Name.Contains(nameFilter));

if (minAge.HasValue)
    users = users.Where(u => u.Age >= minAge.Value);

if (isActive.HasValue)
    users = users.Where(u => u.IsActive == isActive.Value);

return await users.ToListAsync();
```

**Pain Points:**

- ❌ Variabile mutabile riassegnata continuamente
- ❌ Non fluent (nessuna chain)
- ❌ Verbose (4+ righe per filtro)

---

## 💡 La Soluzione: `PipeIf`

### Implementazione

Aggiungi 2 metodi a `PipeExtensions.cs`:

```csharp
/// <summary>
/// Conditionally applies a transformation. Returns value unchanged if condition is false.
/// </summary>
/// <remarks>
/// Universal pattern for conditional transformations. Works with any type including LINQ queries.
/// </remarks>
public static T PipeIf<T>(this T value, bool condition, Func<T, T> transform)
    => condition ? transform(value) : value;

/// <summary>
/// Applies one of two transformations based on condition.
/// </summary>
public static T PipeIf<T>(this T value, bool condition,
    Func<T, T> trueTransform,
    Func<T, T> falseTransform)
    => condition ? trueTransform(value) : falseTransform(value);
```

### Utilizzo

```csharp
// ✅ SOLUZIONE: Clean, fluent, immutable
return await dbContext.Users
    .PipeIf(!string.IsNullOrEmpty(nameFilter), q => q.Where(u => u.Name.Contains(nameFilter)))
    .PipeIf(minAge.HasValue, q => q.Where(u => u.Age >= minAge.Value))
    .PipeIf(isActive.HasValue, q => q.Where(u => u.IsActive == isActive.Value))
    .ToListAsync();

// ✅ Con 2 branch
var query = users
    .PipeIf(sortAscending,
        q => q.OrderBy(x => x.Name),
        q => q.OrderByDescending(x => x.Name));

// ✅ Universal - non solo LINQ
var config = new Config()
    .PipeIf(isDevelopment, c => c.WithDebugMode())
    .PipeIf(useCache, c => c.WithCache());
```

---

## ✅ Perché `PipeIf`?

### Pro

| Aspetto          | Valore                                                        |
| ---------------- | ------------------------------------------------------------- |
| **Filosofia**    | ✅ "Conditional pipe composition" = pattern, non utility      |
| **Universalità** | ✅ Funziona con LINQ + qualsiasi tipo                         |
| **Package**      | ✅ Zero nuovo package (aggiungi a `PipeExtensions`)           |
| **Scope creep**  | ✅ Pattern finito (2 metodi), niente da aggiungere            |
| **Coerenza**     | ✅ Completa famiglia: `Pipe`, `PipeWhen`, `PipeTap`, `PipeIf` |
| **Effort**       | ✅ ~1 ora totale                                              |

### Pattern Alignment

```csharp
// Corsinvest.Fx ha già:
value.Pipe(transform)              // Unconditional
value.PipeWhen(pred, t1, t2)       // Conditional su valore
value.PipeTap(action)              // Side effect

// Aggiungiamo:
value.PipeIf(condition, t1, t2)    // Conditional su flag esterno ← NEW
```

---

## 🔄 Alternativa: `WhereIf`

### Cosa Sarebbe

Nuovo package `Corsinvest.Fx.Linq` con LINQ-specific API:

```csharp
public static IQueryable<T> WhereIf<T>(
    this IQueryable<T> source,
    bool condition,
    Expression<Func<T, bool>> predicate)
    => condition ? source.Where(predicate) : source;

// Utilizzo
return await dbContext.Users
    .WhereIf(!string.IsNullOrEmpty(nameFilter), u => u.Name.Contains(nameFilter))
    .WhereIf(minAge.HasValue, u => u.Age >= minAge.Value)
    .ToListAsync();
```

### Perché NO (per ora)

| Aspetto           | Problema                                                  |
| ----------------- | --------------------------------------------------------- |
| **Nuovo package** | ⚠️ Maintenance overhead                                   |
| **Scope creep**   | ⚠️ Rischio richieste `SelectIf`, `OrderByIf`, `TakeIf`... |
| **Filosofia**     | ⚠️ Più "utility" che "pattern moderno"                    |
| **Universalità**  | ⚠️ Solo LINQ, non generalizzabile                         |

**Policy**: Consideriamo `WhereIf` **solo se** la community chiede esplicitamente dopo v1.1.

---

## 📊 Comparison Matrix

| Criterio               | `PipeIf`            | `WhereIf`            | Documentation Only   |
| ---------------------- | ------------------- | -------------------- | -------------------- |
| **Readability (LINQ)** | ⭐⭐⭐ Good         | ⭐⭐⭐⭐⭐ Excellent | ⭐⭐ Fair            |
| **Universalità**       | ⭐⭐⭐⭐⭐ Any type | ⭐⭐ LINQ only       | ⭐⭐⭐ DIY           |
| **Philosophy fit**     | ⭐⭐⭐⭐⭐ Pattern  | ⭐⭐⭐ Utility       | ⭐⭐⭐⭐ Empowerment |
| **Maintenance**        | ⭐⭐⭐⭐⭐ Minimal  | ⭐⭐⭐ New package   | ⭐⭐⭐⭐⭐ Zero      |
| **Scope creep risk**   | ⭐⭐⭐⭐⭐ None     | ⭐⭐ High            | ⭐⭐⭐⭐⭐ N/A       |
| **Time to implement**  | ~1 hour             | ~2 hours             | ~15 min              |

**Winner**: `PipeIf` (balance ottimale tra utility e philosophy)

---

## 🚀 Implementation Plan

### Step 1: Code (15 min)

**File**: `src/Corsinvest.Fx.Functional/PipeExtensions.cs`

Aggiungi 2 metodi con XML docs completi.

### Step 2: Tests (20 min)

**File**: `tests/Corsinvest.Fx.Functional.Tests/PipeExtensionsTests.cs`

```csharp
public class PipeIfTests
{
    [Fact] public void PipeIf_WhenTrue_AppliesTransform() { ... }

    [Fact] public void PipeIf_WhenFalse_ReturnsOriginal() { ... }

    [Fact] public void PipeIf_WithTwoBranches_AppliesTrueBranch() { ... }

    [Fact] public void PipeIf_WithLinqQuery_AppliesConditionalFilter() { ... }

    [Fact] public void PipeIf_CanChainMultipleConditions() { ... }
}
```

**Target**: 15-20 test, 95%+ coverage

### Step 3: Documentation (10 min)

**File**: `src/Corsinvest.Fx.Functional/README.md`

Aggiungi sezione "Conditional Pipelines with PipeIf" con esempi LINQ + general purpose.

### Step 4: Examples (15 min)

Aggiungi esempio in `/examples` se necessario, oppure aggiorna esempi esistenti.

**Total Effort**: ~1 ora

---

## 📚 Use Cases

### 1. Dynamic LINQ Query Building

```csharp
var users = await dbContext.Users
    .PipeIf(hasNameFilter, q => q.Where(u => u.Name.Contains(nameFilter)))
    .PipeIf(hasAgeFilter, q => q.Where(u => u.Age >= minAge))
    .PipeIf(hasRoleFilter, q => q.Where(u => u.Roles.Contains(role)))
    .ToListAsync();
```

### 2. Configuration Pipelines

```csharp
var config = new ConfigBuilder()
    .PipeIf(isDevelopment, c => c.WithDebugMode())
    .PipeIf(useCache, c => c.WithCache())
    .PipeIf(logLevel == "verbose", c => c.WithVerboseLogging())
    .Build();
```

### 3. Builder Pattern con Optional Steps

```csharp
var http = new HttpClientBuilder()
    .PipeIf(useAuth, b => b.WithAuthentication(token))
    .PipeIf(useRetry, b => b.WithRetryPolicy(3))
    .PipeIf(useTimeout, b => b.WithTimeout(TimeSpan.FromSeconds(30)))
    .Build();
```

### 4. Conditional Transformations

```csharp
var data = rawData
    .PipeIf(needsNormalization, d => d.Normalize())
    .PipeIf(needsValidation, d => d.Validate())
    .PipeIf(needsEnrichment, d => d.Enrich());
```

---

## ✅ Decision Framework (PROJECT.md)

| Domanda                            | Risposta                 | Valutazione      |
| ---------------------------------- | ------------------------ | ---------------- |
| C# ha questa feature nativa?       | ❌ NO                    | ✅ Considera     |
| È un pattern riconosciuto e utile? | ✅ YES                   | ✅ Considera     |
| Risolve un problema reale?         | ✅ YES (dynamic queries) | ✅ Alta priorità |
| È semplice da capire?              | ✅ YES                   | ✅ Buon fit      |
| Richiede dipendenze esterne?       | ❌ NO                    | ✅ Buon fit      |
| La community lo chiede?            | ⚠️ UNKNOWN               | ⚠️ Wait & see    |

**Risultato**: **5/6 criteri passano** → Feature candidata per v1.1

---

## 🎯 Raccomandazione Finale

### ✅ Per v1.1: Implementa `PipeIf`

**Reasoning**:

1. Pattern universale, non LINQ-specific utility
2. Completa la famiglia `Pipe*` in modo coerente
3. Zero nuovo package da mantenere
4. Minimal scope (2 metodi, finito)
5. ~1 ora effort totale
6. Filosoficamente aligned con project vision

### ⏸️ Per v1.0: Niente

Focus su roadmap attuale (README + Release).
LINQ è "nice-to-have", non "must-have" per v1.0.

### 🔮 Future: `WhereIf` solo se richiesto

Se community chiede esplicitamente `WhereIf` **con use cases concreti**:

- Crea `Corsinvest.Fx.Linq` package
- Include **solo** `WhereIf` (4 overloads)
- Policy "add on demand" per altri metodi (`SelectIf`, `OrderByIf`, etc.)

---

## 📝 Success Criteria

Per considerare questa feature un successo dopo v1.1:

1. ✅ **Adoption**: Usato in almeno 2-3 esempi nella documentazione
2. ✅ **No confusion**: Zero issue "non capisco PipeIf" nel primo mese
3. ✅ **No scope creep**: Zero richieste per LINQ-specific methods nel primo mese
4. ✅ **Test coverage**: 95%+ coverage
5. ✅ **Performance**: Zero overhead vs ternary manual

---

**Fine del documento**
