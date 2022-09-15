//A bunch of test stuff


int ReadFileMemory(const char* filename, long* plFileSize, unsigned char** pFilePtr)
{
    FILE* stream;
    unsigned char* ptr;
    int retVal = 1;
    fopen_s(&stream, filename, "rb");
    if (stream == NULL)
        return 0;

    fseek(stream, 0, SEEK_END);

    *plFileSize = ftell(stream);
    fseek(stream, 0, SEEK_SET);
    ptr = malloc((*plFileSize) + 1);
    if (ptr == NULL)
        retVal = 0;
    else
    {
        if (fread(ptr, 1, *plFileSize, stream) != (*plFileSize))
            retVal = 0;
    }
    fclose(stream);
    *pFilePtr = ptr;
    return retVal;
}

#include <time.h>

int deflateFile(char* filename, int level)
{
    int BlockSizeCompress = 0x8000;
    long lFileSize;
    Bytef* i;
    Bytef* o;

    if (ReadFileMemory(filename, &lFileSize, &i) == 0)
    {
        printf("error reading %s\n", filename);
        return 1;
    }
    else printf("%lu bytes read\n", lFileSize);

    o = (unsigned char*)malloc(lFileSize + 0x80000);

    //variable names set to match c# stream version
    z_stream _s;
    uLong count = lFileSize;
    uLong off = 0;
    int err = 0;
    memset(&_s, 0, sizeof(z_stream));

    time_t prg_begin, prg_end;
    char str[100];
    prg_begin = clock();
    _s.next_in = i;
    _s.next_out = o;
    uLong offset = 0;
    uLong _p = 0;


    //init
    deflateInit(&_s, level);
    _s.total_in = 0;
    _s.avail_out = BlockSizeCompress;
    _s.total_out = 0u;

    //Stream Write Code

    _s.next_out = o + _s.total_out; //point to correct location

    while (err >= 0 && off < count) //process the buffer
    {
        _s.avail_in = (uInt)(count - off);
        _s.next_in = i + offset + off;

        while (err == 0 && _s.avail_in != 0)
        {
            err = deflate(&_s, 2);

            if (err == 0 && _s.avail_out == 0)
            {
                int hdr = 0; //_p == 0 && Headerless ? 2 : 0;
                //this.BaseStream.Write(_b, hdr, (int)_s.total_out - hdr);
                _p += _s.total_out - hdr;
                _s.next_out = o + _p;
                _s.avail_out = BlockSizeCompress; // (uInt)_b.Length;
                _s.total_out = 0u;
            }
        }
        off += (int)_s.total_in;
        _s.total_in = 0;
    }


    //Stream Dispose code

    _s.next_in = 0;
    _s.avail_in = 0;
    _s.next_out = o + _p + _s.total_out; //point to correct location

    int hdr = 0; //_p == 0 && Headerless ? 2 : 0;
    while (err == 0 && _s.state->pending > BlockSizeCompress)
    {
        err = deflate(&_s, 2);
        //this.BaseStream.Write(_b, hdr, (int)_s.total_out - hdr);
        _p += _s.total_out - hdr;
        _s.avail_out = (uInt)BlockSizeCompress;
        _s.total_out = 0u;
        _s.next_out = o;
        hdr = 0;
    }

    err = deflate(&_s, 4);

    if (err == 1 && _s.total_out - hdr > 0)
    {
        //this.BaseStream.Write(_b, hdr, (int)_s.total_out - hdr);
        _p += (int)_s.total_out - hdr;
    }
    //}
    deflateEnd(&_s);
    //_s = null;


    prg_end = clock();
    printf("%f seconds taken for deflate\n", (double)(prg_end - prg_begin) / (double)CLK_TCK);

    o = (unsigned char*)realloc(o, _p);
    printf("%ld bytes compressed\n", _p);
    char wn[256];
    sprintf(wn, "%s_c", filename);
    FILE* out;
    fopen_s(&out, wn, "wb");
    fwrite(o, _p, 1, out);
    fclose(out);

    return 0;
}

int inflateFile(char* filename, long fullSize)
{
    int BlockSizeUncompress = 0x8000;
    long lFileSize;
    Bytef* UncprPtr;
    Bytef* CprPtr;

    if (ReadFileMemory(filename, &lFileSize, &CprPtr) == 0)
    {
        printf("error reading %s\n", filename);
        return 1;
    }
    else printf("%lu bytes read\n", lFileSize);

    z_stream zcpr;
    int ret = Z_OK;
    long lOrigToDo = fullSize;
    long lOrigDone = 0;
    int step = 0;
    memset(&zcpr, 0, sizeof(z_stream));
    UncprPtr = (unsigned char*)malloc(fullSize);

    inflateInit(&zcpr);

    zcpr.next_in = CprPtr;
    zcpr.next_out = UncprPtr;


    do
    {
        long all_read_before = zcpr.total_in;
        zcpr.avail_in = min(lOrigToDo, BlockSizeUncompress);
        zcpr.avail_out = BlockSizeUncompress;
        ret = inflate(&zcpr, Z_SYNC_FLUSH);
        lOrigDone += (zcpr.total_in - all_read_before);
        lOrigToDo -= (zcpr.total_in - all_read_before);
        step++;
    } while (ret == Z_OK);

    long lSizeUncpr = zcpr.total_out;
    inflateEnd(&zcpr);

    char wn[256];
    sprintf(wn, "%s_dc", filename);
    FILE* out;
    fopen_s(&out, wn, "wb");
    fwrite(UncprPtr, zcpr.total_out, 1, out);
    fclose(out);


    return 0;
}
int main(int argc, char* argv[])
{
    if (argc <= 2)
    {
        printf("run %s <level0-9> <File>\n", argv[0]);
        return 0;
    }

    deflateFile(argv[2], atol(argv[1]));

    return 0;

    char* s = "potatoes";
    Bytef* x = (Bytef*)s;
    uLong crc = crc32(0, x, 8);

    uLong adler = adler32(0, x, 8);

    int size = 0x20;

    z_stream zstrm;
    memset(&zstrm, 0, sizeof(z_stream));
    Bytef* inBuffer = (Bytef*)malloc(0x500);
    Bytef* outBuffer = (Bytef*)malloc(0x500);
    memset(inBuffer, 0, 0x500);
    memset(outBuffer, 0, 0x500);

    for (size_t i = 0; i < size; i++)
        inBuffer[i] = (Bytef)i % 0x10;

    char* version = "1";

    deflateInit_(&zstrm, 9, version, sizeof(z_stream));

    zstrm.next_in = inBuffer;
    zstrm.avail_in = size;
    zstrm.total_in = 0;

    zstrm.next_out = outBuffer;
    zstrm.avail_out = (uInt)0x500;
    zstrm.total_out = (uInt)0;

    int r;
    r = deflate(&zstrm, 0); //None,  Partial,  Sync,  Full,  Finish,  Block
    r = deflate(&zstrm, 4); //Finish
    deflateEnd(&zstrm);

    for (size_t i = 0; i < zstrm.total_out; i++)
        printf("0x%x,", outBuffer[i]);
    printf("\n");

    printf("hello, world\n");
    return 0;

    /*  printf("Pending: %lu, ", s->pending); //Nanook log
    for (size_t i = 0; i < s->pending; i++)
      printf("0x%x,", s->pending_out[i]);
    printf("\n");
    */
}
