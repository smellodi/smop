﻿using Smop.IonVision;
using System;

namespace Smop.ML;

public class Communicator : IDisposable
{
    public ParameterDefinition? Parameter { get; set; } = null;

    public event EventHandler<Recipe>? RecipeReceived;

    public bool IsConnected => _server.IsClientConnected;


    public Communicator(bool isSimulating)
    {
        if (isSimulating)
        {
            _simulator = new Simulator();
        }

        _server.RecipeReceived += Server_RecipeReceived;
    }

    public async void Config(ChannelProps[] channels)
    {
        await _server.SendAsync(new Config("config", channels));
    }

    public async void Publish(ScanResult scan)
    {
        if (Parameter == null)
        {
            throw new Exception("Parameter is not set");
        }

        var packet = Measurement.From(scan, Parameter);
        await _server.SendAsync(packet);
    }

    public void Dispose()
    {
        _simulator?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Internal

    readonly Simulator? _simulator = null;
    readonly Server _server = new();

    private void Server_RecipeReceived(object? sender, Recipe e)
    {
        RecipeReceived?.Invoke(this, e);
    }
}
