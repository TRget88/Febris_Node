# Third-party notices

febris-node is licensed under AGPL-3.0-only (see [`LICENSE`](LICENSE)). That licence covers
the first-party source in this repository. It does **not** cover the third-party material
listed below, which remains under its own terms.

There are two distinct kinds of third-party material here, and the distinction matters:

1. **Vendored browser assets** -- real files committed to this repository and served to
   browsers by the Portal. These are genuinely redistributed by this repo. Sections 1 and 2.
2. **NuGet package references** -- declared in the `.csproj` files and restored from
   nuget.org at build time. No `.nupkg` and no compiled third-party assembly is committed
   here, but these packages do end up inside any container image or `dotnet publish` output
   you produce. Section 4.

Everything below was read out of the files in this repository. Where a component ships no
licence artefact, that is stated rather than papered over.

---

## 1. Gentelella admin theme (vendored, pruned)

Path: `enduser/FebrisEndUserPortal/wwwroot/gentelella-master/`

The theme is [Gentelella](https://github.com/ColorlibHQ/gentelella) by Aigars Silkalns /
Colorlib, **MIT** -- licence text at `gentelella-master/LICENSE.txt`.

Upstream Gentelella vendors ~46 jQuery plugins. This tree was pruned to the assets the
Portal actually loads: **81 files total** -- 4 at the theme root (`LICENSE.txt`, `README.md`,
`build/css/custom.min.css`, `build/js/custom.min.js`) and 77 under `vendors/`. Of the 45
`vendors/` directories, **15 still contain code or font assets**. The other 30 retain only
their licence files (see section 3).

The 15 surviving libraries, plus Sizzle, which is not its own `vendors/` directory but ships
inside the jQuery bundle and keeps its licence file there -- hence 16 rows below for 15
directories:

| Library | Version | Licence | Licence text in this repo |
|---|---|---|---|
| Bootstrap | 4.3.1 | MIT | `vendors/bootstrap/LICENSE`, `vendors/bootstrap/site/docs/4.3/about/license.md` |
| Font Awesome | 4.6.3 | CSS: MIT / fonts: SIL OFL 1.1 | header of `vendors/font-awesome/css/font-awesome.css` -- see notes (a) and (a2) |
| jQuery | 2.2.4 | MIT | `vendors/jquery/LICENSE.txt`, `vendors/jquery/AUTHORS.txt` |
| Sizzle (bundled inside jQuery) | -- | MIT | `vendors/jquery/external/sizzle/LICENSE.txt` |
| Moment.js | 2.13.0 | MIT | `vendors/moment/LICENSE` |
| Select2 | 4.0.3 | MIT | `vendors/select2/LICENSE.md` |
| Chart.js | 2.1.4 | MIT | `vendors/Chart.js/LICENSE.md` |
| iCheck | 1.0.2 | MIT | header of `vendors/iCheck/icheck.min.js` -- see note (a) |
| NProgress | 0.2.0 | MIT | `vendors/nprogress/License.md` |
| FastClick | -- | MIT | `vendors/fastclick/LICENSE` (© The Financial Times Ltd) |
| bootstrap-daterangepicker | 3.0.3 | MIT | header of `vendors/bootstrap-daterangepicker/daterangepicker.js` -- see note (a) |
| Autosize | 3.0.15 | MIT | `vendors/autosize/LICENSE.md` |
| Devbridge jQuery-Autocomplete | 1.2.24 | MIT | `vendors/devbridge-autocomplete/license.txt`, `.../dist/license.txt` |
| JQVMap | -- | MIT (elected) / GPL | `vendors/jqvmap/LICENSE` -- see section 5 |
| bootstrap-progressbar | 0.9.0 | MIT | `vendors/bootstrap-progressbar/LICENSE` |
| jQuery Tags Input | 1.3.3 | MIT | header of `vendors/jquery.tagsinput/src/jquery.tagsinput.js` -- see note (a) |

Notes:

- **(a)** These four ship without a standalone `LICENSE` file. The grant survives only as a
  comment header in the asset itself. Verified against git history: they carried no licence
  file *before* the theme prune either, so this is an upstream packaging gap in the vendored
  drop rather than something the prune removed. For the three MIT entries the header carries
  the copyright line and names the licence, which serves as the notice. Adding the full texts
  would still be an improvement.
- **(a2)** Font Awesome is **not** in that category, and the distinction is not cosmetic. The
  webfont files under `vendors/font-awesome/fonts/` are redistributed by this repository under
  SIL OFL 1.1, which conditions redistribution on each copy being accompanied by the licence
  text itself -- a URL pointing at it, which is all the CSS header provides, does not satisfy
  that condition. **No copy of the OFL 1.1 text exists anywhere in this repository.** This is
  an unmet redistribution requirement, not optional polish: the text must be added next to the
  fonts, copied verbatim from the canonical source at <https://openfontlicense.org/>, together
  with the upstream copyright and Reserved Font Name line for Font Awesome 4.6.3.
- Only `vendors/jqvmap/dist/jqvmap.css` survives from JQVMap. The map JavaScript does not,
  so no version string is recoverable from this tree.

## 2. ASP.NET Core scaffold libraries (vendored)

Path: `enduser/FebrisEndUserPortal/wwwroot/lib/`

The client-side validation stack from the default ASP.NET Core project template. These are a
separate copy from the theme's -- note the different jQuery and the one non-MIT entry.

| Library | Version | Licence | Licence text in this repo |
|---|---|---|---|
| Bootstrap | 4.3.1 | MIT | `lib/bootstrap/LICENSE` |
| jQuery | 3.5.1 | MIT | `lib/jquery/LICENSE.txt` |
| jQuery Validation | 1.17.0 | MIT (© Jörn Zaefferer) | `lib/jquery-validation/LICENSE.md` |
| jQuery Unobtrusive Validation | 3.2.11 | **Apache-2.0** (© .NET Foundation) | `lib/jquery-validation-unobtrusive/LICENSE.txt` |

Everything else under `wwwroot/` -- `css/`, `js/site.js`, `JSScriptLib/`, `images/`,
`favicon.ico` -- is first-party and covered by the repository's AGPL-3.0-only licence.

## 3. Licence files retained without code

The prune removed the code but kept the licence file for 30 `vendors/` directories:

`bootstrap-datetimepicker`, `cropper`, `datatables.net` (and the `-bs`, `-buttons`,
`-buttons-bs`, `-fixedheader`, `-fixedheader-bs`, `-keytable`, `-responsive`,
`-responsive-bs`, `-scroller`, `-scroller-bs` variants), `DateJS`, `echarts`, `eve`, `Flot`,
`flot-spline`, `fullcalendar`, `google-code-prettify`, `jquery.easy-pie-chart`,
`jquery-knob`, `jquery-mousewheel`, `jszip`, `mjolnic-bootstrap-colorpicker`, `mocha`,
`normalize-css`, `pdfmake`, `pnotify`, `raphael`.

**No code from any of these is distributed by this repository.** The files are listed here
so that a reader who greps for `LICENSE` under `wwwroot/` and finds, say, an Apache-2.0
`COPYING` under `pnotify/` knows it is a stranded artefact rather than an undeclared
dependency. Most are MIT, with three exceptions. `echarts` is BSD-3-Clause (© 2013 Baidu Inc.).
`eve`, `google-code-prettify`, `mjolnic-bootstrap-colorpicker` and `pnotify` are Apache-2.0.
`jszip` is dual MIT/GPLv3 (section 5).

## 4. NuGet package references

Direct `PackageReference` entries across the eleven projects that ship in this repository --
`Febris.UserNode.Api`, `Febris.UserNode.Portal`, `Febris.UserNode.LogicLayer`,
`Febris.UserNode.DataAccessLayer`, `Febris.EnumLibrary`, `Febris.ModelLibrary`,
`Febris.SharedServices`, `Febris.XApi.Models`, and the three test projects.

Licences below are the licence expression declared in each package's `.nuspec`. Where a
package declares no expression, that is stated.

| Package | Version | Licence |
|---|---|---|
| AWSSDK.S3 | 3.7.511.8 | Apache-2.0 |
| AspNetCoreRateLimit | 5.0.0 | not declared in package metadata -- see note (c) |
| FluentAssertions | 6.12.0 | Apache-2.0 |
| MailKit | 4.16.0 | MIT |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.10 | MIT |
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 8.0.10 | MIT |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.10 | MIT |
| Microsoft.AspNetCore.Identity.UI | 8.0.10 | MIT |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 8.0.10 | MIT |
| Microsoft.EntityFrameworkCore | 8.0.10 | MIT |
| Microsoft.EntityFrameworkCore.Design | 8.0.10 | MIT (build-time, `PrivateAssets=all`) |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.10 | MIT (tests only) |
| Microsoft.EntityFrameworkCore.Tools | 8.0.10 | MIT (build-time, `PrivateAssets=all`) |
| Microsoft.Extensions.Caching.StackExchangeRedis | 8.0.10 | MIT |
| Microsoft.Extensions.Configuration | 8.0.0 | MIT |
| Microsoft.Extensions.Configuration.Json | 8.0.0 | MIT |
| Microsoft.NET.Test.Sdk | 17.11.0 | MIT (tests only) |
| Microsoft.VisualStudio.Azure.Containers.Tools.Targets | 1.11.1 | **proprietary Microsoft EULA** -- see note (d) |
| Microsoft.VisualStudio.Web.CodeGeneration.Design | 3.1.5 | Apache-2.0 (scaffolding, build-time) |
| MimeKit | 4.16.0 | MIT |
| Moq | 4.18.4 | declared by URL -- see note (e) (tests only) |
| NCrontab.Signed | 3.3.2 | Apache-2.0 (`COPYING.txt` in the package) |
| NWebsec.AspNetCore.Middleware | 3.0.0 | BSD-3-Clause |
| Newtonsoft.Json | 13.0.3 | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.10 | PostgreSQL licence |
| Serilog | 3.1.1 | Apache-2.0 |
| Serilog.AspNetCore | 8.0.0 | Apache-2.0 |
| Serilog.Enrichers.Environment | 2.2.0 | Apache-2.0 |
| Serilog.Enrichers.Process | 2.0.2 | Apache-2.0 |
| Serilog.Enrichers.Thread | 3.1.0 | Apache-2.0 (declared by `licenseUrl`) |
| Serilog.Settings.Configuration | 8.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 6.0.0 | Apache-2.0 |
| Swashbuckle.AspNetCore | 6.5.0 | MIT |
| System.ComponentModel.Annotations | 5.0.0 | MIT |
| System.IdentityModel.Tokens.Jwt | 8.0.2 | MIT |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT |
| System.ServiceProcess.ServiceController | 8.0.0 | MIT |
| X.PagedList.Mvc.Core | 8.1.0 | declared by URL -- see note (e) |
| Xunit.SkippableFact | 1.4.13 | MS-PL (tests only) |
| coverlet.collector | 6.0.2 | MIT (tests only, `PrivateAssets=all`) |
| xunit | 2.9.2 | Apache-2.0 (tests only) |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 (tests only, `PrivateAssets=all`) |

Notes:

- **(c)** `aspnetcoreratelimit.nuspec` carries no `<license>` element and no `<licenseUrl>`.
  Consult the upstream project at
  <https://github.com/stefanprodan/AspNetCoreRateLimit> (the `projectUrl` and `repository`
  recorded in the nuspec) for its terms.
