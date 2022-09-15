using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Nanook
{
    public unsafe class Program
    {
        private static Stream ReadFile(string fn, bool bufferAll)
        {
            if (!bufferAll)
                return File.OpenRead(fn);
            MemoryStream ms = new MemoryStream();
            using (Stream fsIn = File.OpenRead(fn))
                fsIn.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        public static void ZlibCompress(string inFile, string outFile, int level, int bufferSize, bool bufferFile, bool headerless)
        {
            using (Stream ms = ReadFile(inFile, bufferFile))
            {
                Console.WriteLine($"{ms.Length} bytes read");
                using (Stream mso = new MemoryStream(new byte[ms.Length + 0x80000]))
                {
                    using (ZlibDeflateStream zlib = new ZlibDeflateStream(level, headerless, bufferSize, mso, true))
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        ms.CopyTo(zlib);
                        sw.Stop();
                        Console.WriteLine($"{sw.Elapsed.TotalSeconds} seconds taken for deflate");
                    }

                    Console.WriteLine($"{mso.Position} bytes compressed");
                    mso.SetLength(mso.Position);
                    mso.Position = 0;
                    using (Stream fsOut = File.OpenWrite(outFile))
                        mso.CopyTo(fsOut);
                }
            }
        }

        public static void ZlibDecompress(string inFile, string outFile, int bufferSize, bool bufferFile, bool headerless)
        {
            using (Stream ms = ReadFile(inFile, true))
            {
                Console.WriteLine($"{ms.Length} bytes read");
                using (Stream fsOut = File.OpenWrite(outFile))
                {
                    using (ZlibInflateStream zi = new ZlibInflateStream(headerless, bufferSize, ms, bufferFile))
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        zi.CopyTo(fsOut);
                        sw.Stop();
                        Console.WriteLine($"{sw.Elapsed.TotalSeconds} seconds taken for inflate (+write)");
                    }
                }
            }
        }


        public static bool ZLibBlockTest(string inFile, int blockSize, bool headerless)
        {
            byte[] buf = new byte[blockSize * 2]; //room for blocks that don't compress well
            List<long> blockSizes = new List<long>();
            int read;
            byte[] bc;

            using (MemoryStream compress = new MemoryStream())
            {
                using (MemoryStream ms = (MemoryStream)ReadFile(inFile, true))
                {
                    bc = ms.ToArray();
                    using (ZlibDeflateStream zlib = new ZlibDeflateStream(9, headerless, blockSize, compress, true))
                    {
                        long outBlock;
                        while (true)
                        {
                            if ((read = ms.Read(buf, 0, blockSize)) == 0)
                                break; //can exit here is stream ends
                            zlib.Write(buf, 0, read);
                            outBlock = zlib.BlockFlush();
                            if (outBlock == 0)
                                break;
                            blockSizes.Add(outBlock);
                        }
                        blockSizes.Add(0); //store an ending 0
                    }
                }

                compress.Position = 0;
                int inBlock = 0;
                using (MemoryStream decompress = new MemoryStream())
                {
                    //Larger buffer so there's room to expand (might need a bug fix)
                    using (ZlibInflateStream zlib = new ZlibInflateStream(blockSizes[inBlock++], headerless, buf.Length, compress, true))
                    {
                        while ((read = zlib.Read(buf, 0, blockSize)) != 0 && inBlock < blockSizes.Count)
                        {
                            zlib.BlockFlush((int)blockSizes[inBlock++]); //last will be 0
                            decompress.Write(buf, 0, read);
                        }
                    }
                    decompress.Position = 0;

                    byte[] bd = decompress.ToArray();
                    bool eq = true;
                    if (bc.Length != bd.Length)
                        return false;
                    for (int i = 0; eq && i < bc.Length; i++)
                    {
                        if (bc[i] != bd[i])
                            return false;
                    }
                }
            }
            return true;
        }

        public static void ZlibOldCompress(string inFile, string outFile, int level)
        {
            using (Stream ms = ReadFile(inFile, true))
            {
                Console.WriteLine($"{ms.Length} bytes read");
                using (Stream mso = new MemoryStream(new byte[ms.Length]))
                {
                    using (SharpCompress.Compressors.Deflate.ZlibStream zlib = new SharpCompress.Compressors.Deflate.ZlibStream(mso, SharpCompress.Compressors.CompressionMode.Compress, (SharpCompress.Compressors.Deflate.CompressionLevel)level))
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        ms.CopyTo(zlib);
                        sw.Stop();
                        Console.WriteLine($"{sw.Elapsed.TotalSeconds} seconds taken for deflate");
                        Console.WriteLine($"{mso.Position} bytes compressed");
                        mso.SetLength(mso.Position);
                        mso.Position = 0;
                        using (Stream fsOut = File.OpenWrite(outFile))
                            mso.CopyTo(fsOut);
                    }

                }
            }
        }

        public static void ZlibOldDecompress(string inFile, string outFile)
        {
            using (Stream fsOut = File.OpenWrite(outFile))
            {
                using (Stream fsIn = File.OpenRead(inFile))
                {
                    using (SharpCompress.Compressors.Deflate.ZlibStream zlib = new SharpCompress.Compressors.Deflate.ZlibStream(fsIn, SharpCompress.Compressors.CompressionMode.Decompress))
                        zlib.CopyTo(fsOut);
                }
            }
        }

        public static int Main(string[] args)
        {
            return 0;
        }

    }
}
