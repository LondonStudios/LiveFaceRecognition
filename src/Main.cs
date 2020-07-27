using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;

namespace FacialRecognition
{
    public class Main : BaseScript
    {
        public string firstName;
        public string lastName;
        public string pncNotes;
        public bool detecting;

        public int camera = 0;
        public int sign = 0;
        public int sign2 = 0;
        public int cameraNetId;
        public int blip;

        public Dictionary<Vector3, int> cameraDatabase = new Dictionary<Vector3, int> { };

        public Main()
        {
            Request(GetHashKey("prop_cctv_cam_04a"));
            Request(GetHashKey("prop_afsign_vbike"));

            TriggerEvent("chat:addSuggestion", "/lfr", "Setup or remove live facial recognition", new[]
            {
                new { name="Action", help="setup/remove" },
            });

            TriggerEvent("chat:addSuggestion", "/facedetect", "Add yourself to the facial recognition system", new[]
            {
                new { name="First Name", help="eg, John" },
                new { name="Last Name", help="eg, Smith" },
                new { name="PNC Notes", help="eg, Wanted - GBH" },
            });

            RegisterCommand("lfr", new Action<int, List<object>, string>((source, args, raw) =>
            {
                try
                {
                    var arg = Convert.ToString(args[0]);
                    if (arg.ToLower() == "setup")
                    {
                        SetupCamera();
                    }
                    else if (arg.ToLower() == "remove")
                    {
                        RemoveCamera();
                    }
                }
                catch
                {
                    ProcessError("Usage /fn [setup/remove]");
                }
            }), false);

            RegisterCommand("facedetect", new Action<int, List<object>, string>((source, args, raw) =>
            {
                try
                {
                    firstName = Convert.ToString(args[0]);
                    lastName = Convert.ToString(args[1]);
                    var argshandler = args.ConvertAll(x => Convert.ToString(x));
                    argshandler.Remove(firstName);
                    argshandler.Remove(lastName);
                    pncNotes = string.Join(" ", argshandler);
                    detecting = true;

                    CameraDetection();

                    ShowNotification("You have now been added to the ~b~facial recognition system.");
                }
                catch
                {
                    ProcessError("Usage /facedetect [First Name] [Last Name] [PNC Notes]");
                }
            }), false);

            EventHandlers["Client:SyncCamera"] += new Action<Vector3, int, bool>((location, id, remove) =>
            {
                if (!remove)
                {
                    cameraDatabase.Add(location, id);
                }
                else
                {
                    if (cameraDatabase.ContainsKey(location))
                    {
                        cameraDatabase.Remove(location);
                    }
                }
            });

            EventHandlers["Client:CameraNotification"] += new Action<Vector3, int, string, string>((targetCoords, netId, name, pncNotes) =>
            {
                Vector3 playerCoords = GetEntityCoords(PlayerPedId(), true);
                var distance = Vdist(playerCoords.X, playerCoords.Y, playerCoords.Z, targetCoords.X, targetCoords.Y, targetCoords.Z);
                if (distance < 26.0f)
                {
                    PlaySoundFrontend(-1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET", true);
                    var ped = NetworkGetEntityFromNetworkId(netId);
                    ProcessHeadshot(ped, name, pncNotes);
                }
            });
        }

        private async void ProcessHeadshot(int ped, string name, string pnc)
        {
            var handle = RegisterPedheadshot(ped);
            while (!IsPedheadshotReady(handle) || (!IsPedheadshotValid(handle)))
            {
                await Delay(0);
            }
            await Delay(300);
            var txd = GetPedheadshotTxdString(handle);

            BeginTextCommandThefeedPost("STRING");
            AddTextComponentSubstringPlayerName($"Name: {name}\nPNC Notes: {pnc}");
            var title = "Facial Recognition";
            var subtitle = "New Detection:";
            var iconType = 0;
            EndTextCommandThefeedPostMessagetext(txd, txd, false, iconType, title, subtitle);
            EndTextCommandThefeedPostTicker(false, true);
        }

        private void ProcessError(string message = "An error has occured.")
        {
            PlaySoundFrontend(-1, "Place_Prop_Fail", "DLC_Dmod_Prop_Editor_Sounds", false);
            TriggerEvent("chat:addMessage", new
            {
                color = new[] { 255, 0, 0 },
                args = new[] { "[Facial Recognition]", $"{message}" }
            });
        }

        private void SetupCamera()
        {
            if (IsEligible())
            {
                SpawnModel();
                ShowNotification("You have setup a ~b~facial recognition ~w~camera at this location.");
            }
            else
            {
                if (!IsPedInAnyVehicle(PlayerPedId(), true))
                {
                    ShowNotification("You must be inside a vehicle to setup a camera.");
                }
                else
                {
                    ShowNotification("You are not able to setup a camera.");
                }
            }
        }

        [Command("spawnsign")]
        private void SpawnSign()
        {

        }

        private async void CameraDetection()
        {
            while (detecting)
            {
                if (cameraDatabase.Keys.Count < 1)
                {
                    await Delay(20000);
                    if (cameraDatabase.Keys.Count < 1)
                    {
                        detecting = false;
                        ShowNotification("No ~b~facial recognition ~w~cameras setup, use ~b~/facedetect [First Name] [Last Name] [PNC Notes] ~w~again when ready.");
                    }
                }
                var coords = GetEntityCoords(PlayerPedId(), true);

                foreach (KeyValuePair<Vector3, int> kvp in cameraDatabase)
                {
                    if (coords.DistanceToSquared(kvp.Key) < 20.0f)
                    {
                        TriggerServerEvent("Server:CameraNotification", GetEntityCoords(PlayerPedId(), true), Game.Player.Character.NetworkId, firstName + " " + lastName, pncNotes);
                        ShowNotification("You have ~g~activated ~w~a ~b~facial recognition ~w~camera");
                        await Delay(10000);
                        break;
                    }
                }
                await Delay(350);
            }
        }

        private void SetupSign(Vector3 coords, Vector3 coords2, float heading = 0f)
        {
            sign = CreateObject(GetHashKey("prop_afsign_vbike"), coords.X, coords.Y, coords.Z, true, true, true);
            PlaceObjectOnGroundProperly(sign);
            SetEntityHeading(sign, heading + -110f);
            var networkId = ObjToNet(sign);
            SetNetworkIdExistsOnAllMachines(networkId, true);
            SetNetworkIdCanMigrate(networkId, false);
            NetworkSetNetworkIdDynamic(networkId, true);

            sign2 = CreateObject(GetHashKey("prop_afsign_vbike"), coords2.X, coords2.Y, coords2.Z, true, true, true);
            PlaceObjectOnGroundProperly(sign2);
            SetEntityHeading(sign2, heading + -110f);
            var networkId2 = ObjToNet(sign2);
            SetNetworkIdExistsOnAllMachines(networkId2, true);
            SetNetworkIdCanMigrate(networkId2, false);
            NetworkSetNetworkIdDynamic(networkId2, true);

            FreezeEntityPosition(sign, true);
            FreezeEntityPosition(sign2, true);
        }

        private async void SpawnModel()
        {
            var vehicle = GetVehiclePedIsIn(PlayerPedId(), false);
            var coords = GetEntityCoords(PlayerPedId(), true);
            camera = CreateObject(GetHashKey("prop_cctv_cam_04a"), coords.X, coords.Y, coords.Z, true, true, true);
            FreezeEntityPosition(camera, true);
            var networkId = ObjToNet(camera);
            cameraNetId = networkId;
            SetNetworkIdExistsOnAllMachines(networkId, true);
            SetNetworkIdCanMigrate(networkId, false);
            NetworkSetNetworkIdDynamic(networkId, true);
            AttachEntityToEntity(camera, vehicle, GetEntityBoneIndexByName(vehicle, "windscreen"), 0f, -1f, 0.3f, 0f, 0f, 90f, false, false, false, false, 0, true);
            var offset = GetOffsetFromEntityInWorldCoords(vehicle, 0f, 5f, 0f);
            var offset2 = GetOffsetFromEntityInWorldCoords(vehicle, 0f, -5f, 0f);
            SetupSign(offset, offset2, GetEntityHeading(vehicle));

            TriggerServerEvent("Server:SyncCamera", GetEntityCoords(camera, true), networkId, false);

            blip = AddBlipForEntity(camera);
            SetBlipAsFriendly(blip, true);
            SetBlipColour(blip, 38);
            SetBlipDisplay(blip, 2);
            AddTextEntry("camera", "Facial Recognition Camera");
            BeginTextCommandSetBlipName("camera");
            EndTextCommandSetBlipName(blip);
            await Delay(100);
        }

        private void ShowNotification(string text)
        {
            SetNotificationTextEntry("STRING");
            AddTextComponentString(text);
            EndTextCommandThefeedPostTicker(false, false);
        }

        private void RemoveCamera()
        {
            if (!(camera == 0))
            {
                var coords = GetEntityCoords(camera, true);
                var netid = cameraNetId;
                DeleteEntity(ref camera);
                DeleteEntity(ref sign);
                DeleteEntity(ref sign2);
                sign = 0;
                sign2 = 0;
                camera = 0;
                cameraNetId = 0;
                TriggerServerEvent("Server:SyncCamera", coords, netid, true);
                ShowNotification("~b~Facial Recognition ~w~camera removed.");
            }
            else
            {
                ProcessError("No camera found to remove");
            }
        }

        private bool IsEligible()
        {
            if (IsPedInAnyVehicle(PlayerPedId(), true) && camera == 0)
            {
                return true;
            }
            return false;
        }


        private async void Request(int model)
        {
            RequestModel((uint)model);
            while (!HasModelLoaded((uint)model))
            {
                await Delay(0);
            }
        }
    }
}