- **(d)** Flagged because it is the one non-open-source item in the dependency set. The
  package declares its licence as a bundled `EULA.md` -- "Microsoft Software License Terms --
  Microsoft Visual Studio Container Tools Targets" -- whose grant is scoped to developing,
  building and testing your own applications. It is Visual Studio container tooling: the
  package contains only `build/` (MSBuild `.props`/`.targets`) and `tools/` (MSBuild task
  assemblies), with **no `lib/`**, so it contributes no assembly to the published output. It
  is referenced by `enduser/FebrisEndUserApi/Febris.UserNode.Api.csproj` and
  `enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj`. The `docker compose` path
  builds through the `Dockerfile`s and does not invoke this tooling.
- **(e)** Moq 4.18.4 and X.PagedList.Mvc.Core 8.1.0 declare their licence with a
  `licenseUrl` pointing at the project's own `LICENSE` file rather than an SPDX expression,
  and neither `.nupkg` embeds a licence file. Read the terms at the declared URLs:
  <https://raw.githubusercontent.com/moq/moq4/main/License.txt> and
  <https://github.com/dncuug/X.PagedList/blob/master/LICENSE>.

This table lists **direct** references only. Each of these packages pulls its own
dependencies, and those are what actually end up in a published image. To resolve the full
transitive closure for a given project (the Portal has the widest graph):

