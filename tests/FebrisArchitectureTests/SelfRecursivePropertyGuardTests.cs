// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Febris.ArchitectureTests
{
    /// <summary>
    /// A property getter must never return the property it belongs to.
    ///
    /// <para>
    /// WHY THIS EXISTS. Fourteen controllers in the node Portal declared
    /// <c>private string StatusMessage { get { return StatusMessage; } set { TempData[...] = value; } }</c>.
    /// The getter returns itself, so any read is unbounded recursion ending in a
    /// <c>StackOverflowException</c> -- which .NET cannot catch and which terminates the PROCESS
    /// rather than failing one request. On a node that is every user's session, not just the
    /// unlucky one.
    /// </para>
    ///
    /// <para>
    /// It shipped and survived for years because it was LATENT: every use site only ever wrote the
    /// property and the views read <c>TempData</c> directly, so the getter had no caller. That is
    /// exactly what makes it worth a guard rather than a one-time fix. The defect is invisible to
    /// the compiler, invisible to every test, and armed by adding one innocuous read.
    /// </para>
    ///
    /// <para>
    /// THE NEXT INSTANCE WOULD HAVE BEEN AN EXACT COPY. The fourteen were not fourteen mistakes,
    /// they were fourteen SCAFFOLDS: the node Portal's own controller generator,
    /// <c>Templates/ControllerGenerator/MvcControllerWithContext.cshtml</c>, emitted this getter,
    /// and all fourteen still carried its fingerprint down to the redundant <c>return;</c> in the
    /// setter. That template is fixed in the same change and is scanned by this guard, because
    /// fixing the copies while leaving the generator is how a fix looks complete and quietly is not.
    /// </para>
    ///
    /// <para>
    /// <b>KNOWN BLIND SPOTS.</b> This is a regex over source, not a compiler, and it is worth being
    /// honest about the edges so nobody over-trusts it. It does NOT detect indirect recursion --
    /// <c>return $"{P}";</c>, <c>return P ?? "";</c>, or <c>var v = P; return v;</c> -- nor a
    /// declaration split across more than two lines. It reads C# comment and string syntax well
    /// enough for this tree but does not implement C# 11 raw string literals. What it does cover is
    /// every shape pinned by the theory below, including the <c>this.</c>-qualified and
    /// <c>as string</c>-cast forms, which between them are the plausible ways this returns.
    /// </para>
    /// </summary>
    public class SelfRecursivePropertyGuardTests
    {
        /// <summary>Trees this guard polices. Node-side only, matching the audit's scope.</summary>
        private static readonly string[] Roots =
        {
            "enduser/FebrisEndUserPortal",
            "enduser/FebrisEndUserApi",
            "enduser/FebrisEndUserBLL",
            "enduser/FebrisEndUserDAL"
        };

        /// <summary>
        /// Comments blanked, line count and offsets preserved, STRING LITERALS RESPECTED.
        ///
        /// <para>
        /// The literal handling is not decoration. A naive scan for <c>//</c> truncates any line
        /// holding a URL, and this codebase is full of them. Getting that wrong would silently
        /// shrink what the guard can see, which is the failure mode a guard must not have: it would
        /// keep reporting green while looking at less and less source.
        /// </para>
        /// </summary>
        internal static string StripCommentsPreservingLayout(string source)
        {
            StringBuilder outp = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];

                // Verbatim string: @"..." where "" is an escaped quote.
                // Verbatim literals, in all three spellings C# accepts: @"", $@"" and @$"". Only a
                // prefix CONTAINING @ is verbatim -- a bare $"" still treats backslash as an escape,
                // so it belongs in the ordinary branch below.
                //
                // Handling one spelling and not the other is not cosmetic. In @$"C:\" the trailing
                // backslash is literal; read as an escape it swallows the closing quote, the literal
                // never ends, and the comment on that line is never blanked. A commented-out
                // `return P;` then reads as live code and the guard reports a FALSE POSITIVE, which
                // is the failure mode that gets a guard deleted rather than fixed.
                int verbatimPrefix = 0;
                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    verbatimPrefix = 1;
                }
                else if ((c == '@' || c == '$')
                         && i + 2 < source.Length
                         && source[i + 1] == (c == '@' ? '$' : '@')
                         && source[i + 2] == '"')
                {
                    verbatimPrefix = 2;
                }

                if (verbatimPrefix > 0)
                {
                    for (int k = 0; k < verbatimPrefix; k++)
                    {
                        outp.Append(source[i + k]);
                    }
                    outp.Append('"');
                    i += verbatimPrefix + 1;
                    while (i < source.Length)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"')
                            {
                                outp.Append("\"\"");
                                i += 2;
                                continue;
                            }
                            outp.Append('"');
                            i++;
                            break;
                        }
                        outp.Append(source[i]);
                        i++;
                    }
                    continue;
                }

                // Ordinary string or char literal.
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    outp.Append(c);
                    i++;
                    while (i < source.Length)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            outp.Append(source[i]).Append(source[i + 1]);
                            i += 2;
                            continue;
                        }
                        outp.Append(source[i]);
                        bool done = source[i] == quote || source[i] == '\n';
                        i++;
                        if (done)
                        {
                            break;
                        }
                    }
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n')
                    {
                        outp.Append(' ');
                        i++;
                    }
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
                    {
                        outp.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < source.Length)
                    {
                        outp.Append("  ");
                        i += 2;
                    }
                    continue;
                }

                outp.Append(c);
                i++;
            }

            return outp.ToString();
        }

        /// <summary>
        /// Words that can stand where a property's TYPE would, and never introduce a property.
        ///
        /// <para>
        /// Needed because the access modifier is OPTIONAL below (see <see cref="BlockProperty"/>),
        /// and without this blocklist <c>namespace Foo {</c> and <c>class Bar {</c> parse as
        /// properties. An earlier version required a modifier and still miscounted three class
        /// declarations as properties, which mattered: they inflated the non-vacuity count and hid
        /// how close it was to its floor.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> NotATypeName = new HashSet<string>(StringComparer.Ordinal)
        {
            "namespace", "class", "struct", "interface", "record", "enum", "delegate", "event",
            "using", "else", "try", "finally", "do", "unsafe", "checked", "unchecked", "fixed",
            "lock", "switch", "return", "new", "where"
        };

        /// <summary>
        /// A property declaration whose name we capture: optional attributes, optional modifiers, a
        /// type, a name, then a brace on the same line or the next. Requiring the brace excludes
        /// fields.
        ///
        /// <para>
        /// THE MODIFIER IS OPTIONAL, deliberately. C# defaults a member to private, so
        /// <c>string P { get { return P; } }</c> is both legal and exactly the defect -- and an
        /// earlier version of this regex required <c>public|private|protected|internal</c> and
        /// therefore could not see it. Same for a default interface implementation.
        /// </para>
        ///
        /// <para>
        /// The name may be qualified for an explicit interface implementation
        /// (<c>string IThing.P</c>), so the qualifier is matched and discarded and only the final
        /// segment is captured.
        /// </para>
        /// </summary>
        private static readonly Regex BlockProperty = new Regex(
            @"^[ \t]*(?:\[[^\]]*\][ \t]*)*(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|new|required)[ \t]+)*(?<type>[\w<>?\[\],\.]+)[ \t]+(?:[\w<>\.]+\.)?(?<name>\w+)[ \t]*(?:\r?\n[ \t]*)?\{",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// <c>public string Foo =&gt; Foo;</c> -- the whole property is one expression, and if that
        /// expression is the property itself it is the same infinite recursion on one line.
        /// </summary>
        private static readonly Regex ExpressionProperty = new Regex(
            @"^[ \t]*(?:\[[^\]]*\][ \t]*)*(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|new|required)[ \t]+)*(?<type>[\w<>?\[\],\.]+)[ \t]+(?:[\w<>\.]+\.)?(?<name>\w+)[ \t]*=>[ \t]*(?<body>[^;]+);",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Strips a leading <c>this.</c> (and any explicit-interface qualifier) so the comparison is
        /// against the bare member name.
        ///
        /// <para>
        /// This is the highest-risk gap the first shipped version had. <c>get { return this.P; }</c>
        /// is the SAME defect written in the most conventional C# style there is, and the detector
        /// could not see it, nor <c>get =&gt; this.P;</c>, nor <c>T P =&gt; this.P;</c>. A guard that
        /// only catches the exact formatting of the instance you already fixed is a guard against
        /// copy-paste, not against the defect.
        /// </para>
        /// </summary>
        private static string Unqualify(string expression)
        {
            string e = expression.Trim();
            if (e.StartsWith("this.", StringComparison.Ordinal))
            {
                e = e.Substring(5).Trim();
            }
            return e;
        }

        /// <summary>
        /// Strips a trailing <c>as SomeType</c> cast, and surrounding parentheses.
        ///
        /// <para>
        /// <c>return StatusMessage as string;</c> is the single most likely way this defect comes
        /// back, and it is ONE TOKEN from the fix applied here fourteen times: every fixed getter now
        /// reads <c>return TempData["StatusMessage"] as string;</c>, and anybody trimming that
        /// expression lands exactly on the recursive version. An adversarial pass over the guard
        /// found it undetected.
        /// </para>
        /// </summary>
        private static string Simplify(string expression)
        {
            string e = expression.Trim();

            for (int guard = 0; guard < 4; guard++)
            {
                string before = e;

                Match cast = Regex.Match(e, @"^(?<inner>.+?)\s+as\s+[\w<>?\[\],\.]+$", RegexOptions.Singleline);
                if (cast.Success)
                {
                    e = cast.Groups["inner"].Value.Trim();
                }

                while (e.Length > 1 && e[0] == '(' && e[e.Length - 1] == ')')
                {
                    e = e.Substring(1, e.Length - 2).Trim();
                }

                e = Unqualify(e);

                if (e == before)
                {
                    break;
                }
            }

            return e;
        }

        /// <summary>Body of a getter that returns <paramref name="name"/>, however it is dressed up.</summary>
        private static bool ReturnsItself(string body, string name)
        {
            foreach (Match r in Regex.Matches(body, @"\breturn\s+(?<e>[^;]+?)\s*;"))
            {
                if (Simplify(r.Groups["e"].Value) == name)
                {
                    return true;
                }
            }

            foreach (Match g in Regex.Matches(body, @"\bget\b\s*=>\s*(?<e>[^;]+);"))
            {
                if (Simplify(g.Groups["e"].Value) == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks a property as having a getter at all, which is what separates it from a method or a
        /// type that happened to match the declaration shape.
        ///
        /// <para>
        /// <c>\s*</c> and not <c>[ \t]*</c>, and that single character is the whole reason this
        /// guard works. The first version required <c>get</c> and its brace on the SAME line. Every
        /// real instance in this codebase is written across two lines, so the filter rejected all
        /// fourteen before the recursion check ever ran, and the guard reported green on a tree with
        /// the defect deliberately put back. Mutation testing caught it. A guard that cannot see its
        /// own subject matter is worse than no guard, because it is believed.
        /// </para>
        /// </summary>
        private static readonly Regex HasGetter = new Regex(@"\bget\b\s*(?:\{|=>)", RegexOptions.Compiled);

        /// <summary>
        /// THE DETECTOR. One implementation, used both by the tree scan and by the synthetic-shape
        /// test below.
        ///
        /// <para>
        /// It was originally duplicated -- the shape test carried its own copy -- and mutation
        /// testing showed what that is worth: breaking the real detector left the shape test green,
        /// because the shape test was exercising a different copy of the logic. A test that proves a
        /// detector works has to call the detector that ships.
        /// </para>
        /// </summary>
        /// <param name="source">C# source, comments NOT yet stripped.</param>
        /// <param name="propertiesScanned">Incremented per property-with-getter examined.</param>
        /// <returns>Line number and name of each offending property.</returns>
        internal static List<(int Line, string Name)> FindSelfRecursiveGetters(string source, ref int propertiesScanned)
        {
            string live = StripCommentsPreservingLayout(source);
            List<(int, string)> found = new List<(int, string)>();

            foreach (Match m in BlockProperty.Matches(live))
            {
                string name = m.Groups["name"].Value;

                // `namespace Foo {` and `class Bar {` match the declaration shape and are not
                // properties. Excluding them is not tidiness: counted, they inflated the
                // non-vacuity total by three and disguised how close it sat to its floor.
                if (NotATypeName.Contains(m.Groups["type"].Value))
                {
                    continue;
                }

                int open = live.IndexOf('{', m.Index + m.Length - 1);
                if (open < 0)
                {
                    continue;
                }

                int depth = 0;
                int close = -1;
                for (int i = open; i < live.Length; i++)
                {
                    if (live[i] == '{')
                    {
                        depth++;
                    }
                    else if (live[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            close = i;
                            break;
                        }
                    }
                }
                if (close < 0)
                {
                    continue;
                }

                string body = live.Substring(open, close - open + 1);
                if (!HasGetter.IsMatch(body))
                {
                    continue;
                }

                propertiesScanned++;

                if (ReturnsItself(body, name))
                {
                    found.Add((LineOf(live, m.Index), name));
                }
            }

            foreach (Match m in ExpressionProperty.Matches(live))
            {
                if (NotATypeName.Contains(m.Groups["type"].Value))
                {
                    continue;
                }

                propertiesScanned++;
                if (Simplify(m.Groups["body"].Value) == m.Groups["name"].Value)
                {
                    found.Add((LineOf(live, m.Index), m.Groups["name"].Value));
                }
            }

            return found;
        }

        private static IEnumerable<string> SourceFiles(string repoRoot)
        {
            foreach (string root in Roots)
            {
                string dir = Path.Combine(repoRoot, root.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (string f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    if (ProjectGraph.IsInBuildOutput(f))
                    {
                        continue;
                    }

                    if (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return f;
                        continue;
                    }

                    // SCAFFOLDING TEMPLATES, which are .cshtml and were the ROOT CAUSE. The node
                    // Portal's own controller generator emitted this getter, so the fourteen
                    // instances were not fourteen mistakes -- they were fourteen scaffolds, all
                    // carrying the template's fingerprint down to the redundant `return;` in the
                    // setter. Fixing the copies while leaving the generator would have put the
                    // defect back on the next `Add Controller`.
                    //
                    // Scoped to Templates/ rather than all .cshtml: ordinary Razor views are full
                    // of constructs this C#-oriented parser has no business reading.
                    if (f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                        && f.IndexOf(Path.DirectorySeparatorChar + "Templates" + Path.DirectorySeparatorChar,
                                     StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        yield return f;
                    }
                }
            }
        }

        /// <summary>Line number of a character offset, 1-based.</summary>
        private static int LineOf(string source, int offset)
        {
            int line = 1;
            for (int i = 0; i < offset && i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    line++;
                }
            }
            return line;
        }

        [Fact]
        public void No_property_getter_returns_the_property_it_belongs_to()
        {
            string repoRoot = ProjectGraph.FindRepoRoot();

            List<string> offenders = new List<string>();
            int propertiesScanned = 0;

            foreach (string file in SourceFiles(repoRoot))
            {
                foreach ((int line, string name) in
                         FindSelfRecursiveGetters(File.ReadAllText(file), ref propertiesScanned))
                {
                    offenders.Add(ProjectGraph.Rel(repoRoot, file) + ":" + line + "  " + name);
                }
            }

            // Not just "> 0". Fourteen instances lived in this tree, and a scan that suddenly sees a
            // handful of properties has stopped looking at most of it -- the exact way this guard
            // already failed once.
            //
            // The floor is 25 against a real count around 50. An earlier version set it at 50, which
            // sounded rigorous and was a trap: the true count was 53, three of which were class
            // declarations wrongly parsed as properties, so once that miscount was fixed the
            // assertion sat EXACTLY on its floor. Deleting one controller would have failed this
            // test for a reason having nothing to do with the defect it polices, and a guard that
            // cries wolf gets deleted. A floor is a smoke alarm, not a spec.
            Assert.True(propertiesScanned >= 25,
                "the scan found only " + propertiesScanned +
                " properties with getters across the node trees, which means it has gone blind rather than clean");

            Assert.True(offenders.Count == 0,
                "these property getters return the property they belong to. That is unbounded recursion and a StackOverflowException, which .NET cannot catch and which kills the whole process rather than failing one request. Return the backing store instead:\n  " +
                string.Join("\n  ", offenders.OrderBy(o => o, StringComparer.Ordinal)));
        }

        [Fact]
        public void The_comment_stripper_does_not_eat_urls_or_string_literals()
        {
            // A guard is only as good as what it can see. A naive `//` scan truncates every line
            // holding a URL, and this codebase is full of them -- the guard would keep reporting
            // green while quietly looking at less and less source. Pinned rather than assumed.
            string sample = string.Join("\n", new[]
            {
                "string url = \"https://example.com/a\"; // trailing comment",
                "string verbatim = @\"C:\\x\\y\"; /* block */ int keep = 1;",
                "string quoted = \"he said \\\"hi\\\" // not a comment\";",
                "// whole line",
                "int after = 2;"
            });

            string stripped = StripCommentsPreservingLayout(sample);

            Assert.Contains("https://example.com/a", stripped);
            Assert.Contains("// not a comment", stripped);
            Assert.Contains("int keep = 1;", stripped);
            Assert.Contains("int after = 2;", stripped);
            Assert.DoesNotContain("trailing comment", stripped);
            Assert.DoesNotContain("whole line", stripped);
            Assert.DoesNotContain("block", stripped);

            // Verbatim in all three spellings. Handling @"" and $@"" but not @$"" left a trailing
            // backslash reading as an escape, so the literal never closed and the comment after it
            // survived -- turning commented-out code into an apparent violation.
            foreach (string prefix in new[] { "@", "$@", "@$" })
            {
                string line = "string p = " + prefix + "\"C:\\\"; // return Sneaky;";
                string s = StripCommentsPreservingLayout(line);
                Assert.True(s.IndexOf("return Sneaky", StringComparison.Ordinal) < 0,
                    "a comment after a " + prefix + "\"\" literal must still be stripped, or commented-out code reads as live");
            }

            // Line count preserved, so reported line numbers mean something.
            Assert.Equal(sample.Split('\n').Length, stripped.Split('\n').Length);
        }

        [Theory]
        // The shape that actually shipped, fourteen times, written across two lines. The first
        // version of this guard could not see it.
        [InlineData(@"
public class Sample
{
    [TempData]
    private string Multiline
    {
        get
        {
            return Multiline;
        }
        set
        {
            Store[""x""] = value;
        }
    }
}", true, "the two-line block form -- the shape that shipped")]
        [InlineData(@"
public class Sample
{
    private string OneLine
    {
        get { return OneLine; }
        set { Store = value; }
    }
}", true, "the single-line block form")]
        [InlineData(@"
public class Sample
{
    private string ArrowAccessor
    {
        get => ArrowAccessor;
        set { Store = value; }
    }
}", true, "the expression-bodied accessor")]
        [InlineData(@"
public class Sample
{
    private string ArrowProperty => ArrowProperty;
}", true, "the expression-bodied property")]
        // The `this.`-qualified forms. The most conventional C# style there is, and the first
        // shipped version of this guard missed all three -- a guard against copy-paste rather than
        // against the defect. Found by an adversarial pass over the guard itself, not by writing it.
        [InlineData(@"
public class Sample
{
    private string Qualified
    {
        get
        {
            return this.Qualified;
        }
    }
}", true, "a this.-qualified return")]
        [InlineData(@"
public class Sample
{
    private string QualifiedArrow
    {
        get => this.QualifiedArrow;
    }
}", true, "a this.-qualified expression accessor")]
        [InlineData(@"
public class Sample
{
    private string QualifiedProperty => this.QualifiedProperty;
}", true, "a this.-qualified expression property")]
        // THE MOST LIKELY REINTRODUCTION, and it is one token from the fix this change applied
        // fourteen times. Every fixed getter now reads `return TempData["StatusMessage"] as string;`
        // -- trim the indexer and you land exactly here. An adversarial pass over the guard found
        // this undetected, which is a better reason to have written the pass than any of the rest.
        [InlineData(@"
public class Sample
{
    private string Cast
    {
        get
        {
            return Cast as string;
        }
    }
}", true, "a self-return wearing an `as` cast")]
        [InlineData(@"
public class Sample
{
    private string Parenthesised
    {
        get
        {
            return (this.Parenthesised);
        }
    }
}", true, "a parenthesised, this.-qualified self-return")]
        // No access modifier at all. C# defaults members to private, so this compiles and is the
        // same defect; the first version required an explicit modifier and could not see it.
        [InlineData(@"
public class Sample
{
    string Implicit
    {
        get
        {
            return Implicit;
        }
    }
}", true, "a property with no access modifier")]
        // Explicit interface implementation, where the declared name is qualified.
        [InlineData(@"
public class Sample : IThing
{
    string IThing.Member
    {
        get
        {
            return Member;
        }
    }
}", true, "an explicit interface implementation")]
        // A type declaration is not a property. Counting these inflated the non-vacuity total and
        // hid how close it sat to its floor.
        [InlineData(@"
namespace Outer
{
    public class Program
    {
        public string Ok
        {
            get
            {
                return Store;
            }
        }
    }
}", false, "a namespace and a class are not properties")]
        // The sharper version of the case above, and the one that earns the keyword blocklist its
        // place. Here the ENCLOSING NAMESPACE is named `Store` and an inner getter legitimately
        // returns `Store`. Without the blocklist the namespace parses as a property whose body
        // returns its own name, and the guard reports a violation in correct code -- a false
        // positive, which is how a guard gets deleted rather than fixed. The first version of this
        // theory could not tell the two apart: it only asked whether a recursion was DETECTED, and
        // emptying the blocklist changed the count without changing the answer, so the mutation
        // escaped.
        [InlineData(@"
namespace Store
{
    public class Sample
    {
        public string Ok
        {
            get
            {
                return Store;
            }
        }
    }
}", false, "a namespace whose name is returned inside it is not a self-recursive property")]
        [InlineData(@"
public class Sample
{
    private string Fine
    {
        get
        {
            return TempData[""Fine""] as string;
        }
        set
        {
            TempData[""Fine""] = value;
        }
    }
}", false, "a getter reading a backing store is fine")]
        [InlineData(@"
public class Sample
{
    private string Other
    {
        get
        {
            return Different;
        }
    }
}", false, "returning a DIFFERENT member is fine")]
        public void The_guard_recognises_every_shape_the_defect_can_take(string source, bool expected, string because)
        {
            // Written against synthetic source rather than the tree, so it keeps testing the
            // detector after the real instances are fixed. Without this the tree scan passes forever
            // and nobody can tell whether it still detects anything. It calls the SHIPPING detector,
            // not a copy -- an earlier version duplicated the logic here, and breaking the real one
            // left this green.
            int scanned = 0;
            bool detected = FindSelfRecursiveGetters(source, ref scanned).Count > 0;

            Assert.True(detected == expected, because + " (expected " + expected + ", got " + detected + ")");
        }
    }
}
