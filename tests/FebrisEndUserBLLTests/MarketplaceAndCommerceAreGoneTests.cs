// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the hub-capability teardown (owner ruling 2026-08-01). Commerce/billing and the
    /// marketplace are permanently-closed hub capabilities (OSS_RELEASE_MAP 4.1.3) and no
    /// marketplace or commerce model has ever had a table on the node -- confirmed against a live
    /// database: zero matching DbSets, zero references in any non-designer migration, zero tables.
    ///
    /// <para>
    /// They were nevertheless still shipping as node code: controllers, logic, remote queries,
    /// views and nav that a self-hoster could click into and that always rendered empty, because
    /// the hub-federation gate defaults closed. This guard fails if any of it comes back.
    /// </para>
    ///
    /// <para>
    /// IMPORTANT: the MODELS deliberately survive in shared/FebrisModelLibrary. The marketplace may
    /// reopen later and would need them, and that assembly is compiled by the hub too. This guard
    /// therefore asserts on the NODE assemblies only -- asserting on the model library would be
    /// wrong and would fail for the right reasons at the wrong layer.
    /// </para>
    /// </summary>
    public class MarketplaceAndCommerceAreGoneTests
    {
        private static Assembly LogicLayer => typeof(Febris.UserNode.LogicLayer.Logic.DataLogic.ICohortLogic).Assembly;
        private static Assembly DataAccessLayer => typeof(Febris.UserNode.DataAccessLayer.Queries.DataQueries.IHardwareLinkedModuleQueries).Assembly;
        private static Assembly Portal => typeof(Febris.UserNode.Portal.Startup).Assembly;

        public static TheoryData<string> BannedTypeNames => new TheoryData<string>
        {
            // marketplace
            "IMarketplaceListingLogic", "MarketplaceListingLogic",
            "IPrivateMarketplaceListingWhiteListLogic", "PrivateMarketplaceListingWhiteListLogic",
            "IMarketplaceListingQueries", "MarketplaceListingQueries",
            "MarketplaceController", "MarketplaceListingController",
            "PrivateMarketplaceListingWhiteListController",
            // commerce
            "IPurchaseLogic", "PurchaseLogic", "IPurchaseQueries", "PurchaseQueries",
            "IPurchaseDisputeLogic", "PurchaseDisputeLogic",
            "IInvoiceLogic", "InvoiceLogic", "IInvoiceQueries", "InvoiceQueries",
            "PurchaseController", "PurchaseDisputeController", "InvoiceController",
            // marketplace-scoped taxonomy: node plumbing only, models stay in ModelLibrary
            "ICategoryLogic", "CategoryLogic", "IIndustryLogic", "IndustryLogic",
            "IFocusLogic", "FocusLogic", "ITagLogic", "TagLogic",
            "CategoryQueries", "IndustryQueries", "FocusQueries", "TagQueries",
        };

        [Theory]
        [MemberData(nameof(BannedTypeNames))]
        public void NoNodeAssemblyDeclaresTheTornDownTypes(string typeName)
        {
            Assembly[] nodeAssemblies = new[] { LogicLayer, DataAccessLayer, Portal };

            string[] offenders = nodeAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => string.Equals(t.Name, typeName, StringComparison.Ordinal))
                .Select(t => t.Assembly.GetName().Name + " :: " + t.FullName)
                .ToArray();

            offenders.Should().BeEmpty(
                "{0} is a hub capability torn out of the node; it must not be reintroduced into a node assembly",
                typeName);
        }

        [Fact]
        public void TheSharedModelsSurvive_BecauseTheMarketplaceMayReopen()
        {
            // The counterpart to the guard above: deleting node plumbing must never turn into
            // deleting the shared models. The hub compiles this assembly and a reopened
            // marketplace would need them.
            Assembly models = typeof(Febris.ModelLibrary.Models.DataModels.Category).Assembly;

            foreach (string name in new[] { "Category", "Industry", "Focus", "Tag", "MarketplaceListing", "Purchase", "Invoice" })
            {
                models.GetTypes().Any(t => t.Name == name)
                    .Should().BeTrue("{0} must remain in ModelLibrary for a future marketplace", name);
            }
        }
    }
}
