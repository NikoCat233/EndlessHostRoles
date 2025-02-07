using System;
using System.Collections;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using Array = Il2CppSystem.Array;
using Buffer = Il2CppSystem.Buffer;

namespace EHR.Modules;

public abstract class GameOptionsSender
{
    protected abstract bool IsDirty { get; set; }

    protected virtual void SendGameOptions()
    {
        IGameOptions opt = BuildGameOptions();

        // option => byte[]
        MessageWriter writer = MessageWriter.Get();
        writer.Write(opt.Version);
        writer.StartMessage(0);
        writer.Write((byte)opt.GameMode);

        if (opt.TryCast(out NormalGameOptionsV08 normalOpt))
            NormalGameOptionsV08.Serialize(writer, normalOpt);
        else if (opt.TryCast(out HideNSeekGameOptionsV08 hnsOpt))
            HideNSeekGameOptionsV08.Serialize(writer, hnsOpt);
        else
        {
            writer.Recycle();
            Logger.Error("Option cast failed", ToString());
        }

        writer.EndMessage();

        // Array & Send
        var byteArray = new Il2CppStructArray<byte>(writer.Length - 1);
        // MessageWriter.ToByteArray
        Buffer.BlockCopy(writer.Buffer.CastFast<Array>(), 1, byteArray.CastFast<Array>(), 0, writer.Length - 1);

        SendOptionsArray(byteArray);
        writer.Recycle();
    }

    protected virtual void SendOptionsArray(Il2CppStructArray<byte> optionArray)
    {
        for (byte i = 0; i < GameManager.Instance.LogicComponents.Count; i++)
        {
            Il2CppSystem.Object logicComponent = GameManager.Instance.LogicComponents[(Index)i];
            if (logicComponent.TryCast<LogicOptions>(out _)) SendOptionsArray(optionArray, i, -1);
        }
    }

    protected static void SendOptionsArray(Il2CppStructArray<byte> optionArray, byte LogicOptionsIndex, int targetClientId)
    {
        try
        {
            MessageWriter writer = MessageWriter.Get(HazelExtensions.SendOption);

            writer.StartMessage(targetClientId == -1 ? Tags.GameData : Tags.GameDataTo);

            {
                writer.Write(AmongUsClient.Instance.GameId);
                if (targetClientId != -1) writer.WritePacked(targetClientId);

                writer.StartMessage(1);

                {
                    writer.WritePacked(GameManager.Instance.NetId);
                    writer.StartMessage(LogicOptionsIndex);

                    {
                        writer.WriteBytesAndSize(optionArray);
                    }

                    writer.EndMessage();
                }

                writer.EndMessage();
            }

            writer.EndMessage();

            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
        catch (Exception ex) { Logger.Fatal(ex.ToString(), "GameOptionsSender.SendOptionsArray"); }
    }

    protected abstract IGameOptions BuildGameOptions();

    protected virtual bool AmValid()
    {
        return true;
    }

    #region Static

    public static readonly List<GameOptionsSender> AllSenders = new(15) { new NormalGameOptionsSender() };

    public static IEnumerator SendAllGameOptionsAsync()
    {
        AllSenders.RemoveAll(s => s == null || !s.AmValid());

        foreach (GameOptionsSender sender in AllSenders.ToArray())
        {
            if (sender.IsDirty)
            {
                sender.SendGameOptions();
                yield return null;
            }

            sender.IsDirty = false;
        }
    }

    public static void SendAllGameOptions()
    {
        AllSenders.RemoveAll(s => s == null || !s.AmValid());

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var index = 0; index < AllSenders.Count; index++)
        {
            GameOptionsSender sender = AllSenders[index];
            if (sender.IsDirty) sender.SendGameOptions();

            sender.IsDirty = false;
        }
    }

    #endregion
}