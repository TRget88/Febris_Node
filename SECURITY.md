# Security Policy

## Reporting a vulnerability

Please report suspected vulnerabilities **privately**. Do not open a public issue for a
security bug.

Use this repository's **[Report a vulnerability](https://github.com/TRget88/Febris_Node/security/advisories/new)**
tab (Security -> Advisories -> Report a vulnerability). That channel is private between you
and the maintainers until a fix is published. It is the only reporting channel for this
project -- there is no security mailing address.

Please include the affected component, a description, and reproduction steps or a proof of
concept.

We will acknowledge receipt, investigate, and coordinate a fix and a disclosure timeline
with you.

## Supported versions

This project is pre-1.0 and maintained by one person. Security fixes land on the default
branch. There is no long-term support branch yet, and there is no backporting policy to
promise you. Run from the default branch.

## Scope

In scope: the node's own code -- the API (`Febris.UserNode.Api`), the Portal
(`Febris.UserNode.Portal`), the logic and data-access layers, the shared libraries under
`shared/`, and the self-hosting material (`docker-compose.yml`, `selfhost/generate-env.sh`,
`selfhost/Caddyfile`, the two `Dockerfile`s).

Out of scope, but still worth telling us about: vulnerabilities in the upstream images the
stack pulls (`postgres:16-alpine`, `valkey/valkey:8-alpine`, `caddy:2-alpine`, the
`mcr.microsoft.com/dotnet` bases) or in third-party NuGet packages. Report those upstream
first. See [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) for what is in the dependency
set.

## Known posture of the default deployment

These are documented defaults, not vulnerabilities. Read them before deciding what to
report, and see [`SELF_HOSTING.md`](SELF_HOSTING.md) for the operational detail.

- `selfhost/generate-env.sh` generates the Postgres password, the JWT secret and the first
  admin password with `openssl rand`, writes them to `.env`, and `chmod 600`s the file. The
  secrets live in that file in plaintext, so protect it accordingly.
- The script prints the generated first-login password to your terminal. Change it after
  first login.
- The bundled Caddy proxy terminates TLS with a self-signed certificate, so browsers and
  `curl` will complain until you put a real certificate in front of it.

## Secret scanning

Every release export is scanned before publication with **both** gitleaks (full history,
`--log-opts=--all`, plus a working-tree `dir` pass) and **trufflehog** (filesystem and git,
verified mode). The gate is *zero un-allowlisted findings*. Neither scanner alone is
sufficient -- trufflehog only reports credentials it can verify against a live service, and
gitleaks covers the unverified-but-real-shaped case -- so both must pass.

The allowlist is committed in this repository at [`.gitleaks.toml`](.gitleaks.toml) so the
exclusions are auditable rather than asserted. Entries are classified false positives:
vendored third-party assets, deliberate dummy fixtures, runtime-generated test keys, and
reserved `example.*` placeholder domains. If you believe an allowlist entry is hiding a real
credential, that is itself worth reporting through the channel above.
