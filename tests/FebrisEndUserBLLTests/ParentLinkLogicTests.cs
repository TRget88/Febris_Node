// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.Models.UserModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins ParentLinkLogic: the admin-only write path that creates and removes the
    /// FERPA parent-to-student links XApiAccessScope later reads. Verifies the admin
    /// gate, student-actor resolution, parent-role enforcement, and idempotency, so
    /// a regression cannot silently grant or widen access.
    /// </summary>
    public class ParentLinkLogicTests
    {
        // Builds a Moq-backed UserManager. FindByIdAsync and IsInRoleAsync are
        // virtual, so they can be set up per test; the rest of the (null) ctor args
        // are the standard pattern for unit-testing against UserManager.
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        // An IHttpContextAccessor whose principal carries the given role (plus a
        // NameIdentifier so the audit line has an acting-admin id).
        private static IHttpContextAccessor Accessor(string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            };
            if (role != null) claims.Add(new Claim(ClaimTypes.Role, role));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var ctx = new DefaultHttpContext { User = principal };
            var acc = new Mock<IHttpContextAccessor>();
            acc.Setup(a => a.HttpContext).Returns(ctx);
            return acc.Object;
        }

        [Fact]
        public async Task Link_is_denied_for_a_non_admin()
        {
            var links = new Mock<IParentLinkedStudentQueries>();
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var logic = new ParentLinkLogic(Accessor("Educator"), um.Object, links.Object);

            bool ok = await logic.Link(Guid.NewGuid(), Guid.NewGuid());

            ok.Should().BeFalse("only admins may link, educators are excluded");
            links.Verify(l => l.Link(It.IsAny<ParentLinkedStudent>()), Times.Never);
        }

        [Fact]
        public async Task Link_creates_a_link_for_admin_when_student_has_actor_and_parent_is_a_parent()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();

            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.Exists(parentId, studentActor)).ReturnsAsync(false);
            links.Setup(l => l.Link(It.IsAny<ParentLinkedStudent>())).ReturnsAsync((ParentLinkedStudent p) => p);

            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var student = new LocalApplicationUser { Id = studentId, Actor = studentActor, FirstName = "Sam", Email = "s@x.io" };
            var parent = new LocalApplicationUser { Id = parentId };
            um.Setup(m => m.FindByIdAsync(studentId.ToString())).ReturnsAsync(student);
            um.Setup(m => m.FindByIdAsync(parentId.ToString())).ReturnsAsync(parent);
            um.Setup(m => m.IsInRoleAsync(parent, "UserParent")).ReturnsAsync(true);

            var logic = new ParentLinkLogic(Accessor("Admin"), um.Object, links.Object);
            bool ok = await logic.Link(parentId, studentId);

            ok.Should().BeTrue();
            links.Verify(l => l.Link(It.Is<ParentLinkedStudent>(
                p => p.ParentUserId == parentId && p.StudentUserId == studentId && p.StudentActorId == studentActor)),
                Times.Once);
        }

        [Fact]
        public async Task Link_fails_when_student_has_no_actor()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentId = Guid.NewGuid();
            var links = new Mock<IParentLinkedStudentQueries>();
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            um.Setup(m => m.FindByIdAsync(studentId.ToString()))
              .ReturnsAsync(new LocalApplicationUser { Id = studentId, Actor = null });

            var logic = new ParentLinkLogic(Accessor("Admin"), um.Object, links.Object);
            bool ok = await logic.Link(parentId, studentId);

            ok.Should().BeFalse("a student with no learner actor has nothing to grant");
            links.Verify(l => l.Link(It.IsAny<ParentLinkedStudent>()), Times.Never);
        }

        [Fact]
        public async Task Link_fails_when_target_is_not_a_parent_account()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();
            var links = new Mock<IParentLinkedStudentQueries>();
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var student = new LocalApplicationUser { Id = studentId, Actor = studentActor };
            var notParent = new LocalApplicationUser { Id = parentId };
            um.Setup(m => m.FindByIdAsync(studentId.ToString())).ReturnsAsync(student);
            um.Setup(m => m.FindByIdAsync(parentId.ToString())).ReturnsAsync(notParent);
            um.Setup(m => m.IsInRoleAsync(notParent, "UserParent")).ReturnsAsync(false);

            var logic = new ParentLinkLogic(Accessor("Admin"), um.Object, links.Object);
            bool ok = await logic.Link(parentId, studentId);

            ok.Should().BeFalse("the target must be a UserParent account");
            links.Verify(l => l.Link(It.IsAny<ParentLinkedStudent>()), Times.Never);
        }

        [Fact]
        public async Task Link_is_idempotent_when_already_linked()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();
            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.Exists(parentId, studentActor)).ReturnsAsync(true);
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var student = new LocalApplicationUser { Id = studentId, Actor = studentActor };
            var parent = new LocalApplicationUser { Id = parentId };
            um.Setup(m => m.FindByIdAsync(studentId.ToString())).ReturnsAsync(student);
            um.Setup(m => m.FindByIdAsync(parentId.ToString())).ReturnsAsync(parent);
            um.Setup(m => m.IsInRoleAsync(parent, "UserParent")).ReturnsAsync(true);

            var logic = new ParentLinkLogic(Accessor("Admin"), um.Object, links.Object);
            bool ok = await logic.Link(parentId, studentId);

            ok.Should().BeTrue("a duplicate link is a no-op success");
            links.Verify(l => l.Link(It.IsAny<ParentLinkedStudent>()), Times.Never);
        }

        [Fact]
        public async Task Unlink_is_denied_for_a_non_admin()
        {
            var links = new Mock<IParentLinkedStudentQueries>();
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var logic = new ParentLinkLogic(Accessor("User"), um.Object, links.Object);

            bool ok = await logic.Unlink(Guid.NewGuid(), Guid.NewGuid());

            ok.Should().BeFalse();
            links.Verify(l => l.Unlink(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Unlink_delegates_to_the_dal_for_an_admin()
        {
            Guid parentId = Guid.NewGuid();
            Guid actorId = Guid.NewGuid();
            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.Unlink(parentId, actorId)).ReturnsAsync(true);
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var logic = new ParentLinkLogic(Accessor("ITAdmin"), um.Object, links.Object);

            bool ok = await logic.Unlink(parentId, actorId);

            ok.Should().BeTrue();
            links.Verify(l => l.Unlink(parentId, actorId), Times.Once);
        }

        [Fact]
        public async Task GetLinkedStudents_is_empty_for_a_non_admin()
        {
            var links = new Mock<IParentLinkedStudentQueries>();
            Mock<UserManager<LocalApplicationUser>> um = MockUserManager();
            var logic = new ParentLinkLogic(Accessor("User"), um.Object, links.Object);

            List<Febris.ModelLibrary.ViewModels.ParentLinkViewModel> result =
                await logic.GetLinkedStudents(Guid.NewGuid());

            result.Should().BeEmpty();
            links.Verify(l => l.GetByParent(It.IsAny<Guid>()), Times.Never);
        }
    }
}
