using System;
using System.Runtime.InteropServices;
using NUnitLite;
using NUnit.Framework;

namespace Nvidia.TextureTools.UnitTests 
{
    [TestFixture]
    public class Test
    {
        private static int _count;

        // NOTE: This stress test takes around 5 times longer on 
        // Mac because it seems Mono's GC is that much slower.

        [Test]
        [Repeat(200)]
        [Parallelizable]
        public void StressTest()
        {
            var count = ++_count;
            var seed = unchecked(123456 * count);
            Console.WriteLine("StressTest Seed=" + seed);

            TestRandomAllFormats(32, 32, seed);
            TestRandomAllFormats(32, 64, seed+1);
            TestRandomAllFormats(61, 127, seed+2);
            TestRandomAllFormats(1024, 32, seed+3);
            TestRandomAllFormats(32, 1024, seed+4);
            TestRandomAllFormats(1024, 1024, seed+5);
        }

        private void TestRandomAllFormats(int sizeX, int sizeY, int seed)
        {
            TestRandom(sizeX, sizeY, seed, Format.DXT1, AlphaMode.Premultiplied, Quality.Highest);
            TestRandom(sizeX, sizeY, seed+1, Format.DXT1a, AlphaMode.Premultiplied, Quality.Highest);
            TestRandom(sizeX, sizeY, seed+2, Format.DXT3, AlphaMode.Premultiplied, Quality.Highest);
            TestRandom(sizeX, sizeY, seed+3, Format.DXT5, AlphaMode.Premultiplied, Quality.Highest);
        }

        private void TestRandom(int sizeX, int sizeY, int seed, Format format, AlphaMode alphaMode, Quality quality)
        {
            Console.WriteLine("TestRandom {0} {1}x{2} {3} {4} {5}", seed, sizeX, sizeY, format, alphaMode, quality);
            var data = CreateRandomTexture(sizeX, sizeY, seed);
            TestCompress(sizeX, sizeY, data, Format.DXT1, AlphaMode.Premultiplied, Quality.Highest);
        }

        private static byte[] CreateRandomTexture(int sizeX, int sizeY, int seed)
        {
            var rand = new Random(seed);

            var data = new byte[sizeX * sizeY * 4];
            for (int i = 0; i < data.Length; i += 4)
            {
                data[i + 0] = (byte)rand.Next(0, 255);
                data[i + 1] = (byte)rand.Next(0, 255);
                data[i + 2] = (byte)rand.Next(0, 255);
                data[i + 3] = 255;
            }

            return data;
        }

        private void TestCompress(int sizeX, int sizeY, byte[] data, Format format, AlphaMode alphaMode, Quality quality)
        {
            var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                var dataPtr = dataHandle.AddrOfPinnedObject();

                var inputOptions = new InputOptions();
                inputOptions.SetTextureLayout(TextureType.Texture2D, sizeX, sizeY, 1);
                inputOptions.SetMipmapData(dataPtr, sizeX, sizeY, 1, 0, 0);
                inputOptions.SetMipmapGeneration(false);
                inputOptions.SetGamma(1.0f, 1.0f);
                inputOptions.SetAlphaMode(alphaMode);

                var compressionOptions = new CompressionOptions();
                compressionOptions.SetFormat(format);
                compressionOptions.SetQuality(quality);

                var dataHelper = new DataHelper();

                var outputOptions = new OutputOptions();
                outputOptions.SetOutputHeader(false);
                outputOptions.SetOutputOptionsOutputHandler(dataHelper.BeginImageInternal, dataHelper.WriteDataInternal, dataHelper.EndImageInternal);
                outputOptions.Error += OutputOptionsOnError;

                Collect();

                var compressor = new Compressor();
                var estsize = compressor.EstimateSize(inputOptions, compressionOptions);
                Assert.True(compressor.Compress(inputOptions, compressionOptions, outputOptions));

                Collect();

                Assert.AreEqual(estsize, dataHelper.buffer.Length);                
            }
            finally
            {
                dataHandle.Free();
            }
        }

        private void OutputOptionsOnError(Error error)
        {
            Console.WriteLine("OutputOptionsOnError! " + error);
            throw new Exception();
        }

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect(); 
        }

        class DataHelper
        {
            public byte[] buffer;
            int offset;

            public void BeginImageInternal(int size, int width, int height, int depth, int face, int miplevel)
            {
                Collect();
                buffer = new byte [size];
                offset = 0;
            }

            public bool WriteDataInternal(IntPtr data, int length)
            {
                Collect();
                Marshal.Copy(data, buffer, offset, length);
                offset += length;
                return true;
            }

            public void EndImageInternal()
            {
                Collect();
            }
        }

        public static int Main(String[] args)
        {
            return new AutoRun().Execute(args);
        }        
    }    
}
