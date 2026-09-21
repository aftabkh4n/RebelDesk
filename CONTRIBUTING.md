# Contributing to RebelDesk

Thanks for taking the time to help. RebelDesk is a self-hosted remote-desktop tool. Because it can control other people's computers, we hold contributions to a strict standard.

## Ground rules

- Small, reviewable pull requests. If a change touches more than ~400 lines across more than 5 files, open an issue first to align on scope.
- New behaviour requires either a test or a documented manual verification step. Do not report a mocked feature as working.
- Anything that touches authentication, session lifecycle, input injection, capture, or the WebRTC negotiation must include a `docs/SECURITY_MODEL.md` update in the same PR.
- Localised strings only in `client/src/i18n/locales/*.json`. Do not hard-code English into components.
- Match existing formatting: `dotnet format` for C#, `eslint --fix` + Prettier for TypeScript.

## Development setup

See the `Quick start` section in the top-level `README.md`.

## Reporting issues

Use the templates in `.github/ISSUE_TEMPLATE`. For security issues, follow `SECURITY.md` instead — do not open a public issue.

## Pull request checklist

- [ ] Description explains the "why", not only the "what".
- [ ] `docs/` updated if user-facing behaviour changed.
- [ ] `STATUS.md` updated if the change moves a feature between maturity categories.
- [ ] `NOTICE` updated if you added a new third-party dependency.
- [ ] For UI changes: screenshot or short screen recording.
- [ ] For agent changes: manual verification steps for the host operator.

## Licensing of contributions

By opening a pull request you agree that your contribution is licensed under the same AGPL-3.0 as the project. Do not paste code you do not have the right to license.

## Code of Conduct

See `CODE_OF_CONDUCT.md`. In short: be respectful, focus on the problem, no personal attacks.
