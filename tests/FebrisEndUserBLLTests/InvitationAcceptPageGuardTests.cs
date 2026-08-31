// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Source-level guards on the invitation ACCEPT page (invitation flow 2026-08-21).
    ///
    /// <para>
    /// WHY SOURCE LEVEL, stated plainly because it is the weaker kind of test and the choice should
    /// be defended rather than assumed. These four properties live in a Razor PageModel whose
    /// controls are exercised through <c>UserManager</c>, <c>SignInManager</c> and a live
    /// <c>HttpContext</c>. Standing all three up would produce a test that mostly exercises ASP.NET
    /// and would still not fail if somebody deleted the one line that matters. What follows pins
    /// the decisions instead.
    /// </para>
    ///
    /// <para>
    /// THE FIRST GUARD IS THE POINT OF THE WHOLE FILE. The central developer-org invite flow ships
    /// <see cref="Febris.ModelLibrary.Models.DataModels.InviteRecipientMatch.RecipientEmailMatches"/>
    /// deliberately UNCALLED, with a source comment explaining that wiring it in was deferred -- so
    /// any holder of that flow's token can redeem it. This node's flow calls it. A copy of a
    /// known-flawed design reverting to the original is the exact regression worth a guard.
    /// </para>
    /// </summary>
    public class InvitationAcceptPageGuardTests
    {
        // Delegates to the shared walk. The marker used to be Febris.sln, which the public cut
        // deletes, so this guard failed in the export for a reason unrelated to what it guards.
        // See TestRepoRoot.
        private static string RepoRoot()
        {
            return TestRepoRoot.Find();
        }

        /// <summary>
        /// The accept page's code-behind with comments STRIPPED. Load-bearing: this suite has been
        /// burned before by a guard that matched the prose explaining it, and the page's own doc
        /// comment names every symbol asserted below.
        /// </summary>
        private static string PageSource()
        {
            string path = Path.Combine(RepoRoot(), "enduser", "FebrisEndUserPortal",
                "Areas", "Identity", "Pages", "Account", "AcceptInvitation.cshtml.cs");
            File.Exists(path).Should().BeTrue("the accept page must exist at " + path);

            string source = File.ReadAllText(path);
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            string noDoc = Regex.Replace(noBlock, @"^[ \t]*///.*$", string.Empty, RegexOptions.Multiline);
            return Regex.Replace(noDoc, @"^[ \t]*//.*$", string.Empty, RegexOptions.Multiline);
        }

        /// <summary>One named method's body, comments already stripped.</summary>
        private static string MethodBody(string signatureFragment)
        {
            string code = PageSource();
            int start = code.IndexOf(signatureFragment, StringComparison.Ordinal);
            start.Should().BeGreaterThan(-1, signatureFragment + " must still exist");

            int depth = 0;
            bool opened = false;
            for (int i = start; i < code.Length; i++)
            {
                if (code[i] == '{') { depth++; opened = true; }
                else if (code[i] == '}')
                {
                    depth--;
                    if (opened && depth == 0)
                    {
                        return code.Substring(start, i - start + 1);
                    }
                }
            }
            throw new InvalidOperationException("Could not find the end of " + signatureFragment);
        }

        [Fact]
        public void TheRecipientBinding_IsActuallyCalled()
        {
            // The central flow's documented defect, guarded against here. Without this call an
            // invitation link is a bearer token for an account at the granted role, and forwarding
            // the email is account transfer.
            MethodBody("OnPostAsync").Should().Contain("RecipientEmailMatches",
                "the invitee must prove they are the addressee, or a forwarded link transfers the account");
        }

        [Fact]
        public void TheAddress_IsNotPrefilledFromTheLink()
        {
            // Enforcing the binding and then handing over the answer would be pointless. The page
            // must not read an email out of the query string.
            string page = PageSource();
            page.Should().NotContain("OnGetAsync(string code, string email",
                "the address must not arrive from the link");
            page.Should().NotContain("Input.Email = ",
                "the address field must not be prefilled from anything the link carries");
        }

        [Fact]
        public void TheInvitation_IsConsumedOnPostOnly()
        {
            // A GET must not spend an invitation, or a link preview, a mail scanner or a
            // prefetching browser burns it before the invitee ever reads the message.
            MethodBody("OnGetAsync").Should().NotContain("Consume",
                "fetching the link must not spend the invitation");
            MethodBody("OnPostAsync").Should().Contain("Consume",
                "the invitation is claimed by a deliberate submit");
        }

        [Fact]
        public void ALostConsumeRace_RollsTheAccountBack()
        {
            // If the invitation was revoked between validating and claiming it, the account created
            // moments earlier must not survive -- otherwise cancelling an invitation still produced
            // an account.
            string post = MethodBody("OnPostAsync");
            post.Should().Contain("RollBackAsync",
                "an unclaimable invitation must not leave an account behind");
            MethodBody("RollBackAsync").Should().Contain("DeleteAsync");
        }

        [Fact]
        public void ThePage_IsAnonymousButDoesNotConsultTheRegistrationPolicy()
        {
            // Anonymous because the invitee has no account yet; independent of the registration mode
            // because an invitation is admin-initiated creation, not self-registration. Closing
            // registration must not strand people who already hold a valid invitation.
            string page = PageSource();
            page.Should().Contain("[AllowAnonymous]");
            page.Should().NotContain("IRegistrationPolicy",
                "an invitation carries its own authorization -- the mode governs strangers, not invitees");
            page.Should().NotContain("SelfRegistrationEnabled");
        }

        [Fact]
        public void TheGuards_DiscriminateRatherThanMatchAnything()
        {
            // Sanity on the guards themselves: prove the extracted bodies are the real ones and not
            // an empty string that trivially satisfies a NotContain, and that the strip really did
            // remove comments (the class doc above mentions RecipientEmailMatches by name).
            MethodBody("OnPostAsync").Length.Should().BeGreaterThan(500);
            MethodBody("OnGetAsync").Length.Should().BeGreaterThan(20);
            PageSource().Should().NotContain("known-flawed design",
                "comment stripping must actually strip, or every assertion here could match prose");
        }
    }
}
