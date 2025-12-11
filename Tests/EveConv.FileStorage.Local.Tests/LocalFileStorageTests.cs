using System.Text;
using EveConv.Abstraction;
using EveConv.FileStorage.Local;
using Microsoft.Extensions.Options;

namespace EveConv.FileStorage.Local.Tests
{
    public class LocalFileStorageTests : IDisposable
    {
        private readonly string _tempRootPath;
        private readonly LocalFileStorage _storage;
        private readonly MockMimeTypeDetection _mimeTypeDetection;

        public LocalFileStorageTests()
        {
            _tempRootPath = Path.Combine(Path.GetTempPath(), $"LocalFileStorageTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempRootPath);

            _mimeTypeDetection = new MockMimeTypeDetection();
            var config = Options.Create(new LocalFileStorageConfiguration { RootPath = _tempRootPath });
            _storage = new LocalFileStorage(config, _mimeTypeDetection, null);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (Directory.Exists(_tempRootPath))
            {
                Directory.Delete(_tempRootPath, true);
            }
        }

        [Fact]
        public async Task CreateIndexAsync_CreatesDirectory_Success()
        {
            // Arrange
            var indexName = "test-index";

            // Act
            await _storage.CreateIndexAsync(indexName, TestContext.Current.CancellationToken);

            // Assert
            var indexPath = Path.Combine(_tempRootPath, indexName);
            Assert.True(Directory.Exists(indexPath));
        }

        [Fact]
        public async Task EnsureIndexExistsAsync_WhenIndexExists_DoesNotThrow()
        {
            // Arrange
            var indexName = "existing-index";
            var indexPath = Path.Combine(_tempRootPath, indexName);
            Directory.CreateDirectory(indexPath);

            // Act & Assert
            await _storage.EnsureIndexExistsAsync(indexName, TestContext.Current.CancellationToken);
            Assert.True(Directory.Exists(indexPath));
        }

        [Fact]
        public async Task EnsureIndexExistsAsync_WhenIndexDoesNotExist_CreatesIndex()
        {
            // Arrange
            var indexName = "new-index";

            // Act
            await _storage.EnsureIndexExistsAsync(indexName, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(Directory.Exists(Path.Combine(_tempRootPath, indexName)));
        }

        [Theory]
        [InlineData(typeof(ArgumentNullException), null)]
        [InlineData(typeof(ArgumentException), "")]
        [InlineData(typeof(ArgumentException), "   ")]
        public async Task EnsureIndexExistsAsync_WithNullOrWhiteSpace_ThrowsArgumentException(Type ex, string? name)
        {
            await Assert.ThrowsAsync(ex, () => _storage.EnsureIndexExistsAsync(name!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task WriteFileAsync_CreatesFileSuccessfully()
        {
            // Arrange
            var indexName = "write-index";
            var fileId = "file-001";
            var fileName = "test.txt";
            var content = "Hello, World!";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            // Act
            await _storage.WriteFileAsync(indexName, fileId, fileName, stream, TestContext.Current.CancellationToken);

            // Assert
            var filePath = Path.Combine(_tempRootPath, indexName, fileId, fileName);
            Assert.True(File.Exists(filePath));
            var savedContent = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal(content, savedContent);
        }

        [Theory]
        [InlineData(null, "fileId", "fileName")]
        [InlineData("index", null, "fileName")]
        [InlineData("index", "fileId", null)]
        public async Task WriteFileAsync_WithNullParameters_ThrowsArgumentNullException(string? indexName, string? fileId, string? fileName)
        {
            // Arrange
            using var stream = new MemoryStream();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.WriteFileAsync(indexName!, fileId!, fileName!, stream, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ReadFileAsync_ReturnsCorrectFileContent()
        {
            // Arrange
            var indexName = "read-index";
            var fileId = "file-002";
            var fileName = "document.txt";
            var content = "Test content for reading";

            var indexPath = Path.Combine(_tempRootPath, indexName);
            var fileFolderPath = Path.Combine(indexPath, fileId);
            var filePath = Path.Combine(fileFolderPath, fileName);
            Directory.CreateDirectory(fileFolderPath);
            await File.WriteAllTextAsync(filePath, content, TestContext.Current.CancellationToken);

            // Act
            var result = await _storage.ReadFileAsync(indexName, fileId, fileName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(fileName, result.FileName);
            Assert.Equal("text/plain", result.FileType);
            Assert.True(result.FileSize > 0);

            using var readStream = await result.GetStreamAsync();
            using var reader = new StreamReader(readStream);
            var readContent = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            Assert.Equal(content, readContent);
        }

        [Fact]
        public async Task ReadFileAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var indexName = "read-index";
            var fileId = "non-existent";
            var fileName = "missing.txt";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                _storage.ReadFileAsync(indexName, fileId, fileName, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(null, "fileId", "fileName")]
        [InlineData("index", null, "fileName")]
        [InlineData("index", "fileId", null)]
        public async Task ReadFileAsync_WithNullParameters_ThrowsArgumentNullException(string? indexName, string? fileId, string? fileName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.ReadFileAsync(indexName!, fileId!, fileName!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DeleteFileAsync_RemovesFileSuccessfully()
        {
            // Arrange
            var indexName = "delete-index";
            var fileId = "file-003";
            var fileName = "to-delete.txt";

            var indexPath = Path.Combine(_tempRootPath, indexName);
            var fileFolderPath = Path.Combine(indexPath, fileId);
            var filePath = Path.Combine(fileFolderPath, fileName);
            Directory.CreateDirectory(fileFolderPath);
            await File.WriteAllTextAsync(filePath, "Delete me", TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(fileFolderPath));

            // Act
            await _storage.DeleteFileAsync(indexName, fileId, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(Directory.Exists(fileFolderPath));
        }

        [Fact]
        public async Task DeleteFileAsync_WithNonExistentFile_DoesNotThrow()
        {
            // Arrange
            var indexName = "delete-index";
            var fileId = "non-existent-file";

            // Act & Assert (should not throw)
            await _storage.DeleteFileAsync(indexName, fileId, TestContext.Current.CancellationToken);
        }

        [Theory]
        [InlineData(null, "fileId")]
        [InlineData("index", null)]
        public async Task DeleteFileAsync_WithNullParameters_ThrowsArgumentNullException(string? indexName, string? fileId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.DeleteFileAsync(indexName!, fileId!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task WriteFileAsync_OverwritesExistingFile()
        {
            // Arrange
            var indexName = "overwrite-index";
            var fileId = "file-004";
            var fileName = "overwrite.txt";

            var indexPath = Path.Combine(_tempRootPath, indexName);
            var fileFolderPath = Path.Combine(indexPath, fileId);
            var filePath = Path.Combine(fileFolderPath, fileName);
            Directory.CreateDirectory(fileFolderPath);
            await File.WriteAllTextAsync(filePath, "Original content", TestContext.Current.CancellationToken);

            using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("New content"));

            // Act
            await _storage.WriteFileAsync(indexName, fileId, fileName, stream2, TestContext.Current.CancellationToken);

            // Assert
            var content = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
            Assert.Equal("New content", content);
        }

        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LocalFileStorage(null!, _mimeTypeDetection, null));
        }

        [Fact]
        public void Constructor_WithNullOrWhiteSpaceRootPath_ThrowsArgumentException()
        {
            // Arrange
            var config = Options.Create(new LocalFileStorageConfiguration { RootPath = null! });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new LocalFileStorage(config, _mimeTypeDetection, null));
        }

        private class MockMimeTypeDetection : IMimeTypeDetection
        {
            public string GetFileType(string filename)
            {
                var ext = Path.GetExtension(filename).ToLowerInvariant();
                return ext switch
                {
                    ".txt" => "text/plain",
                    ".json" => "application/json",
                    ".pdf" => "application/pdf",
                    _ => "application/octet-stream"
                };
            }
        }
    }
}
