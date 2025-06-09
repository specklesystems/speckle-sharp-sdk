// using System; // Removed based on IDE0005
#pragma warning disable IDE0005 // Suppress unnecessary using directives for this file if they cause build issues
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models; // For Base
using Speckle.Sdk.Serialisation; // Added for Id, Json
using Speckle.Sdk.Serialisation.V2; // For ISerializeProcessFactory, SerializeProcess, etc.
using Speckle.Sdk.Serialisation.V2.Send; // For ObjectSaver, BaseSerializer etc.
// using Speckle.Sdk.Serialisation.V2.Receive; // Removed based on IDE0005 - For IDeserializeProcessFactory
using Speckle.Sdk.Serialization.Tests.Framework; // For ExceptionSendCacheManager, ExceptionServerObjectManager, MemoryServerObjectManager
using Speckle.Sdk.Credentials; // For Account
using Xunit;
using Microsoft.Extensions.Logging;
using Speckle.Sdk.Logging; // Re-added for ISdkActivityFactory, ISdkMetricsFactory
using Speckle.Sdk.Transports; // For ProgressArgs
using System.Collections.Concurrent; // Re-added: For ConcurrentDictionary
using Speckle.Sdk.SQLite; // Added for ISqLiteJsonCacheManager

// It's assumed that Fixtures and TestServiceSetup are in a namespace accessible either directly
// or via a using statement like 'using Speckle.Sdk.Tests.Integration;'
// This was inferred from previous analysis of Fixtures.cs and TestServiceSetup.cs
// using Speckle.Sdk.Tests.Integration; // Removed based on IDE0005
#pragma warning restore IDE0005 // Restore warning checks for other parts of the file if any


namespace Speckle.Sdk.Tests.Integration.Api;

// Helper class: Custom SerializeProcessFactory for injecting faulty managers
public class TestSerializeProcessFactory : ISerializeProcessFactory
{
    private readonly IBaseChildFinder _baseChildFinder;
    private readonly IObjectSerializerFactory _objectSerializerFactory;
    private readonly Func<ISqLiteJsonCacheManager> _cacheManagerFactory;
    private readonly Func<IServerObjectManager> _serverManagerFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SerializeProcessOptions? _optionsOverride;

    public TestSerializeProcessFactory(
        IBaseChildFinder baseChildFinder,
        IObjectSerializerFactory objectSerializerFactory,
        Func<ISqLiteJsonCacheManager> cacheManagerFactory,
        Func<IServerObjectManager> serverManagerFactory,
        ILoggerFactory loggerFactory,
        SerializeProcessOptions? optionsOverride = null)
    {
        _baseChildFinder = baseChildFinder;
        _objectSerializerFactory = objectSerializerFactory;
        _cacheManagerFactory = cacheManagerFactory;
        _serverManagerFactory = serverManagerFactory;
        _loggerFactory = loggerFactory;
        _optionsOverride = optionsOverride;
    }

    public ISerializeProcess CreateSerializeProcess(
        Uri url, string streamId, string? authorizationToken,
        IProgress<ProgressArgs>? progress, CancellationToken cancellationToken,
        SerializeProcessOptions? options = null)
    {
        var cacheManager = _cacheManagerFactory();
        // Use a real server object manager for this specific overload,
        // as Operations.Send2 provides the necessary details (url, token)
        // unless the intent is to globally force an exception server manager via _serverManagerFactory.
        // For now, let's assume _serverManagerFactory is for specific test cases
        // and Operations.Send2 might use a real one if not overridden.
        // However, to ensure specific behavior for tests, we should allow _serverManagerFactory to dictate.
        var serverManager = _serverManagerFactory();


        return new SerializeProcess(
            progress,
            new ObjectSaver(progress, cacheManager, serverManager, _loggerFactory.CreateLogger<ObjectSaver>(), cancellationToken, _optionsOverride ?? options),
            _baseChildFinder,
            new BaseSerializer(cacheManager, _objectSerializerFactory), // BaseSerializer needs a cache manager
            _loggerFactory,
            cancellationToken,
            _optionsOverride ?? options);
    }

    public ISerializeProcess CreateSerializeProcess(
        ISqLiteJsonCacheManager sqLiteJsonCacheManager, IServerObjectManager serverObjectManager,
        IProgress<ProgressArgs>? progress, CancellationToken cancellationToken,
        SerializeProcessOptions? options = null)
    {
        return new SerializeProcess(
            progress,
            new ObjectSaver(progress, sqLiteJsonCacheManager, serverObjectManager, _loggerFactory.CreateLogger<ObjectSaver>(), cancellationToken, _optionsOverride ?? options),
            _baseChildFinder,
            new BaseSerializer(sqLiteJsonCacheManager, _objectSerializerFactory),
            _loggerFactory,
            cancellationToken,
            _optionsOverride ?? options);
    }

