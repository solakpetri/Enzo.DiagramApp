# Public Release Checklist

## Security

- [x] No secrets in current files.
- [x] Git history reviewed.
- [ ] Production credentials rotated if ever exposed.
- [x] SVG injection protections tested.
- [x] API limits reviewed.
- [x] GitHub Actions permissions reviewed.

## Azure Shutdown

- [x] Azure deployment workflow removed.
- [x] Azure production endpoint removed from current docs.
- [x] Historical Azure note added.
- [ ] GitHub Azure secrets and variables can be removed.
- [ ] Azure federated credential can be removed.
- [ ] Azure Container App can be stopped or deleted.
- [ ] GHCR package retained if useful.

## Retired Features

- [x] README contains `Removed and retired features`.
- [x] MCP experiment marked removed.
- [x] ChatGPT App / Apps SDK experiment marked removed.
- [x] Active Azure hosting marked stopped.
- [x] Inline ChatGPT image rendering not presented as guaranteed.
- [x] Interactive frontend clearly marked out of scope.

## Repository

- [ ] README reviewed.
- [x] BENCHMARKS.md reviewed.
- [x] SECURITY.md added.
- [x] CONTRIBUTING.md added.
- [ ] License confirmed. No root repository license file is currently present.
- [ ] Examples work from clean clone.
- [x] Tests pass.

## GitHub About Recommendation

Recommended About text:

```text
AI-first diagram engine with a compact DSL for generating, validating, and rendering sequence diagrams, flows, and BPMN.
```

Suggested topics:

```text
diagram-as-code
sequence-diagram
dotnet
dsl
ai
llm
svg
png
cli
api
```

## GitHub After Publication

- [ ] Enable branch protection or a ruleset for `main`.
- [ ] Enable Dependabot and security alerts.
- [ ] Enable secret scanning and push protection if available.
- [ ] Enable private vulnerability reporting if desired.

## External Azure Cleanup

Repository changes do not delete Azure or GitHub account resources. After merge, manually:

1. Stop or delete the Azure Container App.
2. Remove the Azure federated credential used by GitHub Actions.
3. Remove the Azure service principal or app registration if no longer used.
4. Remove Azure-related GitHub secrets and variables.
5. Review GHCR images and retain or delete them based on whether container self-hosting is still supported.

Do not remove NuGet Trusted Publishing configuration if CLI package publishing remains active.
