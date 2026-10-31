#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FishingPrototype.Editor
{
    public static class CombatSceneBuilder
    {
        public const string scenePath = "Assets/FishingCombat/Scenes/FishingPrototype.unity";
        const string prefabFolder = "Assets/FishingCombat/Prefabs/";
        const string materialPath = "Assets/FishingCombat/Art/Material_CombatLine.mat";
        const string soundPath = "Assets/FishingCombat/Art/Audio_Capture.wav";
        static Color panel = new Color(0.035f, 0.065f, 0.085f, 0.94f);
        static Color accent = new Color(0.25f, 0.9f, 0.77f);

        public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "Prefab_" + name + ".prefab");
        static GameObject Save(GameObject root, string name)
        {
            PlayerPrefabBuilder.BakeSprites(root, name);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "Prefab_" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
        static GameObject Visual(string name, Vector2 position, Vector2 size, Color color, Transform parent = null)
        {
            var go = CombatAuthoring.CreateVisual(name, position, size, color);
            if (parent) go.transform.SetParent(parent, false);
            return go;
        }

        [MenuItem("낚싯대 전투/전체 프리팹 및 편집 씬 준비")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이를 종료하세요.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(prefabFolder);
            var scene = EditorSceneManager.OpenScene(scenePath);
            BuildEffects(); BuildActors(); BuildLevel(); BuildCamera(); BuildSystems(); BuildHud(); UpgradePlayer();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player) throw new Exception("플레이어 프리팹을 먼저 배치하세요.");
            var level = Instance("Level", "Environment_Level", scene);
            var camera = Instance("Camera", "Camera_Main", scene);
            var systems = Instance("Systems", "System_Combat", scene);
            var hudObject = Instance("Hud", "Canvas_CombatHud", scene);
            var follow = camera.GetComponent<SmoothCameraFollow>(); follow.target = player.transform;
            var stage = systems.GetComponent<StageDirector>(); stage.player = player; stage.rod = player.GetComponentInChildren<FishingRodController>();
            stage.combatGate = level.transform.Find("Environment_CombatGate").gameObject;
            stage.finishGate = level.transform.Find("Environment_FinishGate").gameObject;
            stage.startCheckpoint = level.transform.Find("Point_StartCheckpoint"); stage.middleCheckpoint = level.transform.Find("Point_MiddleCheckpoint");
            stage.firstTrigger = level.transform.Find("Point_FirstTrigger"); stage.secondTrigger = level.transform.Find("Point_SecondTrigger");
            stage.firstLeftSpawn = level.transform.Find("Point_FirstLeft"); stage.firstRightSpawn = level.transform.Find("Point_FirstRight");
            stage.secondLeftSpawn = level.transform.Find("Point_SecondLeft"); stage.secondRightSpawn = level.transform.Find("Point_SecondRight");
            var hud = hudObject.GetComponent<CombatHud>(); hud.player = player; hud.health = player.GetComponent<PlayerHealth>(); hud.rod = stage.rod; hud.stage = stage;
            foreach (var component in new Component[] { follow, stage, hud })
            { EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Check();
        }
        static GameObject Instance(string prefab, string name, Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root.name == name) return root;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(prefab), scene); go.name = name; return go;
        }

        static void BuildEffects()
        {
            if (!Prefab("EnemyPoison"))
            {
                var go = Visual("Effect_EnemyPoison", Vector2.zero, new Vector2(3f, 2f), new Color(0.3f, 0.85f, 0.2f, 0.35f));
                go.AddComponent<PoisonCloud>(); Save(go, "EnemyPoison");
            }
            if (!Prefab("CounterPoison"))
            {
                var go = new GameObject("Effect_CounterPoison"); go.transform.localScale = new Vector3(3f, 2f, 1f);
                go.AddComponent<PoisonCloud>().damagesEnemies = true;
                var texture = new Texture2D(32, 32); texture.wrapMode = TextureWrapMode.Clamp;
                for (int y=0; y<32; y++) for (int x=0; x<32; x++)
                { float radius = new Vector2((x+0.5f)/16f-1f,(y+0.5f)/16f-1f).magnitude; texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-radius)*0.8f)); }
                texture.Apply(); var sprite = Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*0.5f,32);
                for (int i=0;i<6;i++)
                {
                    var puff = new GameObject("Sprite_PurpleSmoke_"+i); puff.transform.SetParent(go.transform,false);
                    puff.transform.localPosition = new Vector3((i%3-1)*0.24f,(i/3-0.5f)*0.3f,0); puff.transform.localScale=Vector3.one*0.72f;
                    var renderer=puff.AddComponent<SpriteRenderer>(); renderer.sprite=sprite;renderer.color=new Color(0.75f,0.25f,1f,0.85f);renderer.sortingOrder=6;
                }
                Save(go,"CounterPoison");
            }
            if (!Prefab("SpawnWarning")) Save(Visual("Sprite_SpawnWarning",Vector2.zero,new Vector2(1.5f,0.12f),new Color(1f,0.55f,0.2f)),"SpawnWarning");
        }

        static void BuildActors()
        {
            if (!Prefab("Shuriken"))
            {
                var go=CombatAuthoring.CreateHookable("Projectile_Shuriken",Vector2.zero,Vector2.one*0.55f,Color.yellow,HookableBody.BodyKind.Prop,0.5f,1000f,false,"object.shuriken");
                CombatShapes.DressShuriken(go);go.GetComponent<Rigidbody2D>().gravityScale=0;go.AddComponent<Shuriken>(); AddBodyHud(go); Save(go,"Shuriken");
            }
            if (!Prefab("Bottle"))
            {
                var go=CombatAuthoring.CreateHookable("Prop_PoisonBottle",Vector2.zero,new Vector2(0.5f,0.8f),Color.cyan,HookableBody.BodyKind.Prop,1f,500f,false,"object.bottle");
                go.AddComponent<PoisonBottle>().counterCloudPrefab=Prefab("CounterPoison").GetComponent<PoisonCloud>();AddBodyHud(go);Save(go,"Bottle");
            }
            if (!Prefab("Rock"))
            {
                var go=CombatAuthoring.CreateHookable("Prop_ThrowingRock",Vector2.zero,Vector2.one*0.8f,new Color(0.42f,0.47f,0.52f),HookableBody.BodyKind.Prop,3.2f,160f,true,"object.rock");
                AddBodyHud(go);Save(go,"Rock");
            }
            foreach (bool heavy in new[]{false,true})
            {
                string name=heavy?"HeavyEnemy":"NormalEnemy";if(Prefab(name))continue;
                var go=CombatAuthoring.CreateHookable("Enemy_"+name,Vector2.zero,heavy?new Vector2(1.35f,2.15f):new Vector2(0.85f,1.65f),Color.red,HookableBody.BodyKind.Enemy,heavy?3.4f:1.15f,heavy?150f:72f,false,heavy?"enemy.heavy":"enemy.normal");
                go.GetComponent<Rigidbody2D>().freezeRotation=true;CombatShapes.DressEnemy(go,heavy);
                var ai=go.AddComponent<TangEnemyAI>();ai.poisonCloudPrefab=Prefab("EnemyPoison").GetComponent<PoisonCloud>();ai.shurikenPrefab=Prefab("Shuriken").GetComponent<Shuriken>();
                AddBodyHud(go);Save(go,name);
            }
        }
        static void AddBodyHud(GameObject owner)
        {
            var go=new GameObject("Canvas_BodyHud",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(owner.transform,false);
            go.transform.localPosition=new Vector3(0,0.75f,0);go.transform.localScale=new Vector3(0.01f/owner.transform.localScale.x,0.01f/owner.transform.localScale.y,0.01f);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=20;
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(130,34);
            var label=Text(go.transform,"Text_Name",0,0,130,26,owner.GetComponent<HookableBody>().DisplayName,14);
            var bg=Image(go.transform,"Image_HealthBackground",0,28,130,4,new Color(0.2f,0.1f,0.13f));
            var fill=Image(bg.transform,"Image_HealthFill",0,0,130,4,new Color(1f,0.43f,0.36f));Fill(fill);
            var hud=go.AddComponent<BodyHud>();hud.body=owner.GetComponent<HookableBody>();hud.canvas=canvas;hud.nameText=label;hud.healthFill=fill;
        }

        static void BuildLevel()
        {
            if(Prefab("Level"))return;
            var level=new GameObject("Environment_Level");
            Visual("Sprite_Backdrop",new Vector2(17,1.2f),new Vector2(78,8),new Color(0.08f,0.12f,0.17f),level.transform).GetComponent<SpriteRenderer>().sortingOrder=-10;
            for(int i=-3;i<=12;i++)Visual("Sprite_DistanceMarker_"+i,new Vector2(i*4f,-1.85f),new Vector2(0.04f,0.35f),new Color(0.2f,0.28f,0.34f),level.transform).GetComponent<SpriteRenderer>().sortingOrder=-2;
            Visual("Sprite_Sea",new Vector2(17,-4.15f),new Vector2(78,2.15f),new Color(0.06f,0.3f,0.5f),level.transform).GetComponent<SpriteRenderer>().sortingOrder=-4;
            Visual("Sprite_SeaSurface",new Vector2(17,-3.18f),new Vector2(78,0.12f),new Color(0.2f,0.72f,0.9f),level.transform).GetComponent<SpriteRenderer>().sortingOrder=-3;
            var ground=Visual("Environment_Ground",new Vector2(17,-2.65f),new Vector2(78,1.25f),new Color(0.13f,0.18f,0.22f),level.transform);ground.AddComponent<BoxCollider2D>();ground.AddComponent<GroundSurface>();
            Visual("Sprite_GroundEdge",new Vector2(17,-2f),new Vector2(78,0.08f),new Color(0.33f,0.78f,0.68f),level.transform).GetComponent<SpriteRenderer>().sortingOrder=1;
            Visual("Environment_CombatGate",new Vector2(10.5f,1.3f),new Vector2(0.3f,6.6f),new Color(0.85f,0.25f,0.22f,0.72f),level.transform).AddComponent<BoxCollider2D>();
            Visual("Environment_FinishGate",new Vector2(36.5f,1.3f),new Vector2(0.3f,6.6f),new Color(0.74f,0.24f,0.78f,0.72f),level.transform).AddComponent<BoxCollider2D>();
            Point(level,"Point_StartCheckpoint",-8,-1.15f);Point(level,"Point_MiddleCheckpoint",14,-1.15f);
            Point(level,"Point_FirstTrigger",-4.5f,0);Point(level,"Point_SecondTrigger",20.5f,0);
            Point(level,"Point_FirstLeft",1.5f,-1.1f);Point(level,"Point_FirstRight",8.5f,-1.1f);Point(level,"Point_SecondLeft",23,-1.1f);Point(level,"Point_SecondRight",34,-1.1f);
            for(int i=0;i<3;i++){var bottle=(GameObject)PrefabUtility.InstantiatePrefab(Prefab("Bottle"));bottle.name="Prop_PoisonBottle_"+i;bottle.transform.SetParent(level.transform,false);bottle.transform.localPosition=new Vector3(i*12,-1.5f,0);}
            var rock=(GameObject)PrefabUtility.InstantiatePrefab(Prefab("Rock"));rock.name="Prop_ThrowingRock";rock.transform.SetParent(level.transform,false);rock.transform.localPosition=new Vector3(-1,-1.55f,0);
            Save(level,"Level");
        }
        static void Point(GameObject level,string name,float x,float y){var go=new GameObject(name);go.transform.SetParent(level.transform,false);go.transform.localPosition=new Vector3(x,y,0);}
        static void BuildCamera()
        {
            if(Prefab("Camera"))return;
            var go=new GameObject("Camera_Main");go.tag="MainCamera";go.transform.position=new Vector3(0,0.5f,-10);
            var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5.2f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.055f,0.075f,0.105f);
            go.AddComponent<AudioListener>();go.AddComponent<SmoothCameraFollow>();go.AddComponent<CameraShake>();Save(go,"Camera");
        }
        static void BuildSystems()
        {
            if(Prefab("Systems"))return;
            var go=new GameObject("System_Combat");go.AddComponent<PrototypeBootstrap>();var stage=go.AddComponent<StageDirector>();
            stage.normalEnemyPrefab=Prefab("NormalEnemy").GetComponent<HookableBody>();stage.heavyEnemyPrefab=Prefab("HeavyEnemy").GetComponent<HookableBody>();stage.warningPrefab=Prefab("SpawnWarning");Save(go,"Systems");
        }

        static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        static Text Text(Transform parent,string name,float x,float y,float width,float height,string value,int size)
        {
            var text=Rect(parent,name,x,y,width,height).gameObject.AddComponent<Text>();text.font=CombatAuthoring.KoreanFont;text.fontSize=size;text.color=new Color(0.9f,0.96f,0.97f);text.text=value;text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        static Text LocalText(Transform parent,string key,float x,float y,float w,float h,int size)
        {var text=Text(parent,"Text_"+key.Replace('.','_'),x,y,w,h,CombatText.Get(key),size);text.gameObject.AddComponent<LocalizedText>().textKey=key;return text;}
        static Image Image(Transform parent,string name,float x,float y,float w,float h,Color color)
        {var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();image.color=color;image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FishingCombat/Art/Player/Sprite_Rod_00.png");image.raycastTarget=false;return image;}
        static void Fill(Image image){image.type=UnityEngine.UI.Image.Type.Filled;image.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;image.fillOrigin=0;}
        static void BuildHud()
        {
            if(Prefab("Hud"))return;
            var go=new GameObject("Canvas_CombatHud",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=0.5f;
            var hud=go.AddComponent<CombatHud>();var root=go.transform;
            Image(root,"Image_HealthPanel",24,24,300,100,panel);Image(root,"Image_HealthAccent",24,24,4,100,accent);
            LocalText(root,"player.name",42,34,170,30,23);hud.healthText=Text(root,"Text_HealthValue",220,40,90,24,CombatText.Format("hud.health_value",100,100),14);
            Image(root,"Image_HealthBackground",42,76,264,12,new Color(0.15f,0.22f,0.25f));hud.healthFill=Image(root,"Image_HealthFill",42,76,264,12,accent);Fill(hud.healthFill);
            LocalText(root,"hud.tip",42,96,264,22,14);
            hud.poisonPanel=Image(root,"Image_PoisonPanel",24,132,300,36,new Color(0.15f,0.24f,0.09f,0.95f)).gameObject;
            hud.poisonText=Text(hud.poisonPanel.transform,"Text_Poison",14,6,285,25,CombatText.Format("hud.poison",2f),14);hud.poisonPanel.SetActive(false);
            Image(root,"Image_ObjectivePanel",866,24,390,92,panel);LocalText(root,"hud.objective",884,34,350,25,14);hud.objectiveText=Text(root,"Text_Objective",884,60,350,48,CombatText.Get("objective.start"),18);
            hud.capturePanel=Image(root,"Image_CapturePanel",490,435,300,55,panel).gameObject;LocalText(hud.capturePanel.transform,"hud.capture_heavy",14,7,270,24,14);
            hud.captureFill=Image(hud.capturePanel.transform,"Image_CaptureFill",14,38,272,5,accent);Fill(hud.captureFill);hud.capturePanel.SetActive(false);
            Image(root,"Image_EquipmentPanel",24,620,320,76,panel);hud.heldText=Text(root,"Text_Held",40,630,290,27,CombatText.Format("hud.equipped",CombatText.Get("equipment.bobber")),18);
            hud.controlsText=Text(root,"Text_Controls",40,661,290,26,CombatText.Get("hud.attack_controls"),14);hud.helpToggleText=Text(root,"Text_HelpToggle",1045,663,215,30,CombatText.Format("hud.help_toggle",CombatText.Get("ui.open")),18);
            hud.helpPanel=Image(root,"Image_HelpPanel",390,170,500,300,panel).gameObject;Image(hud.helpPanel.transform,"Image_HelpAccent",0,0,500,3,accent);
            LocalText(hud.helpPanel.transform,"hud.help_title",24,22,450,35,23);LocalText(hud.helpPanel.transform,"hud.help_body",24,75,450,220,18);hud.helpPanel.SetActive(false);Save(go,"Hud");
        }

        public static void UpgradePlayer()
        {
            var root=PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.playerPath);
            try
            {
                if(!root.GetComponent<PoisonStatus>())root.AddComponent<PoisonStatus>();
                var rod=root.GetComponentInChildren<FishingRodController>();var data=new SerializedObject(rod);
                if(!data.FindProperty("tipBody").objectReferenceValue)
                {
                    var tip=Visual("Sprite_LineEnd",Vector2.zero,Vector2.one*0.18f,new Color(0.95f,0.85f,0.38f));tip.transform.SetParent(rod.transform,true);
                    var rb=tip.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0.25f;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                    var collider=tip.AddComponent<CircleCollider2D>();collider.isTrigger=true;collider.radius=1.1f;tip.AddComponent<TipContact>().owner=rod;tip.GetComponent<SpriteRenderer>().sortingOrder=5;
                    PlayerPrefabBuilder.BakeSprites(tip,"LineTip");
                    Directory.CreateDirectory("Assets/FishingCombat/Art");
                    var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(!material){material=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,materialPath);}
                    var line=MakeLine(rod.transform,"Line_Rope",24,0.055f,0.035f,new Color(0.82f,0.91f,0.96f),new Color(0.48f,0.7f,0.78f),2,material);
                    var strike=MakeLine(rod.transform,"Line_StrikeZone",24,0.045f,0.095f,new Color(0.74f,0.88f,0.9f,0.7f),new Color(1f,0.8f,0.28f),3,material);strike.enabled=false;
                    var outline=MakeLine(rod.transform,"Line_AimOutline",5,0.04f,0.04f,Color.cyan,Color.cyan,4,material);outline.enabled=false;
                    data.FindProperty("tipBody").objectReferenceValue=rb;data.FindProperty("tipCollider").objectReferenceValue=collider;data.FindProperty("line").objectReferenceValue=line;data.FindProperty("strikeLine").objectReferenceValue=strike;data.FindProperty("outline").objectReferenceValue=outline;data.ApplyModifiedPropertiesWithoutUndo();
                }
                if(!rod.captureAudio)
                {
                    if(!File.Exists(soundPath))
                    {
                        using(var writer=new BinaryWriter(File.Create(soundPath)))
                        { int count=2205;writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(22050);writer.Write(44100);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);for(int i=0;i<count;i++)writer.Write((short)(Mathf.Sin(i*0.18f)*0.1f*(1f-i/(float)count)*32767)); }
                        AssetDatabase.ImportAsset(soundPath);
                    }
                    var audio=rod.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(soundPath);rod.captureAudio=audio;
                }
                PrefabUtility.SaveAsPrefabAsset(root,PlayerPrefabBuilder.playerPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static LineRenderer MakeLine(Transform parent,string name,int count,float start,float end,Color a,Color b,int order,Material material)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=count;line.startWidth=start;line.endWidth=end;line.startColor=a;line.endColor=b;line.sortingOrder=order;line.sharedMaterial=material;return line;}

        [MenuItem("낚싯대 전투/전체 편집 씬 검사")]
        public static void Check()
        {
            foreach(string name in new[]{"NormalEnemy","HeavyEnemy","Bottle","Rock","Shuriken","EnemyPoison","CounterPoison","SpawnWarning","Level","Camera","Systems","Hud"})
            {
                var prefab=Prefab(name);if(!prefab)throw new Exception("프리팹 누락: "+name);
                foreach(var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))if(!component||!MonoScript.FromMonoBehaviour(component))throw new Exception("스크립트 누락: "+name);
                foreach(var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))if(renderer.sprite&&!EditorUtility.IsPersistent(renderer.sprite))throw new Exception("임시 스프라이트: "+name);
            }
            PlayerPrefabBuilder.Check();
            Debug.Log("FULL SCENE PREFAB CHECKS PASSED");
        }
    }
}
#endif
