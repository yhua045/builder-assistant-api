using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BuilderAssistantApi.Api.Controllers;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class UploadsControllerTests
{
    private readonly Mock<ILogger<UploadsController>> _loggerMock;
    private readonly UploadsController _controller;

    public UploadsControllerTests()
    {
        _loggerMock = new Mock<ILogger<UploadsController>>();
        _controller = new UploadsController(_loggerMock.Object);
    }

    [Fact]
    public void Init_ShouldLogInformation_WhenCalled()
    {
        // Act
        var result = _controller.Init();

        // Assert
        Assert.IsType<NoContentResult>(result);
        
        // Verify that LogInformation was called with the expected message
        VerifyLogCalled(LogLevel.Information, "Upload initialization requested", Times.Once());
        
        // Verify that LogDebug was called with the expected message
        VerifyLogCalled(LogLevel.Debug, "Upload initialization completed successfully", Times.Once());
    }

    [Fact]
    public void Health_ShouldLogInformation_WhenCalled()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify that LogInformation was called for health check requested
        VerifyLogCalled(LogLevel.Information, "Health check requested", Times.Once());
        
        // Verify that LogInformation was called for health check completed
        VerifyLogCalled(LogLevel.Information, "Health check completed with status: Healthy", Times.Once());
    }

    [Fact]
    public void Health_ShouldReturnCorrectResponse_WhenCalled()
    {
        // Act
        var result = _controller.Health();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        
        // Use reflection to check the anonymous object properties
        var responseType = response.GetType();
        var statusProperty = responseType.GetProperty("Status");
        var timestampProperty = responseType.GetProperty("Timestamp");
        var versionProperty = responseType.GetProperty("Version");
        
        Assert.Equal("Healthy", statusProperty?.GetValue(response));
        Assert.IsType<DateTimeOffset>(timestampProperty?.GetValue(response));
        Assert.Equal("1.0.0", versionProperty?.GetValue(response));
    }

    private void VerifyLogCalled(LogLevel level, string message, Times times)
    {
        _loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}