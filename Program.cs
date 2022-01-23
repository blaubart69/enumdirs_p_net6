using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spi;

struct Options
{
    public bool summarize;
}

class Stats
{
    public UInt64 files;
    public UInt64 dirs;
    public UInt64 filesize;
}

internal class Program
{
    static void PrintStats(Stats stats)
    {
        Console.WriteLine(
                $"dirs  {stats.dirs,12:n0}" +
              $"\nfiles {stats.files,12:n0}" +
              $"\nsize  {stats.filesize,12:n0}"
        );
    }
    static void EnumdirP(string startPath, Stats stats, Options opts)
    {
        var paths = System.Threading.Channels.Channel.CreateUnbounded<string>();

        paths.Writer.TryWrite(startPath);
        long count = 1;

        for (int i = 0; i < 16; ++i)
        {
            Task.Run( async () =>
            {
                for (;;)
                {
                    try
                    {
                        string path;
                        try
                        {
                            path = await paths.Reader.ReadAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"X: ReadAsync {ex}");
                            break;
                        }

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
                                Interlocked.Increment(ref stats.dirs);
                                if (fi.LinkTarget == null) // don't follow links
                                {
                                    Interlocked.Increment(ref count);
                                    paths.Writer.TryWrite(filename);
                                    continue;
                                }
                            }
                            else
                            {
                                Interlocked.Increment(ref stats.files);
                                Interlocked.Add(ref stats.filesize, (ulong)fi.Length);
                                if (!opts.summarize)
                                {
                                    Console.WriteLine($"{fi.Length,12:n0}\t{filename}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"X: Task-EnumerateOneDirectory: {ex.Message}");
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref count) == 0)
                        {
                            paths.Writer.Complete();
                        }
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
        Stats stats = new Stats();
        Options opts = new Options()
        {
            summarize = true
        };
        EnumdirP(path, stats, opts);
        PrintStats(stats);
    }
}
