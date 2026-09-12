# Security Policy

## Supported Versions

Security fixes are handled for the latest code on `main` and the latest published CLI package version when one exists.

## Reporting A Vulnerability

Please do not report vulnerabilities publicly in issues or discussions.

Use GitHub private vulnerability reporting if it is enabled for this repository. If it is not enabled, contact the maintainer through a private channel already associated with the project or open a public issue requesting a private reporting path without including vulnerability details.

## Security-Sensitive Areas

Reports are especially useful for:

- DSL parser or validator denial-of-service cases.
- SVG/XML injection through user-controlled labels or messages.
- PNG rendering CPU, memory, or image-size abuse.
- API authentication and authorization bypasses.
- Hosted render URL predictability, expiration, cleanup, or storage exposure.
- Error responses that expose stack traces, local paths, environment values, or secrets.

Do not include real API keys, cloud credentials, or other secrets in reports.
