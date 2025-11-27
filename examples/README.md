# Corsinvest.Fx.Functional - Examples

Questo progetto contiene esempi pratici di utilizzo delle funzionalità di `Corsinvest.Fx.Functional`.

## Esempi Tap con Person Class

Il file `PipePersonExample.cs` mostra come usare `Tap` per modificare proprietà di oggetti in modalità fluent.

### 1. Basic Tap Example
Utilizzo base di `Tap` per impostare proprietà:

```csharp
var person = new Person()
    .Tap(p => p.Name = "Mario Rossi")
    .Tap(p => p.Age = 30)
    .Tap(p => p.Email = "mario.rossi@example.com");
```

### 2. Chained Tap Example
Costruzione completa di un oggetto con side effects:

```csharp
var person = new Person()
    .Tap(p => p.Name = "Giovanni Bianchi")
    .Tap(p => p.Age = 25)
    .Tap(p => p.Email = "giovanni.bianchi@example.com")
    .Tap(p => p.City = "Milano")
    .Tap(p => p.IsActive = true)
    .Tap(p => p.LastModified = DateTime.UtcNow)
    .Tap(p => Console.WriteLine($"Created: {p.Name}"));
```

### 3. Conditional Tap Example
Modifica proprietà solo quando la condizione è vera:

```csharp
bool shouldActivate = true;
bool shouldSetCity = false;

var person = new Person()
    .Tap(p => p.Name = "Laura Verdi")
    .Tap(p => p.Age = 28)
    .TapIf(shouldActivate, p => p.IsActive = true)
    .TapIf(shouldSetCity, p => p.City = "Roma")  // Non viene eseguito
    .Tap(p => p.Email = "laura.verdi@example.com");
```

### 4. Complex Pipeline Example
Combinazione di `Pipe` e `Tap` per trasformazioni complesse:

```csharp
var person = CreateBasePerson()
    .Tap(p => p.Name = p.Name.ToUpper())
    .Pipe(p => { p.Age += 1; return p; })
    .Tap(p => Console.WriteLine($"After age increment: {p.Age}"))
    .Tap(p => { if (p.Age >= 18) p.IsActive = true; })
    .Tap(p => p.LastModified = DateTime.UtcNow)
    .Tap(p => p.Email = GenerateEmail(p.Name));
```

### 5. Async Tap Example
Side effects asincroni con `TapAsync`:

```csharp
var person = await CreateBasePerson()
    .TapAsync(async p =>
    {
        p.Name = "Paolo Ferrari";
        await Task.Delay(10);
        Console.WriteLine("Name set asynchronously");
    })
    .TapAsync(async p =>
    {
        p.Email = await FetchEmailAsync(p.Name);
        Console.WriteLine($"Email fetched: {p.Email}");
    })
    .TapIfAsync(true, async p =>
    {
        await LogPersonAsync(p);
    });
```

## PersonDto Builder Example

Esempio reale di costruzione di un DTO con validazione e logging:

```csharp
public static PersonDto BuildPerson(string firstName, string lastName, int age, string email, bool enableLogging = false)
{
    return new PersonDto()
        .Tap(p => p.FirstName = firstName)
        .Tap(p => p.LastName = lastName)
        .Tap(p => p.FullName = $"{firstName} {lastName}")
        .TapIf(enableLogging, p => Console.WriteLine($"Building person: {p.FullName}"))
        .Tap(p => p.Age = age)
        .Tap(p => p.IsAdult = age >= 18)
        .Tap(p => p.Email = email)
        .Tap(p => p.NormalizedEmail = email.ToLowerInvariant().Trim())
        .TapIf(enableLogging, p => Console.WriteLine($"Email normalized: {p.NormalizedEmail}"))
        .Tap(p => p.CreatedAt = DateTime.UtcNow)
        .Tap(p =>
        {
            if (!p.IsAdult) Console.WriteLine($"Warning: {p.FullName} is a minor!");
        });
}
```

## Eseguire gli esempi

```bash
dotnet run --project examples/Corsinvest.Fx.Examples.csproj
```

## Output previsto

```
╔════════════════════════════════════════════════╗
║  Corsinvest.Fx.Functional - Tap Examples      ║
╚════════════════════════════════════════════════╝

=== Pipe Tap Examples with Person ===

1. Basic Tap Example:
Person { Name = Mario Rossi, Age = 30, Email = mario.rossi@example.com, ... }

2. Chained Tap Example:
Created: Giovanni Bianchi
Person { Name = Giovanni Bianchi, Age = 25, Email = giovanni.bianchi@example.com, ... }

3. Conditional Tap Example:
IsActive: True
City: '' (empty because condition was false)
Person { Name = Laura Verdi, Age = 28, Email = laura.verdi@example.com, ... }

4. Complex Pipeline Example (Pipe + Tap):
After age increment: 19
Person { Name = UNKNOWN, Age = 19, Email = unknown@example.com, ... }

5. Async Tap Example:
Name set asynchronously
Email fetched: paolo.ferrari@example.com
[LOG] Person created: Paolo Ferrari
Person { Name = Paolo Ferrari, Age = 18, Email = paolo.ferrari@example.com, ... }

=== PersonDto Builder Example ===

Building person: Marco Neri
Email normalized: marco.neri@example.com
PersonDto { FullName = Marco Neri, Age = 30, Email = marco.neri@example.com, IsAdult = True }

Warning: Sofia Gialli is a minor!
PersonDto { FullName = Sofia Gialli, Age = 16, Email = sofia.gialli@example.com, IsAdult = False }
```

## Vantaggi di Tap

1. **Fluent API**: Permette di concatenare operazioni in modo naturale e leggibile
2. **Separazione delle responsabilità**: Side effects (logging, audit) separati dalla logica di business
3. **Debugging facilitato**: Facile aggiungere/rimuovere log senza modificare la struttura del codice
4. **Condizionalità**: `TapIf` permette di eseguire side effects solo quando necessario
5. **Supporto async**: `TapAsync` per operazioni asincrone senza rompere il flusso

## Quando usare Tap

- Logging e debugging
- Modifica di proprietà di oggetti mutabili
- Audit e tracking
- Notifiche e alert
- Metriche e contatori
- Operazioni condizionali che non devono modificare il flusso principale
