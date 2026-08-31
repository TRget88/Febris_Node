// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
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
    /// Audit C-09 -- editing a device INSERTED a duplicate instead of updating it.
    ///
    /// <para>
    /// <c>HardwareLogic.Update</c> built a brand-new <c>LocalHardware</c> with <c>//Id = input.Id</c>
    /// commented out and then called Update. <c>LocalHardware.Id</c> is store-generated, so EF saw
    /// a default key, marked the entity Added and INSERTED.
    /// </para>
    ///
    /// <para>
    /// Why that is worse than a lost update: <c>PhysicalLicense</c> is the device AUTHENTICATION
    /// credential, and the duplicate row carries the same one. <c>HardwareQueries.GetByKey</c>
    /// resolves it with an unordered <c>FirstOrDefaultAsync</c> over a column with no unique
    /// constraint and no index, so which of the two rows authenticates is arbitrary. Locking a
    /// device reported success while the device kept authenticating as the other row -- the node's
    /// only device-revocation control did nothing. The duplicate also took a fresh
    /// <c>uuid_generate_v4()</c> UUID, breaking anything that resolves the device by UUID.
    /// </para>
    ///
    /// <para>
    /// Scope note: these pin PERSISTENCE, not revocation latency. A persisted lock is enforced at
    /// token issuance (<c>HardwareKeyAuthorization:128</c>) and at refresh (<c>:178</c>, which
    /// re-reads the live row and refuses), and re-checked per request from the JWT claim
    /// (<c>AttributeClasses:125</c>). Because that last check reads the CLAIM rather than the
    /// database, a live access token stays valid until it expires -- 15 minutes
    /// (<c>HardwareKeyAuthorization:292</c>). The audit's "8-day refresh cache" is the refresh
    /// token's cache TTL, not the revocation window. Closing the remaining 15-minute gap is the
    /// pre-existing, in-code tracked item A-02 Stage 2.
    /// </para>
    /// </summary>
    public class HardwareEditPersistenceTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static HardwareLogic BuildLogic(DataDbContext context)
        {
            var accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());
            return new HardwareLogic(accessor.Object, new HardwareQueries(context), new Mock<IHardwareRevocationList>().Object,
                new ConfigurationBuilder().AddInMemoryCollection().Build());
        }

        [Fact]
        public async Task EditingADevice_UpdatesTheRow_InsteadOfInsertingADuplicate()
        {
            using DataDbContext context = BuildContext(nameof(EditingADevice_UpdatesTheRow_InsteadOfInsertingADuplicate));
            Guid originalUuid = Guid.NewGuid();
            context.Hardware.Add(new LocalHardware
            {
                UUID = originalUuid,
                DescriptiveName = "Lab unit 1",
                PhysicalLicense = "LICENSE-001",
                IsLockedOut = false
            });
            context.SaveChanges();
            long id = context.Hardware.Single().Id;

            HardwareLogic logic = BuildLogic(context);
            await logic.Update(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    Id = id,
                    HardwareKind = HardwareKind.LaptopPC,
                    DescriptiveName = "Lab unit 1 (renamed)",
                    PhysicalLicense = "LICENSE-001",
                    IsLockedOut = true
                }
            });

            context.Hardware.Count().Should().Be(1,
                "editing a device must UPDATE it -- a second row sharing PhysicalLicense makes which row authenticates arbitrary");

            LocalHardware stored = context.Hardware.Single();
            stored.Id.Should().Be(id);
            stored.UUID.Should().Be(originalUuid, "the device identity must survive an edit");
            stored.DescriptiveName.Should().Be("Lab unit 1 (renamed)");
            stored.IsLockedOut.Should().BeTrue("this is the node's only device-revocation control");
        }

        [Fact]
        public async Task LockingADevice_Persists_AndIsVisibleToTheCredentialLookup()
        {
            // The end-to-end shape of the defect: lock the device, then resolve it the way
            // authentication does. Before the fix this returned an unlocked duplicate.
            using DataDbContext context = BuildContext(nameof(LockingADevice_Persists_AndIsVisibleToTheCredentialLookup));
            context.Hardware.Add(new LocalHardware
            {
                UUID = Guid.NewGuid(),
                DescriptiveName = "Lab unit 2",
                // Seeded as the HASH, because that is what the database holds since the T9 change.
                // The lookup below still passes the PLAINTEXT, which is the production contract: the
                // device sends its credential, GetByKey hashes it and matches. Seeding cleartext
                // here would test a state the node can no longer be in.
                PhysicalLicense = Febris.SharedServices.DeviceCredential.Hash("LICENSE-002"),
                IsLockedOut = false
            });
            context.SaveChanges();
            long id = context.Hardware.Single().Id;

            HardwareLogic logic = BuildLogic(context);
            await logic.Update(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    Id = id,
                    HardwareKind = HardwareKind.LaptopPC,
                    DescriptiveName = "Lab unit 2",
                    // Deliberately omitted: Update no longer copies PhysicalLicense (T9). Leaving it
                    // null here also proves the edit does not blank the stored hash.
                    IsLockedOut = true
                }
            });

            LocalHardware byCredential = await new HardwareQueries(context).GetByKey("LICENSE-002");
            byCredential.Should().NotBeNull();
            byCredential.IsLockedOut.Should().BeTrue(
                "the row the credential resolves to must be the locked one -- with a duplicate present this was arbitrary");
        }

        [Fact]
        public async Task EditingAMissingDevice_ReturnsDefault_RatherThanCreatingOne()
        {
            using DataDbContext context = BuildContext(nameof(EditingAMissingDevice_ReturnsDefault_RatherThanCreatingOne));
            context.SaveChanges();

            HardwareLogic logic = BuildLogic(context);
            await logic.Update(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware { Id = 4242, HardwareKind = HardwareKind.LaptopPC, DescriptiveName = "ghost", PhysicalLicense = "X" }
            });

            context.Hardware.Count().Should().Be(0, "an edit of a nonexistent device must not create one");
        }
    }
}
