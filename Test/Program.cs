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
            //PORT DECOMPRESS
            string f = @"D:\NKitFiles\PSP\007 - From Russia with Love (UK) (En,Fr,De,Es,It).iso";
            //f = @"D:\NKitFiles\Temp\Wii BackupDisc [010E01] [655CA219].tmp";
            bool match = ZLibBlockTest(f, 0x8000, true);
            //ZlibCompress(f, f + "_c", 9, 0x8000, false, false);
            //ZlibOldDecompress(f + "_New", f + "_do");
            //ZlibDecompress(f + "_c", f + "_d", 0x8000, true, false);
            return 0;
            //PORT COMPRESS
            //if (args.Length < 2)
            //{
            //    Console.Write($"run zlibCPort <level0-9> <File>\n");
            //    return 0;
            //}

            //ZlibCompress(args[1], args[1] + "_CPort", int.Parse(args[0]), 0x8000);
            //return 0;

            //OLD COMPRESS
            //if (args.Length < 2)
            //{
            //    Console.Write($"run zlibOld <level0-9> <File>\n");
            //    return 0;
            //}

            //ZlibOldCompress(args[1], args[1] + "_Old", int.Parse(args[0]));
            //return 0;



            int level = 9;
            int inSize = 0x20;

            byte[] version = Encoding.ASCII.GetBytes("\0");
            byte[] s = Encoding.ASCII.GetBytes("potatoes");

            byte[] zdB = new byte[0x500];
            byte[] ziB = new byte[0x500];
            for (int i = 0; i < inSize; i++)
                zdB[i] = (byte)(i % 0x10);

            //string f = @"D:\NKitFiles\PSP\007 - From Russia with Love (UK) (En,Fr,De,Es,It).iso";
            //string f = @"D:\NKitFiles\Scan\Wii\LEGO Star Wars - The Complete Saga (Europe) (En,Fr,De,Es,It,Da).nkit";

            //ZlibCompress(f, f + "Zlib9100", 9, 0x100);

            //ZlibOldDecompress(f + "Zlib9", f + "Zlib9Dec");


            //ZlibOldCompress(@"D:\NKitFiles\PSP\007 - From Russia with Love (UK) (En,Fr,De,Es,It).iso",
            //                @"D:\NKitFiles\PSP\007 - From Russia with Love (UK) (En,Fr,De,Es,It).isoOLDZlib9", 9);

            //Stream fs = File.OpenRead(@"D:\NKitFiles\Minecraft - PlayStation 3 Edition (Europe) (En,Ja,Fr,De,Es,It,Nl,Pt,Sv,No,Da,Fi,Zh,Ko,Pl,Ru,Tr).iso");
            //using (Stream fsw = File.OpenWrite(@"D:\NKitFiles\Minecraft - PlayStation 3 Edition (Europe) (En,Ja,Fr,De,Es,It,Nl,Pt,Sv,No,Da,Fi,Zh,Ko,Pl,Ru,Tr).isozlib"))
            //{
            //    using (ZlibDeflateStream zds = new ZlibDeflateStream(9, false, 0x8000, fsw, true))
            //    {
            //        fs.CopyTo(zds);
            //    }
            //}


            //using (Stream fswd = File.OpenWrite(@"D:\NKitFiles\Minecraft - PlayStation 3 Edition (Europe) (En,Ja,Fr,De,Es,It,Nl,Pt,Sv,No,Da,Fi,Zh,Ko,Pl,Ru,Tr).isozlibDEF"))
            //{
            //    using (Stream fsq = File.OpenRead(@"D:\NKitFiles\Minecraft - PlayStation 3 Edition (Europe) (En,Ja,Fr,De,Es,It,Nl,Pt,Sv,No,Da,Fi,Zh,Ko,Pl,Ru,Tr).isozlib"))
            //    {
            //        using (SharpCompress.Compressors.Deflate.ZlibStream stream = new SharpCompress.Compressors.Deflate.ZlibStream(fsq, SharpCompress.Compressors.CompressionMode.Decompress)) //prevent base stream being read beyond the block size
            //            stream.CopyTo(fswd);
            //    }
            //}
            return 0;

            int read = 0;
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZlibDeflateStream zd = new ZlibDeflateStream(9, false, 0x10, ms, true))
                {
                    zd.Write(zdB, 0, 0x10);
                    zd.Write(zdB, 0x10, 0x10);
                }

                ms.Position = 0;
                using (ZlibInflateStream zi = new ZlibInflateStream(ms, true))
                    zi.Read(ziB, 0, ziB.Length);
            }

            fixed (byte* x = s, v = version)
            {
                uint crc = ZLib.crc32(0, x, 8);
                uint adler = ZLib.adler32(0, x, 8);

                ZLib.z_stream_s zstrm = new ZLib.z_stream_s();
                ZLib.deflateInit_(zstrm, 9, "1.2.12", 0);

                
                byte[] inBuffer = new byte[0x500];
                for (int i = 0; i < inSize; i++)
                    inBuffer[i] = (byte)(i % 0x10);
                byte[] outBuffer = new byte[0x500];
                byte[] tstBuffer = new byte[0x500];
                GCHandle hInput = GCHandle.Alloc(inBuffer, GCHandleType.Pinned);
                GCHandle hOutput = GCHandle.Alloc(outBuffer, GCHandleType.Pinned);
                GCHandle hTstput = GCHandle.Alloc(tstBuffer, GCHandleType.Pinned);

                zstrm.next_in = (byte*)hInput.AddrOfPinnedObject().ToPointer();
                zstrm.avail_in = (uint)inSize;
                zstrm.total_in = 0;

                zstrm.next_out = (byte*)hOutput.AddrOfPinnedObject().ToPointer();
                zstrm.avail_out = (uint)0x500;
                zstrm.total_out = (uint)0;

                int r;
                r = ZLib.deflate(zstrm, 0); //None,  Partial,  Sync,  Full,  Finish,  Block
                r = ZLib.deflate(zstrm, 4); //Finish
                ZLib.deflateEnd(zstrm);
                Trace.WriteLine($"0x{(BitConverter.ToString(outBuffer, 0, (int)zstrm.total_out).Replace("-", ",0x"))}");



                ZLib.z_stream_s zstrm2 = new ZLib.z_stream_s();
                int x1 = ZLib.inflateInit_(zstrm2, "1.2.12", 0);

                zstrm2.next_in = (byte*)hOutput.AddrOfPinnedObject().ToPointer();
                zstrm2.avail_in = zstrm.total_out;
                zstrm2.total_in = (uint)0;

                zstrm2.next_out = (byte*)hTstput.AddrOfPinnedObject().ToPointer();
                zstrm2.avail_out = (uint)0x500;
                zstrm2.total_out = (uint)0;

                int x2 = ZLib.inflate(zstrm2, 0);
                int x3 = ZLib.inflate(zstrm2, 4);
                ZLib.inflateEnd(zstrm2);


                //MemoryStream ms = new MemoryStream(outBuffer);
                //byte[] test;
                //using (MemoryStream ms2 = new MemoryStream())
                //{
                //    using (SharpCompress.Compressors.Deflate.ZlibStream stream = new SharpCompress.Compressors.Deflate.ZlibStream(ms, SharpCompress.Compressors.CompressionMode.Decompress)) //prevent base stream being read beyond the block size
                //        stream.CopyTo(ms2, inSize);
                //    test = ms2.ToArray();
                //}

                bool eq = true;
                for (int i = 0; eq && i < inSize; i++)
                {
                    if (inBuffer[i] != tstBuffer[i])
                        eq = false;
                }

                if (eq)
                {
                    Console.WriteLine("SUCCESS");
                }







                hInput.Free();
                hOutput.Free();


            }
            return (int)(0);
        }

    }
}
