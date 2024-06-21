# Core

[![Twitter Follow](https://img.shields.io/twitter/follow/SpeckleSystems?style=social)](https://twitter.com/SpeckleSystems) [![Community forum users](https://img.shields.io/discourse/users?server=https%3A%2F%2Fdiscourse.speckle.works&style=flat-square&logo=discourse&logoColor=white)](https://discourse.speckle.works) [![website](https://img.shields.io/badge/https://-speckle.systems-royalblue?style=flat-square)](https://speckle.systems) [![docs](https://img.shields.io/badge/docs-speckle.guide-orange?style=flat-square&logo=read-the-docs&logoColor=white)](https://speckle.guide/dev/)

### **Disclaimer**

This is an early alpha release, not meant for use in production! We're working to stabilise the 2.0 API, and until then there will be breaking changes. You have been warned!

## Introduction

### Core

Core is the .NET SDK for Speckle 2.0. It uses .NET Standard 2.0 and has been tested on Windows and MacOS.

## Documentation

Comprehensive developer and user documentation can be found in our:

#### 📚 [Speckle Docs website](https://speckle.guide/dev/)

## Developing & Debugging

### Building

Make sure you clone this repository together with its submodules: `git clone https://github.com/specklesystems/Core.git -recursive`.
Afterwards, just restore all the NuGet packages and hit Build!

### Developing

This project is evolving fast, to better understand how to use Core we suggest checking out the Unit and Integration tests. Running the integration tests locally requires a local server running on your computer.

We'll be also adding [preliminary documentation on our forum](https://discourse.speckle.works/c/speckle-insider/10).

### Tests

There are two test projects, one for unit tests and one for integration tests. The latter needs a server running locally in order to run.

## Contributing

Before embarking on submitting a patch, please make sure you read:

- [Contribution Guidelines](CONTRIBUTING.md),
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Community

The Speckle Community hangs out on [the forum](https://discourse.speckle.works), do join and introduce yourself & feel free to ask us questions!

## License

Unless otherwise described, the code in this repository is licensed under the Apache-2.0 license.

# Objects

[![Twitter Follow](https://img.shields.io/twitter/follow/SpeckleSystems?style=social)](https://twitter.com/SpeckleSystems) [![Community forum users](https://img.shields.io/discourse/users?server=https%3A%2F%2Fdiscourse.speckle.works&style=flat-square&logo=discourse&logoColor=white)](https://discourse.speckle.works) [![website](https://img.shields.io/badge/https://-speckle.systems-royalblue?style=flat-square)](https://speckle.systems) [![docs](https://img.shields.io/badge/docs-speckle.guide-orange?style=flat-square&logo=read-the-docs&logoColor=white)](https://speckle.guide/dev/)

**Status**

![.NET Core](https://github.com/specklesystems/Objects/workflows/.NET%20Core/badge.svg)


## Introduction

Before venturing any further please make sure to check the following:

- [Code of Conduct](CODE_OF_CONDUCT.md),
- [Contribution Guidelines](CONTRIBUTING.md),
- [License](LICENSE)

### Objects

The Speckle 2.0 object model: geometry and element base classes. It uses .NET Standard 2.0 and has been tested on Windows and MacOS.

**NOTE:** this is the default object model we ship with Speckle. You can develop your own or fork this and extend it too!

More info on Objects and Kits 2.0 can be found in [this community forum thread](https://discourse.speckle.works/t/introducing-kits-2-0/710/34).

## Documentation

Comprehensive developer and user documentation can be found in our:

#### 📚 [Speckle Docs website](https://speckle.guide/dev/)

## Developing & Debugging

### Building

Just restore all the NuGet packages and hit Build!

### Developing

Objects is just a set of Data Transfer Objects, it's quite straightforward to understand how they work!

#### Host application support

In order to better support interop between the various AEC host applications and Speckle, Objects also contains classes that help to deal with native object types and their properties.

For example, you'll see a `\Revit` folder. That contains a series of classes that extend the basic ones with a series of default Revit properties. This is the approach we'll follow with other host applications as well.

## Contributing

Please make sure you read the [contribution guidelines](CONTRIBUTING.md) for an overview of the best practices we try to follow.

## Community

The Speckle Community hangs out on [the forum](https://discourse.speckle.works), do join and introduce yourself & feel free to ask us questions!

## License

Unless otherwise described, the code in this repository is licensed under the Apache-2.0 license.
