Submodule of https://github.com/uwx/CasCore, branch `nfmw` - a fork of
https://github.com/DouglasDwyer/CasCore with the fixes NFM World needs.

The upstream package is stuck at 0.3.0, and a plugin sandbox cannot work on .NET 11
without these (commit messages in the fork carry the reasoning):

- `Allow base method calls through the sandbox` - every `base.Foo()` in a plugin was
  routed to the static guard, which refuses overridable methods, so it threw.
- `Fix System.Text.Json bindings against .NET 11` - `WithDefaultSandbox` threw when built.
- `allow 2D arrays` / `ReadOnlySpan op_Implicit` - array intrinsic members share one
  metadata token, so they can only be admitted per array type; `T[,]` bindings and the
  `T[]` to `ReadOnlySpan<T>` conversion were refused.
- `never resolve a host assembly while instrumenting` - `PatchMethodCall` resolved a
  `call` target's declaring type, which fails in a `PublishSingleFile` build or a browser
  host, where no host assembly is on disk. `call` never dispatches, so the lookup was
  unnecessary.

To update: rebase `nfmw` on upstream, re-apply unrelated upstream work, and bump the
submodule pointer. `NFMWorld.Library` references `CasCore/CasCore/CasCore.csproj`.
