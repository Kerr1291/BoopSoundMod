//#define LEGACY_VERSION_1221 //uncomment for the 1221 version; also requires a version of mod common to be built that compiles with 1221
using System.IO;
using Modding;
using UnityEngine;
using ModCommon;
using System.Collections;
using System.Reflection;

namespace ModTemplate
{
    /*
     * Add a reference to ModCommon, UnityEngine.dll, and PlayMaker.dll to allow this to build
     * 
     * For a nicer building experience, change 
     * SET MOD_DEST="K:\Games\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Mods"
     * in install_build.bat to point to your hollow knight mods folder...
     * 
     */
    public partial class BoopSoundMod : Mod<SaveSettings, BoopSoundModSettings>, ITogglableMod
    {
        public static BoopSoundMod Instance { get; private set; }

        CommunicationNode comms;

        const string boopSound = "Boop.wav";
        const string boopSoundCyclone = "BoopC.wav";
        string BoopPath = Application.dataPath + "/Managed/Mods/" + boopSound;
        string BoopPathC = Application.dataPath + "/Managed/Mods/" + boopSoundCyclone;
        static AudioClip boopClip;
        static AudioClip boopClipCyclone;
        HeroController hero;
        AudioSource highBoop;

        public override void Initialize()
        {
            if(Instance != null)
            {
                Log("Warning: "+this.GetType().Name+" is a singleton. Trying to create more than one may cause issues!");
                return;
            }

            Instance = this;
            comms = new CommunicationNode();
            comms.EnableNode( this );

            Log( this.GetType().Name + " initializing!" );

            SetupDefaulSettings();

            UnRegisterCallbacks();
            RegisterCallbacks();
        }

        void SetupDefaulSettings()
        {
            string globalSettingsFilename = Application.persistentDataPath + ModHooks.PathSeperator + GetType().Name + ".GlobalSettings.json";

            bool forceReloadGlobalSettings = false;
            if( GlobalSettings != null && GlobalSettings.SettingsVersion != BoopSoundModSettingsVars.GlobalSettingsVersion )
            {
                forceReloadGlobalSettings = true;
            }
            else
            {
                Log( "Global settings version match!" );
            }

            if( forceReloadGlobalSettings || !File.Exists( globalSettingsFilename ) )
            {
                if( forceReloadGlobalSettings )
                {
                    Log( "Global settings are outdated! Reloading global settings" );
                }
                else
                {
                    Log( "Global settings file not found, generating new one... File was not found at: " + globalSettingsFilename );
                }

                GlobalSettings.Reset();

                GlobalSettings.SettingsVersion = BoopSoundModSettingsVars.GlobalSettingsVersion;
            }

            SaveGlobalSettings();
        }

        ///Revert all changes the mod has made
        public void Unload()
        {
            UnRegisterCallbacks();
            comms.DisableNode();
            Instance = null;
        }

        //TODO: update when version checker is fixed in new modding API version
        public override string GetVersion()
        {
            return BoopSoundModSettingsVars.ModVersion;
        }

        //TODO: update when version checker is fixed in new modding API version
        public override bool IsCurrent()
        {
            return true;
        }

        void RegisterCallbacks()
        {
            ModHooks.Instance.SlashHitHook -= BoopOnHit;
            ModHooks.Instance.SlashHitHook += BoopOnHit;
        }

        void UnRegisterCallbacks()
        {
            ModHooks.Instance.SlashHitHook -= BoopOnHit;
        }

        void BoopOnHit( Collider2D otherCollider, GameObject gameObject )
        {
            if( hero == null || hero != HeroController.instance )
                ContractorManager.Instance.StartCoroutine( CheckAndInit() );
            
            if( otherCollider.gameObject == null )
                return;
#if LEGACY_VERSION_1221
            bool isEnemy = FSMUtility.LocateFSM( otherCollider.gameObject, "health_manager_enemy" ) != null || FSMUtility.LocateFSM( otherCollider.gameObject, "health_manager" ) != null; //for 1221
#else
            bool isEnemy = otherCollider.gameObject.IsGameEnemy();
#endif
            if( !isEnemy )
            {
                if( boopClip != null )
                {
                    if( !highBoop.isPlaying )
                        highBoop.Play();
                }
            }
            else
            {
                if( boopClip != null )
                {
                    Dev.Log( "playing boop! " + gameObject.name );
                    hero.GetComponent<AudioSource>().PlayOneShot( boopClip );
                    
                }
            }
        }   
        
