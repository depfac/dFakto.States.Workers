using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using dFakto.States.Workers.FileStores;
using dFakto.States.Workers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dFakto.States.Workers.Tests
{
    public class GZipWorkerTests : BaseTests
    {
        private const string Content = "Hello world";

        private readonly FileStoreFactory _fileStoreFactory;
        public GZipWorkerTests()
        {
            _fileStoreFactory = Host.Services.GetService<FileStoreFactory>();
        }
        
        [Theory]
        [InlineData("helloworld.txt.gz","test")]
        [InlineData("helloworld.gz","test")]
        [InlineData("helloworld.data","test")]
        [InlineData("helloworld","test")]
        public async Task Simple_Gunzip_File(string fileName, string fileStoreName)
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName(fileStoreName);
            
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateGZipFileInStore(fileStore, fileName);

            var result = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
            });

            Assert.Equal(Content, await ReadTextFileInStore(result));
            Assert.True(await fileStore.Exists(token));
        }
        
        [Theory]
        [InlineData("helloworld")]
        public async Task Simple_compress_File(string fileName)
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName("test");

            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateFileInStore(fileStore, fileName);

            var compressedFileToken = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
                Compress = true
            });

            var outputFileToken = await UnzipFile("test", compressedFileToken);
            Assert.Equal(Content, await ReadTextFileInStore(outputFileToken));
            Assert.True(await fileStore.Exists(token));
        }
        
        [Fact]
        public async Task Gunzip_File_DeleteSource()
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName("test");
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateGZipFileInStore(fileStore, "helloworld.txt.gz");

            var result = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
                DeleteSource = true
            });

            Assert.Equal(Content, await ReadTextFileInStore(result));
            Assert.False(await fileStore.Exists(token));
        }
        
        [Fact]
        public async Task Compress_Delete_Source()
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName("test");
            
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateFileInStore(fileStore, "helloworld.txt");

            var compressedFileToken = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
                Compress = true,
                DeleteSource = true
            });

            var outputFileToken = await UnzipFile("test", compressedFileToken);
            Assert.Equal(Content, await ReadTextFileInStore(outputFileToken));
            Assert.False(await fileStore.Exists(token));
        }
        
        [Fact]
        public async Task Gunzip_File_SetOutputFileName()
        {   
            var fileStore = _fileStoreFactory.GetFileStoreFromName("test");
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateGZipFileInStore(fileStore, "helloworld.txt.gz");

            var result = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
                OutputFileName = "hello.txt"
            });

            Assert.Equal(Content, await ReadTextFileInStore(result));
            Assert.True(await fileStore.Exists(token));
            Assert.Equal("hello.txt", await fileStore.GetFileName(result));
        }
        
        [Fact]
        public async Task Compress_Set_Filename()
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName("test");
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateFileInStore(fileStore, "helloworld.txt");

            var compressedFileToken = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                FileToken = token,
                Compress = true,
                OutputFileName = "hello.txt"
            });
            
            Assert.Equal("hello.txt", await fileStore.GetFileName(compressedFileToken));
        }

        [Theory]
        [InlineData("helloworld")]
        public async Task Compress_usingFileName(string fileName)
        {
            string fileStoreName = "test";
            var fileStore = _fileStoreFactory.GetFileStoreFromName(fileStoreName);

            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateFileInStore(fileStore, fileName);

            var compressedFileToken = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                InputFileName = fileName,
                InputFileStoreName = fileStoreName, 
                FileToken = token,
                Compress = true
            });

            var outputFileToken = await UnzipFile("test", compressedFileToken);
            Assert.Equal(Content, await ReadTextFileInStore(outputFileToken));
            Assert.True(await fileStore.Exists(token));
        }
        
        [Theory]
        [InlineData("helloworld.txt.gz","test")]
        public async Task Simple_Gunzip_UsingFileName(string fileName, string fileStoreName)
        {
            var fileStore = _fileStoreFactory.GetFileStoreFromName(fileStoreName);
            
            GZipWorker worker = Host.Services.GetService<GZipWorker>();
            var token = await CreateGZipFileInStore(fileStore, fileName);

            var result = await worker.DoJsonWork<GZipInput,string>(new GZipInput
            {
                InputFileName = fileName,
                InputFileStoreName = fileStoreName
            });

            Assert.Equal(Content, await ReadTextFileInStore(result));
            Assert.True(await fileStore.Exists(token));
        }

        private async Task<string> CreateGZipFileInStore(IFileStore fileStore, string fileName)
        {
            string token = await fileStore.CreateFileToken(fileName);
            await using (var writer = await fileStore.OpenWrite(token))
            await using (var zipStream = new GZipStream(writer, CompressionMode.Compress))
            {
                zipStream.Write(Encoding.UTF8.GetBytes(Content));
                await zipStream.FlushAsync();
            }

            return token;
        }

        private async Task<string> CreateFileInStore(IFileStore fileStore, string fileName)
        {
            string token = await fileStore.CreateFileToken(fileName);
            await using (var writer = await fileStore.OpenWrite(token))
            {
                writer.Write(Encoding.UTF8.GetBytes(Content));
                await writer.FlushAsync();
            }
            return token;
        }

        private async Task<string> UnzipFile(string outputFileName, string compressedFileToken)
        {
            using (var fileStore = _fileStoreFactory.GetFileStoreFromName("test"))
            {
                var outputFileToken = await fileStore.CreateFileToken(outputFileName);
                using (var compressedFileStream = await fileStore.OpenRead(compressedFileToken))
                using (var gzipStream = new GZipStream(compressedFileStream, CompressionMode.Decompress))
                using (var inputStream = await fileStore.OpenWrite(outputFileToken))
                {
                    await gzipStream.CopyToAsync(inputStream, 2048);
                }
                return outputFileToken;
            }
        }

        private async Task<string> ReadTextFileInStore(string fileToken)
        {
            using(var fileStore = _fileStoreFactory.GetFileStoreFromFileToken(fileToken))
            await using (var stream = await fileStore.OpenRead(fileToken))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}