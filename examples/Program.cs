using Corsinvest.Fx.Examples;

Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║       Corsinvest.Fx - Practical Examples v1.0            ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

// Core Examples (Priority High)
OptionBasics.Run();
ResultOfValidation.Run();
ResultOfRailway.Run();
UnionTypes.Run();
PipeWorkflow.Run();
CombinedPatterns.Run();

// Advanced Examples (Priority Medium)
OptionChaining.Run();
ResultOfRecover.Run();
await DeferAsync.Run();

// CompileTime Examples
CompileTimeBasics.Run();
await CompileTimeBasics.RunAsync();

Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
Console.WriteLine("║                  All Examples Completed!                  ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
