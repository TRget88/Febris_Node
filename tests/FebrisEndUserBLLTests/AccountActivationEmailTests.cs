// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.UserModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.PrimaryLogicLayer.Logic.UserLogic;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the account-activation email (2026-08-21).
    ///
    /// <para>
    /// THE DEFECT THIS CLOSES. <c>UserLogic.Create</c> generated a random password, assigned it,
    /// DISCARDED it (the local was never read again) and sent nothing. A person an admin created had
    /// an account with a password nobody knew and no notification that it existed, and their only
    /// route in was guessing that Forgot Password might work on an address they had never
    /// registered. The bulk path had the same defect wearing a disguise: it DID send mail, but an
    /// <c>EmailVerification</c> link to <c>/Account/ConfirmEmail</c> for an account already created
    /// with <c>EmailConfirmed = true</c>, so it confirmed nothing and still left no way to get a
    /// password.
    /// </para>
    ///
    /// <para>
    /// Two properties of the link are asserted here because both fail SILENTLY. It must be
    /// ABSOLUTE, since the AccountActivation email template only renders its button for an absolute
    /// http/https URI. And the token parameter must be named <c>code</c>, since
    /// <c>SensitiveQueryRedactor</c> blanks exactly that key before the analytics middleware stores
    /// the query string in a table rendered to org admins.
    /// </para>
    /// </summary>
    public class AccountActivationEmailTests
    {
        private static Mock<UserManager<LocalApplicationUser>> MockUserManager()
        {
            Mock<IUserStore<LocalApplicationUser>> store = new Mock<IUserStore<LocalApplicationUser>>();
            return new Mock<UserManager<LocalApplicationUser>>(
                store.Object, null, null, null, null, null, null, null, null);
        }

        /// <summary>What the logic layer actually asked the mail and URL layers to do.</summary>
        private sealed class Sent
        {
            public int Count;
            public string ToAddress;
            public string EmailTypeName;
            public string Url;
            public UrlRouteContext RouteContext;
        }

        private static IHttpContextAccessor AccessorFor(string role, string scheme)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Role, role) }, "test");
            DefaultHttpContext context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            if (scheme != null)
            {
                context.Request.Scheme = scheme;
            }
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(context);
            return accessor.Object;
        }

        /// <summary>
        /// A real-ish IUrlHelper. Url.Page is an EXTENSION method, so it cannot be stubbed directly:
        /// it funnels into IUrlHelper.RouteUrl(UrlRouteContext). Capturing that context is what makes
        /// the page name, the parameter name and the protocol assertable rather than a mock echoing
        /// back whatever string the test told it to.
        /// </summary>
        private static IUrlHelperFactory UrlFactory(Sent sent, string returned = "https://node.example/x")
        {
            Mock<IUrlHelper> url = new Mock<IUrlHelper>();

            // ActionContext is NOT decoration. UrlHelperExtensions.Page dereferences
            // urlHelper.ActionContext.RouteData.Values before it ever reaches RouteUrl, so a mock
            // without one throws a NullReferenceException that the production code's own catch then
            // swallows -- and the test sees "no email sent" with no clue why. That is exactly how
            // this harness failed the first time it ran.
            url.SetupGet(u => u.ActionContext).Returns(new ActionContext
            {
                RouteData = new RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
            });

            url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
                .Callback<UrlRouteContext>(ctx => sent.RouteContext = ctx)
                .Returns(returned);

            Mock<IUrlHelperFactory> factory = new Mock<IUrlHelperFactory>();
            factory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(url.Object);
            return factory.Object;
        }

        private static IEmailSender Emailer(Sent sent, Exception throws = null)
        {
            Mock<IEmailSender> mail = new Mock<IEmailSender>();
            var setup = mail.Setup(m => m.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            if (throws != null)
            {
                setup.ThrowsAsync(throws);
            }
            else
            {
                setup.Returns(Task.CompletedTask)
                     .Callback<string, string, string>((to, subject, body) =>
                     {
                         sent.Count++;
                         sent.ToAddress = to;
                         sent.EmailTypeName = subject;
                         sent.Url = body;
                     });
            }
            // Count the throwing case too, so "it tried" is distinguishable from "it never tried".
            if (throws != null)
            {
                mail.Setup(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string>((to, subject, body) => { sent.Count++; })
                    .ThrowsAsync(throws);
            }
            return mail.Object;
        }

        private static UserLogic BuildLogic(
            Sent sent, string actorRole = "Admin", string scheme = "https",
            IEmailSender emailer = null, IUrlHelperFactory urlFactory = null)
        {
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.UpdateAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync("RAW-RESET-TOKEN");

            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            actors.Setup(a => a.Create(It.IsAny<Actor>()))
                .ReturnsAsync(new Actor() { UUID = Guid.NewGuid() });

            Mock<IPasswordGenerator> passwords = new Mock<IPasswordGenerator>();
            passwords.Setup(p => p.PasswordRandomize()).Returns("Generated1!aaaa");

            return new UserLogic(
                AccessorFor(actorRole, scheme),
                users.Object,
                urlFactory ?? UrlFactory(sent),
                Mock.Of<IActionContextAccessor>(),
                passwords.Object,
                Mock.Of<IImageFileHandler>(),
                actors.Object,
                Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(),
                Mock.Of<IParentLinkLogic>(),
                emailer ?? Emailer(sent));
        }

        private static LocalUserCreation NewUser(
            InstitutionUserAccountType role = InstitutionUserAccountType.User)
        {
            return new LocalUserCreation
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                EmailAddress = "ada@school.example",
                UserAccountType = role
            };
        }

        // ---- 1. The mail is actually sent -------------------------------------------------------

        [Fact]
        public async Task CreatingAUser_SendsThemAnActivationEmail()
        {
            // The whole point. Before this, an admin-created account notified nobody.
            Sent sent = new Sent();

            LocalApplicationUser created = await BuildLogic(sent).Create(NewUser());

            created.Should().NotBeNull();
            sent.Count.Should().Be(1, "an admin-created account must tell its owner that it exists");
            sent.ToAddress.Should().Be("ada@school.example");
            sent.EmailTypeName.Should().Be(EmailType.AccountActivation.ToString(),
                "this codebase passes the EmailType NAME where a subject belongs");
        }

        [Theory]
        [InlineData(InstitutionUserAccountType.User)]
        [InlineData(InstitutionUserAccountType.Educator)]
        [InlineData(InstitutionUserAccountType.Admin)]
        [InlineData(InstitutionUserAccountType.ITAdmin)]
        public async Task EveryRoleGetsTheMail_NotJustLearners(
            InstitutionUserAccountType role)
        {
            // Only the User branch mints an xAPI Actor, so it is the branch a send could
            // accidentally end up inside. Every role must be reachable, not just the one that
            // happens to run the longest code path.
            //
            // The acting operator is ITAdmin, not Admin, and that is a correction the first run of
            // this test forced. An Admin can grant neither Admin nor ITAdmin: RoleRankPolicy's
            // ceiling rule (peers may administer each other) applies only at the node's TOP local
            // rank, and since SuperAdmin was removed from the node that top rank is ITAdmin. The
            // code was right and the expectation was wrong.
            Sent sent = new Sent();

            await BuildLogic(sent, actorRole: "ITAdmin").Create(NewUser(role));

            sent.Count.Should().Be(1);
        }

        // ---- 2. The link's two silent-failure properties ----------------------------------------

        [Fact]
        public async Task TheLink_IsAbsolute()
        {
            // The AccountActivation template only builds its anchor for an absolute http/https URI.
            // A relative link sends a button-less email with nothing in any log to explain it.
            Sent sent = new Sent();

            await BuildLogic(sent, scheme: "https").Create(NewUser());

            sent.RouteContext.Should().NotBeNull();
            sent.RouteContext.Protocol.Should().Be("https",
                "a protocol is what makes Url.Page return an absolute URL");
        }

        [Fact]
        public async Task TheLink_UsesTheRequestScheme_NotAHardcodedHttps()
        {
            // The bulk path used to hardcode "https", which sends a node reached over plain http a
            // link to a scheme it does not serve.
            Sent sent = new Sent();

            await BuildLogic(sent, scheme: "http").Create(NewUser());

            sent.RouteContext.Protocol.Should().Be("http");
        }

        [Fact]
        public async Task TheLink_FallsBackToHttps_WhenThereIsNoRequestScheme()
        {
            // Background or non-request contexts have no scheme. Falling back to https is the safe
            // direction, and falling back to NOTHING would produce a relative URL and a
            // button-less email.
            Sent sent = new Sent();

            await BuildLogic(sent, scheme: string.Empty).Create(NewUser());

            sent.RouteContext.Protocol.Should().Be("https");
        }

        [Fact]
        public async Task TheLink_PointsAtTheActivationPage_WithTheTokenNamedCode()
        {
            Sent sent = new Sent();

            await BuildLogic(sent).Create(NewUser());

            RouteValueDictionary values = new RouteValueDictionary(sent.RouteContext.Values);
            values.Should().ContainKey("page");
            values["page"].Should().Be("/Account/ActivateAccount");
            values.Should().ContainKey("area");
            values["area"].Should().Be("Identity");

            values.Should().ContainKey("code",
                "the token parameter must be named 'code' -- SensitiveQueryRedactor blanks exactly "
                + "that key before analytics stores the query string in a table shown to org admins");
            values["code"].Should().NotBeNull();
            values["code"].ToString().Should().NotBeEmpty();
        }

        [Fact]
        public void TheChosenParameterName_IsStillOneTheRedactorBlanks()
        {
            // The other half of the same guarantee, with a negative control proving the name is
            // load-bearing rather than incidental.
            SensitiveQueryRedactor.SensitiveKeys.Should().Contain("code");
            SensitiveQueryRedactor.Redact("?code=RAW-RESET-TOKEN")
                .Should().NotContain("RAW-RESET-TOKEN");
            SensitiveQueryRedactor.Redact("?activation=RAW-RESET-TOKEN")
                .Should().Contain("RAW-RESET-TOKEN",
                    "this is what naming the parameter anything else would retain");
        }

        [Fact]
        public async Task TheTokenIsAPasswordResetToken_NotAnEmailConfirmationToken()
        {
            // The bulk path used to send an EMAIL CONFIRMATION token to accounts created with
            // EmailConfirmed already true, so the link confirmed nothing and set no password. The
            // token type is what makes the difference, and it is invisible in the URL.
            Sent sent = new Sent();
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.UpdateAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync("RAW-RESET-TOKEN");

            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            actors.Setup(a => a.Create(It.IsAny<Actor>())).ReturnsAsync(new Actor() { UUID = Guid.NewGuid() });
            Mock<IPasswordGenerator> passwords = new Mock<IPasswordGenerator>();
            passwords.Setup(p => p.PasswordRandomize()).Returns("Generated1!aaaa");

            UserLogic logic = new UserLogic(
                AccessorFor("Admin", "https"), users.Object, UrlFactory(sent),
                Mock.Of<IActionContextAccessor>(), passwords.Object, Mock.Of<IImageFileHandler>(),
                actors.Object, Mock.Of<ICohortQueries>(), Mock.Of<ICohortMemberQueries>(),
                Mock.Of<IParentLinkLogic>(), Emailer(sent));

            await logic.Create(NewUser());

            users.Verify(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()),
                Times.Once, "the link must let them SET a password");
            users.Verify(u => u.GenerateEmailConfirmationTokenAsync(It.IsAny<LocalApplicationUser>()),
                Times.Never, "confirming an already-confirmed address is the defect, not the fix");
        }

        // ---- 3. A mail failure must not lose the account ----------------------------------------

        [Fact]
        public async Task AFailedSend_DoesNotThrow_AndStillReturnsTheAccount()
        {
            // EmailService rethrows on every failure, and the account is already committed by the
            // time this runs -- so an uncaught send would 500 the operator AFTER creating a user.
            // Several existing node call sites have exactly that shape. This one does not.
            Sent sent = new Sent();
            IEmailSender exploding = Emailer(sent, new InvalidOperationException("smtp unreachable"));

            LocalApplicationUser created = await BuildLogic(sent, emailer: exploding).Create(NewUser());

            created.Should().NotBeNull("a mail failure must not lose an account that already exists");
            created.Email.Should().Be("ada@school.example");
            sent.Count.Should().Be(1, "it must have TRIED, so a silent no-send is distinguishable");
        }

        [Fact]
        public async Task AFailedUrlBuild_DoesNotThrow()
        {
            // Url.Page throws if the helper is unusable. Same reasoning: the account exists already.
            Sent sent = new Sent();
            Mock<IUrlHelper> broken = new Mock<IUrlHelper>();
            broken.SetupGet(u => u.ActionContext).Returns(new ActionContext
            {
                RouteData = new RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
            });
            broken.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>()))
                .Throws(new InvalidOperationException("no route data"));
            Mock<IUrlHelperFactory> factory = new Mock<IUrlHelperFactory>();
            factory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>())).Returns(broken.Object);

            LocalApplicationUser created = await BuildLogic(sent, urlFactory: factory.Object).Create(NewUser());

            created.Should().NotBeNull();
            sent.Count.Should().Be(0, "the send never got as far as being attempted");
        }

        // ---- 4. Paths that must NOT send it -----------------------------------------------------

        [Fact]
        public async Task ARefusedCreate_SendsNothing()
        {
            // An Educator may not create an Admin. The rank gate returns before the account exists,
            // so there must be no mail either -- otherwise a refused create would still tell
            // somebody an account was made for them.
            Sent sent = new Sent();

            LocalApplicationUser created = await BuildLogic(sent, actorRole: "Educator")
                .Create(NewUser(InstitutionUserAccountType.Admin));

            created.Should().BeNull();
            sent.Count.Should().Be(0);
        }

        // ---- 5. Resend --------------------------------------------------------------------------

        /// <summary>Build a logic whose UserManager resolves ONE target user holding
        /// <paramref name="targetRole"/>, for exercising the resend path.</summary>
        private static UserLogic BuildForResend(
            Sent sent, string actorRole, string targetRole,
            bool targetExists = true, bool targetDeleted = false, IEmailSender emailer = null)
        {
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            LocalApplicationUser target = targetExists
                ? new LocalApplicationUser
                {
                    Email = "ada@school.example",
                    UserName = "ada@school.example",
                    IsDeleted = targetDeleted
                }
                : null;

            users.Setup(u => u.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(target);
            users.Setup(u => u.GetRolesAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(new List<string> { targetRole });
            users.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync("RAW-RESET-TOKEN");

            return new UserLogic(
                AccessorFor(actorRole, "https"), users.Object, UrlFactory(sent),
                Mock.Of<IActionContextAccessor>(), Mock.Of<IPasswordGenerator>(),
                Mock.Of<IImageFileHandler>(), Mock.Of<IActorLogic>(), Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(), Mock.Of<IParentLinkLogic>(),
                emailer ?? Emailer(sent));
        }

        [Fact]
        public async Task Resend_SendsTheSameActivationLink()
        {
            // The button exists because the activation email's own copy tells a recipient whose link
            // lapsed to ask an administrator to resend.
            Sent sent = new Sent();

            (bool allowed, bool wasSent) = await BuildForResend(sent, "Admin", "User")
                .ResendActivation(Guid.NewGuid());

            allowed.Should().BeTrue();
            wasSent.Should().BeTrue();
            sent.Count.Should().Be(1);
            sent.EmailTypeName.Should().Be(EmailType.AccountActivation.ToString());
            sent.ToAddress.Should().Be("ada@school.example");

            RouteValueDictionary values = new RouteValueDictionary(sent.RouteContext.Values);
            values["page"].Should().Be("/Account/ActivateAccount");
            values.Should().ContainKey("code");
            sent.RouteContext.Protocol.Should().Be("https");
        }

        [Fact]
        public async Task Resend_MintsAFreshToken_SoALapsedLinkIsRecoverable()
        {
            // If it reused a token there would be nothing to resend: the whole point is that the
            // original has expired.
            Sent sent = new Sent();
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            users.Setup(u => u.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(new LocalApplicationUser { Email = "ada@school.example" });
            users.Setup(u => u.GetRolesAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync(new List<string> { "User" });
            users.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()))
                .ReturnsAsync("RAW-RESET-TOKEN");

            UserLogic logic = new UserLogic(
                AccessorFor("Admin", "https"), users.Object, UrlFactory(sent),
                Mock.Of<IActionContextAccessor>(), Mock.Of<IPasswordGenerator>(),
                Mock.Of<IImageFileHandler>(), Mock.Of<IActorLogic>(), Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(), Mock.Of<IParentLinkLogic>(), Emailer(sent));

            await logic.ResendActivation(Guid.NewGuid());
            await logic.ResendActivation(Guid.NewGuid());

            users.Verify(u => u.GeneratePasswordResetTokenAsync(It.IsAny<LocalApplicationUser>()),
                Times.Exactly(2), "each press must mint a new token, not replay the old one");
        }

        [Theory]
        [InlineData("Educator", "User", true)]
        [InlineData("Admin", "User", true)]
        [InlineData("Admin", "Educator", true)]
        [InlineData("ITAdmin", "Admin", true)]
        [InlineData("Educator", "Admin", false)]
        [InlineData("Educator", "Educator", false)]
        [InlineData("Admin", "Admin", false)]
        [InlineData("Admin", "ITAdmin", false)]
        [InlineData("User", "User", false)]
        public async Task Resend_AppliesTheSameRankGateAsTheLockoutToggle(
            string actorRole, string targetRole, bool expectedAllowed)
        {
            // RoleRankPolicy.CanLock is REUSED rather than a second rule invented: the question is
            // identical (may this operator act on that account) and two rank rules for one question
            // is how they drift. Peers are refused, which makes this STRICTER than the gate on
            // creating the account -- deliberate, and in the safe direction. A peer's own route is
            // Forgot Password.
            Sent sent = new Sent();

            (bool allowed, bool wasSent) = await BuildForResend(sent, actorRole, targetRole)
                .ResendActivation(Guid.NewGuid());

            allowed.Should().Be(expectedAllowed);
            sent.Count.Should().Be(expectedAllowed ? 1 : 0,
                "a refused resend must not mail anything either");
        }

        [Fact]
        public async Task Resend_RefusesAnAccountThatDoesNotExist()
        {
            Sent sent = new Sent();

            (bool allowed, bool wasSent) = await BuildForResend(sent, "ITAdmin", "User", targetExists: false)
                .ResendActivation(Guid.NewGuid());

            allowed.Should().BeFalse();
            sent.Count.Should().Be(0);
        }

        [Fact]
        public async Task Resend_RefusesASoftDeletedAccount()
        {
            // A soft-deleted account is RETAINED, not live. Mailing it a working password-setup link
            // would quietly undo the deletion, which is why ResetPassword refuses one too.
            Sent sent = new Sent();

            (bool allowed, bool wasSent) = await BuildForResend(sent, "ITAdmin", "User", targetDeleted: true)
                .ResendActivation(Guid.NewGuid());

            allowed.Should().BeFalse();
            sent.Count.Should().Be(0);
        }

        [Fact]
        public async Task Resend_ReportsAFailedSend_RatherThanAGreenTick()
        {
            // Allowed and Sent are separate for exactly this case: the operator pressed the button,
            // they were entitled to, and the mail still did not go. Telling them it worked is the
            // one outcome worse than telling them it did not.
            Sent sent = new Sent();
            IEmailSender exploding = Emailer(sent, new InvalidOperationException("smtp unreachable"));

            (bool allowed, bool wasSent) = await BuildForResend(sent, "Admin", "User", emailer: exploding)
                .ResendActivation(Guid.NewGuid());

            allowed.Should().BeTrue();
            wasSent.Should().BeFalse();
            sent.Count.Should().Be(1, "it must have TRIED");
        }

        [Fact]
        public async Task SelfRegistrationAndInvitationAcceptance_SendNoActivationMail()
        {
            // Both of those set their own password during the flow. An activation link afterwards
            // would be a live password-reset token mailed to somebody who did not ask for one.
            Sent sent = new Sent();
            Mock<UserManager<LocalApplicationUser>> users = MockUserManager();
            users.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((LocalApplicationUser)null);
            users.Setup(u => u.CreateAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            users.Setup(u => u.AddToRoleAsync(It.IsAny<LocalApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            Mock<IActorLogic> actors = new Mock<IActorLogic>();
            actors.Setup(a => a.Create(It.IsAny<Actor>())).ReturnsAsync(new Actor() { UUID = Guid.NewGuid() });

            UserLogic logic = new UserLogic(
                AccessorFor("Admin", "https"), users.Object, UrlFactory(sent),
                Mock.Of<IActionContextAccessor>(), Mock.Of<IPasswordGenerator>(),
                Mock.Of<IImageFileHandler>(), actors.Object, Mock.Of<ICohortQueries>(),
                Mock.Of<ICohortMemberQueries>(), Mock.Of<IParentLinkLogic>(), Emailer(sent));

            await logic.CreateSelfRegistered("Ada", "L", "a@school.example", "P@ssw0rd!", false);
            await logic.CreateFromInvitation("Ada", "L", "b@school.example", "P@ssw0rd!", "User");

            sent.Count.Should().Be(0);
        }
    }
}
