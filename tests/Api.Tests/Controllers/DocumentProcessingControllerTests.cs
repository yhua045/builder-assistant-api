using Xunit;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class DocumentProcessingControllerTests
{
    private readonly Mock<IGroqService> _groqServiceMock;
    private readonly DocumentProcessingController _controller;

    public DocumentProcessingControllerTests()
    {
        _groqServiceMock = new Mock<IGroqService>();
        _controller = new DocumentProcessingController(_groqServiceMock.Object);
    }

    [Fact]
    public async Task ProcessStt_ReturnsOkResult()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var response = new AiSttResponse { Transcript = "Test" };
        _groqServiceMock.Setup(s => s.ProcessSttAsync(It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ProcessStt(fileMock.Object, "audio/mpeg", "test.mp3", "whisper", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task ParseTaskDraft_ReturnsOkResult()
    {
        // Arrange
        var request = new TaskDraftRequest { Transcript = "Fix sink" };
        var response = new TaskDraftResponse { Title = "Task" };
        _groqServiceMock.Setup(s => s.ParseTaskDraftAsync(request.Transcript, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ParseTaskDraft(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
    }
}