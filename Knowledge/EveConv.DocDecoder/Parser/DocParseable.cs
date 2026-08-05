using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using EveConv.Abstraction;
using EveConv.Abstraction.DocParser;
using EveConv.Abstraction.DocParser.Block;

namespace EveConv.DocDecoder.Parser
{
    public abstract class DocParseable : IDocumentParser
    {
        public const int DEFAULT_PRIORITY = 100;

        protected IMimeTypeDetection _mimeTypeDetection;

        protected DocParseable(IMimeTypeDetection mimeTypeDetection)
        {
            this._mimeTypeDetection = mimeTypeDetection;
        }

        public virtual int Priority => DEFAULT_PRIORITY;

        public abstract bool Accept(string fileType);

        public async Task<Document> ParseAsync(string source, Stream fileStream, CancellationToken cancellationToken = default)
        {
            var (paragraphs, sections) = await ParseAsync(fileStream, cancellationToken).ConfigureAwait(false);
            var metadata = await GetMetadataAsync(source, fileStream, cancellationToken).ConfigureAwait(false);
            return new(GetFileType(source), source)
            {
                Paragraphs = paragraphs ?? [],
                Sections = sections ?? [],
                Metadata = metadata ?? [],
            };
        }

        protected virtual string GetFileType(string source)
        {
            return _mimeTypeDetection.GetFileType(source);
        }

        protected virtual Task<(IEnumerable<IParagraphBlock> Paragraphs, IEnumerable<SectionBlock> Sections)> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(IEnumerable<IParagraphBlock>, IEnumerable<SectionBlock>)>(([], []));
        }

        protected virtual async Task<Dictionary<string, string?>> GetMetadataAsync(string source, Stream fileStream, CancellationToken cancellationToken = default)
        {
            var metadata = await GetFileSystemMetadataAsync(source, fileStream).ConfigureAwait(false);
            return metadata.ToDictionary();
        }

        /// <summary>
        /// Extracts basic file system metadata from the source path and stream.
        /// </summary>
        protected virtual Task<BaseMetadata> GetFileSystemMetadataAsync(string source, Stream fileStream)
        {
            var metadata = new BaseMetadata();

            // Extract file size from stream
            try
            {
                metadata.FileSize = fileStream.Length;
            }
            catch { }

            // Extract file system timestamps if source is a local file path
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                TryFillFileInfo(metadata, uri.LocalPath);
            }
            else if (!Uri.TryCreate(source, UriKind.Absolute, out _))
            {
                // Treat as local file path
                TryFillFileInfo(metadata, source);
            }

            return Task.FromResult(metadata);

            static void TryFillFileInfo(BaseMetadata metadata, string filePath)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                    {
                        return;
                    }
                    metadata.FileSize ??= fileInfo.Length;
                    metadata.CreatedAt ??= fileInfo.CreationTime;
                    metadata.UpdatedAt ??= fileInfo.LastWriteTime;
                    metadata.LastAccessedAt ??= fileInfo.LastAccessTime;
                    metadata.FileAttributes ??= fileInfo.Attributes;
                }
                catch { }
            }
        }

    }

    public record BaseMetadata
    {
        /// <summary>File size in bytes.</summary>
        public long? FileSize { get; set; }

        /// <summary>File creation time.</summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>File last modification time.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>File last access time.</summary>
        public DateTime? LastAccessedAt { get; set; }

        /// <summary>Document author / creator.</summary>
        public string? Author { get; set; }

        /// <summary>User who last modified the document.</summary>
        public string? LastModifiedBy { get; set; }

        /// <summary>Document title.</summary>
        public string? Title { get; set; }

        /// <summary>Document subject.</summary>
        public string? Subject { get; set; }

        /// <summary>Keywords or tags.</summary>
        public string? Keywords { get; set; }

        /// <summary>Total page count (for paged formats).</summary>
        public int? PageCount { get; set; }

        /// <summary>Total word count.</summary>
        public int? WordCount { get; set; }

        /// <summary>Total character count.</summary>
        public int? CharacterCount { get; set; }

        /// <summary>Document revision / version number.</summary>
        public string? RevisionNumber { get; set; }

        /// <summary>Last printed date.</summary>
        public DateTime? LastPrinted { get; set; }

        /// <summary>Name of the application that created the file.</summary>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// File attributes (e.g., ReadOnly, Hidden, System) as defined in <see cref="System.IO.FileAttributes"/>.
        /// </summary>
        public FileAttributes? FileAttributes { get; set; }

        /// <summary>
        /// Converts this metadata instance to a <see cref="Dictionary{String, String}"/>.
        /// Null-valued properties are excluded. <see cref="DateTime"/> values use the round-trip ("O") format.
        /// Uses runtime type resolution so that properties declared on derived classes are included.
        /// </summary>
        public virtual Dictionary<string, string?> ToDictionary()
        {
            var properties = GetType().GetProperties();
            var dict = new Dictionary<string, string?>();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(this);
                if (value is null) continue;
                dict[prop.Name] = value.ToString();
            }
            return dict;
        }
    }
}
