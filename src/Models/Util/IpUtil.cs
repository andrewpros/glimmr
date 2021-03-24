﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Serilog;

namespace Glimmr.Models.Util {
    public static class IpUtil {
        public static IPEndPoint Parse(string endpoint, int portIn) {
            if (string.IsNullOrEmpty(endpoint)
                || endpoint.Trim().Length == 0) {
                throw new ArgumentException("Endpoint descriptor may not be empty.");
            }

            if (portIn != -1 &&
                (portIn < IPEndPoint.MinPort
                 || portIn > IPEndPoint.MaxPort)) {
                throw new ArgumentException($"Invalid default port '{portIn}'");
            }

            string[] values = endpoint.Split(new[] {':'});
            IPAddress ipAddress;
            int port;

            switch (values.Length) {
                //check if we have an IPv6 or ports
                // ipv4 or hostname
                case <= 2: {
                    port = values.Length == 1 ? portIn : GetPort(values[1]);

                    //try to use the address as IPv4, otherwise get hostname
                    if (!IPAddress.TryParse(values[0], out ipAddress))
                        ipAddress = GetIpFromHost(values[0]);
                    if (ipAddress == null) return null;
                    break;
                }
                //ipv6
                //could [a:b:c]:d
                case > 2 when values[0].StartsWith("[") && values[^2].EndsWith("]"): {
                    var ipString
                        = string.Join(":", values.Take(values.Length - 1).ToArray());
                    ipAddress = IPAddress.Parse(ipString);
                    port = GetPort(values[^1]);
                    break;
                }
                //[a:b:c] or a:b:c
                case > 2:
                    ipAddress = IPAddress.Parse(endpoint);
                    port = portIn;
                    break;
            }

            if (port == -1)
                throw new ArgumentException($"No port specified: '{endpoint}'");

            return new IPEndPoint(ipAddress, port);
        }

        private static int GetPort(string p) {
            if (!int.TryParse(p, out var port)
                || port < IPEndPoint.MinPort
                || port > IPEndPoint.MaxPort) {
                throw new FormatException($@"Invalid end point port '{p}'");
            }

            return port;
        }

        public static IPAddress GetIpFromHost(string p) {
            if (string.IsNullOrEmpty(p)) return null;
            try {
                var hosts = Dns.GetHostAddresses(p);

                // Use the first address like always if found
                if (hosts.Length > 0) {
                    return hosts[0];
                }

                // If not, try this
                var dns = Dns.GetHostEntry(p);
                if (dns.AddressList.Length > 0) {
                    return dns.AddressList[0];
                }
                
                // If still no dice, try getting appending .local
                dns = Dns.GetHostEntry(p + ".local");
                if (dns.AddressList.Length > 0) {
                    return dns.AddressList[0];
                }
                
                
            } catch (Exception e) {
                Log.Debug("DNS Res ex: " + e.Message);
            }

            return null;
        }

       
        public static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            throw new Exception("No network adapters found in " + JsonConvert.SerializeObject(host));
        }
       
    }
}