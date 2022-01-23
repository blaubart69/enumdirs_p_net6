using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spi;

internal class Program
{
    static void EnumdirP(string startPath)
    {
        var paths = System.Threading.Channels.Channel.CreateUnbounded<string>();

        paths.Writer.TryWrite(startPath);
        long count = 1;

        for (int i = 0; i < 15; ++i)
        {
            Task.Run(async () =>
            {
               for (; ; )
               {
                   string path = await paths.Reader.ReadAsync();
                   foreach (string filename in System.IO.Directory.EnumerateFileSystemEntries(path, "*"
                   , new EnumerationOptions()
                   {
                       RecurseSubdirectories = false,
                       ReturnSpecialDirectories = false,
                       AttributesToSkip = 0
                   }))
                   {
                       var fi = new FileInfo(filename);
                       if (fi.Attributes.HasFlag(FileAttributes.Directory))
                       {
                           Interlocked.Increment(ref count);
                           paths.Writer.TryWrite(filename);
                           continue;
                       }
                       Console.WriteLine($"{fi.Length,12:n0}\t{filename}");
                   }
                   if (Interlocked.Decrement(ref count) == 0)
                   {
                       paths.Writer.Complete();
                   }
               }
           });
        }
        paths.Reader.Completion.Wait();
    }
    static void Main(string[] args)
    {
        string path = args.Length switch
        {
            0 => ".",
            _ => args[0]
        };
        enumdir_p(path);
    }
}
