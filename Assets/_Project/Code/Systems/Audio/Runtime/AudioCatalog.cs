using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Audio
{
    public enum AudioChannel
    {
        Music = 0,
        SoundEffect = 1
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField] private string cueId;
        [SerializeField] private string displayName;
        [SerializeField] private AudioChannel channel =
            AudioChannel.SoundEffect;
        [SerializeField] private AudioClip[] clips =
            Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;
        [SerializeField] private bool loop;

        public string CueId => cueId;
        public string DisplayName => displayName;
        public AudioChannel Channel => channel;
        public AudioClip[] Clips => clips;
        public float VolumeScale => volumeScale;
        public bool Loop => loop;

        public bool HasClips
        {
            get
            {
                if (clips == null)
                {
                    return false;
                }

                for (int index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    [CreateAssetMenu(
        fileName = "AudioCatalog",
        menuName = "2026TapTap/Audio/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "AudioCatalog";

        [SerializeField] private AudioCueDefinition[] cues =
            Array.Empty<AudioCueDefinition>();

        private Dictionary<string, AudioCueDefinition> lookup;

        public IReadOnlyList<AudioCueDefinition> Cues => cues;

        public static AudioCatalog LoadDefault()
        {
            return Resources.Load<AudioCatalog>(
                DefaultResourcePath);
        }

        public bool TryGetCue(
            string cueId,
            out AudioCueDefinition definition)
        {
            EnsureLookup();
            if (!string.IsNullOrWhiteSpace(cueId) &&
                lookup.TryGetValue(cueId, out definition))
            {
                return definition != null;
            }

            definition = null;
            return false;
        }

        public AudioCueDefinition Find(string cueId)
        {
            return TryGetCue(cueId, out AudioCueDefinition definition)
                ? definition
                : null;
        }

        public void Configure(IEnumerable<AudioCueDefinition> value)
        {
            cues = value != null
                ? new List<AudioCueDefinition>(value).ToArray()
                : Array.Empty<AudioCueDefinition>();
            lookup = null;
        }

        private void OnValidate()
        {
            lookup = null;
            if (cues == null)
            {
                cues = Array.Empty<AudioCueDefinition>();
            }
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, AudioCueDefinition>(
                StringComparer.OrdinalIgnoreCase);
            if (cues == null)
            {
                return;
            }

            for (int index = 0; index < cues.Length; index++)
            {
                AudioCueDefinition definition = cues[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.CueId))
                {
                    continue;
                }

                lookup[definition.CueId.Trim()] = definition;
            }
        }
    }

    public static class AudioCueIds
    {
        public const string MusicMenuOpening = "music.menu.opening";
        public const string MusicAmbientLoop = "music.ambient.loop";
        public const string MusicAtmosphereAMinor = "music.atmosphere.a_minor";
        public const string MusicAtmosphereDSharpMinor = "music.atmosphere.d_sharp_minor";
        public const string MusicAtmosphereEMinor = "music.atmosphere.e_minor";
        public const string MusicAtmosphereFMinor = "music.atmosphere.f_minor";
        public const string MusicBanjo = "music.banjo";
        public const string MusicChoices = "music.choices";
        public const string MusicKeyboard = "music.keyboard";
        public const string MusicBox = "music.music_box";
        public const string MusicSadPiano = "music.sad_piano";
        public const string MusicVocalChop = "music.vocal_chop";
        public const string MusicWakeUp = "music.wake_up";
        public const string MusicAlienSacrifice = "music.alien_sacrifice";
        public const string AmbienceMachineHum = "ambience.machine_hum";
        public const string AmbiencePlantRustle = "ambience.plant_rustle";
        public const string AmbienceWaterDrip = "ambience.water_drip";
        public const string AmbienceWindGust = "ambience.wind_gust";

        public const string FootstepsLand = "sfx.footsteps.land";
        public const string FootstepsWater = "sfx.footsteps.water";
        public const string PlayerJump = "sfx.player.jump";
        public const string PlayerClimb = "sfx.player.climb";
        public const string PlayerClimbVine = "sfx.player.climb_vine";
        public const string ButtonPress = "sfx.button.press";
        public const string ButtonRelease = "sfx.button.release";
        public const string PlayerDie = "sfx.player.die";
        public const string PlayerRespawn = "sfx.player.respawn";
        public const string CheckpointUnlock = "sfx.checkpoint.unlock";
        public const string CheckpointSwitch = "sfx.checkpoint.switch";
        public const string PlayerSwim = "sfx.player.swim";
        public const string PlayerWaterEnter = "sfx.player.water_enter";
        public const string PlayerWaterExit = "sfx.player.water_exit";
        public const string DoorOpen = "sfx.door.open";
        public const string DoorClose = "sfx.door.close";
        public const string PlantGrow = "sfx.plant.grow";
        public const string SteamBurst = "sfx.steam.burst";
        public const string SteamLoop = "sfx.steam.loop";
        public const string PaintAcquire = "sfx.paint.acquire";
        public const string PaintSelect = "sfx.paint.select";
        public const string PaintComplete = "sfx.paint.complete";
        public const string CrankTurnLoop = "sfx.crank.turn_loop";
        public const string NoticeDeny = "sfx.notice.deny";
        public const string NoticeConfirm = "sfx.notice.confirm";
        public const string BlockPickup = "sfx.block.pickup";
        public const string BlockDrop = "sfx.block.drop";
        public const string PlatformMoveLoop = "sfx.platform.move_loop";
        public const string MachineStart = "sfx.machine.start";
        public const string MachineStop = "sfx.machine.stop";
        public const string BubbleLoop = "sfx.bubble.loop";
        public const string PlayerBuoyEnter = "sfx.player.buoy_enter";
        public const string PlayerBuoyExit = "sfx.player.buoy_exit";
        public const string PlayerImpact = "sfx.player.impact";
        public const string PlantBurn = "sfx.plant.burn";
        public const string LavaCool = "sfx.lava.cool";
    }
}
