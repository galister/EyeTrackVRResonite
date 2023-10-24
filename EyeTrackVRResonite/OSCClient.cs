using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Elements.Core;
using OscCore;
using OscCore.LowLevel;

namespace EyeTrackVRResonite
{
    // Credit to yewnyx on the VRC OSC Discord for this

    public class ETVROSC
    {
        private static bool _oscSocketState;
        public readonly object Lock = new();
        public readonly Dictionary<string, float> Parameters = new();
        public readonly float2[] EyeLeftRightEuler = new float2[2];
        public DateTime LastUpdate = DateTime.MinValue;

        private static UdpClient? _receiver;
        private static Task? _task;

        private const int DefaultPort = 9000;

        public ETVROSC(int? port = null)
        {
            if (_receiver != null)
            {
                return;
            }

            IPAddress.TryParse("127.0.0.1", out var candidate);

            _receiver = port.HasValue
                ? new UdpClient(new IPEndPoint(candidate, port.Value))
                : new UdpClient(new IPEndPoint(candidate, DefaultPort));

            _oscSocketState = true;
            _task = Task.Run(ListenLoop);
        }

        private async void ListenLoop()
        {
            UniLog.Log("Started EyeTrackVR loop");
            while (_oscSocketState)
            {
                var result = await _receiver.ReceiveAsync();
                var bytes = new System.ArraySegment<byte>(result.Buffer, 0, result.Buffer.Length);
                if (IsBundle(bytes))
                {
                    var bundle = new OscBundleRaw(bytes);
                    foreach (var message in bundle)
                        ProcessOscMessage(message);
                }
                else
                {
                    var message = new OscMessageRaw(bytes);
                    ProcessOscMessage(message);
                }
            }
        }

        private const string PARAM_PREFIX = "/avatar/parameters/FT/v2/";
        private void ProcessOscMessage(OscMessageRaw message)
        {

            if (message.Address == "/tracking/eye/LeftRightPitchYaw")
            {
                var arg0 = message[0];
                var arg1 = message[1];
                var arg2 = message[2];
                var arg3 = message[3];

                EyeLeftRightEuler[0] = new float2(message.ReadFloat(ref arg0), message.ReadFloat(ref arg1));
                EyeLeftRightEuler[1] = new float2(message.ReadFloat(ref arg2), message.ReadFloat(ref arg3));
                LastUpdate = DateTime.Now;
                return;
            }
            else if (!message.Address.StartsWith(PARAM_PREFIX))
                return;

            var address = message.Address.Substring(PARAM_PREFIX.Length);
            var arg = message[0];
            switch (arg.Type)
            {
                case (OscToken.Float):
                    lock (Lock)
                    {
                        Parameters[address] = message.ReadFloat(ref arg);
                    }
                    break;
                case (OscToken.Int):
                    lock (Lock)
                    {
                        Parameters[address] = message.ReadInt(ref arg);
                    }
                    break;
                default:
                    break;
            }
        }

        private static readonly byte[] BundlePrefix = Encoding.ASCII.GetBytes("#bundle");
        private static bool IsBundle(System.ArraySegment<byte> bytes)
        {
            var prefix = BundlePrefix;
            if (bytes.Count < prefix.Length)
                return false;

            var i = 0;
            foreach (var b in bytes)
            {
                if (i < prefix.Length && b != prefix[i++])
                    return false;
                if (i == prefix.Length)
                    break;
            }
            return true;
        }


        public static void Teardown()
        {
            UniLog.Log("EyeTrackVR teardown called");
            _oscSocketState = false;
            _receiver.Close();
            _task.Wait();
            UniLog.Log("EyeTrackVR teardown completed");
        }
    }
}
