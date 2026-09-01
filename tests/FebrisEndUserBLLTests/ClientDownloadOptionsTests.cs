using Febris.EnumLibrary;
using Febris.UserNode.Portal;
using Xunit;

namespace Febris.UserNode.Portal.Tests
{
    /// <summary>
    /// Pins the link-out behaviour the portal relies on when this node holds no local copy of a
    /// client package. The properties worth pinning are: it is ON without configuration (a node
    /// nobody configured must still point its operator somewhere), blanking it turns link-out
    /// off completely (the air-gap path), and a misconfigured value degrades to "no link" rather
    /// than to a relative URL that would resolve against the node's own host.
    /// </summary>
    public class ClientDownloadOptionsTests
    {
        [Fact]
        public void Default_configuration_links_out_without_any_operator_action()
        {
            ClientDownloadOptions options = new ClientDownloadOptions();

            Assert.Equal("https://www.febr.is", options.BaseUrl);
            Assert.Equal("https://www.febr.is/#pc", options.DownloadUrlFor(LocalSoftwarePackageType.PC));
        }

        [Theory]
        [InlineData(LocalSoftwarePackageType.PC, "pc")]
        [InlineData(LocalSoftwarePackageType.AndroidMobileServer, "mobile-server")]
        [InlineData(LocalSoftwarePackageType.AndroidMobileCompanion, "mobile-companion")]
        [InlineData(LocalSoftwarePackageType.CSharp, "sdk-csharp")]
        [InlineData(LocalSoftwarePackageType.CPP, "sdk-cpp")]
        public void Every_real_kind_has_an_anchor_matching_the_landing_page(
            LocalSoftwarePackageType kind, string expectedAnchor)
        {
            // These strings are a contract with tools/generate_site.py in the Febris_Landing
            // repository, which emits one card per kind with exactly these ids. A change on
            // either side without the other silently drops the visitor at the top of the page.
            Assert.Equal(expectedAnchor, ClientDownloadOptions.AnchorFor(kind));
            Assert.Equal("https://www.febr.is/#" + expectedAnchor,
                new ClientDownloadOptions().DownloadUrlFor(kind));
        }

        [Fact]
        public void The_None_kind_has_no_page_and_produces_no_link()
        {
            Assert.Null(ClientDownloadOptions.AnchorFor(LocalSoftwarePackageType.None));
            Assert.Null(new ClientDownloadOptions().DownloadUrlFor(LocalSoftwarePackageType.None));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Blanking_the_base_url_disables_link_out_entirely(string configured)
        {
            // The air-gapped path. An operator with no internet must not be shown a URL they
            // cannot reach, and must get the same empty state the portal had before link-out.
            ClientDownloadOptions options = new ClientDownloadOptions { BaseUrl = configured };

            Assert.Null(options.DownloadUrlFor(LocalSoftwarePackageType.PC));
            Assert.Null(options.DownloadUrlFor(LocalSoftwarePackageType.CSharp));
        }

        [Theory]
        [InlineData("febr.is")]                       // no scheme, would become a relative link
        [InlineData("/downloads")]                    // path only
        [InlineData("ftp://febr.is")]                 // wrong scheme
        [InlineData("javascript:alert(1)")]           // never emit this into an href
        [InlineData("not a url at all")]
        public void A_misconfigured_base_url_produces_no_link_rather_than_a_bad_one(string configured)
        {
            ClientDownloadOptions options = new ClientDownloadOptions { BaseUrl = configured };

            Assert.Null(options.DownloadUrlFor(LocalSoftwarePackageType.PC));
        }

        [Theory]
        [InlineData("https://www.febr.is", "https://www.febr.is/#pc")]
        [InlineData("https://www.febr.is/", "https://www.febr.is/#pc")]
        [InlineData("https://www.febr.is///", "https://www.febr.is/#pc")]
        [InlineData("  https://www.febr.is  ", "https://www.febr.is/#pc")]
        [InlineData("https://mirror.example.com/febris", "https://mirror.example.com/febris/#pc")]
        [InlineData("https://mirror.example.com/febris/", "https://mirror.example.com/febris/#pc")]
        [InlineData("http://10.0.0.5:8080", "http://10.0.0.5:8080/#pc")]
        public void Operator_supplied_roots_normalise_predictably(string configured, string expected)
        {
            // A LAN mirror on a subdirectory is the supported air-gap alternative to blanking it,
            // so a configured path must survive and the trailing slash must not double up.
            ClientDownloadOptions options = new ClientDownloadOptions { BaseUrl = configured };

            Assert.Equal(expected, options.DownloadUrlFor(LocalSoftwarePackageType.PC));
        }

        [Theory]
        [InlineData(LocalSoftwarePackageType.PC, "https://www.febr.is/#docs-pc")]
        [InlineData(LocalSoftwarePackageType.AndroidMobileServer, "https://www.febr.is/#docs-mobile-server")]
        [InlineData(LocalSoftwarePackageType.AndroidMobileCompanion, "https://www.febr.is/#docs-mobile-companion")]
        [InlineData(LocalSoftwarePackageType.CSharp, "https://www.febr.is/#docs-sdk-csharp")]
        [InlineData(LocalSoftwarePackageType.CPP, "https://www.febr.is/#docs-sdk-cpp")]
        public void Documentation_routes_to_the_public_how_to_page(
            LocalSoftwarePackageType kind, string expected)
        {
            // Anchors are a contract with the landing site generator, which emits a docs-<slug>
            // id per component. A mismatch drops the reader at the top of the page.
            Assert.Equal(expected, new ClientDownloadOptions().DocumentationUrlFor(kind));
        }

        [Fact]
        public void Documentation_is_disabled_by_the_same_switch_as_downloads()
        {
            // An air-gapped node must not be handed a URL it cannot reach, for either purpose.
            ClientDownloadOptions off = new ClientDownloadOptions { BaseUrl = "" };

            Assert.Null(off.DocumentationUrlFor(LocalSoftwarePackageType.PC));
            Assert.Null(new ClientDownloadOptions().DocumentationUrlFor(LocalSoftwarePackageType.None));
        }

        [Fact]
        public void Documentation_and_download_share_one_configured_root()
        {
            // A LAN mirror must serve both from the same place, so these must never diverge.
            ClientDownloadOptions mirror = new ClientDownloadOptions { BaseUrl = "https://mirror.example.com/febris/" };

            Assert.Equal("https://mirror.example.com/febris/#pc",
                mirror.DownloadUrlFor(LocalSoftwarePackageType.PC));
            Assert.Equal("https://mirror.example.com/febris/#docs-pc",
                mirror.DocumentationUrlFor(LocalSoftwarePackageType.PC));
        }

        [Fact]
        public void A_query_or_fragment_on_the_configured_root_is_discarded()
        {
            // GetLeftPart(UriPartial.Path) drops both. Pinned because a stray fragment would
            // otherwise produce two '#' characters and break the anchor silently.
            ClientDownloadOptions options = new ClientDownloadOptions
            {
                BaseUrl = "https://www.febr.is/?utm=x#already"
            };

            Assert.Equal("https://www.febr.is/#pc", options.DownloadUrlFor(LocalSoftwarePackageType.PC));
        }
    }
}
