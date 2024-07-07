﻿using System;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;

// All errors in this file are fake, the build succeeds
// https://github.com/CrowdedMods/CrowdedMod/blob/master/src/CrowdedMod/Patches/CreateGameOptionsPatches.cs
namespace EHR.Patches
{
    internal static class Crowded
    {
        public const int MaxPlayers = 127;
        public const int MaxImpostors = 127 / 2;

        [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
        public static class CreateOptionsPickerAwake
        {
            public static void Postfix(CreateOptionsPicker __instance)
            {
                if (__instance.mode != SettingsMode.Host) return;

                {
                    var firstButtonRenderer = __instance.MaxPlayerButtons[0];
                    firstButtonRenderer.GetComponentInChildren<TextMeshPro>().text = "-";
                    firstButtonRenderer.enabled = false;

                    var firstButtonButton = firstButtonRenderer.GetComponent<PassiveButton>();
                    firstButtonButton.OnClick.RemoveAllListeners();
                    firstButtonButton.OnClick.AddListener((Action)(() =>
                    {
                        for (var i = 1; i < 11; i++)
                        {
                            var playerButton = __instance.MaxPlayerButtons[i];

                            var tmp = playerButton.GetComponentInChildren<TextMeshPro>();
                            var newValue = Mathf.Max(byte.Parse(tmp.text) - 10, byte.Parse(playerButton.name) - 2);
                            tmp.text = newValue.ToString();
                        }

                        __instance.UpdateMaxPlayersButtons(__instance.GetTargetOptions());
                    }));
                    Object.Destroy(firstButtonRenderer);

                    var lastButtonRenderer = __instance.MaxPlayerButtons[^1];
                    lastButtonRenderer.GetComponentInChildren<TextMeshPro>().text = "+";
                    lastButtonRenderer.enabled = false;

                    var lastButtonButton = lastButtonRenderer.GetComponent<PassiveButton>();
                    lastButtonButton.OnClick.RemoveAllListeners();
                    lastButtonButton.OnClick.AddListener((Action)(() =>
                    {
                        for (var i = 1; i < 11; i++)
                        {
                            var playerButton = __instance.MaxPlayerButtons[i];

                            var tmp = playerButton.GetComponentInChildren<TextMeshPro>();
                            var newValue = Mathf.Min(byte.Parse(tmp.text) + 10,
                                MaxPlayers - 14 + byte.Parse(playerButton.name));
                            tmp.text = newValue.ToString();
                        }

                        __instance.UpdateMaxPlayersButtons(__instance.GetTargetOptions());
                    }));
                    Object.Destroy(lastButtonRenderer);

                    for (var i = 1; i < 11; i++)
                    {
                        var playerButton = __instance.MaxPlayerButtons[i].GetComponent<PassiveButton>();
                        var text = playerButton.GetComponentInChildren<TextMeshPro>();

                        playerButton.OnClick.RemoveAllListeners();
                        playerButton.OnClick.AddListener((Action)(() =>
                        {
                            var maxPlayers = byte.Parse(text.text);
                            var maxImp = Mathf.Min(__instance.GetTargetOptions().NumImpostors, maxPlayers / 2);
                            __instance.GetTargetOptions().SetInt(Int32OptionNames.NumImpostors, maxImp);
                            __instance.ImpostorButtons[1].TextMesh.text = maxImp.ToString();
                            __instance.SetMaxPlayersButtons(maxPlayers);
                        }));
                    }

                    foreach (var button in __instance.MaxPlayerButtons)
                    {
                        button.enabled = button.GetComponentInChildren<TextMeshPro>().text == __instance.GetTargetOptions().MaxPlayers.ToString();
                    }
                }

                {
                    var secondButton = __instance.ImpostorButtons[1];
                    secondButton.SpriteRenderer.enabled = false;
                    Object.Destroy(secondButton.transform.FindChild("ConsoleHighlight").gameObject);
                    Object.Destroy(secondButton.PassiveButton);
                    Object.Destroy(secondButton.BoxCollider);

                    var secondButtonText = secondButton.TextMesh;
                    secondButtonText.text = __instance.GetTargetOptions().NumImpostors.ToString();

                    var firstButton = __instance.ImpostorButtons[0];
                    firstButton.SpriteRenderer.enabled = false;
                    firstButton.TextMesh.text = "-";

                    var firstPassiveButton = firstButton.PassiveButton;
                    firstPassiveButton.OnClick.RemoveAllListeners();
                    firstPassiveButton.OnClick.AddListener((Action)(() =>
                    {
                        var newVal = Mathf.Clamp(
                            byte.Parse(secondButtonText.text) - 1,
                            1,
                            __instance.GetTargetOptions().MaxPlayers / 2
                        );
                        __instance.SetImpostorButtons(newVal);
                        secondButtonText.text = newVal.ToString();
                    }));

                    var thirdButton = __instance.ImpostorButtons[2];
                    thirdButton.SpriteRenderer.enabled = false;
                    thirdButton.TextMesh.text = "+";

                    var thirdPassiveButton = thirdButton.PassiveButton;
                    thirdPassiveButton.OnClick.RemoveAllListeners();
                    thirdPassiveButton.OnClick.AddListener((Action)(() =>
                    {
                        var newVal = Mathf.Clamp(
                            byte.Parse(secondButtonText.text) + 1,
                            1,
                            __instance.GetTargetOptions().MaxPlayers / 2
                        );
                        __instance.SetImpostorButtons(newVal);
                        secondButtonText.text = newVal.ToString();
                    }));
                }
            }
        }

        [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateMaxPlayersButtons))]
        public static class CreateOptionsPickerUpdateMaxPlayersButtons
        {
            public static bool Prefix(CreateOptionsPicker __instance, [HarmonyArgument(0)] IGameOptions opts)
            {
                if (__instance.CrewArea)
                {
                    __instance.CrewArea.SetCrewSize(opts.MaxPlayers, opts.NumImpostors);
                }

                var selectedAsString = opts.MaxPlayers.ToString();
                for (var i = 1; i < __instance.MaxPlayerButtons.Count - 1; i++)
                {
                    __instance.MaxPlayerButtons[i].enabled = __instance.MaxPlayerButtons[i].GetComponentInChildren<TextMeshPro>().text == selectedAsString;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateImpostorsButtons))]
        public static class CreateOptionsPickerUpdateImpostorsButtons
        {
            public static bool Prefix()
            {
                return false;
            }
        }

        [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.AreInvalid))]
        public static class InvalidOptionsPatches
        {
            public static bool Prefix(GameOptionsData __instance, [HarmonyArgument(0)] int maxExpectedPlayers)
            {
                return __instance.MaxPlayers > maxExpectedPlayers ||
                       __instance.NumImpostors < 1 ||
                       __instance.NumImpostors + 1 > maxExpectedPlayers / 2 ||
                       __instance.KillDistance is < 0 or > 2 ||
                       __instance.PlayerSpeedMod is <= 0f or > 3f;
            }
        }

        [HarmonyPatch(typeof(SecurityLogger), nameof(SecurityLogger.Awake))]
        public static class SecurityLoggerPatch
        {
            public static void Postfix(ref SecurityLogger __instance)
            {
                __instance.Timers = new float[127];
            }
        }
    }
}