using System.Security.Claims;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class FeatureFlagsControllerTests
{
    private readonly Mock<IFeatureFlagService> _serviceMock;
    private readonly Mock<IFeatureCacheInvalidator> _invalidatorMock;
    private readonly Mock<IFeatureRepository> _repositoryMock;
    private readonly FeatureFlagsController _controller;

    public FeatureFlagsControllerTests()
    {
        _serviceMock = new Mock<IFeatureFlagService>();
        _invalidatorMock = new Mock<IFeatureCacheInvalidator>();
        _repositoryMock = new Mock<IFeatureRepository>();
        _controller = new FeatureFlagsController(_serviceMock.Object, _invalidatorMock.Object, _repositoryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ── GET /api/features ────────────────────────────────────────────────────

    [Fact]
    public async Task GetFeatures_Anonymous_ReturnsOkWithAsAnonymousTrue()
    {
        // Arrange
        var response = new FeatureFlagDto(
            null, true,
            [new FeatureItemDto(FeatureKeys.OcrScan, false, "default_off", null)]);

        _serviceMock
            .Setup(s => s.GetEffectiveFlagsAsync(null, It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetFeatures(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
    }

    [Fact]
    public async Task GetFeatures_Authenticated_PassesUserIdAndRolesToService()
    {
        // Arrange
        const long userId = 42L;
        var response = new FeatureFlagDto(
            "42", false,
            [new FeatureItemDto(FeatureKeys.OcrScan, true, "role:Premium", null)]);

        _serviceMock
            .Setup(s => s.GetEffectiveFlagsAsync(userId, It.Is<IReadOnlyList<string>?>(roles => roles != null && roles.Contains("Premium")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "42"),
                        new Claim(ClaimTypes.Role, "Premium")
                    ], "test"))
            }
        };

        // Act
        var result = await _controller.GetFeatures(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, ok.Value);
        _serviceMock.Verify(
            s => s.GetEffectiveFlagsAsync(userId, It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetFeatures_ServiceThrows_PropagatesException()
    {
        // Arrange
        _serviceMock
            .Setup(s => s.GetEffectiveFlagsAsync(It.IsAny<long?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.GetFeatures(CancellationToken.None));
    }

    // ── POST /api/features/admin/entitlements ────────────────────────────────

    [Fact]
    public async Task UpsertEntitlement_Valid_CallsRepositoryAndInvalidatesCache()
    {
        // Arrange
        var request = new UpsertRoleEntitlementRequest("Premium", FeatureKeys.OcrScan, true, null);

        _serviceMock
            .Setup(s => s.GetEffectiveFlagsAsync(It.IsAny<long?>(), It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureFlagDto(null, true, []));

        // Act
        var result = await _controller.UpsertEntitlement(request, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        _invalidatorMock.Verify(i => i.InvalidateRole("Premium"), Times.Once);
    }

    // ── DELETE /api/features/admin/entitlements/{roleName}/{featureKey} ──────

    [Fact]
    public async Task DeleteEntitlement_Valid_CallsRepositoryAndInvalidatesCache()
    {
        // Act
        var result = await _controller.DeleteEntitlement("Premium", FeatureKeys.OcrScan, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _invalidatorMock.Verify(i => i.InvalidateRole("Premium"), Times.Once);
    }
}

