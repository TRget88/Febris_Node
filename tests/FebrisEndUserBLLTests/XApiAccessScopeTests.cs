// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// FERPA-critical. Pins <see cref="XApiAccessScope.ResolveAsync"/>, the single
    /// default-deny resolver that decides which xApi actors a request may read.
    /// ActorLogic and StatementLogic both gate every parent/learner read on it, so
    /// a regression here is a student-data-exposure bug.
    /// </summary>
    public class XApiAccessScopeTests
    {
        private static ClaimsPrincipal Principal(string role, Guid? userId = null, Guid? actor = null)
        {
            var claims = new List<Claim>();
            if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
            if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (actor.HasValue) claims.Add(new Claim("Actor", actor.Value.ToString()));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private static Mock<IParentLinkedStudentQueries> Links(Guid parentId, params Guid[] actorIds)
        {
            var mock = new Mock<IParentLinkedStudentQueries>();
            mock.Setup(l => l.GetStudentActorIdsForParent(parentId))
                .ReturnsAsync(actorIds.ToList());
            return mock;
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("ITAdmin")]
        [InlineData("Educator")]
        [InlineData("SuperAdmin")]   // Febris super admin (IsLocalFebrisAdmin)
        public async Task Staff_roles_are_unrestricted(string role)
        {
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal(role));
            scope.Unrestricted.Should().BeTrue($"{role} reads tenant-wide");
        }

        [Fact]
        public async Task Learner_is_scoped_to_their_own_actor_only()
        {
            Guid own = Guid.NewGuid();
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("User", actor: own));
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEquivalentTo(new[] { own });
        }

        [Fact]
        public async Task Learner_without_an_actor_claim_is_denied()
        {
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("User"));
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEmpty();
        }

        [Fact]
        public async Task Parent_is_scoped_to_exactly_their_linked_students_actors()
        {
            Guid parentId = Guid.NewGuid();
            Guid childA = Guid.NewGuid();
            Guid childB = Guid.NewGuid();
            Mock<IParentLinkedStudentQueries> links = Links(parentId, childA, childB);

            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("UserParent", parentId), links.Object);

            scope.Unrestricted.Should().BeFalse("a parent never reads tenant-wide");
            scope.AllowedActorUuids.Should().BeEquivalentTo(new[] { childA, childB });
            links.Verify(l => l.GetStudentActorIdsForParent(parentId), Times.Once);
        }

        [Fact]
        public async Task Parent_with_no_links_is_denied()
        {
            Guid parentId = Guid.NewGuid();
            Mock<IParentLinkedStudentQueries> links = Links(parentId); // no linked actors
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("UserParent", parentId), links.Object);
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEmpty();
        }

        [Fact]
        public async Task Parent_does_not_see_another_parents_students()
        {
            Guid parentA = Guid.NewGuid();
            Guid parentB = Guid.NewGuid();
            Guid childOfA = Guid.NewGuid();
            Guid childOfB = Guid.NewGuid();
            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetStudentActorIdsForParent(parentA)).ReturnsAsync(new List<Guid> { childOfA });
            links.Setup(l => l.GetStudentActorIdsForParent(parentB)).ReturnsAsync(new List<Guid> { childOfB });

            ActorAccessScope scopeA = await XApiAccessScope.ResolveAsync(Principal("UserParent", parentA), links.Object);

            scopeA.AllowedActorUuids.Should().BeEquivalentTo(new[] { childOfA });
            scopeA.AllowedActorUuids.Should().NotContain(childOfB);
        }

        [Fact]
        public async Task Parent_with_missing_user_id_is_denied_and_links_not_queried()
        {
            // No NameIdentifier claim -> GetUserId() is null -> no parent id to resolve.
            var links = new Mock<IParentLinkedStudentQueries>();
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("UserParent"), links.Object);
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEmpty();
            links.Verify(l => l.GetStudentActorIdsForParent(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Parent_ignores_a_stray_actor_claim_and_uses_only_links()
        {
            // Even if a parent principal somehow carries an Actor claim, the parent
            // branch must use the links and never the Actor claim (no self-actor leak).
            Guid parentId = Guid.NewGuid();
            Guid strayActor = Guid.NewGuid();
            Guid child = Guid.NewGuid();
            Mock<IParentLinkedStudentQueries> links = Links(parentId, child);

            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("UserParent", parentId, strayActor), links.Object);

            scope.AllowedActorUuids.Should().BeEquivalentTo(new[] { child });
            scope.AllowedActorUuids.Should().NotContain(strayActor);
        }

        [Fact]
        public async Task Unknown_role_is_denied()
        {
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(Principal("ContentDeveloper"));
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEmpty();
        }

        [Fact]
        public async Task Null_principal_is_denied()
        {
            ActorAccessScope scope = await XApiAccessScope.ResolveAsync(null);
            scope.Unrestricted.Should().BeFalse();
            scope.AllowedActorUuids.Should().BeEmpty();
        }
    }
}
