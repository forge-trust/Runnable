---
description: Comprehensive PR Feedback Review Protocol
---

# PR Feedback Review Protocol

Follow this protocol to ensure all PR feedback is addressed comprehensively, including comments, annotations, and hidden pagination items.

## 1. Fetch Context
If you don't have the full PR context, fetch it.
- **Get Review Comments**: Use `mcp_remote-github_pull_request_read` with `method: "get_review_comments"`.
  - **CRITICAL**: Check `pageInfo.hasNextPage`. If `true`, you MUST fetch subsequent pages.
- **Get Issue Comments**: Use `mcp_remote-github_pull_request_read` with `method: "get_comments"` (for general discussion comments).

## 2. Compile Feedback
Create a structured list (or update `task.md`) with all distinct feedback items.
- **Categorize**:
  - 🔴 **Security/Critical**: Must fix immediately.
  - 🟡 **Functional**: Bugs or incorrect logic.
  - 🔵 **Style/Refactor**: Code quality improvements (clean up, naming, patterns).
  - ⚪ **Debatable**: Items that require discussion or might be deferred.

## 3. Review Annotations and Diff
- If the user points to specific commits or annotations, use `mcp_remote-github_get_commit` or diff tools to find inline annotations that might not appear in the top-level comment threads.
- **Verify**: Check the code *as it exists now*. Often feedback is "addressed" but not resolved in GitHub. Verify the code state matching the feedback.

## 4. Execution Plan
- **Plan**: Create `implementation_plan.md` grouping fixes by file or component to avoid context switching.
- **Execute**: Apply fixes.
- **Verify**: Run tests (`dotnet test`) after applying fixes.

## 5. Closure
- **Walkthrough**: Create a `walkthrough.md` detailing what was fixed.
- **Notify**: Inform the user which items were fixed, which were deferred, and why.