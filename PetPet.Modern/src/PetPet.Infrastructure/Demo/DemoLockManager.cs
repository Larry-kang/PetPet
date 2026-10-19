using System.Diagnostics;

namespace PetPet.Infrastructure.Demo;

public static class DemoLockManager
{
    private static FileStream? _runLockStream;

    public static FileStream AcquireRunLock(string demoDirectory)
    {
        Directory.CreateDirectory(demoDirectory);
        var lockPath = Path.Combine(demoDirectory, ".run.lock");
        try
        {
            _runLockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return _runLockStream;
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"無法取得 Demo 執行鎖 (.run.lock)。可能已有另一個 PetPet Demo 執行個體正在執行。\n路徑: {lockPath}\n錯誤: {ex.Message}", ex);
        }
    }

    public static void ReleaseRunLock()
    {
        _runLockStream?.Dispose();
        _runLockStream = null;
    }

    public static IDisposable AcquireInitLock(string demoDirectory, int timeoutSeconds = 10)
    {
        Directory.CreateDirectory(demoDirectory);
        var lockPath = Path.Combine(demoDirectory, ".init.lock");
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return stream;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }

        throw new TimeoutException($"初始化鎖 (.init.lock) 等候逾時 ({timeoutSeconds} 秒)。另一個初始化程序可能正在執行或已終止。");
    }
}
