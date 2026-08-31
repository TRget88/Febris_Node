// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.SharedServices;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// <see cref="HardwareKind"/> is the node's ONLY source of truth for what kind of machine a
    /// device is.
    ///
    /// <para>
    /// The node used to keep its own copy of a HardwareType table, seeded with three rows, to fill
    /// a registration dropdown. That vocabulary belongs to the hub, and the node never read it to
    /// decide anything, so the copy has been removed entirely along with its seeder, queries and
    /// logic. These tests pin that registration works with no lookup of any kind present.
    /// </para>
    /// </summary>
    public class LocalHardwareKindTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static HardwareLogic BuildLogic(DataDbContext context)
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new HardwareLogic(accessor.Object, new HardwareQueries(context), new Mock<IHardwareRevocationList>().Object,
                new ConfigurationBuilder().AddInMemoryCollection().Build());
        }

        [Fact]
        public async Task Registering_StoresTheKind_WithNoLookupPresent()
        {
            using DataDbContext context = BuildContext(nameof(Registering_StoresTheKind_WithNoLookupPresent));

            // Create now MINTS the credential and returns it once; PhysicalLicense supplied here is
            // ignored (audit T9). These tests are about HardwareKind, so the credential is discarded.
            (LocalHardware created, _) = await BuildLogic(context).Create(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    HardwareKind = HardwareKind.MobileServer,
                    DescriptiveName = "Bay 3 server",
                    PhysicalLicense = "LICENSE-KIND-1"
                }
            });

            created.Should().NotBeNull("Create must return the PERSISTED row, not the caller's unsaved instance");
            created.Id.Should().NotBe(0, "the store-generated key must reach the caller");

            LocalHardware stored = context.Hardware.Single();
            stored.HardwareKind.Should().Be(HardwareKind.MobileServer);
        }

        [Fact]
        public async Task EditingToADifferentKind_UpdatesInPlace()
        {
            using DataDbContext context = BuildContext(nameof(EditingToADifferentKind_UpdatesInPlace));
            HardwareLogic logic = BuildLogic(context);

            // Credential minted and discarded -- see the note above.
            (LocalHardware created, _) = await logic.Create(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    HardwareKind = HardwareKind.LaptopPC,
                    DescriptiveName = "Reclassified unit",
                    PhysicalLicense = "LICENSE-KIND-3"
                }
            });

            await logic.Update(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    Id = created.Id,
                    HardwareKind = HardwareKind.DesktopPC,
                    DescriptiveName = "Reclassified unit",
                    PhysicalLicense = "LICENSE-KIND-3"
                }
            });

            context.Hardware.Count().Should().Be(1, "an edit must not duplicate the row (audit C-09)");
            context.Hardware.Single().HardwareKind.Should().Be(HardwareKind.DesktopPC);
        }

        [Fact]
        public async Task CreationPreperation_TouchesNoStore()
        {
            // Registration prep used to round-trip a lookup table to build a SelectList, and threw
            // out of a GET if that failed. The kind dropdown renders off the enum now, so an empty
            // database is enough to register a device.
            using DataDbContext context = BuildContext(nameof(CreationPreperation_TouchesNoStore));

            LocalHardwareCreationViewModel prep = await BuildLogic(context).CreationPreperation();

            prep.Should().NotBeNull();
            prep.Hardware.Should().NotBeNull("the view binds Hardware.HardwareKind directly");
            prep.Hardware.HardwareKind.Should().Be(HardwareKind.Unknown, "an unmade choice is the zero sentinel");
        }

        [Fact]
        public void TheEnumHasAZeroSentinel_AndNoDuplicateValues()
        {
            // The persisted column is NOT NULL, so every pre-existing row landed on 0 when the
            // column was added. Zero therefore has to mean "not determined" and never a real kind.
            HardwareKind[] all = Enum.GetValues(typeof(HardwareKind)).Cast<HardwareKind>().ToArray();

            all.Should().Contain(HardwareKind.Unknown);
            ((int)HardwareKind.Unknown).Should().Be(0);
            all.Select(k => (int)k).Should().OnlyHaveUniqueItems();
            all.Where(k => k != HardwareKind.Unknown).Should().OnlyContain(k => (int)k > 0);
        }
    }
}
