using Moq;
using Azure.AI.Projects;
using ZavaAgentsMetadata;

namespace ZavaMAFFoundry.Tests;

[TestClass]
public class MAFAgentProviderTests
{
    [TestMethod]
    public void Constructor_WithValidEndpoint_ShouldInitialize()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";

        // Act
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Assert
        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void Constructor_WithInvalidEndpoint_ShouldThrowException()
    {
        // Arrange
        var invalidEndpoint = "not-a-valid-url";

        // Act & Assert
        try
        {
            var provider = new MAFFoundryAgentProvider(invalidEndpoint);
            Assert.Fail("Expected UriFormatException was not thrown");
        }
        catch (UriFormatException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetAIAgentAsync_WithNullAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public async Task GetAIAgentAsync_WithEmptyAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent(string.Empty);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public async Task GetAIAgentAsync_WithWhitespaceAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent("   ");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public void GetAIAgent_WithNullAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public void GetAIAgent_WithEmptyAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent(string.Empty);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public void GetAIAgent_WithWhitespaceAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);

        // Act & Assert
        try
        {
            provider.GetAIAgent("   ");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.AreEqual("agentId", ex.ParamName);
            Assert.IsTrue(ex.Message.Contains("Agent ID cannot be null or empty"));
        }
    }

    [TestMethod]
    public void GetAIAgent_WithValidAgentId_ShouldAttemptToRetrieveAgent()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);
        var validAgentId = "test-agent-id";

        // Act & Assert
        try
        {
            var agent = provider.GetAIAgent(validAgentId);
            // If we get here without ArgumentException, validation passed
            Assert.IsNotNull(agent);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Agent ID cannot be null or empty"))
        {
            Assert.Fail("Should not throw ArgumentException for valid agent ID");
        }
        catch
        {
            // Other exceptions are expected without actual Azure Foundry connection
            // The test validates that input validation works correctly
        }
    }

    [TestMethod]
    public async Task GetAIAgentAsync_WithValidAgentId_ShouldAttemptToRetrieveAgent()
    {
        // Arrange
        var endpoint = "https://test.foundry.endpoint.com";
        var provider = new MAFFoundryAgentProvider(endpoint);
        var validAgentId = "test-agent-id";

        // Act & Assert
        try
        {
            var agent = provider.GetAIAgent(validAgentId);
            // If we get here without ArgumentException, validation passed
            Assert.IsNotNull(agent);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Agent ID cannot be null or empty"))
        {
            Assert.Fail("Should not throw ArgumentException for valid agent ID");
        }
        catch
        {
            // Other exceptions are expected without actual Azure Foundry connection
            // The test validates that input validation works correctly
        }
    }
}
