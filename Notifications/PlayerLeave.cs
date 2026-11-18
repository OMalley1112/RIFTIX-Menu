using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using StupidTemplate.Notifications;
using UnityEngine;

namespace StupidTemplate.Patches
{
    [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), "OnPlayerLeftRoom")]
    public class LeavePatch : MonoBehaviour
    {
        private static void Prefix(Player otherPlayer)
        {
            // If this is the player who put the Dino Row's night up, the
            // map gets its daylight back the moment they are gone.
            Menu.ConsoleAssetPages.AtmosphereOwnerLeft(otherPlayer.ActorNumber);

            if (otherPlayer != PhotonNetwork.LocalPlayer && otherPlayer != a)
            {
                NotifiLib.SendNotification("<color=grey>[</color><color=red>LEAVE</color><color=grey>]</color> <color=white>Name: " + otherPlayer.NickName + "</color>");
                a = otherPlayer;
            }
        }

        private static Player a;
    }
}