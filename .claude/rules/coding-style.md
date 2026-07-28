---
alwaysApply: true
description: >
  Enforces modern C# 14 coding conventions, naming standards, and file
  organization for all .NET code in this repository.
---

# C# Coding Style

## File Organization

- **File-scoped namespaces always.** Block-scoped namespaces waste indentation for zero benefit.
- **One type per file.** File name must match the type name exactly (`OrderService.cs` contains `OrderService`).
- **Order members:** constants, fields, constructors, properties, public methods, private methods. Consistent ordering reduces cognitive load when scanning a file.

## Type Declarations

- **Primary constructors for DI injection.** Eliminates boilerplate field assignments and `_field = field` ceremony.

```csharp
// DO
public sealed class OrderService(IDbContext db, TimeProvider clock) { }

// DON'T
public class OrderService
{
    private readonly IDbContext _db;
    public OrderService(IDbContext db) { _db = db; }
}
```

- **Records for DTOs and value objects.** Immutability, value equality, and `with` expressions for free.

```csharp
public sealed record CreateOrderRequest(string ProductId, int Quantity);
public sealed record Money(decimal Amount, string Currency);
```

- **`sealed` on classes not designed for inheritance.** The JIT can devirtualize calls on sealed types, and it communicates intent clearly.
- **`internal` by default, `public` only when needed.** Minimize the public API surface. If nothing outside the project references it, it should be `internal`.

## Expressions and Patterns

- **Collection expressions over constructor calls.** Shorter, compiler-optimized, and consistent across collection types.

```csharp
// DO
List<int> ids = [1, 2, 3];
int[] arr = [4, 5, 6];

// DON'T
var ids = new List<int> { 1, 2, 3 };
```

- **Avoid `out` parameters when refactoring.** They hide where a value comes from. Compute it inline at the point of use, or return a tuple, instead.

```csharp
// DO
if (checkOut <= checkIn) return Failure(...);
var nights = checkOut.DayNumber - checkIn.DayNumber; // computed where it's used

// DON'T
bool Validate(DateOnly checkIn, DateOnly checkOut, out int nights) { nights = ...; ... }
```

- **Pattern matching over if-else chains.** Switch expressions and `is` patterns are more readable and exhaustiveness-checked.

```csharp
// DO
var label = status switch
{
    OrderStatus.Pending => "Awaiting payment",
    OrderStatus.Shipped => "On the way",
    _ => "Unknown"
};

// DON'T
string label;
if (status == OrderStatus.Pending) label = "Awaiting payment";
else if (status == OrderStatus.Shipped) label = "On the way";
else label = "Unknown";
```

## Naming and Modifiers

- **`var` for obvious types, explicit types when clarity matters.** Use `var` when the right-hand side makes the type self-evident (`var order = new Order()`); spell it out when it does not (`HttpResponseMessage response = await ...`).
- **Async suffix on all async methods.** `GetOrderAsync`, not `GetOrder`, for methods returning `Task` or `ValueTask`. Prevents accidental sync calls.
- **PascalCase** for public members, types, namespaces, and methods. **camelCase** for local variables and parameters.
- **No `_` prefix on private fields when using primary constructors.** The parameter name is the field name.

## Comments (SonarAnalyzer S125 is disabled)

SonarAnalyzer's S125 ("Remove this commented out code") pattern-matches comment *text* against shapes that look like statements — it doesn't understand prose. Narrative comments kept tripping it as false positives even with no real dead code involved, and rewording around every hit stopped scaling. **S125 is disabled project-wide** (`dotnet_diagnostic.S125.severity = none` in `.editorconfig`) — it is no longer the enforcement mechanism here. Real commented-out dead code should still be deleted on sight in review; git history is the record, not a comment block.

The style habits below are no longer analyzer-enforced but are still good practice for writing comments that read as prose, not as broken code:

- **Use the Unicode arrow `→` for "leads to," never ASCII `->`.** `->` is a real operator token in C-family languages and reads as code, especially paired with a trailing `;`.
- **Never end a comment line with `);`.** A closing paren immediately followed by a semicolon reads exactly like a terminated method call (`(self-service paths);`). Rephrase so the parenthetical isn't the last thing before the terminator, or drop the semicolon for a period.
- **Prefer periods over semicolons between clauses.** Sonar keys off semicolons as a statement-end signal; periods don't trigger it.
- **Avoid dot-prefixed method-call notation** (`.IsConcurrencyToken()`) in prose. Say "marks it as a concurrency token" instead of naming the fluent API call with its parens attached.
- **Avoid `ALLCAPS/ALLCAPS;` patterns** (e.g. `UPDATE/DELETE;`) — spell it out (`UPDATE and DELETE statements.`).
- **Deliberate illustrative code in design-note files** (e.g. `Guidance.cs`) should stay as prose/pseudo-code, not real C# syntax — Sonar can't distinguish an intentional example from dead code, and real dead code should just be deleted (git history is the record), not commented out.

```csharp
// DO
// 3. Load the TRACKED booking. Not found → Not Found.
//    Already cancelled → Conflict (cancellation is terminal).

// DON'T
// 3. Load the TRACKED booking - not found -> Not Found;
//    already cancelled -> Conflict (cancellation is terminal).
```
