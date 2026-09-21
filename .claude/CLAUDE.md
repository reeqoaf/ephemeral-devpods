# Project rules

1. Never add co-author trailers (e.g. `Co-Authored-By: Claude ...`) to git commits.
2. Never `git commit` or `git push` to `main` without explicit user approval first.
3. Use `pnpm` as the default package manager for React/Node projects (not `npm` or `yarn`).
4. Commit messages must be short, single-line, and meaningful — no body/detail paragraphs.
5. Avoid unnecessary `try`/`catch`. The backend has a global exception handler (`ExceptionHandlingMiddleware`) that maps exceptions to responses, so throw typed exceptions and let it handle them. Only catch when the code genuinely handles the failure (e.g. translating a storage/SDK error into a domain result, or cleaning up and continuing), never just to map an error to a status code.
