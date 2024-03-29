﻿using Smop.MainApp.Controllers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IVScan = Smop.IonVision.Defs.ScanResult;

namespace Smop.MainApp.Logging;

internal class DmsCache
{
    public static bool IsEnabled
    {
        get => Properties.Settings.Default.Reproduction_UseDmsCache;
        set
        {
            Properties.Settings.Default.Reproduction_UseDmsCache = value;
            Properties.Settings.Default.Save();
        }
    }

    public DmsCache()
    {
        if (!Directory.Exists(ROOT_FOLDER))
        {
            Directory.CreateDirectory(ROOT_FOLDER);
        }
    }

    public void SetSubfolder(int rows, int cols)
    {
        _subfolder = Path.Combine(ROOT_FOLDER, $"{rows}x{cols}");
        if (!Directory.Exists(_subfolder))
        {
            Directory.CreateDirectory(_subfolder);
        }
    }

    public IVScan? Find(OdorChannels odorChannels, out string? filename)
    {
        if (!IsEnabled)
        {
            filename = null;
            return null;
        }

        var id = ToId(odorChannels.Select(odorChannel => ToChannelId((int)odorChannel.ID, odorChannel.Flow)));
        return Find(id, out filename);
    }

    public IVScan? Find(ML.Recipe recipe, out string? filename)
    {
        if (!IsEnabled)
        {
            filename = null;
            return null;
        }

        if (recipe.Channels != null)
        {
            var id = ToId(recipe.Channels.Select(ch => ToChannelId(ch.Id, ch.Flow)));
            return Find(id, out filename);
        }

        filename = null;
        return null;
    }

    public string? Save(OdorChannels odorChannels, IVScan scan)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var id = ToId(odorChannels.Select(odorChannel => ToChannelId((int)odorChannel.ID, odorChannel.Flow)));
        var filename = Save(id, scan);

        return filename;
    }

    public string? Save(ML.Recipe recipe, IVScan scan)
    {
        if (!IsEnabled)
        {
            return null;
        }

        string? filename = null;
        if (recipe.Channels != null)
        {
            var id = ToId(recipe.Channels.Select(ch => ToChannelId(ch.Id, ch.Flow)));
            filename = Save(id, scan);
        }

        return filename;
    }

    // Internal

    static readonly string ROOT_FOLDER = "cache";

    string _subfolder = ROOT_FOLDER;


    private static string ToChannelId(int id, double flow) => $"{id}-{flow}";
    private static string ToId(IEnumerable<string> list) => string.Join('x', list);
    private static string ToFilename(string id) => $"dms_{id}.json";

    private IVScan? Find(string id, out string? filename)
    {
        filename = ToFilename(id);
        var cachedFilename = Path.Combine(_subfolder, filename);

        if (File.Exists(cachedFilename))
        {
            using StreamReader reader = new(cachedFilename);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<IVScan>(json);
        }

        filename = null;
        return null;
    }

    private string Save(string id, IVScan scan)
    {
        string filename = ToFilename(id);
        var cachedFilename = Path.Combine(_subfolder, filename);

        var json = JsonSerializer.Serialize(scan);
        using StreamWriter writer = new(cachedFilename);
        writer.Write(json);

        return filename;
    }
}