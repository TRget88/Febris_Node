// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
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
    /// Audit T9, the last member: the device authentication credential was stored in CLEARTEXT, was
    /// whatever free text an admin typed, and was rendered in FOUR Portal views -- including the
    /// device LIST, which printed every credential on the node on one page to any Educator, Admin or
    /// ITAdmin.
    ///
    /// <para>
    /// It is now MINTED by the node with 256 bits of entropy, shown exactly once, and stored only as
    /// a hash. Generation and hashing had to ship together: hashing an admin-chosen string would be
    /// worse than the status quo -- low entropy AND unrecoverable.
    /// </para>
    ///
    /// <para>
    /// The migration that converted existing rows was verified against the real database: a device
    /// holding the credential "test" ended up holding its SHA-256, still resolved by hashing what it
    /// sends, and a second run of the UPDATE changed 0 rows. Already-provisioned devices keep
    /// working because only the STORED form changed, not the protocol.
    /// </para>
    /// </summary>
    public class DeviceCredentialLifecycleTests
    {
        private static DataDbContext BuildContext(string dbName)
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName).Options);
        }

        private static HardwareLogic BuildLogic(DataDbContext context, params string[] roles)
        {
            return BuildLogic(context, new Mock<IHardwareRevocationList>(), roles);
        }

        /// <summary>
        /// Overload that lets a test hold the revocation mock, so the regeneration tests can assert
        /// on what was published rather than only on what was written to the row.
        /// </summary>
        private static HardwareLogic BuildLogic(
            DataDbContext context, Mock<IHardwareRevocationList> revocations, params string[] roles)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                roles.Select(r => new Claim(ClaimTypes.Role, r)), "test");
            DefaultHttpContext http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();

            return new HardwareLogic(accessor.Object, new HardwareQueries(context), revocations.Object, config);
        }

        private static HardwareLogic AdminLogic(DataDbContext context)
        {
            return BuildLogic(context, InstitutionUserAccountType.Admin.ToString());
        }

        private static LocalHardwareCreationViewModel NewDevice(string typedLicense = null)
        {
            return new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    HardwareKind = HardwareKind.MobileServer,
                    DescriptiveName = "Bay 1 headset",
                    PhysicalLicense = typedLicense
                }
            };
        }

        [Fact]
        public async Task RegisteringADevice_StoresOnlyTheHash_AndReturnsThePlaintextOnce()
        {
            using DataDbContext context = BuildContext(nameof(RegisteringADevice_StoresOnlyTheHash_AndReturnsThePlaintextOnce));

            (LocalHardware created, string credential) = await AdminLogic(context).Create(NewDevice());

            credential.Should().NotBeNullOrEmpty("the operator has to be given something to put in the device");

            LocalHardware stored = context.Hardware.Single();
            stored.PhysicalLicense.Should().NotBe(credential, "the CREDENTIAL must never reach the database");
            stored.PhysicalLicense.Should().Be(DeviceCredential.Hash(credential));
            DeviceCredential.LooksHashed(stored.PhysicalLicense).Should().BeTrue();

            created.Should().NotBeNull();
        }

        [Fact]
        public async Task AnAdminTypedCredentialIsIgnored()
        {
            // The whole point of minting. If a typed value were accepted and then hashed, the
            // credential would be BOTH low-entropy and unrecoverable.
            using DataDbContext context = BuildContext(nameof(AnAdminTypedCredentialIsIgnored));

            (_, string credential) = await AdminLogic(context).Create(NewDevice("hunter2"));

            credential.Should().NotBe("hunter2");
            context.Hardware.Single().PhysicalLicense.Should().NotBe(DeviceCredential.Hash("hunter2"));
        }

        [Fact]
        public async Task TheStoredValueCannotBeUsedAsACredential()
        {
            // A stolen database row must not be replayable as a login. Hashing the stored hash gives
            // a different value, so presenting it would not resolve the device.
            using DataDbContext context = BuildContext(nameof(TheStoredValueCannotBeUsedAsACredential));

            await AdminLogic(context).Create(NewDevice());
            string stored = context.Hardware.Single().PhysicalLicense;

            DeviceCredential.Hash(stored).Should().NotBe(stored,
                "if the stored value hashed to itself, reading the table would be enough to authenticate");
        }

        [Fact]
        public async Task TwoDevicesGetDifferentCredentials()
        {
            using DataDbContext context = BuildContext(nameof(TwoDevicesGetDifferentCredentials));
            HardwareLogic logic = AdminLogic(context);

            (_, string first) = await logic.Create(NewDevice());
            (_, string second) = await logic.Create(NewDevice());

            second.Should().NotBe(first);
            context.Hardware.Select(h => h.PhysicalLicense).Distinct().Should().HaveCount(2);
        }

        [Fact]
        public async Task RegeneratingIssuesANewCredentialAndInvalidatesTheOld()
        {
            // The ONLY recovery path, because the stored hash cannot be reversed.
            using DataDbContext context = BuildContext(nameof(RegeneratingIssuesANewCredentialAndInvalidatesTheOld));
            HardwareLogic logic = AdminLogic(context);

            (LocalHardware created, string original) = await logic.Create(NewDevice());
            string originalHash = context.Hardware.Single().PhysicalLicense;

            string replacement = await logic.RegenerateCredential(created.Id);

            replacement.Should().NotBeNullOrEmpty();
            replacement.Should().NotBe(original);

            string newHash = context.Hardware.Single().PhysicalLicense;
            newHash.Should().Be(DeviceCredential.Hash(replacement));
            newHash.Should().NotBe(originalHash, "the previous credential must stop working immediately");
        }

        [Fact]
        public async Task RegeneratingAMissingDeviceReturnsNull()
        {
            using DataDbContext context = BuildContext(nameof(RegeneratingAMissingDeviceReturnsNull));

            (await AdminLogic(context).RegenerateCredential(999999)).Should().BeNull();
        }

        [Fact]
        public async Task RegeneratingIsRefusedForAnUnprivilegedCaller()
        {
            using DataDbContext context = BuildContext(nameof(RegeneratingIsRefusedForAnUnprivilegedCaller));

            (LocalHardware created, _) = await AdminLogic(context).Create(NewDevice());
            string before = context.Hardware.Single().PhysicalLicense;

            HardwareLogic unprivileged = BuildLogic(context, InstitutionUserAccountType.User.ToString());
            string result = await unprivileged.RegenerateCredential(created.Id);

            result.Should().BeNull();
            context.Hardware.Single().PhysicalLicense.Should().Be(before,
                "a refused regeneration must not change the stored credential");
        }

        [Fact]
        public async Task EditingADeviceDoesNotDisturbItsCredential()
        {
            // Update deliberately no longer copies PhysicalLicense. The edit form no longer carries
            // the field, so copying it would overwrite a valid hash with an empty string and lock
            // the device out silently -- which is exactly the class of defect C-09 was.
            using DataDbContext context = BuildContext(nameof(EditingADeviceDoesNotDisturbItsCredential));
            HardwareLogic logic = AdminLogic(context);

            (LocalHardware created, _) = await logic.Create(NewDevice());
            string before = context.Hardware.Single().PhysicalLicense;

            await logic.Update(new LocalHardwareCreationViewModel
            {
                Hardware = new LocalHardware
                {
                    Id = created.Id,
                    HardwareKind = HardwareKind.DesktopPC,
                    DescriptiveName = "Renamed unit",
                    PhysicalLicense = null
                }
            });

            LocalHardware stored = context.Hardware.Single();
            stored.DescriptiveName.Should().Be("Renamed unit", "the edit itself must still work");
            stored.PhysicalLicense.Should().Be(before, "editing a device must never disturb its credential");
        }
    }
}
