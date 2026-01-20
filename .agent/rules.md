# Project Rules & Preferences


## General Interaction


- **Review before Commitment**: **CRITICAL** Always request user review and approval of changes (via `notify_user` or explicitly showing the diff) before running `git commit`.
- **Linting**: Always run linters (e.g., `dotnet format` or IDE-integrated linters) after making changes to ensure code quality **(on touched files only)**.


## Coding Standards


- **Concurrency**: Prefer using `System.Threading.Channels` and `SemaphoreSlim` for parallel processing to ensure ordered streaming and backpressure.
- **Security**: Always perform robust path traversal checks (normalization + equality + StartsWith) for any file IO derived from user input.
- **CORS**: Enforce explicit origin specification in production environments.

### Reliability & Quality

- **Error Handling**: Maintain consistent try/catch patterns. Generally propagate exceptions unless a specific fallback (or cleanup) strategy exists; avoid silently swallowing errors.
- **Logging**: Use structured logging. Map exceptions appropriately: `Error` for failures requiring attention, `Warning` for handled transient issues, and `Info` for significant state changes.
- **Test Coverage**:
  - **Unit Tests**: Ensure all public methods have corresponding unit tests.
  - **Security**: Unit tests must verify path traversal checks (e.g., in `ExportEngine`).
  - **Concurrency**: Integration tests must verify concurrency limits and cleanups (e.g., in `ParallelSelectAsyncEnumerable`).
