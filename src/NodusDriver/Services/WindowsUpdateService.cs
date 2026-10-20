using System.Runtime.InteropServices;
using NodusDriver.Models;

namespace NodusDriver.Services;

public sealed class WindowsUpdateService
{
    private readonly LogService _log;

    public WindowsUpdateService(LogService log) => _log = log;

    public Task<IReadOnlyList<DriverUpdate>> SearchDriverUpdatesAsync() => Task.Run<IReadOnlyList<DriverUpdate>>(() =>
    {
        dynamic? session = null;
        dynamic? searcher = null;
        dynamic? searchResult = null;

        try
        {
            session = CreateComObject("Microsoft.Update.Session");
            session.ClientApplicationID = "NODUS Driver";
            searcher = session.CreateUpdateSearcher();
            searcher.Online = true;

            _log.Info("Starting Windows Update Agent driver scan.");
            searchResult = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'");

            var updates = new List<DriverUpdate>();
            for (var i = 0; i < (int)searchResult.Updates.Count; i++)
            {
                dynamic update = searchResult.Updates.Item(i);
                try
                {
                    var identity = update.Identity;
                    updates.Add(new DriverUpdate(
                        (string)identity.UpdateID,
                        (int)identity.RevisionNumber,
                        SafeString(() => (string)update.Title, "Driver update"),
                        SafeString(() => (string)update.DriverManufacturer, "Microsoft Update"),
                        SafeString(() => (string)update.DriverModel, "Compatible hardware"),
                        SafeString(() => (string)update.DriverClass, "Driver"),
                        SafeString(() => ((DateTime)update.DriverVerDate).ToString("yyyy-MM-dd"), "—"),
                        SafeBool(() => (bool)update.RebootRequired),
                        SafeLong(() => (long)update.MaxDownloadSize)));
                }
                finally
                {
                    ReleaseCom(update);
                }
            }

            _log.Info($"Windows Update Agent returned {updates.Count} driver updates.");
            return updates;
        }
        catch (Exception ex)
        {
            _log.Error("Driver scan failed.", ex);
            throw new InvalidOperationException(FriendlyComMessage(ex), ex);
        }
        finally
        {
            ReleaseCom(searchResult);
            ReleaseCom(searcher);
            ReleaseCom(session);
        }
    });

    public Task<InstallSummary> InstallAsync(IEnumerable<DriverUpdate> selected) => Task.Run(() =>
    {
        var requested = selected.ToList();
        if (requested.Count == 0)
            return new InstallSummary(false, false, false, 0, "No updates selected.");

        dynamic? session = null;
        dynamic? searcher = null;
        dynamic? collection = null;
        dynamic? downloader = null;
        dynamic? downloadResult = null;
        dynamic? installer = null;
        dynamic? installResult = null;

        try
        {
            session = CreateComObject("Microsoft.Update.Session");
            session.ClientApplicationID = "NODUS Driver";
            searcher = session.CreateUpdateSearcher();
            searcher.Online = true;
            collection = CreateComObject("Microsoft.Update.UpdateColl");

            foreach (var target in requested)
            {
                dynamic? searchResult = null;
                try
                {
                    var criteria = $"UpdateID='{target.UpdateId}' and RevisionNumber={target.RevisionNumber}";
                    searchResult = searcher.Search(criteria);
                    if ((int)searchResult.Updates.Count == 0)
                    {
                        _log.Info($"Selected update no longer applicable: {target.Title}");
                        continue;
                    }

                    dynamic update = searchResult.Updates.Item(0);
                    try
                    {
                        if (!SafeBool(() => (bool)update.EulaAccepted))
                            update.AcceptEula();
                        collection.Add(update);
                    }
                    finally
                    {
                        ReleaseCom(update);
                    }
                }
                finally
                {
                    ReleaseCom(searchResult);
                }
            }

            if ((int)collection.Count == 0)
                return new InstallSummary(false, false, false, 0, "The selected updates are no longer applicable.");

            _log.Info($"Downloading {collection.Count} selected driver update(s).");
            downloader = session.CreateUpdateDownloader();
            downloader.Updates = collection;
            downloadResult = downloader.Download();
            var downloadCode = (int)downloadResult.ResultCode;
            var downloadOk = downloadCode is 2 or 3;
            if (!downloadOk)
                return new InstallSummary(false, false, false, downloadCode, $"Driver download failed (result {downloadCode}).");

            _log.Info("Download complete. Starting install.");
            installer = session.CreateUpdateInstaller();
            installer.Updates = collection;
            installResult = installer.Install();

            var installCode = (int)installResult.ResultCode;
            var installOk = installCode is 2 or 3;
            var reboot = SafeBool(() => (bool)installResult.RebootRequired);
            _log.Info($"Install result={installCode}; reboot={reboot}.");

            return new InstallSummary(true, installOk, reboot, installCode,
                installOk
                    ? reboot ? "Drivers installed. Windows needs a restart." : "Drivers installed successfully."
                    : $"Installation finished with result code {installCode}.");
        }
        catch (Exception ex)
        {
            _log.Error("Driver installation failed.", ex);
            return new InstallSummary(false, false, false, Marshal.GetHRForException(ex), FriendlyComMessage(ex));
        }
        finally
        {
            ReleaseCom(installResult);
            ReleaseCom(installer);
            ReleaseCom(downloadResult);
            ReleaseCom(downloader);
            ReleaseCom(collection);
            ReleaseCom(searcher);
            ReleaseCom(session);
        }
    });

    private static dynamic CreateComObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: true)!;
        return Activator.CreateInstance(type)!;
    }

    private static string SafeString(Func<string> read, string fallback)
    {
        try
        {
            var value = read();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch { return fallback; }
    }

    private static bool SafeBool(Func<bool> read)
    {
        try { return read(); }
        catch { return false; }
    }

    private static long SafeLong(Func<long> read)
    {
        try { return read(); }
        catch { return 0; }
    }

    private static string FriendlyComMessage(Exception ex)
    {
        var root = ex.InnerException ?? ex;
        var hr = Marshal.GetHRForException(root);
        return $"Windows Update could not complete the operation (0x{hr:X8}). {root.Message}";
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
