﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    public class Bard : RoleBase
    {
        public static int BardCreations;
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            BardCreations = 0;
        }

        public static void OnMeetingHudDestroy(ref string name)
        {
            BardCreations++;
            try
            {
                name = ModUpdater.Get("https://v1.hitokoto.cn/?encode=text");
            }
            catch
            {
                name = GetString("ByBardGetFailed");
            }

            name += "\n\t\t——" + GetString("ByBard");
        }
    }
}
