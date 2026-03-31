using System.Diagnostics;
using Microsoft.Extensions.Options;
using NMT_api.Services.Translation.Configuration;

namespace NMT_api.Services.Translation.PythonBridge;

public sealed class PythonBackendProcessHostedService(
    IWebHostEnvironment environment,
    IOptions<PythonTranslationBackendOptions> options,
    ILogger<PythonBackendProcessHostedService> logger) : IHostedService
{
    private Process? _pythonProcess;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        PythonTranslationBackendOptions settings = options.Value;
        if (!environment.IsDevelopment() || !settings.AutoStartInDevelopment)
        {
            return;
        }

        string workingDirectory = Path.GetFullPath(settings.AutoStartWorkingDirectory, environment.ContentRootPath);
        if (!Directory.Exists(workingDirectory))
        {
            logger.LogWarning("Python auto-start skipped. Working directory not found: {WorkingDirectory}", workingDirectory);
            return;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = settings.AutoStartCommand,
            Arguments = settings.AutoStartArguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            _pythonProcess = Process.Start(startInfo);
            if (_pythonProcess is null)
            {
                logger.LogWarning("Python auto-start failed: process creation returned null.");
                return;
            }

            _pythonProcess.OutputDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    logger.LogInformation("[python-backend] {Message}", eventArgs.Data);
                }
            };
            _pythonProcess.ErrorDataReceived += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                {
                    logger.LogWarning("[python-backend] {Message}", eventArgs.Data);
                }
            };

            _pythonProcess.BeginOutputReadLine();
            _pythonProcess.BeginErrorReadLine();

            logger.LogInformation(
                "Python backend auto-started with command '{Command} {Arguments}' in '{WorkingDirectory}'.",
                settings.AutoStartCommand,
                settings.AutoStartArguments,
                workingDirectory);

            if (settings.AutoStartWarmupDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.AutoStartWarmupDelaySeconds), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Python auto-start failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_pythonProcess is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (!_pythonProcess.HasExited)
            {
                _pythonProcess.Kill(entireProcessTree: true);
                logger.LogInformation("Python backend process stopped.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop Python backend process.");
        }

        _pythonProcess.Dispose();
        _pythonProcess = null;
        return Task.CompletedTask;
    }
}
