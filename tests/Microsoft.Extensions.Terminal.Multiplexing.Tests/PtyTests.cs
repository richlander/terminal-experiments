// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// PTY tests - these require actual process spawning and may hang in some environments.
/// Run with: dotnet test --filter "Category=Pty"
/// </summary>
[Trait("Category", "Pty")]
public class PtyTests
{
    // Skipped by default - PTY tests can hang in CI
    // Run manually with: dotnet test --filter "Category=Pty"
}
