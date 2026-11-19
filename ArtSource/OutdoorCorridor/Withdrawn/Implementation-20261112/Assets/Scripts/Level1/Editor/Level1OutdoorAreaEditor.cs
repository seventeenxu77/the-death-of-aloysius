using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aloysius.Level1.Editor
{
    public static class Level1OutdoorAreaEditor
    {
        const string ScenePath = "Assets/Scenes/level1.unity";
        const string AssetDir = "Assets/Art/OutdoorCorridor";
        const string RootName = "Outdoor Exit | concrete ground and crate cover";
        static Material concrete, seam, wood, rim, nearWall, middleWall, farWall, window;

        public static void WithdrawUnapprovedArea()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Withdraw in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            string dir = "ArtSource/OutdoorCorridor/Withdrawn";
            Directory.CreateDirectory(dir);
            if (!EditorSceneManager.SaveScene(scene, dir + "/level1-before-withdrawal-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
                throw new IOException("Could not preserve current scene before withdrawal.");
            var root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == RootName && g.GetComponent<Level1OutdoorArea>());
            if (root) Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save restored scene.");
            Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Room |"));
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.identity, 5, true, true);
            Directory.CreateDirectory("Logs/Level1/Outdoor");
            File.WriteAllText("Logs/Level1/Outdoor/Withdrawn.txt", "Removed only newly added outdoor root; other live scene objects preserved. " + DateTime.Now.ToString("O"));
        }

        [MenuItem("Aloysius/Level1/Create Outdoor Concrete Exit")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create in Edit mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.GetRootGameObjects().Any(g => g.name == RootName))
                throw new InvalidOperationException("Outdoor area already exists; edit the existing hierarchy instead of overwriting it.");
            string backupDir = "ArtSource/OutdoorCorridor/SceneBackups";
            Directory.CreateDirectory(backupDir);
            string backup = backupDir + "/level1-before-outdoor-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not save live scene backup.");

            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var traversal = components.OfType<Level1TraversalController>().Single();
            var roomView = components.OfType<Level1RoomView>().Single();
            var lighting = components.OfType<Level1Lighting>().Single();
            Camera camera = roomView.GetComponent<Camera>();
            SpriteRenderer pipe = traversal.PipeRenderer;
            if (!pipe || !pipe.sprite) throw new InvalidOperationException("Existing snakeskin pipe is required.");
            Transform floorMarker = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .First(t => t.name.StartsWith("Floor alignment"));
            float floorY = floorMarker.position.y;
            float startX = roomView.RoomSize.x * .5f;
            float exitX = pipe.bounds.min.x + pipe.bounds.size.x * .94f;
            float exitY = traversal.PipeFloorAtX(exitX);

            Directory.CreateDirectory(AssetDir + "/Materials");
            Directory.CreateDirectory(AssetDir + "/Prefabs");
            AssetDatabase.Refresh();
            Shader shader = Shader.Find("Aloysius/Level1/OutdoorSilhouette");
            if (!shader) throw new InvalidOperationException("Outdoor silhouette shader not imported.");
            concrete = MaterialAsset("Concrete charcoal", new Color(.15f,.15f,.145f), shader, .16f);
            seam = MaterialAsset("Concrete joints", new Color(.065f,.067f,.067f), shader);
            wood = MaterialAsset("Crate black wood", new Color(.033f,.03f,.026f), shader);
            rim = MaterialAsset("Crate muted edge", new Color(.16f,.14f,.105f), shader);
            nearWall = MaterialAsset("Near wall", new Color(.07f,.08f,.085f), shader);
            middleWall = MaterialAsset("Middle buildings", new Color(.11f,.14f,.16f), shader);
            farWall = MaterialAsset("Far buildings", new Color(.16f,.19f,.21f), shader);
            window = MaterialAsset("Window and door ink", new Color(.045f,.055f,.065f), shader);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create outdoor concrete exit");
            var area = root.AddComponent<Level1OutdoorArea>();
            var layers = new List<Level1OutdoorParallax>();
            Transform groundLayer = Layer("01 | Concrete ground | gameplay plane", root.transform, camera, lighting, 1, 1, layers);
            Transform covers = Layer("02 | Crates | foreground cover", root.transform, camera, lighting, 1, 1, layers);
            Transform rear = Layer("03 | Rear yard | wall and stacked crates", root.transform, camera, lighting, .82f, .9f, layers);
            Transform middle = Layer("04 | Middle distance | separate buildings", root.transform, camera, lighting, .55f, .7f, layers);
            Transform far = Layer("05 | Far distance | separate buildings", root.transform, camera, lighting, .28f, .5f, layers);
            Transform sky = Layer("06 | Sky only | oversized background", root.transform, camera, lighting, .03f, .1f, layers);
            Transform foreground = Layer("07 | Near edge crates", root.transform, camera, lighting, 1.12f, 1, layers);
            Transform markers = Group("08 | Suspended outlet and landing markers", root.transform);

            // A continuous filled ground mass: no bridge arches or empty space underneath.
            float length = 30;
            var slab = Box("Concrete ground | continuous solid slab", groundLayer,
                new Vector3(startX + length * .5f, floorY - 2.5f, 2), new Vector3(length, 5, 6), concrete);
            var groundCollider = slab.AddComponent<BoxCollider2D>();
            groundCollider.size = Vector2.one; // follows the slab's existing scale
            Box("Plain ground edge", groundLayer, new Vector3(startX + length * .5f, floorY - .035f, -1.015f),
                new Vector3(length, .07f, .035f), rim);
            for (int i = 1; i < 10; i++)
                Box("Expansion joint " + i, groundLayer, new Vector3(startX + i * 3, floorY - 1.9f, -1.025f),
                    new Vector3(.018f, 3.8f, .025f), seam);

            var crateTemplate = MakeCrate("Crate template", root.transform);
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(AssetDir + "/Prefabs/PlainWoodCrate.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(crateTemplate, prefabPath);
            UnityEngine.Object.DestroyImmediate(crateTemplate);
            var coverObjects = new List<Transform>();
            // Leave the whole landing area clear before the first cover cluster.
            coverObjects.Add(Crate(prefab, covers, "Cover A | low box", new Vector3(exitX + 4.3f, floorY, -.55f), new Vector3(1.35f, 1.1f, .9f)));
            coverObjects.Add(Crate(prefab, covers, "Cover A | tall box", new Vector3(exitX + 5.65f, floorY, -.48f), new Vector3(1.35f, 1.65f, 1)));
            coverObjects.Add(Crate(prefab, covers, "Cover B | base", new Vector3(exitX + 11.2f, floorY, -.45f), new Vector3(1.8f, 1.35f, 1.1f)));
            coverObjects.Add(Crate(prefab, covers, "Cover B | upper box", new Vector3(exitX + 11.45f, floorY + 1.35f, -.45f), new Vector3(1.05f, .95f, .95f)));
            coverObjects.Add(Crate(prefab, covers, "Cover C | wide box", new Vector3(exitX + 18.8f, floorY, -.5f), new Vector3(2, 1.45f, 1)));
            coverObjects.Add(Crate(prefab, covers, "Cover C | small box", new Vector3(exitX + 20.45f, floorY, -.5f), new Vector3(1.15f, .85f, .85f)));
            foreach (Transform cover in coverObjects)
            {
                var detection = cover.gameObject.AddComponent<BoxCollider2D>();
                detection.offset = new Vector2(0,.5f); detection.size = Vector2.one; detection.isTrigger = true;
            }
            Crate(prefab, foreground, "Foreground box | partial lower edge", new Vector3(exitX + 8.5f, floorY - 1.2f, -2), new Vector3(2.2f, 1.45f, 1.4f));
            Crate(prefab, foreground, "Foreground box | second partial edge", new Vector3(exitX + 24, floorY - 1.25f, -2), new Vector3(2.6f, 1.55f, 1.4f));

            Box("Plain concrete boundary wall", rear, new Vector3(startX + 21, floorY + .65f, 5), new Vector3(42, 1.3f, .35f), nearWall);
            Box("Wall top edge", rear, new Vector3(startX + 21, floorY + 1.32f, 5), new Vector3(42, .07f, .5f), middleWall);
            for (int i = 0; i < 5; i++)
            {
                float x = startX + 3 + i * 8;
                Crate(prefab, rear, "Rear yard crate " + (i + 1), new Vector3(x, floorY, 4), new Vector3(1.2f, 1.2f, .8f));
            }
            Building(middle, "Storehouse A", new Vector3(startX + 6, floorY, 12), new Vector3(7, 3.8f, 3), middleWall);
            Building(middle, "Storehouse B", new Vector3(startX + 17, floorY, 13), new Vector3(8, 4.7f, 3.5f), middleWall);
            Building(middle, "Service building C", new Vector3(startX + 30, floorY, 12), new Vector3(9, 3.5f, 3), middleWall);
            Building(middle, "Storehouse D", new Vector3(startX + 44, floorY, 13), new Vector3(8, 4.2f, 3), middleWall);
            for (int i = 0; i < 7; i++)
                Building(far, "Far block " + (i + 1), new Vector3(startX + 5 + i * 10, floorY + .2f, 24 + i % 2),
                    new Vector3(6 + i % 3, 5 + (i % 3) * 1.1f, 4), farWall);

            var skyMaterial = MaterialAsset("Night sky", Color.white, shader, 0);
            skyMaterial.SetFloat("_FaceShade", 0); skyMaterial.SetFloat("_StartX", startX);
            skyMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDir + "/NightSkyBackground.png"));
            var skyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyQuad.name = "Night sky | no architecture baked in"; skyQuad.transform.SetParent(sky, false);
            skyQuad.transform.localPosition = new Vector3(startX + 20, floorY + 13, 45);
            skyQuad.transform.localScale = new Vector3(80, 40, 1);
            UnityEngine.Object.DestroyImmediate(skyQuad.GetComponent<Collider>());
            skyQuad.GetComponent<Renderer>().sharedMaterial = skyMaterial;

            Transform exit = Group("Pipe exit | airborne above ground", markers);
            exit.position = new Vector3(exitX, exitY, 0);
            Transform landing = Group("Landing | clear space below outlet", markers);
            landing.position = new Vector3(exitX + .85f, floorY, 0);
            area.Configure(exit, landing, groundCollider, coverObjects.ToArray(), layers.ToArray());
            EditorUtility.SetDirty(area);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save outdoor area.");
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView)
                SceneView.lastActiveSceneView.LookAt(new Vector3(exitX + 8, floorY + 1, 0), Quaternion.identity, 9, true, true);
            Directory.CreateDirectory("Logs/Level1/Outdoor");
            File.WriteAllText("Logs/Level1/Outdoor/Created.txt",
                "Created " + DateTime.Now.ToString("O") + "\nScene: " + ScenePath + "\nBackup: " + backup
                + "\nGround Y: " + floorY + "\nPipe exit: " + exit.position + "\nOutlet clearance: " + (exitY-floorY)
                + "\nCrate prefab: " + prefabPath + "\nNo Play mode, captures or tests were run. Exit traversal is not connected yet.");
        }

        static Material MaterialAsset(string name, Color color, Shader shader, float grain = .06f)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(AssetDir + "/Materials/" + name + ".mat");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Grain", grain);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Transform Group(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Transform Layer(string name, Transform parent, Camera camera, Level1Lighting lighting,
            float xMotion, float yMotion, List<Level1OutdoorParallax> layers)
        {
            Transform group = Group(name, parent);
            var layer = group.gameObject.AddComponent<Level1OutdoorParallax>();
            layer.Configure(camera, lighting, xMotion, yMotion); layers.Add(layer); return group;
        }
        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go;
        }
        static GameObject MakeCrate(string name, Transform parent)
        {
            Transform root = Group(name, parent);
            Box("Dark wooden body", root, new Vector3(0,.5f,0), new Vector3(1,1,1), wood);
            for (int i = 0; i < 5; i++)
                Box("Board joint " + i, root, new Vector3(-.4f + i * .2f,.5f,-.506f), new Vector3(.012f,.86f,.012f), rim);
            foreach (float x in new[] { -.44f, .44f })
                Box("Vertical frame", root, new Vector3(x,.5f,-.53f), new Vector3(.075f,1.02f,.06f), rim);
            foreach (float y in new[] { .055f, .945f })
                Box("Horizontal frame", root, new Vector3(0,y,-.53f), new Vector3(.9f,.07f,.06f), rim);
            var brace = Box("Single diagonal brace", root, new Vector3(0,.5f,-.565f), new Vector3(1.12f,.065f,.045f), rim);
            brace.transform.localRotation = Quaternion.Euler(0,0,43);
            return root.gameObject;
        }
        static Transform Crate(GameObject prefab, Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name; go.transform.localPosition = position; go.transform.localScale = scale;
            return go.transform;
        }
        static void Building(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            Transform building = Group(name, parent); building.localPosition = position;
            Box("Wall volume", building, Vector3.up * size.y * .5f, size, material);
            Box("Flat roof lip", building, Vector3.up * size.y, new Vector3(size.x + .3f,.18f,size.z + .3f), material);
            Box("Plain service door", building, new Vector3(-size.x*.25f,1,-size.z*.5f-.015f), new Vector3(1.1f,2,.035f), window);
            for (int i = 0; i < 3; i++)
                Box("Recessed window " + (i+1), building, new Vector3(size.x * (-.15f + i*.22f),size.y*.7f,-size.z*.5f-.025f),
                    new Vector3(.65f,.85f,.04f), window);
        }
    }
}
