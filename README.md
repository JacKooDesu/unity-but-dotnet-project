# unity-but-dotnet-project

## Why

When you're working with Unity project codebase, but not really want to download the Editor.

This github action will generate csproj and necessary DLLs for you.

## Usage

1. Setup secrets
2. Run action, dispatch the workflow with target `repository` and `branch` parameters
3. Download and extract the artifact the workflow generates
4. Git clone the repository in the extracted directory with name `Project`

## Secret

### Game CI Required

- UNITY_LICENSE
- UNITY_EMAIL
- UNITY_PASSWORD

### Checkout Required

- GH_PAT_TOKEN

> note: `Content` / `Metadata` permission required

## Features

- [x] `csproj` file generation tool
- [x] Resolve engine dlls
- [x] Resolve package dlls
- [ ] Resolve NuGet packages
- [ ] `.meta` file generation tool

## Thanks

- [GameCI](https://game.ci/), provide github action which allow access unity editor easily
- [com.unity.ide.visualstudio](https://github.com/needle-mirror/com.unity.ide.visualstudio) package
