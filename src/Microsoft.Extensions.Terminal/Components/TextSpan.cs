// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// A span of styled text.
/// </summary>
public readonly record struct TextSpan(string Text, TerminalColor Color = TerminalColor.Default);
