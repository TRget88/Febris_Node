// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Linq;
using Febris.UserNode.DataAccessLayer.DataContext;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The node's DataDb model must contain NODE entities only.
    ///
    /// <para>
    /// WHY. EF Core maps every entity REACHABLE from a <c>DbSet</c>, not just the ones you declare.
    /// The shared <c>MessageBoard</c> model navigates to three CENTRAL aggregates -- Institution,
    /// ContentDeveloper and AccreditationBody -- and <c>MessageBoard</c> is a node DbSet. Those
    /// three, plus the six they in turn navigate to, were therefore mapped into the node model and
    /// given tables in every node database. Nine tables, all empty, no controllers, no live callers.
    /// </para>
    ///
    /// <para>
    /// It was not merely untidy. Because the node's model CONTAINED central entities, every time the
    /// central tier evolved one, the node's model diverged from the node's own schema.
    /// <c>ContentDeveloper</c> gained <c>SubscriptionRate</c> and <c>PendingSelfSignUp</c> centrally
    /// while the node's table had neither, so <c>DataDbContext</c> permanently reported pending model
    /// changes -- and <c>dotnet ef migrations add</c> silently bundled four unrelated operations into
    /// any new migration. That is how this was found: it corrupted an unrelated fix, twice.
    /// </para>
    ///
    /// <para>
    /// This is the same defect as Hardware1, which is precisely why it needs a guard. Re-adding a
    /// navigation to a central aggregate from any node entity brings all nine back, silently, and
    /// the only visible symptom is a migration that mysteriously wants to change tables you never
    /// touched.
    /// </para>
    /// </summary>
    public class NodeModelBoundaryTests
    {
        /// <summary>
        /// Central-tier concepts. A node refers to these by UUID when it must, and never owns a row.
        /// </summary>
        private static readonly string[] CentralOnlyEntities =
        {
            "Institution",
            "InstitutionSettings",
            "InstitutionType",
            "DeploymentType",
            "ContentDeveloper",
            "ContentDeveloperSettings",
            "ContentDeveloperType",
            "AccreditationBody",
            "AccreditationBodySettings"
        };

        private static DataDbContext BuildContext()
        {
            return new DataDbContext(new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(nameof(NodeModelBoundaryTests)).Options);
        }

        [Fact]
        public void TheNodeModelContainsNoCentralEntities()
        {
            using DataDbContext context = BuildContext();

            List<string> mapped = context.Model.GetEntityTypes()
                .Select(t => t.ClrType?.Name)
                .Where(n => n != null)
                .ToList();

            mapped.Should().NotBeEmpty("the model must actually have been built, or this guard passes vacuously");

            List<string> leaked = mapped.Intersect(CentralOnlyEntities).OrderBy(n => n).ToList();

            leaked.Should().BeEmpty(
                "a central aggregate reachable from a node DbSet gets its own table in every node database, "
                + "and then drifts out of step every time the central tier evolves it. If this fails, find the "
                + "navigation that reaches it and Ignore() it on the node entity -- see the MessageBoard block "
                + "in DataDbContext. Leaked: " + string.Join(", ", leaked));
        }

        [Fact]
        public void MessageBoardHasNoCentralUuidScalarsEither()
        {
            // Owner ruling 2026-08-09, and this test previously asserted the OPPOSITE. The first
            // pass kept these three on the grounds that a UUID is how a node refers to a central
            // thing. That was wrong for THIS entity. Nothing on the node writes them -- the
            // MessageBoardController Create/Edit bind lists exclude them, so they are permanently
            // Guid.Empty -- and the only filters that ever read them are commented out
            // (CohortQueries:153, HardwareLinkedCurriculumQueries:121).
            //
            // NodeIdentity.InstitutionUUID is a DIFFERENT entity and is unaffected. See below.
            using DataDbContext context = BuildContext();

            IEnumerable<string> properties = context.Model
                .FindEntityType(typeof(Febris.ModelLibrary.Models.DataModels.MessageBoard))
                .GetProperties()
                .Select(p => p.Name)
                .ToList();

            properties.Should().NotContain("InstitutionUUID");
            properties.Should().NotContain("ContentDeveloperUUID");
            properties.Should().NotContain("AccreditationBodyUUID");

            // What was dropped is narrow and specific: the three CENTRAL-filter scalars, on this
            // entity only. UUIDs are not the problem and are not removable as a pattern -- UUID is
            // the BaseModel unique identifier that every entity in this schema carries, and
            // LocationUUID is a NODE concept. Both stay, and are asserted here so this test can
            // never be read as licence to strip UUIDs generally.
            properties.Should().Contain("UUID", "MessageBoard keeps its own BaseModel unique identifier");
            properties.Should().Contain("LocationUUID", "Location is a node concept, unlike the three central ones");
            properties.Should().Contain("Subject", "the message board itself must still be intact");
            properties.Should().Contain("Message");
        }

        [Fact]
        public void MessageBoardHasNoNavigationToACentralAggregate()
        {
            // Pins the mechanism rather than only the symptom: it is the NAVIGATION that maps the
            // aggregate, so a future edit that re-adds one should fail here with a clear reason
            // rather than only showing up as nine tables reappearing.
            using DataDbContext context = BuildContext();

            List<string> navigations = context.Model
                .FindEntityType(typeof(Febris.ModelLibrary.Models.DataModels.MessageBoard))
                .GetNavigations()
                .Select(n => n.Name)
                .ToList();

            navigations.Should().NotContain("Institution");
            navigations.Should().NotContain("ContentDeveloper");
            navigations.Should().NotContain("AccreditationBody");
        }

        [Fact]
        public void NodeIdentityKeepsItsOwnInstitutionUuid()
        {
            // Guards against over-applying the rule above. NodeIdentity.InstitutionUUID is the
            // node's OWN stable identity, generated once by NodeIdentitySeeder and read by
            // NodeStatusLogic. It merely shares a name with the MessageBoard scalar that was
            // dropped, and has nothing to do with it.
            using DataDbContext context = BuildContext();

            context.Model
                .FindEntityType(typeof(Febris.ModelLibrary.Models.DataModels.NodeIdentity))
                .GetProperties()
                .Select(p => p.Name)
                .Should().Contain("InstitutionUUID");
        }

        [Fact]
        public void TheDeviceCredentialHasAUniqueIndex()
        {
            // PhysicalLicense IS the device authentication credential -- HardwareQueries.GetByKey
            // resolves an incoming device by matching it, with an UNORDERED FirstOrDefaultAsync.
            // Verified against a live node database before this was added: the ONLY index on
            // Hardware was the primary key, so every device authentication was a sequential scan
            // AND two rows could carry the same credential, making it arbitrary which one
            // authenticated. Audit C-09 hit exactly that -- locking a device reported success while
            // it kept authenticating as a duplicate row. C-09 fixed the insert; only the schema can
            // stop the duplicate existing.
            //
            // Asserted on the MODEL rather than by behaviour: the InMemory provider does not enforce
            // unique indexes, so a "second insert throws" test would pass here while proving nothing.
            // The enforcement itself was verified against real Postgres -- see docs/BUGS.md.
            using DataDbContext context = BuildContext();

            Microsoft.EntityFrameworkCore.Metadata.IIndex index = context.Model
                .FindEntityType(typeof(Febris.ModelLibrary.Models.DataModels.LocalHardware))
                .GetIndexes()
                .SingleOrDefault(i => i.Properties.Any(p => p.Name == "PhysicalLicense"));

            index.Should().NotBeNull("the device credential must be indexed, or every authentication scans the table");
            index.IsUnique.Should().BeTrue("two devices sharing an auth credential makes it arbitrary which one authenticates");
            index.GetFilter().Should().NotBeNullOrEmpty(
                "the index must be FILTERED, or unregistered devices carrying an empty credential would collide with each other");
        }

        [Fact]
        public void ParentLinkedStudentIndexesAreDeclaredOnTheModel()
        {
            // The other drift cause. 20260805130000_ParentLinkedStudentTable created these two
            // indexes, but the model declared only a bare DbSet, so EF wanted to DROP them from every
            // node database. Declaring them keeps the schema and the model in agreement.
            using DataDbContext context = BuildContext();

            List<string> indexed = context.Model
                .FindEntityType(typeof(Febris.ModelLibrary.Models.DataModels.ParentLinkedStudent))
                .GetIndexes()
                .SelectMany(i => i.Properties)
                .Select(p => p.Name)
                .ToList();

            indexed.Should().Contain("ParentUserId");
            indexed.Should().Contain("StudentActorId");
        }
    }
}
