# Project Rules & Preferences

## General Interaction
- **Review before Commitment**: Always request user review and approval of changes (via `notify_user` or explicitly showing the diff) before running `git commit`.
- **Linting**: Always run linters (e.g., `dotnet format` or IDE-integrated linters) after making changes to ensure code quality **(on touched files only)**.

## Coding Standards
- **Concurrency**: Prefer using `System.Threading.Channels` and `SemaphoreSlim` for parallel processing to ensure ordered streaming and backpressure.
- **Security**: Always perform robust path traversal checks (normalization + equality + StartsWith) for any file IO derived from user input.
- **CORS**: Enforce explicit origin specification in production environments.
