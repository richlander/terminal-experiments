// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Defines a column in a table.
/// </summary>
public readonly record struct TableColumn(string Header, int? Width = null, Alignment Alignment = Alignment.Left);

/// <summary>
/// Defines a row in a table.
/// </summary>
public readonly record struct TableRow(TableCell[] Cells);

/// <summary>
/// Defines a cell in a table.
/// </summary>
public readonly record struct TableCell(string Text, TerminalColor? Color = null);

/// <summary>
/// Scroll direction indicator.
/// </summary>
public enum ScrollDirection
{
    Up,
    Down
}
