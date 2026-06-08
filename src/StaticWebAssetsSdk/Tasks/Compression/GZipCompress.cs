// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO.Compression;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[MSBuildMultiThreadableTask]
public class GZipCompress : Task, IMultiThreadableTask
{
    /// <inheritdoc/>
    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

    [Required]
    public ITaskItem[] FilesToCompress { get; set; }

    public override bool Execute()
    {
        var outputDirectories = FilesToCompress
            .Select(f => Path.GetDirectoryName(f.ItemSpec))
            .Where(td => !string.IsNullOrWhiteSpace(td))
            .Distinct();

        foreach (var outputDirectory in outputDirectories)
        {
            Directory.CreateDirectory(TaskEnvironment.GetAbsolutePath(outputDirectory));
            Log.LogMessage(MessageImportance.Low, "Created directory '{0}'.", outputDirectory);
        }

        Parallel.For(0, FilesToCompress.Length, i =>
        {
            var file = FilesToCompress[i];
            var outputRelativePath = file.ItemSpec;

            if (!AssetToCompress.TryFindInputFilePath(file, TaskEnvironment, Log, out var inputFullPath))
            {
                return;
            }

            AbsolutePath inputAbsolutePath = TaskEnvironment.GetAbsolutePath(inputFullPath);
            AbsolutePath outputAbsolutePath = TaskEnvironment.GetAbsolutePath(outputRelativePath);

            if (!File.Exists(outputAbsolutePath))
            {
                Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because compressed file '{1}' does not exist.", inputFullPath, outputRelativePath);
            }
            else if (File.GetLastWriteTimeUtc(inputAbsolutePath) < File.GetLastWriteTimeUtc(outputAbsolutePath))
            {
                // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                Log.LogMessage(MessageImportance.Low, "Skipping '{0}' because '{1}' is newer than '{2}'.", inputFullPath, outputRelativePath, inputFullPath);
                return;
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
            }

            try
            {
                using var sourceStream = File.OpenRead(inputAbsolutePath);
                using var fileStream = File.Create(outputAbsolutePath);
                using var stream = new GZipStream(fileStream, CompressionLevel.Optimal);

                sourceStream.CopyTo(stream);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
                return;
            }
        });

        return !Log.HasLoggedErrors;
    }
}
