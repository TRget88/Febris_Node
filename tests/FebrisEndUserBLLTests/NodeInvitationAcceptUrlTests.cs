// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Text.RegularExpressions;
using Febris.SharedServices;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins the two properties of the invitation accept URL that are invisible at the call site and
    /// whose failure modes are both SILENT (invitation flow 2026-08-21).
    ///
    /// <list type="number">
    /// <item><b>The token parameter must be named <c>code</c>.</b> The analytics middleware records
    /// <c>Request.QueryString</c> on every request into a table rendered to org admins, and
    /// <c>SensitiveQueryRedactor</c> blanks the values of a FIXED key list. Naming the parameter
    /// anything else would put live invitation tokens in that table for any admin to read -- which
    /// is finding H-26, reopened, on a new parameter.</item>
    /// <item><b>The URL must be ABSOLUTE.</b> The invitation email template renders its button only
    /// when the link parses as an absolute http/https URI (the SCBA-B4 anchor guard), so a relative
    /// URL would send a mail with no button, no error, and no log line.</item>
    /// </list>
    ///
    /// <para>
    /// Checked at SOURCE level because <c>AcceptUrlFor</c> needs a live <c>IUrlHelper</c> and
    /// <c>HttpRequest</c> to invoke, and standing those up would test the framework rather than the
    /// decision. What is worth pinning here is the decision.
    /// </para>
    /// </summary>
    public class NodeInvitationAcceptUrlTests
    {
        /// <summary>Walk up from the test binaries to the repository root.</summary>
        // Delegates to the shared walk. The marker used to be Febris.sln, which the public cut
        // deletes, so this guard failed in the export for a reason unrelated to what it guards.
        // See TestRepoRoot.
        private static string RepoRoot()
        {
            return TestRepoRoot.Find();
        }

        private static string ControllerSource()
        {
            string path = Path.Combine(RepoRoot(),
                "enduser", "FebrisEndUserPortal", "Controllers", "User", "InvitationsController.cs");
            File.Exists(path).Should().BeTrue("the invitations controller must exist at " + path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Strip comments before scanning. Learned the hard way elsewhere in this suite: a guard
        /// that matches its own explanatory prose passes for the wrong reason, and this file's own
        /// doc comment names the parameter it is checking for.
        /// </summary>
        private static string WithoutComments(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"^[ \t]*///?.*$", string.Empty, RegexOptions.Multiline);
        }

        /// <summary>The body of AcceptUrlFor, comments removed.</summary>
        private static string AcceptUrlBody()
        {
            string code = WithoutComments(ControllerSource());
            int start = code.IndexOf("public string AcceptUrlFor", StringComparison.Ordinal);
            start.Should().BeGreaterThan(-1, "AcceptUrlFor must still exist and still be named that");
            int end = code.IndexOf("private ", start, StringComparison.Ordinal);
            if (end < 0)
            {
                end = code.Length;
            }
            return code.Substring(start, end - start);
        }

        [Fact]
        public void TheTokenParameterName_IsOneTheAnalyticsRedactorBlanks()
        {
            // Both halves are asserted. The controller must use `code`, AND `code` must still be a
            // key the redactor blanks -- deleting it from SensitiveKeys would silently reopen H-26
            // just as surely as renaming the parameter would.
            AcceptUrlBody().Should().Contain("code = rawToken",
                "the token parameter must be named 'code', the key SensitiveQueryRedactor blanks");

            SensitiveQueryRedactor.SensitiveKeys.Should().Contain("code",
                "the invitation accept link relies on this key being redacted at analytics capture");
        }

        [Fact]
        public void ARedactedLinkKeepsTheTokenOutOfTheAnalyticsTable()
        {
            // The behavioural half of the same guarantee: what analytics would actually store.
            const string token = "3xAmPl3-t0k3n-w1th-256-b1ts-of-entropy";

            string stored = SensitiveQueryRedactor.Redact("?code=" + token);
            stored.Should().NotContain(token, "a captured invitation link must not retain the token");
            stored.Should().Contain(SensitiveQueryRedactor.Placeholder);

            // And the negative control that shows the choice of name is load-bearing rather than
            // decorative: a plausible alternative name is NOT covered.
            SensitiveQueryRedactor.Redact("?invite=" + token).Should().Contain(token,
                "this is exactly what naming the parameter something else would store");
        }

        [Fact]
        public void TheEmailAddress_IsNotCarriedInTheLink()
        {
            // Two reasons, both real: it would hand the recipient-binding answer to whoever holds
            // the link, and SensitiveQueryRedactor deliberately does NOT redact 'email', so it
            // would be retained verbatim in the analytics table.
            AcceptUrlBody().Should().NotContain("email",
                "the invited address must not travel in the accept link");

            SensitiveQueryRedactor.SensitiveKeys.Should().NotContain("email",
                "the redactor's own reasoning is that identifiers are a PII decision made elsewhere, "
                + "which is precisely why the address must not be put in a query string");
        }

        [Fact]
        public void TheAcceptUrl_IsBuiltAbsolute()
        {
            // A relative link renders a button-less email with nothing in any log to explain it,
            // because the template's SCBA-B4 guard only builds the anchor for an absolute http(s)
            // URI.
            AcceptUrlBody().Should().Contain("protocol: Request.Scheme",
                "the invitation email only renders its button for an ABSOLUTE http/https URL");
        }

        [Fact]
        public void TheGuardWouldFail_IfTheParameterWereRenamed()
        {
            // Mutation check on the guard itself rather than on the code, since the assertions above
            // are string containment and it is worth proving they discriminate. If the body were
            // written with a different parameter name, the first assertion would not match.
            string mutated = AcceptUrlBody().Replace("code = rawToken", "invite = rawToken");
            mutated.Should().NotContain("code = rawToken",
                "sanity: the mutation actually removes what the guard looks for");
        }
    }
}
