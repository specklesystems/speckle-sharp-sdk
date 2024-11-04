<h1 align="center">
  <img src="https://user-images.githubusercontent.com/2679513/131189167-18ea5fe1-c578-47f6-9785-3748178e4312.png" width="150px"/><br/>
  Speckle | Sharp | SDK
</h1>

<p align="center"><a href="https://twitter.com/SpeckleSystems"><img src="https://img.shields.io/twitter/follow/SpeckleSystems?style=social" alt="Twitter Follow"></a> <a href="https://speckle.community"><img src="https://img.shields.io/discourse/users?server=https%3A%2F%2Fspeckle.community&amp;style=flat-square&amp;logo=discourse&amp;logoColor=white" alt="Community forum users"></a> <a href="https://speckle.systems"><img src="https://img.shields.io/badge/https://-speckle.systems-royalblue?style=flat-square" alt="website"></a> <a href="https://speckle.guide/dev/"><img src="https://img.shields.io/badge/docs-speckle.guide-orange?style=flat-square&amp;logo=read-the-docs&amp;logoColor=white" alt="docs"></a></p>

> Speckle is the first AEC data hub that connects with your favorite AEC tools. Speckle exists to overcome the challenges of working in a fragmented industry where communication, creative workflows, and the exchange of data are often hindered by siloed software and processes. It is here to make the industry better.

<h3 align="center">
    .NET SDK, Tests, and Objects
</h3>

<p align="center"><a href="https://codecov.io/gh/specklesystems/speckle-sharp-sdk"><img src="https://codecov.io/gh/specklesystems/speckle-sharp-sdk/graph/badge.svg?token=TTM5OGr38m" alt="Codecov"></a></p>

<br/>

> [!WARNING]
> This is an early beta release, not meant for use in production! We're working to stabilise the 3.0 API, and until then there will be breaking changes. You have been warned!

# Repo structure

This repo is the home of our next-generation Speckle .NET SDK. It uses .NET Standard 2.0 and has been tested on Windows and MacOS.

- **SDK**
  - [`Speckle.Sdk`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Sdk): Transports, serialization, API wrappers, and logging.
- **Speckle Objects**
  - [`Speckle.Objects`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/src/Speckle.Objects): The Speckle Objects classes used for conversions.
- **Tests**
  - [`Tests`](https://github.com/specklesystems/speckle-sharp-sdk/tree/dev/tests): Unit, serialization, integration, and performance tests.

### Other repos

Make sure to also check and ⭐️ these other Speckle next generation repositories:

- [`speckle-sharp-connectors`](https://github.com/specklesystems/speckle-sharp-connectors): our csharp repo of next gen connectors
- [`speckle-sketchup`](https://github.com/specklesystems/speckle-sketchup): Sketchup connector
- [`speckle-powerbi`](https://github.com/specklesystems/speckle-powerbi): PowerBi connector
- and more [connectors & tooling](https://github.com/specklesystems/)!

## Documentation

Comprehensive developer and user documentation can be found in our:

### 📚 [Speckle Docs website](https://speckle.guide/dev/)

# Developing and Debugging

### Building

Make sure you clone this repository together with its submodules: `git clone https://github.com/specklesystems/speckle-sharp-sdk.git -recursive`.
Afterwards, just restore all the NuGet packages and hit Build!

### Developing

This project is evolving fast, to better understand how to use Core we suggest checking out the Unit and Integration tests. Running the integration tests locally requires a local server running on your computer.

We'll be also adding [preliminary documentation on our forum](https://discourse.speckle.works/c/speckle-insider/10).

### Tests

There are two test projects, one for unit tests and one for integration tests. The latter needs a server running locally in order to run.

## Contributing

Before embarking on submitting a patch, please make sure you read:

- [Contribution Guidelines](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

# Security and Licensing
      
### Security

For any security vulnerabilities or concerns, please contact us directly at security[at]speckle.systems.

### License

Unless otherwise described, the code in this repository is licensed under the Apache-2.0 License. Please note that some modules, extensions or code herein might be otherwise licensed. This is indicated either in the root of the containing folder under a different license file, or in the respective file's header. If you have any questions, don't hesitate to get in touch with us via [email](mailto:hello@speckle.systems).

