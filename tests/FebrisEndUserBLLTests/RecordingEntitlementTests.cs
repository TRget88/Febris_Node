// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T6 security unit. Session recordings had NO owner recorded anywhere, so the Portal's two
    /// video loaders served any recording to any signed-in end user who knew its Guid. The only
    /// checks were "do you hold an end-user role" and "does the path stay inside the recordings
    /// folder"; the Guid being unguessable was the sole protection, which is secrecy of the
    /// identifier rather than access control.
    ///
    /// <para>
    /// The rule these pin, in the owner's words: you may watch a recording that is part of your
    /// statement, or you are an educator or admin. The decision is delegated to
    /// <c>XApiAccessScope.ResolveAsync</c>, the node's single existing entitlement resolver, rather
    /// than open-coded into a second drifting copy of the same policy.
    /// </para>
    /// </summary>
    public class RecordingEntitlementTests
    {
        private const string VideoName = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

        private static ClaimsPrincipal Principal(string role, Guid? actor = null, Guid? userId = null)
        {
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Role, role) };
            if (actor.HasValue) claims.Add(new Claim("Actor", actor.Value.ToString()));
            if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private static RecordingLogic Build(
            ClaimsPrincipal user,
            Recording stored,
            IParentLinkedStudentQueries parentLinks = null)
        {
            DefaultHttpContext http = new DefaultHttpContext();
            if (user != null) http.User = user;

            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(http);

            // EXACT-MATCH on purpose. This mock previously answered It.IsAny<string>(), which made
            // every test pass regardless of what name the gate actually looked up -- and that is
            // exactly why the suite stayed green while the shipped code was asking for
            // "{guid}.mp4" against a row stored as "{guid}" and denying every recording to
            // everyone. A lookup stub that ignores its argument cannot test a lookup.
            Mock<IRecordingQueries> recordings = new Mock<IRecordingQueries>();
            recordings.Setup(r => r.GetByName(It.IsAny<string>()))
                .ReturnsAsync((string requested) =>
                    stored != null && requested == stored.Name ? stored : null);

            Mock<IParentLinkedStudentQueries> links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetStudentActorIdsForParent(It.IsAny<Guid>()))
                .ReturnsAsync(new List<Guid>());

            return new RecordingLogic(accessor.Object, recordings.Object, parentLinks ?? links.Object);
        }

        private static Recording OwnedBy(Guid actor) =>
            new Recording { Name = VideoName, ActorUUID = actor };

        // ------------------------------------------------------------------

        [Fact]
        public async Task ALearnerCannotWatchAnotherLearnersRecording()
        {
            // THE defect. Two learners, one recording. Before this check existed, knowing the Guid
            // was sufficient and this returned the video.
            Guid owner = Guid.NewGuid();
            Guid someoneElse = Guid.NewGuid();

            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString(), actor: someoneElse),
                OwnedBy(owner)).MayView(VideoName);

            allowed.Should().BeFalse("a learner may only watch recordings belonging to their own actor");
        }

        [Fact]
        public async Task ALearnerCanWatchTheirOwnRecording()
        {
            // The other side, so the fix cannot be "deny everything".
            Guid owner = Guid.NewGuid();

            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString(), actor: owner),
                OwnedBy(owner)).MayView(VideoName);

            allowed.Should().BeTrue();
        }

        [Theory]
        [InlineData("Educator")]
        [InlineData("Admin")]
        [InlineData("ITAdmin")]
        public async Task StaffMayWatchAnyRecording(string role)
        {
            // The owner's rule explicitly keeps staff unrestricted, matching every other
            // learner-data read on this node.
            bool allowed = await Build(Principal(role), OwnedBy(Guid.NewGuid())).MayView(VideoName);

            allowed.Should().BeTrue(role + " is unrestricted, as on every other learner-data read");
        }

        [Fact]
        public async Task AnUnownedRecordingIsDeniedRatherThanTreatedAsPublic()
        {
            // Deny-on-miss is the whole point. Recordings that predate the ownership table have no
            // row, and allowing on a miss would leave every historical recording exactly as exposed
            // as it was before, which is the defect being fixed.
            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString(), actor: Guid.NewGuid()),
                stored: null).MayView(VideoName);

            allowed.Should().BeFalse("no ownership record means the check cannot justify access");
        }

        [Fact]
        public async Task ALearnerWithNoActorClaimIsDenied()
        {
            // An account with no Actor claim has no subject to scope to. It must yield nothing
            // rather than falling through to everything -- the same trap that produced a real
            // defect in the microcredential read.
            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString()),
                OwnedBy(Guid.NewGuid())).MayView(VideoName);

            allowed.Should().BeFalse();
        }

        [Fact]
        public async Task AParentMayWatchTheirLinkedStudentsRecording()
        {
            Guid student = Guid.NewGuid();
            Guid parentUserId = Guid.NewGuid();

            Mock<IParentLinkedStudentQueries> links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetStudentActorIdsForParent(parentUserId))
                .ReturnsAsync(new List<Guid> { student });

            bool allowed = await Build(
                Principal(InstitutionUserAccountType.UserParent.ToString(), userId: parentUserId),
                OwnedBy(student),
                links.Object).MayView(VideoName);

            allowed.Should().BeTrue("a parent's access is exactly the actors of their linked students");
        }

        [Fact]
        public async Task AParentMayNotWatchAnUnlinkedStudentsRecording()
        {
            Guid parentUserId = Guid.NewGuid();

            Mock<IParentLinkedStudentQueries> links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetStudentActorIdsForParent(It.IsAny<Guid>()))
                .ReturnsAsync(new List<Guid>());

            bool allowed = await Build(
                Principal(InstitutionUserAccountType.UserParent.ToString(), userId: parentUserId),
                OwnedBy(Guid.NewGuid()),
                links.Object).MayView(VideoName);

            allowed.Should().BeFalse();
        }

        // ------------------------------------------------------------------
        // Regression: the gate must tolerate the name the CALLER actually passes
        // ------------------------------------------------------------------

        [Fact]
        public async Task TheGateToleratesTheExtensionTheVideoLoaderAppends()
        {
            // SHIPPED DEFECT, caught after the fact. Register stores the BARE minted Guid, but
            // WidgetController.VideoLoader appends ".mp4" to any extensionless name BEFORE calling
            // the gate. GetByName is an exact match, so every lookup missed and the deny-on-miss
            // branch denied every recording to everyone -- staff included, because the null check
            // runs before the unrestricted branch. It failed safe, but nothing was viewable.
            Guid owner = Guid.NewGuid();

            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString(), actor: owner),
                OwnedBy(owner)).MayView(VideoName + ".mp4");

            allowed.Should().BeTrue(
                "the loader appends .mp4 before calling the gate, and the row stores the bare Guid");
        }

        [Fact]
        public async Task StaffAreAlsoUnblockedByTheExtensionNormalisation()
        {
            // Staff were denied too, which is what made the defect total rather than partial: the
            // null-row check short-circuits before the unrestricted branch is ever evaluated.
            bool allowed = await Build(Principal("Educator"), OwnedBy(Guid.NewGuid()))
                .MayView(VideoName + ".mp4");

            allowed.Should().BeTrue();
        }

        [Fact]
        public async Task NormalisationDoesNotTurnAnUnknownNameIntoAKnownOne()
        {
            // The normalisation must not become a wildcard: stripping an extension may not make a
            // DIFFERENT recording's name resolve.
            bool allowed = await Build(
                Principal(InstitutionUserAccountType.User.ToString(), actor: Guid.NewGuid()),
                OwnedBy(Guid.NewGuid())).MayView("11111111-2222-3333-4444-555555555555.mp4");

            allowed.Should().BeFalse("a different name must still miss");
        }

        [Fact]
        public async Task ARoleWithNoEntitlementIsDenied()
        {
            // Default deny. A principal holding some other role is not implicitly a viewer.
            bool allowed = await Build(Principal("SomeUnrelatedRole"), OwnedBy(Guid.NewGuid()))
                .MayView(VideoName);

            allowed.Should().BeFalse();
        }
    }
}
