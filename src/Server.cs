using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace Server
{
    public class Server : BaseScript
    {
        public Server()
        {
            EventHandlers["Server:SyncCamera"] += new Action<Vector3, int, bool>((location, id, remove) =>
            {
                TriggerClientEvent("Client:SyncCamera", location, id, remove);
            });

            EventHandlers["Server:CameraNotification"] += new Action<Vector3, int, string, string>((targetCoords, netId, name, pncNotes) =>
            {
                TriggerClientEvent("Client:CameraNotification", targetCoords, netId, name, pncNotes);
            });
        }
    }
}
