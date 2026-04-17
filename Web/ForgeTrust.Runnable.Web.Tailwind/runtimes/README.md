# ForgeTrust.Runnable.Web.Tailwind Runtime Packages

Platform-specific runtime packages that carry the official standalone Tailwind CLI binaries used by `ForgeTrust.Runnable.Web.Tailwind`.

## Overview

These packages exist so the main Tailwind package can depend on RID-specific binaries without bundling every executable into one package. Each runtime package contains the native Tailwind CLI for a single supported platform.

## Supported Packages

- `ForgeTrust.Runnable.Web.Tailwind.Runtime.win-x64`
- `ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-x64`
- `ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64`
- `ForgeTrust.Runnable.Web.Tailwind.Runtime.linux-x64`
- `ForgeTrust.Runnable.Web.Tailwind.Runtime.linux-arm64`

## Usage

Most consumers should install only `ForgeTrust.Runnable.Web.Tailwind`. These runtime packages are implementation-detail dependencies that are restored automatically through the main package.

Install one directly only if you have a specialized packaging or build scenario that requires explicit control over the Tailwind CLI binary payload.
