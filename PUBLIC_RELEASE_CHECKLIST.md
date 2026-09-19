# Public Release Checklist

This checklist separates repository-verifiable items from external account or cloud settings that cannot be proven from a clean clone.

## Security

- [x] Current repository files contain no intentional secrets.
- [x] SVG injection protections are covered by regression tests.
- [x] API key authentication is covered by regression tests.
- [x] API request/source/diagram/PNG limits are documented and covered by regression tests.
- [x] Production-style API error responses are tested for secret/detail redaction.
- [ ] Production credentials rotated if any were ever exposed. This requires external account evidence.
- [ ] GitHub private vulnerability reporting status verified. This requires repository settings access.

## Azure Shutdown

- [x] Azure deployment workflow is not present in the repository.
- [x] Azure production endpoint is not documented as active.
- [x] Historical Azure deployment note is retained.
- [x] README states that Azure hosting was stopped and automatic deployment is no longer active.
- [ ] GitHub Azure secrets and variables removed. This requires GitHub settings access.
- [ ] Azure federated credential removed. This requires Azure access.
- [ ] Azure Container App stopped or deleted. This requires Azure access.
- [ ] GHCR package retention policy confirmed. This requires registry/account access.

## Retired Features

- [x] README contains removed and retired features.
- [x] MCP experiment is marked removed, not active.
- [x] ChatGPT App / Apps SDK experiment is marked removed, not active.
- [x] Active Azure hosting is marked stopped.
- [x] Inline ChatGPT image rendering is not presented as guaranteed.
- [x] Interactive frontend is clearly out of scope.
- [x] Enzo is not presented as a complete Mermaid replacement.

## Repository

- [x] README reviewed and rewritten for the final architecture.
- [x] ARCHITECTURE.md reviewed and rewritten for the final architecture.
- [x] BENCHMARKS.md reviewed and preserved as historical benchmark documentation.
- [x] SECURITY.md reviewed and updated.
- [x] CONTRIBUTING.md reviewed and updated.
- [x] Documentation examples are covered by parser regression tests.
- [x] Clean Architecture dependency direction is covered by tests.
- [x] CLI package metadata keeps `PackageId = Enzo.Diagrams.Cli`.
- [x] License confirmed. Root repository license file is MIT.

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

These items require GitHub repository settings access and are not proven by this branch.

## External Azure Cleanup

Repository changes do not delete cloud resources. Manually verify or complete:

1. Stop or delete the Azure Container App.
2. Remove the Azure federated credential used by GitHub Actions.
3. Remove the Azure service principal or app registration if no longer used.
4. Remove Azure-related GitHub secrets and variables.
5. Review GHCR images and retain or delete them based on whether container self-hosting remains useful.

Do not remove NuGet Trusted Publishing configuration if CLI package publishing remains active.
