// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.CompilerServices;

// Test-only visibility: the BLL test project exercises internal startup helpers (e.g.
// SeedData.SeedRolesAsync's fail-fast behavior) that are not part of the public surface.
[assembly: InternalsVisibleTo("Febris.UserNode.LogicLayer.Tests")]
