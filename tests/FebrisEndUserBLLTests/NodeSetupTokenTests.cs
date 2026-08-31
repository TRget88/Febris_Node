// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.DataContext;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.IdentityLogic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the node's first-run claim token (2026-08-21), which replaced a compiled-in seeded
    /// admin because a hardcoded seed is not a reasonable deployment shape for an open-source
    /// project.
    ///
    /// <para>
    /// The security argument rests on one property that is invisible in the token's own code and
    /// easy to undo by accident: <b>the token reaches stdout and no durable medium</b>. That is what
    /// makes "can claim an unclaimed node" mean "can read the operator's console" rather than "can
    /// reach the node over the network". <see cref="TheToken_IsPrintedToStdoutOnly_NeverThroughSerilog"/>
    /// is the guard, and it is the most load-bearing test in this file.
    /// </para>
    /// </summary>
    public class NodeSetupTokenTests
    {
        private static DataDbContext BuildDataContext(string dbName)
        {
            DbContextOptions<DataDbContext> options = new DbContextOptionsBuilder<DataDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new DataDbContext(options);
        }

        private static NodeSetupLogic BuildLogic(DataDbContext context)
        {
            return new NodeSetupLogic(new NodeSetupTokenQueries(context));
        }

        // ---- 1. The token never reaches a durable medium ----------------------------------------

        [Fact]
        public void TheToken_IsPrintedToStdoutOnly_NeverThroughSerilog()
        {
            // THE OWNER DECISION, PINNED. Serilog fans out to the file sink and to any configured
            // shipper, so a token logged through it would land on disk and in whatever aggregator
            // the operator runs, and the trust boundary would silently widen from "reads the
            // console" to "reads the logs". Console.WriteLine stops at stdout.
            //
            // Source-level because the alternative is asserting on a static logger's sinks, which
            // tests the logging library rather than the decision.
            string source = SeederSourceWithoutComments();

            int issueAt = source.IndexOf("IssueSetupTokenIfUnclaimedAsync", StringComparison.Ordinal);
            issueAt.Should().BeGreaterThan(-1, "the seeder method must still exist and be named that");
            string method = source.Substring(issueAt);

            // The token variable must be written by Console, and must NOT appear in any Serilog call.
            method.Should().Contain("Console.WriteLine(\"     \" + token)",
                "the token is printed to stdout");

            // STRING LITERALS ARE STRIPPED BEFORE THE CHECK, and the first version of this guard
            // did not do that and failed on its own product: the seeder legitimately LOGS the
            // sentence "no setup token can be issued", which contains the word but not the value.
            // What matters is whether the token VARIABLE is interpolated into a logging call, so
            // that is what is asserted.
            foreach (Match log in Regex.Matches(method, @"Log\.\w+\([^;]*;", RegexOptions.Singleline))
            {
                WithoutStringLiterals(log.Value).Should().NotMatchRegex(@"\btoken\b",
                    "no Serilog call in the seeder may carry the token VALUE -- found: " + log.Value.Trim());
            }

            // And the logic layer that mints it must not log it either.
            string logic = LogicSourceWithoutComments();
            foreach (Match log in Regex.Matches(logic, @"FebrisLog\.\w+\([^;]*;", RegexOptions.Singleline))
            {
                WithoutStringLiterals(log.Value).Should().NotMatchRegex(@"\brawToken\b",
                    "the minting path must not log the raw token -- found: " + log.Value.Trim());
            }
        }

        [Fact]
        public void TheGuard_Discriminates_RatherThanMatchingAnything()
        {
            // Sanity on the guard above: prove comment stripping really strips (this file's own doc
            // comment names Console.WriteLine and Serilog), and that the extracted source is real.
            string source = SeederSourceWithoutComments();
            source.Length.Should().BeGreaterThan(1000);
            source.Should().NotContain("the trust boundary would silently widen",
                "comment stripping must actually strip, or the assertions could match prose");
            source.Should().Contain("Log.Warning(",
                "the seeder DOES log an event, so 'no Log call mentions the token' is a real "
                + "constraint rather than a vacuous one about a file with no logging");
        }

        // ---- 2. Minting -------------------------------------------------------------------------

        [Fact]
        public async Task IssueToken_ReturnsTheRawToken_AndStoresOnlyItsHash()
        {
            using DataDbContext context = BuildDataContext(nameof(IssueToken_ReturnsTheRawToken_AndStoresOnlyItsHash));

            string raw = await BuildLogic(context).IssueToken();

            raw.Should().NotBeNullOrWhiteSpace();
            raw.Length.Should().BeGreaterThan(40, "256 bits of entropy, base64url encoded");

            NodeSetupToken row = await context.NodeSetupToken.SingleAsync();
            row.TokenHash.Should().Be(DeviceCredential.Hash(raw));
            row.TokenHash.Should().NotBe(raw);
            row.ConsumedAt.Should().BeNull();
            row.ExpiresAt.Should().BeCloseTo(
                DateTime.UtcNow.Add(NodeSetupLogic.TokenLifetime), TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task IssuingAgain_RetiresTheOutstandingToken_SoOnlyOneIsEverLive()
        {
            // Every boot of an unclaimed node prints a token. Leaving the earlier ones live would
            // mean several valid claim secrets sitting in several log scrollbacks.
            using DataDbContext context = BuildDataContext(nameof(IssuingAgain_RetiresTheOutstandingToken_SoOnlyOneIsEverLive));
            NodeSetupLogic logic = BuildLogic(context);

            string first = await logic.IssueToken();
            string second = await logic.IssueToken();

            second.Should().NotBe(first);
            (await context.NodeSetupToken.CountAsync()).Should().Be(1);
            (await logic.Validate(first)).Should().Be(NodeSetupTokenState.NotFound,
                "the previous token must stop working the moment a new one is printed");
            (await logic.Validate(second)).Should().Be(NodeSetupTokenState.Claimable);
        }

        [Fact]
        public async Task EveryIssuedToken_IsDistinct()
        {
            using DataDbContext context = BuildDataContext(nameof(EveryIssuedToken_IsDistinct));
            NodeSetupLogic logic = BuildLogic(context);

            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                seen.Add(await logic.IssueToken()).Should().BeTrue("token " + i + " must be new");
            }
        }

        // ---- 3. Validation ----------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-real-token")]
        public async Task Validate_RejectsAnythingThatIsNotTheToken(string presented)
        {
            using DataDbContext context = BuildDataContext(nameof(Validate_RejectsAnythingThatIsNotTheToken) + presented);
            await BuildLogic(context).IssueToken();

            (await BuildLogic(context).Validate(presented)).Should().Be(NodeSetupTokenState.NotFound);
        }

        [Fact]
        public async Task Validate_ReportsExpiry_Distinctly()
        {
            // Worth distinguishing: the person reading this has console access and can act on it by
            // restarting, whereas "not valid" tells them to go hunting.
            using DataDbContext context = BuildDataContext(nameof(Validate_ReportsExpiry_Distinctly));
            string raw = DeviceCredential.Generate();
            await new NodeSetupTokenQueries(context).Issue(
                DeviceCredential.Hash(raw), DateTime.UtcNow.AddMinutes(-1));

            (await BuildLogic(context).Validate(raw)).Should().Be(NodeSetupTokenState.Expired);
            (await BuildLogic(context).ClaimableUuid(raw)).Should().BeNull();
        }

        [Fact]
        public async Task HasLiveToken_IsFalse_ForAnExpiredOrConsumedOne()
        {
            using DataDbContext context = BuildDataContext(nameof(HasLiveToken_IsFalse_ForAnExpiredOrConsumedOne));
            NodeSetupLogic logic = BuildLogic(context);

            (await logic.HasLiveToken()).Should().BeFalse("nothing issued yet");

            string raw = await logic.IssueToken();
            (await logic.HasLiveToken()).Should().BeTrue();

            Guid? uuid = await logic.ClaimableUuid(raw);
            await logic.Consume(uuid.Value, Guid.NewGuid(), "admin@example.org");
            (await logic.HasLiveToken()).Should().BeFalse("the node has been claimed");
        }

        // ---- 4. Single use ----------------------------------------------------------------------

        [Fact]
        public async Task Consume_SucceedsExactlyOnce()
        {
            // A node must not end up with two first administrators from one token.
            using DataDbContext context = BuildDataContext(nameof(Consume_SucceedsExactlyOnce));
            NodeSetupLogic logic = BuildLogic(context);

            string raw = await logic.IssueToken();
            Guid uuid = (await logic.ClaimableUuid(raw)).Value;

            (await logic.Consume(uuid, Guid.NewGuid(), "first@example.org")).Should().BeTrue();
            (await logic.Consume(uuid, Guid.NewGuid(), "second@example.org")).Should().BeFalse();

            (await logic.Validate(raw)).Should().Be(NodeSetupTokenState.AlreadyClaimed);
        }

        [Fact]
        public async Task Consume_RefusesAnExpiredToken()
        {
            using DataDbContext context = BuildDataContext(nameof(Consume_RefusesAnExpiredToken));
            string raw = DeviceCredential.Generate();
            NodeSetupToken issued = await new NodeSetupTokenQueries(context).Issue(
                DeviceCredential.Hash(raw), DateTime.UtcNow.AddMinutes(-1));

            (await BuildLogic(context).Consume(issued.UUID, Guid.NewGuid(), "late@example.org"))
                .Should().BeFalse("this is the check that makes the setup page roll its account back");
        }

        [Fact]
        public async Task ConsumedRow_IsKept_AsTheAuditRecordOfTheClaim()
        {
            // Who claimed this node, and when, is the one question nobody can answer afterwards if
            // the row is deleted.
            using DataDbContext context = BuildDataContext(nameof(ConsumedRow_IsKept_AsTheAuditRecordOfTheClaim));
            NodeSetupLogic logic = BuildLogic(context);
            Guid adminId = Guid.NewGuid();

            string raw = await logic.IssueToken();
            Guid uuid = (await logic.ClaimableUuid(raw)).Value;
            await logic.Consume(uuid, adminId, "  owner@example.org  ");

            NodeSetupToken row = await context.NodeSetupToken.AsNoTracking().SingleAsync();
            row.ConsumedAt.Should().NotBeNull();
            row.ConsumedByUserId.Should().Be(adminId);
            row.ConsumedByEmail.Should().Be("owner@example.org", "trimmed");
        }

        [Fact]
        public async Task AClaimedNode_DoesNotGetANewTokenFromAnotherIssue_UnlessOneIsAsked()
        {
            // Issue() itself is unconditional -- the "is the node claimed" decision belongs to the
            // caller (the seeder checks for an ITAdmin). Pinned so the responsibility stays where it
            // is documented rather than being assumed to live in here.
            using DataDbContext context = BuildDataContext(nameof(AClaimedNode_DoesNotGetANewTokenFromAnotherIssue_UnlessOneIsAsked));
            NodeSetupLogic logic = BuildLogic(context);

            string raw = await logic.IssueToken();
            Guid uuid = (await logic.ClaimableUuid(raw)).Value;
            await logic.Consume(uuid, Guid.NewGuid(), "owner@example.org");

            string second = await logic.IssueToken();
            (await logic.Validate(second)).Should().Be(NodeSetupTokenState.Claimable);
            (await context.NodeSetupToken.CountAsync()).Should().Be(2,
                "the consumed audit row survives alongside the new token");
        }

        // ---- helpers -----------------------------------------------------------------------------

        // Delegates to the shared walk. The marker used to be Febris.sln, which the public cut
        // deletes, so this guard failed in the export for a reason unrelated to what it guards.
        // See TestRepoRoot.
        private static string RepoRoot()
        {
            return TestRepoRoot.Find();
        }

        /// <summary>Blank out the contents of double-quoted string literals, so a check for an
        /// identifier cannot be satisfied by prose that merely mentions it.</summary>
        private static string WithoutStringLiterals(string source)
        {
            return Regex.Replace(source, "\"(\\\\.|[^\"\\\\])*\"", "\"\"");
        }

        private static string WithoutComments(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            string noDoc = Regex.Replace(noBlock, @"^[ \t]*///.*$", string.Empty, RegexOptions.Multiline);
            return Regex.Replace(noDoc, @"^[ \t]*//.*$", string.Empty, RegexOptions.Multiline);
        }

        private static string SeederSourceWithoutComments()
        {
            string path = Path.Combine(RepoRoot(), "enduser", "FebrisEndUserPortal", "Data", "SeedData.cs");
            File.Exists(path).Should().BeTrue("the seeder must exist at " + path);
            return WithoutComments(File.ReadAllText(path));
        }

        /// <summary>
        /// BOTH claim gates must ignore soft-deleted admins, and they must agree with each other.
        ///
        /// <para>
        /// Deleting an account sets <c>IsDeleted</c> and locks it with <c>LockoutEnd = MaxValue</c>,
        /// but deliberately RETAINS the row for xAPI history and FERPA, and does not strip roles. So
        /// a bare <c>GetUsersInRoleAsync(ITAdmin)</c> still counted a deleted admin as an admin. On a
        /// node whose sole ITAdmin deleted their own account -- one supported user action, on a FRESH
        /// node -- the boot seeder saw an admin and issued no token, <c>/setup</c> saw an admin and
        /// answered 404, and the node was permanently bricked with no way back short of direct SQL.
        /// That is exactly the outcome the self-healing predicate documented on
        /// <c>IssueSetupTokenIfUnclaimedAsync</c> exists to prevent.
        /// </para>
        /// <para>
        /// Source-level, matching the other assertions in this file, because the runtime path needs a
        /// full Identity stack. String literals are NOT stripped here on purpose: the assertion is
        /// about a code expression, and the comment stripper already removes the prose that explains
        /// it, so a passing result cannot come from the explanation.
        /// </para>
        /// <para>
        /// WHAT THIS DOES NOT PROVE, stated so nobody reads more into it. It checks that each gate
        /// CARRIES the filter, scoped to its own method. Agreement between the two is INFERRED from
        /// both carrying the same expression, not asserted directly, so two gates could still
        /// diverge if one grew an extra condition. And being source-text, it cannot catch a change
        /// that keeps the literal while breaking the behaviour. A behavioural test needs a full
        /// UserManager harness, which this project does not have.
        /// </para>
        /// </summary>
        [Fact]
        public void BothClaimGates_IgnoreSoftDeletedAdmins_SoADeletedSoleAdminCannotBrickTheNode()
        {
            string seeder = SeederSourceWithoutComments();
            int issueAt = seeder.IndexOf("IssueSetupTokenIfUnclaimedAsync", StringComparison.Ordinal);
            issueAt.Should().BeGreaterThan(-1, "the seeder method must still exist and be named that");
            string seedGate = seeder.Substring(issueAt);

            seedGate.Should().Contain("GetUsersInRoleAsync",
                "the boot gate still asks Identity who holds the admin role, so this guard is not vacuous");
            seedGate.Should().Contain("!admin.IsDeleted",
                "the boot gate must not count a soft-deleted account as a live admin");

            // Scoped to the METHOD, symmetrically with the seeder assertion above. Searching the
            // whole file would let a "!admin.IsDeleted" anywhere in Setup.cshtml.cs satisfy this,
            // including one in an unrelated helper, which is the fixed-window/wrong-neighbour trap
            // this suite has already been bitten by twice.
            string setup = SetupPageSourceWithoutComments();
            int claimedAt = setup.IndexOf("NodeIsClaimedAsync", StringComparison.Ordinal);
            claimedAt.Should().BeGreaterThan(-1, "the request-time gate must still exist and be named that");
            string setupGate = setup.Substring(claimedAt);

            setupGate.Should().Contain("GetUsersInRoleAsync",
                "the request-time gate still asks Identity who holds the admin role");
            setupGate.Should().Contain("!admin.IsDeleted",
                "the request-time gate must carry the SAME filter as the boot gate, or the banner "
                + "and the page disagree about whether the node is claimable");
        }

        private static string SetupPageSourceWithoutComments()
        {
            string path = Path.Combine(RepoRoot(), "enduser", "FebrisEndUserPortal", "Areas",
                "Identity", "Pages", "Account", "Setup.cshtml.cs");
            File.Exists(path).Should().BeTrue("the setup page model must exist at " + path);
            return WithoutComments(File.ReadAllText(path));
        }

        private static string LogicSourceWithoutComments()
        {
            string path = Path.Combine(RepoRoot(), "enduser", "FebrisEndUserBLL", "Logic",
                "IdentityLogic", "NodeSetupLogic.cs");
            File.Exists(path).Should().BeTrue("the setup logic must exist at " + path);
            return WithoutComments(File.ReadAllText(path));
        }
    }
}
