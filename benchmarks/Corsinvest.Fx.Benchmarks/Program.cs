/*
 * SPDX-FileCopyrightText: Copyright Corsinvest Srl
 * SPDX-License-Identifier: MIT
 */

using BenchmarkDotNet.Running;

// No arguments runs the interactive picker; `--filter *Option*` or `--filter *` runs a selection
// directly, which is what CI and the README's commands use.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

/// <summary>Entry point marker, so the switcher has an assembly to scan.</summary>
public partial class Program;
