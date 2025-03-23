using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;

namespace EyeTrackVRResonite
{
    public class EyeTrackVR : ResoniteMod
    {
        public override string Name => "EyeTrackVRResonite";
        public override string Author => "PLYSHKA + dfgHiatus";
        public override string Version => "2.0.0";
        public override string Link => "https://github.com/Meister1593/EyeTrackVRResonite";

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            new Harmony("net.plyshka.EyeTrackVRResonite").PatchAll();
            Engine.Current.OnShutdown += ETVROSC.Teardown;
        }

        private static ETVROSC _etvr;
        private static ModConfiguration _config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ModEnabled = new("enabled", "Mod Enabled", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Alpha = new("alpha", "Eye Swing Multiplier X", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Beta = new("beta", "Eye Swing Multiplier Y", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> OscPort = new("osc_port", "EyeTrackVR OSC port", () => 9000);

        public static ValueStream<float> CreateStream(World world, string parameter)
        {
            return world.LocalUser.GetStreamOrAdd<ValueStream<float>>(parameter, stream =>
            {
                stream.Name = parameter;
                stream.SetUpdatePeriod(0, 0);
                stream.Encoding = ValueEncoding.Quantized;
                stream.FullFrameBits = 10;
                stream.FullFrameMin = -1;
                stream.FullFrameMax = 1;
            });
        }

        public static void CreateVariable(Slot dvslot, string parameter, ValueStream<float> stream)
        {
            var dv = dvslot.AttachComponent<DynamicValueVariable<float>>();
            dv.VariableName.Value = "User/" + parameter;
            var dvdriver = dvslot.AttachComponent<ValueDriver<float>>();
            dvdriver.ValueSource.Target = stream;
            dvdriver.DriveTarget.Target = dv.Value;
        }

        [HarmonyPatch(typeof(InputInterface), MethodType.Constructor)]
        [HarmonyPatch(new[] { typeof(Engine) })]
        public class InputInterfaceCtorPatch
        {
            public static void Postfix(InputInterface __instance)
            {
                try
                {
                    _etvr = new ETVROSC(_config.GetValue(OscPort));
                    var gen = new EyeTrackVRInterface();
                    __instance.RegisterInputDriver(gen);
                }
                catch (Exception e)
                {
                    Warn("Module failed to initialize.");
                    Warn(e.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(UserRoot), "OnStart")]
        class VRCFTReceiverPatch
        {
            public static void Postfix(UserRoot __instance)
            {
                if (!__instance.ActiveUser.IsLocalUser) return;

                var dvslot = __instance.Slot.FindChildOrAdd("VRCFTReceiver", true);

                if (!EyeTrackVRInterface.VRCFTDictionary.TryGetValue(__instance.World, out var lookup))
                {
                    lookup = new();
                    EyeTrackVRInterface.VRCFTDictionary[__instance.World] = lookup;
                }

                foreach (var transofrmers in EyeTrackVRInterface.FaceTrackParams.Values)
                {
                    foreach (var transformer in transofrmers)
                    {
                        var pair = transformer.Invoke(0);

                        var stream = CreateStream(__instance.World, pair.Key);
                        CreateVariable(dvslot, pair.Key, stream);
                        lookup[pair.Key] = stream;
                    }
                }
            }
        }

        private class EyeTrackVRInterface : IInputDriver
        {
            private Eyes _eyes;
            private const float DefaultPupilSize = 0.0035f;
            public int UpdateOrder => 100;
            public static Dictionary<World, Dictionary<string, ValueStream<float>>> VRCFTDictionary = new();
            private List<KeyValuePair<string, float>> _etvrParameters = new();

            public static readonly Dictionary<string, Func<float, KeyValuePair<string, float>>[]> FaceTrackParams = new()
            {
                ["SmileSadLeft"] = new[] { MkParam("SmileLeft", 0, 3), MkParam("SadLeft", 0, -3) },
                ["SmileSadRight"] = new[] { MkParam("SmileRight", 0, 3), MkParam("SadRight", 0, -3) },
                ["BrowExpressionLeft"] = new[] { MkParam("BrowUpLeft", 0, 1), MkParam("BrowDownLeft", 0, -1) },
                ["BrowExpressionRight"] = new[] { MkParam("BrowUpRight", 0, 1), MkParam("BrowDownRight", 0, -1) },
                ["MouthStretchTightenLeft"] = new[] { MkParam("MouthTightenLeft", 1, -2) },
                ["MouthStretchTightenRight"] = new[] { MkParam("MouthTightenRight", 1, -2) },
                ["MouthClosed"] = new[] { MkParam("MouthClosed") },
                ["MouthUpperUp"] = new[] { MkParam("MouthUpperUp") },
                ["MouthLowerDown"] = new[] { MkParam("MouthLowerDown") },
                ["MouthX"] = new[] { MkParam("MouthRight", 0, 1), MkParam("MouthLeft", 0, -1) },
                ["JawX"] = new[] { MkParam("JawRight", 0, 1), MkParam("JawLeft", 0, -1) },
                ["JawOpen"] = new[] { MkParam("JawOpen") },
                ["CheekPuffLeft"] = new[] { MkParam("CheekPuffLeft") },
                ["CheekPuffRight"] = new[] { MkParam("CheekPuffRight") },
                ["LipPucker"] = new[] { MkParam("LipPucker") },
                ["LipFunnelUpper"] = new[] { MkParam("LipFunnelUpper") },
                ["LipFunnelLower"] = new[] { MkParam("LipFunnelLower") },
            };

            public void CollectDeviceInfos(DataTreeList list)
            {
                var eyeDataTreeDictionary = new DataTreeDictionary();
                eyeDataTreeDictionary.Add("Name", "EyeTrackVR Eye Tracking");
                eyeDataTreeDictionary.Add("Type", "Eye Tracking");
                eyeDataTreeDictionary.Add("Model", "ETVR Module");
                list.Add(eyeDataTreeDictionary);
            }

            public void RegisterInputs(InputInterface inputInterface)
            {
                _eyes = new Eyes(inputInterface, "EyeTrackVR Tracking", true);
            }

            public void UpdateInputs(float deltaTime)
            {

                var focus = Engine.Current.WorldManager?.FocusedWorld;
                // If world is not available
                if (focus != null)
                {
                    // Get or create lookup for world
                    if (!VRCFTDictionary.TryGetValue(focus, out var lookup))
                    {
                        lookup = new();
                        VRCFTDictionary[focus] = lookup;
                    }

                    // user root if null
                    if (focus.LocalUser.Root == null)
                    {
                        Warn("Root not Found");
                        return;
                    }

                    lock (_etvr.Lock)
                    {
                        _etvrParameters.Clear();
                        _etvrParameters.AddRange(_etvr.Parameters);
                    }

                    foreach (var oscParam in _etvrParameters)
                    {
                        if (!FaceTrackParams.TryGetValue(oscParam.Key, out var transformers))
                            continue;

                        foreach (var transformer in transformers)
                        {
                            var param = transformer(oscParam.Value);
                            if (!lookup.TryGetValue(param.Key, out var stream) || (stream != null && stream.IsRemoved))
                            {
                                lookup[param.Key] = null;
                                focus.RunInUpdates(0, () =>
                                {
                                    var s = CreateStream(focus, param.Key);
                                    lookup[param.Key] = s;
                                });
                            }
                            if (stream != null)
                            {
                                stream.Value = param.Value;
                                stream.ForceUpdate();
                            }
                        }
                    }
                }

                _eyes.CombinedEye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
                _eyes.CombinedEye.IsTracking = _etvr.LastUpdate > DateTime.Now.AddSeconds(-5);
                _eyes.CombinedEye.PupilDiameter = DefaultPupilSize;

                _eyes.LeftEye.RawPosition = float3.Zero;
                _eyes.RightEye.RawPosition = float3.Zero;

                var eyeLidLeft = Parameter("EyeLidLeft");
                var eyeLidRight = Parameter("EyeLidRight");

                _eyes.LeftEye.Openness = MathX.Remap(eyeLidLeft, 0f, 0.75f, 0f, 1f);
                _eyes.RightEye.Openness = MathX.Remap(eyeLidRight, 0f, 0.75f, 0f, 1f);

                _eyes.LeftEye.Widen = MathX.Remap(eyeLidLeft, 0.75f, 1f, 0f, 1f);
                _eyes.RightEye.Widen = MathX.Remap(eyeLidRight, 0.75f, 1f, 0f, 1f);

                var eyeSquintLeft = Parameter("EyeSquintLeft");
                var eyeSquintRight = Parameter("EyeSquintRight");

                _eyes.LeftEye.Squeeze = eyeSquintLeft;
                _eyes.RightEye.Squeeze = eyeSquintRight;
                _eyes.LeftEye.Frown = eyeSquintLeft;
                _eyes.RightEye.Frown = eyeSquintRight;

                var leftEyeRot = floatQ.Euler(
                    _etvr.EyeLeftRightEuler[0].x,
                    _etvr.EyeLeftRightEuler[0].y,
                    0);
                var rightEyeRot = floatQ.Euler(
                    _etvr.EyeLeftRightEuler[1].x,
                    _etvr.EyeLeftRightEuler[1].y,
                    0);

                _eyes.LeftEye.UpdateWithRotation(leftEyeRot);
                _eyes.RightEye.UpdateWithRotation(rightEyeRot);
                _eyes.CombinedEye.UpdateWithRotation(leftEyeRot);

                CombineEyeData();

                _eyes.ConvergenceDistance = 0f;
                _eyes.Timestamp += deltaTime;
                _eyes.FinishUpdate();
            }

            private float Parameter(string key)
            {
                if (_etvr.Parameters.TryGetValue(key, out var val))
                    return val;
                return 0;
            }

            private void CombineEyeData()
            {
                _eyes.IsEyeTrackingActive = _eyes.CombinedEye.IsTracking;
                _eyes.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.IsTracking = _eyes.CombinedEye.IsTracking;

                _eyes.LeftEye.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.RightEye.IsDeviceActive = _eyes.CombinedEye.IsDeviceActive;
                _eyes.LeftEye.IsTracking = _eyes.CombinedEye.IsTracking;
                _eyes.RightEye.IsTracking = _eyes.CombinedEye.IsTracking;
                _eyes.LeftEye.PupilDiameter = _eyes.CombinedEye.PupilDiameter;
                _eyes.RightEye.PupilDiameter = _eyes.CombinedEye.PupilDiameter;

                _eyes.CombinedEye.IsTracking = false;

                _eyes.CombinedEye.RawPosition = MathX.Average(_eyes.LeftEye.RawPosition, _eyes.RightEye.RawPosition);

                _eyes.CombinedEye.Openness = MathX.Average(_eyes.LeftEye.Openness, _eyes.RightEye.Openness);
                _eyes.CombinedEye.Widen = MathX.Average(_eyes.LeftEye.Widen, _eyes.RightEye.Widen);
                _eyes.CombinedEye.Squeeze = MathX.Average(_eyes.LeftEye.Squeeze, _eyes.RightEye.Squeeze);
                _eyes.CombinedEye.Frown = MathX.Average(_eyes.LeftEye.Frown, _eyes.RightEye.Frown);
            }

            private static Func<float, KeyValuePair<string, float>> MkParam(string key, float min, float max)
            {
                return (float val) => new KeyValuePair<string, float>(key, MathX.Remap(val, min, max, 0f, 1f));
            }

            private static Func<float, KeyValuePair<string, float>> MkParam(string key)
            {
                return (float val) => new KeyValuePair<string, float>(key, val);
            }

            private static float3 Project2DTo3D(float2 v)
            {
                v *= MathX.Deg2Rad;

                var pitch = v.x;
                var yaw = v.y;

                var x = MathX.Cos(yaw) * MathX.Cos(pitch);
                var y = MathX.Sin(yaw) * -MathX.Cos(pitch);
                var z = MathX.Sin(pitch);

                return new float3(x, y, z);
            }
        }
    }
}
