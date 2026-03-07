using Microsoft.AspNetCore.Http;
using Moq;
using KuechenRezepte.Services;

namespace KuechenRezepte.Tests;

public class RezeptImageServiceTests
{
    private static RezeptImageService CreateService()
    {
        var storageMock = new Mock<IImageStorage>();
        return new RezeptImageService(storageMock.Object);
    }

    private static IFormFile CreateFormFile(string fileName, long sizeBytes, string contentType = "image/jpeg")
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(sizeBytes);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        return fileMock.Object;
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    [InlineData("image.PNG")]
    [InlineData("picture.webp")]
    public void IsValidImage_AllowedExtension_ReturnsTrue(string fileName)
    {
        var service = CreateService();
        var file = CreateFormFile(fileName, 1024);

        var isValid = service.IsValidImage(file, out var error);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.js")]
    [InlineData("document.pdf")]
    [InlineData("archive.zip")]
    public void IsValidImage_DisallowedExtension_ReturnsFalse(string fileName)
    {
        var service = CreateService();
        var file = CreateFormFile(fileName, 1024);

        var isValid = service.IsValidImage(file, out var error);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void IsValidImage_FileTooLarge_ReturnsFalse()
    {
        var service = CreateService();
        var oversizedFile = CreateFormFile("photo.jpg", 3 * 1024 * 1024); // 3 MB

        var isValid = service.IsValidImage(oversizedFile, out var error);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void IsValidImage_ExactlyAtMaxSize_ReturnsTrue()
    {
        var service = CreateService();
        var file = CreateFormFile("photo.jpg", 2 * 1024 * 1024); // exactly 2 MB

        var isValid = service.IsValidImage(file, out var error);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsExternalPath_HttpUrl_ReturnsTrue()
    {
        var service = CreateService();
        Assert.True(service.IsExternalPath("https://example.com/image.jpg"));
    }

    [Fact]
    public void IsExternalPath_RelativePath_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsExternalPath("/uploads/rezept-1-123.jpg"));
    }

    [Fact]
    public void IsExternalPath_Null_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsExternalPath(null));
    }
}