    public ISerializeProcess CreateSerializeProcess(
        ConcurrentDictionary<Id, Json> jsonCache, // Id and Json should now resolve from Speckle.Sdk.Serialisation
        ConcurrentDictionary<string, string> objects,
        IProgress<ProgressArgs>? progress, CancellationToken cancellationToken, SerializeProcessOptions? options = null)
    {
        var memoryCacheManager = new MemoryJsonCacheManager(jsonCache);
        var memoryServerManager = new MemoryServerObjectManager(objects);
        return CreateSerializeProcess(memoryCacheManager, memoryServerManager, progress, cancellationToken, _optionsOverride ?? options);
    }
}

public class OperationsSendIntegrationTests : IAsyncLifetime
{
    private Operations _operationsInstanceForCacheTest;
    private Operations _operationsInstanceForServerTest;
    private Account _account;
    private IServiceProvider _serviceProvider;

    public async Task InitializeAsync()
    {
        _account = await Fixtures.SeedUser(); // Fixtures.SeedUser() is usually enough if client isn't directly needed for setup
        _serviceProvider = TestServiceSetup.GetServiceProvider();

        var baseChildFinder = _serviceProvider.GetRequiredService<IBaseChildFinder>();
        var objectSerializerFactory = _serviceProvider.GetRequiredService<IObjectSerializerFactory>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();

        // Setup for Cache Test
        var cacheTestSerializeProcessFactory = new TestSerializeProcessFactory(
            baseChildFinder,
            objectSerializerFactory,
            () => new ExceptionSendCacheManager(), // Force cache error
            () => new MemoryServerObjectManager(new ConcurrentDictionary<string, string>()), // Benign server part
            loggerFactory
        );
        _operationsInstanceForCacheTest = new Operations(
            _serviceProvider.GetRequiredService<ILogger<Operations>>(),
            _serviceProvider.GetRequiredService<ISdkActivityFactory>(),
            _serviceProvider.GetRequiredService<ISdkMetricsFactory>(),
            cacheTestSerializeProcessFactory,
            _serviceProvider.GetRequiredService<IDeserializeProcessFactory>()
        );

        // Setup for Server Test
        var serverTestSerializeProcessFactory = new TestSerializeProcessFactory(
            baseChildFinder,
            objectSerializerFactory,
            () => new MemoryJsonCacheManager(new ConcurrentDictionary<Id, Json>()), // Id and Json should now resolve from Speckle.Sdk.Serialisation
            () => new ExceptionServerObjectManager(), // Force server error
            loggerFactory
        );
        _operationsInstanceForServerTest = new Operations(
            _serviceProvider.GetRequiredService<ILogger<Operations>>(),
            _serviceProvider.GetRequiredService<ISdkActivityFactory>(),
            _serviceProvider.GetRequiredService<ISdkMetricsFactory>(),
            serverTestSerializeProcessFactory,
            _serviceProvider.GetRequiredService<IDeserializeProcessFactory>()
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Send2_ThrowsSpeckleException_WhenCacheWriteFails()
    {
        // Arrange
        var baseObject = new Base { applicationId = "test-object-cache-fail" };
        var streamId = Guid.NewGuid().ToString(); // Unique streamId for the test

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await _operationsInstanceForCacheTest.Send2(
            new Uri(_account.serverInfo.url), // Corrected casing
            streamId,
            _account.token, // Corrected casing
            baseObject,
            null,
            CancellationToken.None
        ));

        Assert.NotNull(ex.InnerException);
        // ExceptionSendCacheManager by default throws NotImplementedException from most methods
        Assert.IsType<NotImplementedException>(ex.InnerException);
    }

    [Fact]
    public async Task Send2_ThrowsSpeckleException_WhenServerUploadFails()
    {
        // Arrange
        var baseObject = new Base { applicationId = "test-object-server-fail" };
        var streamId = Guid.NewGuid().ToString(); // Unique streamId for the test

        // Act & Assert
        var ex = await Assert.ThrowsAsync<SpeckleException>(async () => await _operationsInstanceForServerTest.Send2(
            new Uri(_account.serverInfo.url), // Corrected casing
            streamId,
            _account.token, // Corrected casing
            baseObject,
            null,
            CancellationToken.None
        ));

        Assert.NotNull(ex.InnerException);
        // ExceptionServerObjectManager throws NotImplementedException for UploadObjects
        Assert.IsType<NotImplementedException>(ex.InnerException);
    }
}
