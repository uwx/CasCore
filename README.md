Submodule of https://github.com/uwx/CasCore, branch `nfmw` - a fork of
https://github.com/DouglasDwyer/CasCore with the fixes NFM World needs.

The upstream package is stuck at 0.3.0, and a plugin sandbox cannot work on .NET 11
without these (commit messages in the fork carry the reasoning):

- `Allow base method calls through the sandbox` - every `base.Foo()` in a plugin was
  routed to the static guard, which refuses overridable methods, so it threw.
- `Fix System.Text.Json bindings against .NET 11` - `WithDefaultSandbox` threw when built.

To update: rebase `nfmw` on upstream, re-apply unrelated upstream work, and bump the
submodule pointer. `NFMWorld.Library` references `CasCore/CasCore/CasCore.csproj`.
