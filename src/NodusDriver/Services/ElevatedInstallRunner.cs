using System.IO;
using System.Security.Principal;
using System.Text.Json;
using NodusDriver.Models;

namespace NodusDriver.Services;

public static class ElevatedInstallRunner
{
    public static async Task<int> RunAsync(string requestPath)
    {
        ElevatedInstallRequest? request = null;
        InstallSummary result;

        try
        {
            if (!IsAdministrator())
                throw new InvalidOperationException("The driver installer was not started with administrator privileges.");

            if (!File.Exists(requestPath))
                throw new FileNotFoundException("The NODUS installation request could not be found.", requestPath);

            var json = await File.ReadAllTextAsync(requestPath);
            request = JsonSerializer.Deserialize<ElevatedInstallRequest>(json)
                ?? throw new InvalidOperationException("The NODUS installation request is invalid.");

            if (request.Updates.Count is < 1 or > 50)
                throw new InvalidOperationException("The installation request contains an invalid number of updates.");

            foreach (var update in request.Updates)
            {
                if (!Guid.TryParse(update.UpdateId, out _))
                    throw new InvalidOperationException("The installation request contains an invalid Windows Update identity.");
            }

            var log = new LogService();
            log.Info($"Elevated installer started for {request.Updates.Count} driver update(s).");
            var service = new WindowsUpdateService(log);
            result = await service.InstallAsync(request.Updates);
        }
        catch (Exception ex)
        {
            result = new InstallSummary(false, false, false, ex.HResult, ex.Message);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ResultPath))
            return result.InstallSucceeded ? 0 : 1;

        try
        {
            var resultDirectory = Path.GetDirectoryName(request.ResultPath);
            if (!string.IsNullOrWhiteSpace(resultDirectory))
                Directory.CreateDirectory(resultDirectory);

            await File.WriteAllTextAsync(request.ResultPath, JsonSerializer.Serialize(result));
        }
        catch
        {
            return 2;
        }

        return result.InstallSucceeded ? 0 : 1;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
