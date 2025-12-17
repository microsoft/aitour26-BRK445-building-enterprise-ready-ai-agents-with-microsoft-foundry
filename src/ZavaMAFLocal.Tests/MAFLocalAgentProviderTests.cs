using Moq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ZavaAgentsMetadata;
using Microsoft.Agents.AI;

namespace ZavaMAFLocal.Tests;

[TestClass]
public class MAFLocalAgentProviderTests
{
    [TestMethod]
    public void Constructor_WithNullServiceProvider_ShouldThrowException()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();

        // Act & Assert
        try
        {
            var provider = new MAFLocalAgentProvider(null!, mockChatClient.Object);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException ex)
        {
            Assert.AreEqual("serviceProvider", ex.ParamName);
        }
    }

    [TestMethod]
    public void Constructor_WithNullChatClient_ShouldThrowException()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Act & Assert
        try
        {
            var provider = new MAFLocalAgentProvider(mockServiceProvider.Object, null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException ex)
        {
            Assert.AreEqual("chatClient", ex.ParamName);
        }
    }

    [TestMethod]
    public void Constructor_WithValidParameters_ShouldCreateProvider()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockChatClient = new Mock<IChatClient>();

        // Act
        var provider = new MAFLocalAgentProvider(mockServiceProvider.Object, mockChatClient.Object);

        // Assert - Provider should be created successfully
        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void GetAgentByName_WithInvalidName_ShouldThrowException()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockChatClient = new Mock<IChatClient>();
        var provider = new MAFLocalAgentProvider(mockServiceProvider.Object, mockChatClient.Object);
        var invalidAgentName = "NonExistentAgent";

        // Mock GetRequiredKeyedService to throw InvalidOperationException (no service registered)
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(AIAgent)))
            .Returns(null);

        // Act & Assert
        try
        {
            provider.GetAgentByName(invalidAgentName);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected exception when service is not registered
        }
    }

    [TestMethod]
    public void GetAgentByName_WithValidName_ShouldReturnAgent()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockChatClient = new Mock<IChatClient>();
        var mockAgent = Mock.Of<AIAgent>();
        var provider = new MAFLocalAgentProvider(mockServiceProvider.Object, mockChatClient.Object);
        var validAgentName = AgentMetadata.GetAgentName(AgentType.ToolReasoningAgent);

        // Setup the service provider to return a mock agent
        var services = new ServiceCollection();
        services.AddKeyedSingleton<AIAgent>(validAgentName, mockAgent);
        var serviceProvider = services.BuildServiceProvider();
        
        var providerWithRealSP = new MAFLocalAgentProvider(serviceProvider, mockChatClient.Object);

        // Act
        var agent = providerWithRealSP.GetAgentByName(validAgentName);

        // Assert
        Assert.IsNotNull(agent);
        Assert.AreSame(mockAgent, agent);
    }

    [TestMethod]
    public void GetAgentByName_AllAgentTypes_ShouldRetrieveFromServiceProvider()
    {
        // Arrange
        var mockChatClient = new Mock<IChatClient>();
        var services = new ServiceCollection();
        
        // Register all agents in the service collection
        foreach (var agentType in AgentMetadata.AllAgents)
        {
            var agentName = AgentMetadata.GetAgentName(agentType);
            var mockAgent = Mock.Of<AIAgent>();
            services.AddKeyedSingleton<AIAgent>(agentName, mockAgent);
        }
        
        var serviceProvider = services.BuildServiceProvider();
        var provider = new MAFLocalAgentProvider(serviceProvider, mockChatClient.Object);

        // Act & Assert
        foreach (var agentType in AgentMetadata.AllAgents)
        {
            var agentName = AgentMetadata.GetAgentName(agentType);
            
            try
            {
                var agent = provider.GetAgentByName(agentName);
                Assert.IsNotNull(agent, $"Agent type {agentType} with name '{agentName}' should return a valid agent");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Agent type {agentType} with name '{agentName}' should be retrievable. Exception: {ex.Message}");
            }
        }
    }
}
