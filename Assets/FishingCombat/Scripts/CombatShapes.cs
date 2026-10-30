using UnityEngine;
namespace FishingPrototype
{
    public static class CombatShapes
    {
        static Sprite star, torso, head, rectangle;
        static Sprite Shape(Vector2[] ring)
        {
            var texture = new Texture2D(64,64); texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[4096];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            {
                Vector2 p = new Vector2((x+.5f)/64f-.5f,(y+.5f)/64f-.5f);
                bool inside=false;
                for(int i=0,j=ring.Length-1;i<ring.Length;j=i++)
                    if((ring[i].y>p.y)!=(ring[j].y>p.y) && p.x<(ring[j].x-ring[i].x)*(p.y-ring[i].y)/(ring[j].y-ring[i].y)+ring[i].x) inside=!inside;
                pixels[y*64+x]=inside?Color.white:Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();
            return Sprite.Create(texture,new Rect(0,0,64,64),Vector2.one*.5f,64);
        }
        static void Init()
        {
            if(rectangle) return;
            rectangle=Shape(new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f)});
            torso=Shape(new[]{new Vector2(-.42f,-.38f),new Vector2(.42f,-.38f),new Vector2(.28f,.22f),new Vector2(-.28f,.22f)});
            head=Shape(new[]{new Vector2(-.3f,-.3f),new Vector2(.3f,-.3f),new Vector2(.42f,.05f),new Vector2(.22f,.4f),new Vector2(-.22f,.4f),new Vector2(-.42f,.05f)});
            var points=new Vector2[8];
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;points[i]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(i%2==0?.5f:.17f);}
            star=Shape(points);
        }
        static void Part(Transform root,string name,Sprite sprite,Vector2 position,Vector2 size,Color color,int layer=2)
        {
            var go=new GameObject("Sprite_" + name.Replace(" ", ""));go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=size;
            var visual=go.AddComponent<SpriteRenderer>();visual.sprite=sprite;visual.color=color;visual.sortingOrder=layer;
        }
        public static void DressEnemy(GameObject enemy,bool heavy)
        {
            Init(); var visual=enemy.GetComponent<SpriteRenderer>();visual.sprite=torso;
            Color robe=heavy?new Color(.25f,.16f,.36f):new Color(.12f,.29f,.27f);
            visual.color=robe;enemy.GetComponent<HookableBody>().baseColor=robe;
            Part(enemy.transform,"두건",head,new Vector2(0,.34f),new Vector2(.7f,.4f),new Color(.09f,.12f,.17f));
            Part(enemy.transform,"눈가",rectangle,new Vector2(0,.36f),new Vector2(.38f,.055f),new Color(.92f,.73f,.53f),3);
            Part(enemy.transform,"눈",rectangle,new Vector2(.1f,.36f),new Vector2(.055f,.035f),Color.black,4);
            Part(enemy.transform,"허리띠",rectangle,new Vector2(0,-.12f),new Vector2(.64f,.08f),new Color(.64f,.43f,.17f),3);
            Part(enemy.transform,"왼발",rectangle,new Vector2(-.19f,-.43f),new Vector2(.22f,.14f),new Color(.08f,.1f,.14f));
            Part(enemy.transform,"오른발",rectangle,new Vector2(.19f,-.43f),new Vector2(.22f,.14f),new Color(.08f,.1f,.14f));
            Part(enemy.transform,"독주머니",head,new Vector2(-.3f,-.08f),new Vector2(.2f,.2f),new Color(.53f,.77f,.24f),4);
            if(heavy) { Part(enemy.transform,"왼쪽 어깨갑옷",head,new Vector2(-.32f,.15f),new Vector2(.4f,.2f),new Color(.51f,.48f,.62f),3);Part(enemy.transform,"오른쪽 어깨갑옷",head,new Vector2(.32f,.15f),new Vector2(.4f,.2f),new Color(.51f,.48f,.62f),3); }
        }
        public static Transform DressPlayer(GameObject player)
        {
            Init();
            var group = new GameObject("Model_Fisher").transform; group.SetParent(player.transform,false);
            player.GetComponent<SpriteRenderer>().sprite = null;
            Part(group,"셔츠",torso,new Vector2(0,-.02f),new Vector2(.85f,.85f),new Color(.85f,.91f,.85f),3);
            Part(group,"왼쪽 조끼",rectangle,new Vector2(-.18f,-.04f),new Vector2(.25f,.43f),new Color(.16f,.48f,.48f),4);
            Part(group,"오른쪽 조끼",rectangle,new Vector2(.16f,-.04f),new Vector2(.22f,.43f),new Color(.16f,.48f,.48f),4);
            Part(group,"조끼 주머니",rectangle,new Vector2(-.18f,-.12f),new Vector2(.17f,.10f),new Color(.3f,.67f,.61f),5);
            Part(group,"얼굴",head,new Vector2(.04f,.24f),new Vector2(.65f,.4f),new Color(.95f,.72f,.48f),4);
            Part(group,"머리카락",rectangle,new Vector2(-.19f,.25f),new Vector2(.13f,.17f),new Color(.22f,.16f,.13f),5);
            Part(group,"눈",rectangle,new Vector2(.18f,.27f),new Vector2(.05f,.04f),new Color(.08f,.13f,.16f),5);
            Part(group,"모자",head,new Vector2(0,.40f),new Vector2(.8f,.26f),new Color(.89f,.7f,.37f),5);
            Part(group,"모자 챙",rectangle,new Vector2(.06f,.35f),new Vector2(.96f,.065f),new Color(.97f,.81f,.48f),6);
            Part(group,"모자 띠",rectangle,new Vector2(0,.39f),new Vector2(.59f,.045f),new Color(.25f,.39f,.35f),6);
            Part(group,"목수건",rectangle,new Vector2(.03f,.12f),new Vector2(.48f,.07f),new Color(.96f,.42f,.24f),5);
            Part(group,"바지",rectangle,new Vector2(0,-.29f),new Vector2(.51f,.18f),new Color(.13f,.26f,.31f),3);
            Part(group,"왼쪽 장화",rectangle,new Vector2(-.17f,-.41f),new Vector2(.25f,.18f),new Color(.10f,.18f,.22f),4);
            Part(group,"오른쪽 장화",rectangle,new Vector2(.17f,-.41f),new Vector2(.25f,.18f),new Color(.10f,.18f,.22f),4);
            Part(group,"손",head,new Vector2(.32f,.01f),new Vector2(.22f,.18f),new Color(.95f,.72f,.48f),6);
            Part(group,"낚시 가방",head,new Vector2(-.36f,-.12f),new Vector2(.3f,.3f),new Color(.68f,.48f,.25f),2);
            return group;
        }

        public static void DressRod(GameObject rod)
        {
            Init();
            Part(rod.transform,"손잡이",rectangle,new Vector2(-.35f,0),new Vector2(.23f,1.5f),new Color(.26f,.18f,.12f),8);
            Part(rod.transform,"릴",head,new Vector2(-.25f,-1.1f),new Vector2(.12f,2.5f),new Color(.65f,.77f,.78f),8);
            Part(rod.transform,"끝 장식",rectangle,new Vector2(.46f,0),new Vector2(.07f,1.3f),new Color(.9f,.96f,.96f),8);
        }

        public static void DressShuriken(GameObject go)
        {
            Init(); go.GetComponent<SpriteRenderer>().sprite=star;go.GetComponent<SpriteRenderer>().color=new Color(.78f,.9f,.96f);
            go.GetComponent<HookableBody>().baseColor=new Color(.78f,.9f,.96f);
            Part(go.transform,"중심 고리",head,Vector2.zero,Vector2.one*.3f,new Color(.11f,.19f,.25f),3);
            go.GetComponent<Rigidbody2D>().angularVelocity=720f;
        }
    }
}
