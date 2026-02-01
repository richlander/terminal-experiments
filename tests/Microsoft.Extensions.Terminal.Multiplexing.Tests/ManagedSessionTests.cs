// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.Extensions.Terminal.Multiplexing.Tests;

/// <summary>
/// ManagedSession tests - these require actual process spawning.
/// Run with: dotnet test --filter "Category=Pty"
/// </summary>
[Trait("Category", "Pty")]
public class ManagedSessionTests
{
    // These tests are skipped by default as they require PTY
    // Run with SKIP_PTY_TESTS=false to enable
}