        IEnumerator CheckAndInit()
        {
            bool heroIsNew = false;
            if( hero == null || hero != HeroController.instance )
            {
                hero = HeroController.instance;
                heroIsNew = true;
            }

            hero.gameObject.PrintSceneHierarchyTree( "BoopKnight" );

            if( hero == null )
                yield break;

            if(boopClip == null)
            {
                WWW www = new WWW("file:///" + BoopPath);

                Dev.Log( "file:///" + BoopPath );

                yield return www;

                while(!www.isDone)
                {
                    yield return new WaitForEndOfFrame();
                }

                boopClip = www.audioClip as AudioClip; 

                www = new WWW( "file:///" + BoopPathC );

                Dev.Log( "file:///" + BoopPathC );

                yield return www;

                while( !www.isDone )
                {
                    yield return new WaitForEndOfFrame();
                }

                boopClipCyclone = www.audioClip as AudioClip;

                //Dev.Log( "Clip " + boopClip.name + " loaded!" );
            }

            if( heroIsNew )
            {
                SetBoop( hero.normalSlash, .8f );
                SetBoop( hero.alternateSlash, 1f );
                SetBoop( hero.upSlash, 1.2f );
                SetBoop( hero.downSlash, .9f );
                SetBoop( hero.wallSlash, 1f );

                highBoop = CreateBoopObject( boopClip, 1.5f, .7f );

                //Dream Nail (get the Dream Nail go and modify the source)
                //Slash-AudioPlayerOneShotSingle
                //-Dash Slash-AudioPlay //has audio source
                //Nail Arts fsm
                //-Play Audio-AudioPlayerOneShotSingle //cyclone
                //-G Slash-AudioPlay //

                {
                    var audioOneShot = hero.gameObject.GetFSMActionOnState<HutongGames.PlayMaker.Actions.AudioPlayerOneShotSingle>("Slash", "Dream Nail");
                    audioOneShot.audioClip.Value = boopClip;
                    GameObject player = audioOneShot.audioPlayer.Value as GameObject;
                    if( player != null && player.GetComponent<AudioSource>() != null )
                    {
                        player.GetComponent<AudioSource>().pitch = .2f;
                    }
                }

                {
                    var audioOneShot = hero.gameObject.GetFSMActionOnState<HutongGames.PlayMaker.Actions.AudioPlay>("G Slash", "Nail Arts");
                    audioOneShot.oneShotClip = boopClip;
                    audioOneShot.volume = 1.5f;
                    GameObject player = audioOneShot.gameObject.GameObject.Value;
                    if( player != null && player.GetComponent<AudioSource>() != null )
                    {
                        player.GetComponent<AudioSource>().pitch = .5f;
                    }
                }

                {
                    var audioOneShot = hero.gameObject.GetFSMActionOnState<HutongGames.PlayMaker.Actions.AudioPlayerOneShotSingle>("Play Audio", "Nail Arts");
                    audioOneShot.audioClip.Value = boopClipCyclone;
                    GameObject player = audioOneShot.audioPlayer.Value as GameObject;
                    if( player != null && player.GetComponent<AudioSource>() != null )
                    {
                        player.GetComponent<AudioSource>().pitch = 1.5f;
                    }
                }

                {
                    var audioOneShot = hero.gameObject.GetFSMActionOnState<HutongGames.PlayMaker.Actions.AudioPlay>("Dash Slash", "Nail Arts");
                    audioOneShot.oneShotClip = boopClip;
                    audioOneShot.volume = 1.2f;
                    GameObject player = audioOneShot.gameObject.GameObject.Value;
                    if( player != null && player.GetComponent<AudioSource>() != null )
                    {
                        player.GetComponent<AudioSource>().pitch = 1.7f;
                    }
                }
            }
        }

        AudioSource CreateBoopObject(AudioClip clip, float pitch, float volume)
        {
            GameObject go = new GameObject("BoopObject");
            go.transform.SetParent( HeroController.instance.transform );
            go.transform.position = Vector3.zero;
            AudioSource a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.pitch = pitch;
            a.volume = volume;
            a.outputAudioMixerGroup = hero.GetComponent<AudioSource>().outputAudioMixerGroup;
            return a;
        }

        static AudioSource GetAudio(NailSlash slash)
        {
            AudioSource audio = null;

            FieldInfo fi = slash.GetType().GetField("audio",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
            audio = fi.GetValue( slash ) as AudioSource;

            return audio;
        }

        static void SetBoop(NailSlash slash, float pitch = 1f)
        {
            AudioSource boopSource = GetAudio(slash);
            boopSource.clip = boopClip;
            boopSource.pitch = pitch;
            boopSource.volume = boopSource.volume * .5f;
        }

        static void SetBoop( AudioSource boopSource, float pitch = 1f )
        {
            boopSource.clip = boopClip;
            boopSource.pitch = pitch;
            boopSource.volume = boopSource.volume * .5f;
        }
        static void SetBoopC( AudioSource boopSource, float pitch = 1f )
        {
            boopSource.clip = boopClipCyclone;
            boopSource.pitch = pitch;
            boopSource.volume = boopSource.volume * .5f;
        }
    }
}
