using UnityEngine;

namespace Aloysius.Level1
{
    // Runtime-only cutaway layer. The saved pipe transform, sprite and material stay intact.
    internal sealed class Level1PipeVision
    {
        public SpriteRenderer Pipe { get; }
        readonly Renderer[] actor;
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        SpriteRenderer interior;

        public Level1PipeVision(SpriteRenderer pipe,Renderer[] actorParts)
        {Pipe=pipe;actor=actorParts;}

        public void Apply(float blend,float brightness,float shell,float outline,float width,Color tint)
        {
            if(!Pipe || !Pipe.sprite)return;
            if(blend>0 && !interior)
            {
                var go=new GameObject("Pipe interior | runtime only");go.hideFlags=HideFlags.DontSave;
                go.transform.SetParent(Pipe.transform,false);go.layer=Pipe.gameObject.layer;
                interior=go.AddComponent<SpriteRenderer>();
            }
            if(interior)
            {
                interior.enabled=blend>0 && Pipe.enabled;
                interior.sprite=Pipe.sprite;interior.sharedMaterial=Pipe.sharedMaterial;
                interior.flipX=Pipe.flipX;interior.flipY=Pipe.flipY;
                interior.sortingLayerID=Pipe.sortingLayerID;
                int firstActor=20;
                if(actor!=null)foreach(var part in actor)if(part)firstActor=Mathf.Min(firstActor,part.sortingOrder);
                interior.sortingOrder=firstActor-1;
                interior.GetPropertyBlock(block);
                block.SetFloat("_PipeFocus",blend);block.SetFloat("_PipeInterior",1);
                block.SetFloat("_PipeInteriorBrightness",brightness);block.SetColor("_PipeTint",tint);
                block.SetFloat("_SceneVisibility",1);interior.SetPropertyBlock(block);
            }
            Pipe.GetPropertyBlock(block);
            block.SetFloat("_PipeFocus",blend);block.SetFloat("_PipeInterior",0);
            block.SetFloat("_PipeShellOpacity",shell);block.SetFloat("_PipeOutlineOpacity",outline);
            block.SetFloat("_PipeOutlineWidth",width);block.SetColor("_PipeTint",tint);
            Pipe.SetPropertyBlock(block);

            var sprite=Pipe.sprite;var rect=sprite.rect;var texture=sprite.texture;
            Vector4 localToUV=new Vector4(sprite.pixelsPerUnit/rect.width,sprite.pixelsPerUnit/rect.height,sprite.pivot.x/rect.width,sprite.pivot.y/rect.height);
            if(Pipe.flipX){localToUV.x=-localToUV.x;localToUV.z=1-localToUV.z;}
            if(Pipe.flipY){localToUV.y=-localToUV.y;localToUV.w=1-localToUV.w;}
            if(actor!=null)foreach(var part in actor)
            {
                if(!part)continue;
                part.GetPropertyBlock(block);block.SetFloat("_PipeFocus",blend);
                block.SetTexture("_PipeMask",texture);block.SetMatrix("_PipeWorldToLocal",Pipe.transform.worldToLocalMatrix);
                block.SetVector("_PipeLocalToUV",localToUV);
                block.SetVector("_PipeUVRect",new Vector4(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
                part.SetPropertyBlock(block);
            }
        }

        public void Dispose()
        {
            if(Pipe){Pipe.GetPropertyBlock(block);block.SetFloat("_PipeFocus",0);Pipe.SetPropertyBlock(block);}
            if(actor!=null)foreach(var part in actor)
            {if(!part)continue;part.GetPropertyBlock(block);block.SetFloat("_PipeFocus",0);part.SetPropertyBlock(block);}
            if(interior)
            {
                interior.enabled=false;
                if(Application.isPlaying)Object.Destroy(interior.gameObject);else Object.DestroyImmediate(interior.gameObject);
            }
        }
    }
}
