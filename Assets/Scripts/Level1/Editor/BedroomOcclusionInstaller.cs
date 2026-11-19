using System;
using System.IO;
using System.Linq;
using System.Text;
using Aloysius.Level1.Boss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class BedroomOcclusionInstaller
    {
        const string Art="Assets/Art/SerpentKing/FullFloor_v002/";
        const string Source="ArtSource/SerpentKing/FullFloor_v002/";
        [MenuItem("Aloysius/Serpent/Embed complete floor with bedroom occlusion")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/level1.unity");if(!scene.isLoaded)throw new InvalidOperationException("Open level1 first.");
            var c=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SerpentCampaign>(true)).Single();
            var wall=c.route.bedroomSpawn.parent.Find("Bedroom architecture").GetComponent<SpriteRenderer>();
            var fx=c.floorBreak;if(!fx||!fx.fullFloorMode)throw new InvalidOperationException("Complete floor not installed.");
            File.Copy(scene.path,Source+"BeforeOcclusion_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity.backup");
            var tex=wall.sprite.texture;Rect rect=wall.sprite.rect;float ppu=wall.sprite.pixelsPerUnit;
            float edge=60f*rect.width/1241f;
            int playerMin=c.actor.Parts.Min(r=>r.sortingOrder),playerMax=c.actor.Parts.Max(r=>r.sortingOrder);
            // The three sprites sample disjoint original rectangles: all architectural pixels stay registered.
            Slice(wall,"01 Rear architecture | walls and distant arches",new Rect(rect.x+edge,rect.y,rect.width-edge*2,rect.height),ppu,-40);
            Slice(wall,"06 Near architecture | left column",new Rect(rect.x,rect.y,edge,rect.height),ppu,playerMax+2);
            Slice(wall,"06 Near architecture | right column",new Rect(rect.xMax-edge,rect.y,edge,rect.height),ppu,playerMax+2);
            wall.enabled=false;EditorUtility.SetDirty(wall);
            fx.enabled=false;fx.floorSortingOrder=-29;fx.rimSortingOrder=playerMax+1;
            fx.debrisSortingOrder=playerMax;fx.dustSortingOrder=playerMax+5;
            fx.gameObject.name="02 Complete floor | surface, near lip, dust and passage";
            fx.enabled=true;fx.ResetFloor();
            foreach(string name in new[]{"Solid masonry under bedroom left","Solid masonry under bedroom right","Floor fracture"})
            {var old=c.hatch.parent.Find(name);if(old)old.gameObject.SetActive(false);}
            c.crackedFloor.gameObject.SetActive(false);
            var shader=fx.floorMaterial.shader;if(ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Floor shader error.");
            EditorUtility.SetDirty(fx);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            var log=new StringBuilder();log.AppendLine(DateTime.Now.ToString("O"));log.AppendLine("Saved: "+scene.path);
            log.AppendLine("Three persistent architecture slices share original source pixels and transforms. Original combined renderer disabled.");
            log.AppendLine("Back-to-front: rear wall -40, full floor -29, bed group -14, player "+playerMin+".."+playerMax+", floor lip "+fx.rimSortingOrder+", near columns "+(playerMax+2)+", foreground dust "+fx.dustSortingOrder);
            log.AppendLine("Legacy underfloor masking blocks disabled. Descending actor and debris pass behind near floor lip below walking Y - 0.12.");
            log.AppendLine("Cracked tile, impact particles, passage anchor and lower-room arrival retained. No Play-mode test.");
            File.WriteAllText(Source+"OcclusionIntegration.txt",log.ToString());Debug.Log(log.ToString());
        }
        static void Slice(SpriteRenderer source,string label,Rect region,float ppu,int order)
        {
            string file=label.StartsWith("01")?"RearArchitecture":label.Contains("left")?"NearColumnLeft":"NearColumnRight";
            string path=Art+file+".asset";var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(!sprite){sprite=Sprite.Create(source.sprite.texture,region,Vector2.one*.5f,ppu,0,SpriteMeshType.FullRect);sprite.name=file;AssetDatabase.CreateAsset(sprite,path);}
            var t=source.transform.Find(label);if(!t){t=new GameObject(label).transform;t.SetParent(source.transform,false);}
            // Account for original pivot; no scaling or relocation of the room itself.
            var original=source.sprite.rect;var pivot=source.sprite.pivot;
            t.localPosition=new Vector3((region.center.x-original.x-pivot.x)/ppu,(region.center.y-original.y-pivot.y)/ppu,0);
            t.localRotation=Quaternion.identity;t.localScale=Vector3.one;
            var r=t.GetComponent<SpriteRenderer>();if(!r)r=t.gameObject.AddComponent<SpriteRenderer>();
            r.sprite=sprite;r.sharedMaterial=source.sharedMaterial;r.color=source.color;r.sortingLayerID=source.sortingLayerID;r.sortingOrder=order;
            EditorUtility.SetDirty(r);
        }
    }
}
