using Xunit;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class TelemetryControllerTests
{
    private readonly Mock<ITelemetryService> _telemetryServiceMock;
    private readonly TelemetryController _controller;

    public TelemetryControllerTests()
    {
        _telemetryServiceMock = new Mock<ITelemetryService>();
        _controller = new TelemetryController(_telemetryServiceMock.Object);
    }

    [Fact]
    public async Task ReportError_ReturnsAccepted()
    {
        // Arrange
        var request = new ErrorTelemetryRequest { Source = "sentry", Level = "error" };
        _telemetryServiceMock.Setup(s => s.ReportErrorAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReportError(request, CancellationToken.None);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task ReportAnalyticsEvent_ReturnsAccepted()
    {
        // Arrange
        var request = new AnalyticsEventRequest { Source = "firebase", EventName = "test_event" };
        _telemetryServiceMock.Setup(s => s.ReportAnalyticsEventAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReportAnalyticsEvent(request, CancellationToken.None);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
    }
}
