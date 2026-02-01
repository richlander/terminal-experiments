// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Terminal.Components;

/// <summary>
/// Specifies the border style for tables.
/// </summary>
public enum TableBorderStyle
{
    /// <summary>
    /// No border.
    /// </summary>
    None,

    /// <summary>
    /// Simple single-line border.
    /// </summary>
    Simple,

    /// <summary>
    /// Rounded corners with single-line border.
    /// </summary>
    Rounded,

    /// <summary>
    /// Double-line border.
    /// </summary>
    Double
}
