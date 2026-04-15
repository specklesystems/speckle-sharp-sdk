![Speckle Box](/logo.png)  
Speckle | Sharp | SDK
=================================================================================================================================

[![Twitter Follow](https://img.shields.io/twitter/follow/SpeckleSystems?style=social)](https://twitter.com/SpeckleSystems) [![Community forum users](https://img.shields.io/discourse/users?server=https%3A%2F%2Fspeckle.community&style=flat-square&logo=discourse&logoColor=white)](https://speckle.community) [![website](https://img.shields.io/badge/https://-speckle.systems-royalblue?style=flat-square)](https://speckle.systems) [![docs](https://img.shields.io/badge/docs-speckle.guide-orange?style=flat-square&logo=read-the-docs&logoColor=white)](https://speckle.guide/dev/)

 > Speckle is the first AEC data hub that connects with your favorite AEC tools. Speckle exists to overcome the challenges of working in a fragmented industry where communication, creative workflows, and the exchange of data are often hindered by siloed software and processes. It is here to make the industry better.

### .NET SDK, Tests, and Objects

[![codecov](https://codecov.io/gh/specklesystems/speckle-sharp-sdk/branch/dev/graph/badge.svg?token=TTM5OGr38m)](https://codecov.io/gh/specklesystems/speckle-sharp-sdk)
<a href="https://www.nuget.org/packages/Speckle.Sdk/"><img alt="NuGet Version" src="https://img.shields.io/nuget/v/Speckle.Sdk?label=Speckle.Sdk"></a>
<a href="https://www.nuget.org/packages/Speckle.Objects/"><img alt="NuGet Version" src="https://img.shields.io/nuget/v/Speckle.Sdk?label=Speckle.Objects"></a>
<a href="https://www.nuget.org/packages/Speckle.Automate.Sdk/"><img alt="NuGet Version" src="https://img.shields.io/nuget/v/Speckle.Sdk?label=Speckle.Automate.Sdk"></a>

> [!WARNING]
> Releases Speckle.Sdk and Speckle.Objects are reliable for production use, but the APIs may not be wholly stable, and there may be breaking changes between releases, with little documentation.

# Repo structure

This repo is the home of our next-generation Speckle .NET SDK.

- **SDK**
  - [`Speckle.Sdk`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Sdk): Send/Receive operations, Serialization, API wrappers, and more!.
  - [`Speckle.Sdk.Dependencies`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Sdk.Dependencies): Dependencies and code that shouldn't cause conflicts in Host Apps.  This uses [IL Repack](https://github.com/gluck/il-repack) to merge together and interalized only to be used by Speckle.
  - [`Speckle.Automate.Sdk`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Automate.Sdk): .NET SDK for [Speckle Automate](https://www.speckle.systems/product/automate)
- **Speckle Objects**
  - [`Speckle.Objects`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Objects): The Speckle Objects classes used for conversions.
- **Tests**
  - [`Tests`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/tests): Unit, serialization, integration, and performance tests.

### Other repos

Make sure to also check and ⭐️ these other  repositories:

- [`speckle-sharp-connectors`](https://github.com/specklesystems/speckle-sharp-connectors): our csharp repo of next gen connectors.
- [`speckle-server`](https://github.com/specklesystems/speckle-server): the speckle server.
- [`speckle-sketchup`](https://github.com/specklesystems/speckle-blender): Blender connector.
- [`speckle-sketchup`](https://github.com/specklesystems/speckle-sketchup): Sketchup connector.
- [`speckle-powerbi`](https://github.com/specklesystems/speckle-powerbi): PowerBi connector.
- and more [connectors & tooling](https://github.com/specklesystems/)!

## Documentation

Developer docs are a bit patchy. See our [📚 Speckle Docs website](https://docs.speckle.systems/developers/introduction)

# Developing and Debugging


To build solutions in this repo, [10.0.2xx of the .NET SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is required.

It is recommended to use JetBrains Rider (version 2025.3 or greater) or Microsoft Visual Studio 2026 (version 18.4 or greater)

From there you can open the main `Speckle.Connectors.slnx` solution and build the project.

For good development experience and environment setup, you the commands are available needed.


### Formatting
We're using [CSharpier](https://github.com/belav/csharpier) to format our code. You can use Csharpier in a few ways:
- Install CSharpier and reformat from CLI
  ```
  dotnet tool restore
  dotnet csharpier format ./
  ```
- Install the CSharpier extension for [Rider](https://plugins.jetbrains.com/plugin/18243-csharpier) or [Visual Studio](https://marketplace.visualstudio.com/items?itemName=csharpier.CSharpier)<br/>
  For best DX, we recommend turning on CSharpier's `reformat on save` setting if you've installed it in your IDE.


### Tests

There are several test projects. It is a requirement that all tests pass for PRs to be merged.

The Integration test projects require a local server to be running.
You must have docker installed. Then you can run `docker compose up` from the root of the repo to start the required containers.

In CI, they will be run against both the public and private versions of the server.
It is important that we remain compatible with both server versions.

## Contributing

Before embarking on submitting a patch, please make sure you read:

- [Contribution Guidelines](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

# Security and Licensing
      
### Security

For any security vulnerabilities or concerns, please contact us directly at security[at]speckle.systems.

### License

Unless otherwise described, the code in this repository is licensed under the Apache-2.0 License. Please note that some modules, extensions or code herein might be otherwise licensed. This is indicated either in the root of the containing folder under a different license file, or in the respective file's header. If you have any questions, don't hesitate to get in touch with us via [email](mailto:hello@speckle.systems).

