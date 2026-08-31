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
    /// Pins the parent-lockout cascade (B-07 sibling): when a student is locked out, the linked
    /// parent is locked only if EVERY one of that parent's children is now locked. One-directional
    /// (student -> parent). Exercised through ParentLinkLogic with a mocked link DAL and a mocked
    /// UserManager, mirroring ParentLinkLogicTests.
    /// </summary>
    public class UserLockoutCascadeTests
    {
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            var store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        private static IHttpContextAccessor Accessor()
        {
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
            var ctx = new DefaultHttpContext { User = principal };
            var acc = new Mock<IHttpContextAccessor>();
            acc.Setup(a => a.HttpContext).Returns(ctx);
            return acc.Object;
        }

        private static readonly DateTimeOffset Locked = DateTimeOffset.MaxValue;
        private static readonly DateTimeOffset NotLocked = DateTimeOffset.UtcNow.AddMinutes(-5);

        [Fact]
        public async Task NoActorId_OnStudentWithoutActor_DoesNothing()
        {
            var links = new Mock<IParentLinkedStudentQueries>();
            var um = MockUserManager();
            var logic = new ParentLinkLogic(Accessor(), um.Object, links.Object);

            await logic.CascadeLockParentIfAllChildrenLocked(
                new LocalApplicationUser { Id = Guid.NewGuid(), Actor = null });

            links.Verify(l => l.GetParentsForStudent(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task AllChildrenLocked_LocksTheParent()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();
            Guid child1Id = Guid.NewGuid();
            Guid child2Id = Guid.NewGuid();
            var lockedStudent = new LocalApplicationUser { Id = child1Id, Actor = studentActor };

            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetParentsForStudent(studentActor)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id, StudentActorId = studentActor }
            });
            links.Setup(l => l.GetByParent(parentId)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id },
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child2Id }
            });

            var child1 = new LocalApplicationUser { Id = child1Id };
            var child2 = new LocalApplicationUser { Id = child2Id };
            var parent = new LocalApplicationUser { Id = parentId };

            var um = MockUserManager();
            um.Setup(m => m.FindByIdAsync(child1Id.ToString())).ReturnsAsync(child1);
            um.Setup(m => m.FindByIdAsync(child2Id.ToString())).ReturnsAsync(child2);
            um.Setup(m => m.FindByIdAsync(parentId.ToString())).ReturnsAsync(parent);
            um.Setup(m => m.GetLockoutEndDateAsync(child1)).ReturnsAsync(Locked);
            um.Setup(m => m.GetLockoutEndDateAsync(child2)).ReturnsAsync(Locked);
            um.Setup(m => m.GetLockoutEndDateAsync(parent)).ReturnsAsync((DateTimeOffset?)null);
            um.Setup(m => m.SetLockoutEndDateAsync(parent, It.IsAny<DateTimeOffset?>()))
              .ReturnsAsync(IdentityResult.Success);
            um.Setup(m => m.UpdateAsync(parent)).ReturnsAsync(IdentityResult.Success);

            var logic = new ParentLinkLogic(Accessor(), um.Object, links.Object);
            await logic.CascadeLockParentIfAllChildrenLocked(lockedStudent);

            um.Verify(m => m.SetLockoutEndDateAsync(parent, DateTimeOffset.MaxValue), Times.Once,
                "all of the parent's children are locked, so the parent is locked too");
        }

        [Fact]
        public async Task OneChildStillUnlocked_DoesNotLockTheParent()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();
            Guid child1Id = Guid.NewGuid();
            Guid child2Id = Guid.NewGuid();
            var lockedStudent = new LocalApplicationUser { Id = child1Id, Actor = studentActor };

            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetParentsForStudent(studentActor)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id, StudentActorId = studentActor }
            });
            links.Setup(l => l.GetByParent(parentId)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id },
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child2Id }
            });

            var child1 = new LocalApplicationUser { Id = child1Id };
            var child2 = new LocalApplicationUser { Id = child2Id };

            var um = MockUserManager();
            um.Setup(m => m.FindByIdAsync(child1Id.ToString())).ReturnsAsync(child1);
            um.Setup(m => m.FindByIdAsync(child2Id.ToString())).ReturnsAsync(child2);
            um.Setup(m => m.GetLockoutEndDateAsync(child1)).ReturnsAsync(Locked);
            um.Setup(m => m.GetLockoutEndDateAsync(child2)).ReturnsAsync(NotLocked);

            var logic = new ParentLinkLogic(Accessor(), um.Object, links.Object);
            await logic.CascadeLockParentIfAllChildrenLocked(lockedStudent);

            um.Verify(m => m.SetLockoutEndDateAsync(It.IsAny<LocalApplicationUser>(), DateTimeOffset.MaxValue), Times.Never,
                "one child is still unlocked, so the parent must not be cascaded");
        }

        [Fact]
        public async Task ParentAlreadyLocked_IsNotReLocked()
        {
            Guid parentId = Guid.NewGuid();
            Guid studentActor = Guid.NewGuid();
            Guid child1Id = Guid.NewGuid();
            var lockedStudent = new LocalApplicationUser { Id = child1Id, Actor = studentActor };

            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetParentsForStudent(studentActor)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id, StudentActorId = studentActor }
            });
            links.Setup(l => l.GetByParent(parentId)).ReturnsAsync(new List<ParentLinkedStudent>
            {
                new ParentLinkedStudent { ParentUserId = parentId, StudentUserId = child1Id }
            });

            var child1 = new LocalApplicationUser { Id = child1Id };
            var parent = new LocalApplicationUser { Id = parentId };

            var um = MockUserManager();
            um.Setup(m => m.FindByIdAsync(child1Id.ToString())).ReturnsAsync(child1);
            um.Setup(m => m.FindByIdAsync(parentId.ToString())).ReturnsAsync(parent);
            um.Setup(m => m.GetLockoutEndDateAsync(child1)).ReturnsAsync(Locked);
            um.Setup(m => m.GetLockoutEndDateAsync(parent)).ReturnsAsync(Locked);

            var logic = new ParentLinkLogic(Accessor(), um.Object, links.Object);
            await logic.CascadeLockParentIfAllChildrenLocked(lockedStudent);

            um.Verify(m => m.SetLockoutEndDateAsync(parent, It.IsAny<DateTimeOffset?>()), Times.Never,
                "an already-locked parent is left untouched (idempotent)");
        }

        [Fact]
        public async Task StudentWithNoParentLinks_DoesNothing()
        {
            Guid studentActor = Guid.NewGuid();
            var lockedStudent = new LocalApplicationUser { Id = Guid.NewGuid(), Actor = studentActor };

            var links = new Mock<IParentLinkedStudentQueries>();
            links.Setup(l => l.GetParentsForStudent(studentActor)).ReturnsAsync(new List<ParentLinkedStudent>());
            var um = MockUserManager();

            var logic = new ParentLinkLogic(Accessor(), um.Object, links.Object);
            await logic.CascadeLockParentIfAllChildrenLocked(lockedStudent);

            um.Verify(m => m.SetLockoutEndDateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
        }
    }
}