```sh
dotnet restore enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj
dotnet list    enduser/FebrisEndUserPortal/Febris.UserNode.Portal.csproj package --include-transitive
```

## 5. Copyleft position

No GPL or AGPL obligation is inherited from any vendored asset.

- **JQVMap** is the only surviving vendored *code* offered under a copyleft grant, and it is
  offered dually: `vendors/jqvmap/LICENSE` opens "All code in this Github Repository are
  available under both the MIT and GPL license." **MIT is elected**, as the dual grant
  permits. The GPL branch of that offer is not exercised and imposes nothing.
- **JSZip** is likewise dual MIT/GPLv3, but only its `LICENSE.markdown` survives the prune --
  no JSZip code is distributed here (section 3). Nothing attaches either way.
- Every other vendored asset is MIT, Apache-2.0 (jQuery Unobtrusive Validation), or SIL OFL
  1.1 (the Font Awesome webfont files). All three are one-way compatible with AGPL-3.0-only,
  which is the direction this repository needs.

## 6. Container images

`docker-compose.yml` pulls `postgres:16-alpine`, `valkey/valkey:8-alpine` and `caddy:2-alpine`,
and the two `Dockerfile`s build `FROM mcr.microsoft.com/dotnet/sdk:8.0` and
`mcr.microsoft.com/dotnet/aspnet:8.0`. Those images are fetched from their registries at
build/run time and are **not** redistributed by this repository. Their contents are governed
by their own licences and by the licences of the packages inside them.

---

If you find an error or an omission in this file, please open an issue at
`https://github.com/TRget88/Febris_Node`. Suspected *security* problems go through
[`SECURITY.md`](SECURITY.md) instead.
