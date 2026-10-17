using System;
using System.Collections.Generic;
using UnityEngine;

namespace SinkScrapt
{
    // Blender models live in Resources/SinkScrapt/Models/<Category>/<asset-id>.fbx (see Docs/BlenderAssets.md). They are visual
    // only: gameplay keeps its colliders in code, and every caller keeps a primitive fallback for a missing model.
    // Material slots are named "<Surface>" (tint supplied by the caller), "<Surface>__<Colour>" (fixed tint) or "Glow__<Colour>"
    // (emissive); "SS_Items" is the shared item palette. They are swapped for the game's own procedural materials at spawn time.
    public static class ModelLibrary
    {
        static readonly Dictionary<string,GameObject> cache=new();
        static readonly Dictionary<string,Material> remapped=new();
        static Material itemMaterial;
        static readonly Dictionary<string,Color> Tints=new() {
            {"DarkSteel",new(.30f,.33f,.34f)},{"Steel",new(.24f,.31f,.32f)},{"Teal",new(.19f,.49f,.47f)},{"Rust",new(.48f,.23f,.13f)},
            {"Brass",new(.62f,.47f,.22f)},{"RedPaint",new(.55f,.12f,.09f)},{"Cream",new(.74f,.71f,.56f)},{"DoorFrame",new(.36f,.40f,.38f)},
            {"Black",new(.05f,.05f,.05f)},{"White",new(.85f,.85f,.82f)},{"DeadGlass",new(.15f,.15f,.14f)},{"PortholeGlass",new(.02f,.09f,.10f)},
            {"Lamp",new(.62f,.80f,.74f)},{"Mattress",new(.55f,.60f,.58f)},{"Blanket",new(.36f,.40f,.30f)},{"Locker",new(.45f,.55f,.52f)},
            {"Galley",new(.70f,.72f,.72f)},{"Fridge",new(.85f,.86f,.84f)},{"Cabinet",new(.50f,.58f,.52f)},{"Sacks",new(.70f,.65f,.50f)},
            {"BoxCard",new(.75f,.70f,.62f)},{"CrateTop",new(.80f,.75f,.70f)},{"OffWhite",new(.80f,.79f,.73f)},{"Antifouling",new(.42f,.12f,.09f)},
            {"Yellow",new(.78f,.60f,.18f)},{"WindowLit",new(1f,.78f,.45f)},{"WindowDark",new(.05f,.08f,.09f)},{"CargoHull",new(.22f,.27f,.32f)},
            {"BoatTeal",new(.20f,.42f,.44f)},{"Orange",new(.90f,.42f,.12f)},{"Scrub",new(.35f,.50f,.30f)},{"CabinPaint",new(.78f,.74f,.62f)},
            {"Pole",new(.55f,.45f,.38f)},{"Workshop",new(.55f,.62f,.60f)},{"Roof",new(.62f,.38f,.26f)},{"Flood",new(1f,.88f,.70f)},
            {"Beacon",new(1f,.55f,.18f)},{"Concrete",new(.60f,.60f,.58f)},{"DockWood",new(.62f,.55f,.46f)},{"Piling",new(.32f,.26f,.20f)},
            {"Rope",new(.74f,.71f,.56f)},{"Chain",new(.20f,.16f,.13f)},{"Screen",new(.10f,.36f,.28f)},{"Mast",new(1f,.15f,.10f)},
            // Room doors (kit_door_*) and their glass; glass alpha is the opacity.
            {"DoorGrey",new(.58f,.62f,.61f)},{"DoorRust",new(.66f,.52f,.44f)},{"DoorWood",new(.72f,.50f,.32f)},{"DoorCream",new(.78f,.75f,.64f)},
            {"Chrome",new(.78f,.79f,.80f)},{"Porthole",new(.10f,.16f,.17f,.42f)},{"Wired",new(.14f,.20f,.17f,.55f)},
            // Cruise furniture (cruise_*, the WorldCruise colours).
            {"DarkWood",new(.36f,.20f,.12f)},{"Mahogany",new(.45f,.22f,.14f)},{"Linen",new(.78f,.76f,.70f)},{"Gold",new(.72f,.56f,.24f)},
            {"PianoBlack",new(.06f,.05f,.05f)},{"Porcelain",new(.86f,.86f,.82f)},{"MarbleWhite",new(.88f,.86f,.82f)},
            {"Crystal",new(1f,.86f,.60f)},{"DeadCrystal",new(.75f,.70f,.55f)},
        };
        static GameObject Load(string path)
        {
            if(!cache.TryGetValue(path,out var source)) {source=Resources.Load<GameObject>("SinkScrapt/Models/"+path);cache[path]=source;}
            return source;
        }
        public static bool Has(string path) => Load(path)!=null;
        // Instantiates the model under a wrapper placed at `position`/`rotation` in `parent`; the imported root keeps its own
        // local transform so any axis conversion from the FBX importer is preserved. Returns null when the model is missing.
        public static GameObject Spawn(string path,Transform parent,Vector3 position,Quaternion? rotation=null,Color? tint=null,Vector3? scale=null)
        {
            var source=Load(path);if(source==null)return null;
            var wrapper=new GameObject(source.name);wrapper.transform.SetParent(parent,false);wrapper.transform.localPosition=position;wrapper.transform.localRotation=rotation??Quaternion.identity;
            if(scale!=null)wrapper.transform.localScale=scale.Value;
            var model=UnityEngine.Object.Instantiate(source,wrapper.transform,false);model.name=source.name+" mesh";
            // Rigged characters are exported without "Apply Transform" and import facing -Z; turn them to face +Z like every
            // other model (this also puts the character's right hand on +X).
            if(path.StartsWith("Characters/",StringComparison.Ordinal))model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.Destroy(collider);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)materials[i]=Remap(materials[i],tint??Color.white);
                renderer.sharedMaterials=materials;
            }
            return wrapper;
        }
        static Material Remap(Material source,Color tint)
        {
            if(source==null)return null;
            string name=source.name.Replace(" (Instance)","");string key=name+tint;
            if(remapped.TryGetValue(key,out var cached)&&cached!=null)return cached;
            return remapped[key]=Resolve(name,tint);
        }
        static Material Resolve(string name,Color tint)
        {
            if(name.StartsWith("SS_Items",StringComparison.Ordinal))return ItemMaterial();
            var parts=name.Split(new[]{"__"},StringSplitOptions.None);
            // Characters and props carry their colour in the slot name: "Col__RRGGBB" (flat) or "Glow__RRGGBB" (emissive).
            if(parts.Length>1&&parts[1].Length>=6&&ColorUtility.TryParseHtmlString("#"+parts[1][..6],out var hex)&&(parts[0]=="Col"||parts[0]=="Glow"))
                return WorldFactory.Material(hex,parts[0]=="Glow");
            var color=parts.Length>1&&Tints.TryGetValue(parts[1],out var fixedTint)?fixedTint:tint;
            // Lamp glass takes the caller's colour (warm or cold lamps) when one is given.
            if(parts.Length>1&&parts[1]=="Lamp"&&tint!=Color.white)color=tint;
            if(parts[0]=="Glow")return WorldFactory.Material(color,true);
            if(parts[0]=="Glass")return WorldFactory.Glass(color);
            if(Enum.TryParse<Surface>(parts[0],out var surface))return Surfaces.Get(surface,color);
            return WorldFactory.Material(color);
        }
        // Items share one point-filtered 128 px palette (8 x 8 swatches) plus an emission palette for the glowing parts.
        static Material ItemMaterial()
        {
            if(itemMaterial!=null)return itemMaterial;
            var albedo=Resources.Load<Texture2D>("SinkScrapt/Models/Items/items_palette");var glow=Resources.Load<Texture2D>("SinkScrapt/Models/Items/items_palette_emission");
            var source=Resources.Load<Material>("SinkScrapt/Lit");
            itemMaterial=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));itemMaterial.name="SS_Items";itemMaterial.color=Color.white;
            if(albedo!=null) {albedo.filterMode=FilterMode.Point;itemMaterial.mainTexture=albedo;}
            if(glow!=null) {glow.filterMode=FilterMode.Point;itemMaterial.EnableKeyword("_EMISSION");itemMaterial.SetTexture("_EmissionMap",glow);itemMaterial.SetColor("_EmissionColor",Color.white*1.5f);}
            itemMaterial.SetFloat("_Smoothness",.2f);
            return itemMaterial;
        }
        // A bone (or any child) by exact name, searched depth-first.
        public static Transform FindBone(Transform root,string name)
        {
            if(root==null)return null;if(root.name==name)return root;
            for(int i=0;i<root.childCount;i++) {var found=FindBone(root.GetChild(i),name);if(found!=null)return found;}
            return null;
        }
        // Asset ids per ItemCatalog definition id (Docs/BlenderAssets.md, build list 1). The package (18) is the supply crate,
        // built by ItemVisual (null here); 19-24 are the cruise-ship scrap.
        public static readonly string[] ItemModels={"scrap_bolt_box","scrap_steel_plate","scrap_valve","scrap_pressure_gauge","scrap_ship_lamp","scrap_old_battery",
            "scrap_broken_radio","scrap_circuit_board","scrap_brass_compass","scrap_electric_motor","scrap_industrial_pump","scrap_generator",
            "tool_flashlight","tool_radio","tool_oxygen_tank","tool_flare","tool_battery","item_key",null,
            "scrap_champagne","scrap_silver_tray","scrap_casino_chips","scrap_ship_bell","scrap_jewellery_box","scrap_gramophone"};
        static readonly Dictionary<int,Sprite> icons=new();
        // The item's rendered icon (ArtSource/Blender/scripts/icons.py -> Resources/SinkScrapt/Icons/item_<id>.png), or null.
        public static Sprite Icon(int definitionId)
        {
            if(!icons.TryGetValue(definitionId,out var sprite)||sprite==null)icons[definitionId]=sprite=Resources.Load<Sprite>("SinkScrapt/Icons/item_"+definitionId);
            return sprite;
        }
    }
}
