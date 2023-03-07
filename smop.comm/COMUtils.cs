using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;

namespace SMOP.Comm
{
    /// <summary>
    /// COM port utils: list of available ports, and firing events when this list changes.
    /// </summary>
    public class COMUtils
    {
        /// <summary>
        /// Port descriptor
        /// </summary>
        public class Port
        {
            public string Name { get; }
            public string? Description { get; }
            public string? Manufacturer { get; }
            public Port(string name, string? description, string? manufacturer)
            {
                Name = name;
                Description = description;
                Manufacturer = manufacturer;
            }
        }

        /// <summary>
        /// Fires when a COM port becomes available
        /// </summary>
        public event EventHandler<Port>? Inserted;
        /// <summary>
        /// Fires when a COM port is removed
        /// </summary>
        public event EventHandler<Port>? Removed;

        /// <summary>
        /// List of all COM ports in the system
        /// </summary>
        public static Port[] Ports => _cachedPorts ??= GetAvailableCOMPorts();

        /// <summary>
        /// Most likely SMOP port, i.e. the one that has a known description
        /// </summary>
        public static Port? SMOPPort => Ports.FirstOrDefault(port => port.Description?.Contains("Smellodi Odor Printer") ?? false);

        public COMUtils()
        {
            Listen("__InstanceCreationEvent", "Win32_SerialPort", ActionType.Inserted);
            Listen("__InstanceDeletionEvent", "Win32_SerialPort", ActionType.Removed);
        }

        // Internal

        private enum ActionType
        {
            Inserted,
            Removed
        }

        static Port[]? _cachedPorts = null;

        private void Listen(string source, string target, ActionType actionType)
        {
            var query = new WqlEventQuery($"SELECT * FROM {source} WITHIN 2 WHERE TargetInstance ISA '{target}'");
            var watcher = new ManagementEventWatcher(query);

            watcher.EventArrived += (s, e) =>
            {
                _cachedPorts = null;
                Port? port = null;

                try
                {
                    var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                    port = CreateCOMPort(target.Properties);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("USB ERROR: " + ex.Message);
                }

                if (port != null)
                {
                    switch (actionType)
                    {
                        case ActionType.Inserted:
                            Inserted?.Invoke(this, port);
                            break;
                        case ActionType.Removed:
                            Removed?.Invoke(this, port);
                            break;
                    }
                }
            };

            watcher.Start();
        }

        private static Port[] GetAvailableCOMPorts()
        {
            var portNames = SerialPort.GetPortNames();

            IEnumerable<Port>? ports = null;

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%' OR Caption LIKE '%Smellodi%'");
                ManagementBaseObject[]? records = searcher.Get().Cast<ManagementBaseObject>().ToArray();
                //foreach (var rec in records)
                //    PrintProperties(rec.Properties);
                ports = records.Select(rec =>
                    {
                        var name = 
                            portNames.FirstOrDefault(name => rec["Caption"]?.ToString()?.Contains($"({name})") ?? false) ??
                            portNames.FirstOrDefault(name => rec["DeviceID"]?.ToString()?.Contains($"{name}") ?? false) ?? 
                            "";
                        var description = rec["Description"]?.ToString();
                        var manufacturer = rec["Manufacturer"]?.ToString();
                        return new Port(name, description, manufacturer);
                    })
                    .Where(p => p.Name.StartsWith("COM"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USB ERROR: " + ex.Message);
            }

            return ports?.ToArray() ?? new Port[] { };
        }

        private static Port? CreateCOMPort(PropertyDataCollection props)
        {
            string? deviceID = null;
            string? descrition = null;
            string? manufacturer = null;

            foreach (PropertyData property in props)
            {
                if (property.Name == "DeviceID")
                {
                    deviceID = (string?)property.Value;
                }
                else if (property.Name == "Description")
                {
                    descrition = (string?)property.Value;
                }
                else if (property.Name == "Manufacturer")
                {
                    manufacturer = (string?)property.Value;
                }
            }

            return deviceID == null ? null : new Port(deviceID, descrition, manufacturer);
        }

        // Debugging

        static HashSet<string> PropsToPrint = new() { "Caption", "Description", "Manufacturer", "Name", "Service"};
        static HashSet<string> ManufacturersToPrint = new() { "microsoft" };
        static HashSet<string> ManufacturersNotToPrint = new() { "microsoft", "standard", "(standard", "intel", "acer", "rivet", "nvidia", "realtek", "generic" };

        /*
        static COMUtils()
        {
            Console.WriteLine("==== PnP devices ===");
            using var pnp = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
            var records = pnp.Get().Cast<ManagementBaseObject>().ToArray();
            foreach (var rec in records)
                PrintProperties(rec.Properties);
            Console.WriteLine("====================");
        }
        */

        static void PrintProperties(PropertyDataCollection props)
        {
            var indent = "    ";
            var man = props["Manufacturer"];
            //if (ManufacturersNotToPrint.Any(m => man?.Value?.ToString()?.ToLower().StartsWith(m) ?? false))
            //    return;
            //if (!ManufacturersToPrint.Any(m => man?.Value?.ToString()?.ToLower().StartsWith(m) ?? false))
            //    return;

            foreach (PropertyData p in props)
            {
                //if (!PropsToPrint.Contains(p.Name))
                //    continue;
                if (p.IsArray)
                {
                    Console.WriteLine($"{indent}{p.Name}: ({p.Type})");
                    if (p.Value != null)
                    {
                        if (p.Value is string[] strings)
                            Console.WriteLine($"{indent}{indent}{string.Join($"\n{indent}{indent}", strings)}");
                        else if (p.Value is ushort[] words)
                            Console.WriteLine($"{indent}{indent}{string.Join($"\n{indent}{indent}", words)}");
                        else if (p.Value is uint[] dwords)
                            Console.WriteLine($"{indent}{indent}{string.Join($"\n{indent}{indent}", dwords)}");
                        else if (p.Value is ulong[] qwords)
                            Console.WriteLine($"{indent}{indent}{string.Join($"\n{indent}{indent}", qwords)}");
                        else
                            Console.WriteLine($"{indent}{indent}{string.Join($"\n{indent}{indent}", (IEnumerable)p.Value)}");
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{indent}none");
                    }
                }
                else
                {
                    Console.WriteLine($"{indent}{p.Name} = {p.Value?.ToString()} ({p.Type})");
                }
            }
            Console.WriteLine();
        }
    }
}