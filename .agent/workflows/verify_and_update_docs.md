---
description: Get all distinct files touched in branch, verify XML comments and Markdown docs, intended for pre-push checks
---

1. ACQUIRE CONTEXT:
   - Identify distinct files changed in `origin/main...HEAD`.
   - Command: `git diff --name-only origin/main...HEAD`

2. VERIFY DOCUMENTATION:
   - Iterate through the changed files, filtering for:
     - **C# Files (`.cs`)**: Check for **XML Documentation Comments** (`///`).
       - Ensure all public types (classes, interfaces, structs, records, enums) and members have summary tags.
       - Verify parameters (`<param>`) and return values (`<returns>`) match the signature.
     - **Markdown Files (`.md`)**:
       - Check for outdated information relative to the code changes.
       - Ensure formatting is consistent.
   - If documentation is missing, outdated, or incorrect, **UPDATE IT**.

3. VERIFY INTEGRITY (PRE-PUSH):
   - Run the test suite to ensure the new documentation or edits didn't break the build (e.g. malformed XML):
     `dotnet test`
   - If this is a `RazorDocs` related module, verify the harvesting logic produces clean `DocNode` results.
