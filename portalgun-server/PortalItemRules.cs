using System.Text.Json.Nodes;
namespace LocalPortalGun;
internal static class PortalItemRules {
    internal static JsonObject Create(JsonObject original) {
        var item=(JsonObject)original.DeepClone();
        item["_id"]=PortalIdentity.Template;item["_name"]="aperture_portal_device";item["_parent"]=PortalIdentity.PistolClass;
        var p=item["_props"]!.AsObject();
        p["Name"]="Aperture Science Handheld Portal Device";p["ShortName"]="Portal Gun";
        p["Description"]="Blue/orange linked portal device";p["Weight"]=1.2;p["Width"]=3;p["Height"]=2;
        p["ExaminedByDefault"]=true;p["CanSellOnRagfair"]=false;p["CanRequireOnRagfair"]=false;
        p["IsFlareGun"]=false;p["weapClass"]="pistol";p["weapUseType"]="secondary";
        p["AllowFeed"]=false;p["AllowJam"]=false;p["AllowMisfire"]=false;p["AllowOverheat"]=false;p["AllowSlide"]=false;
        p["DurabilityBurnRatio"]=0;p["BaseMalfunctionChance"]=0;p["Ergonomics"]=90;
        p["RecoilForceBack"]=0;p["RecoilForceUp"]=0;p["RecoilCamera"]=0;
        // Keep the native chamber structure for the base animator, but accept no ammunition.
        foreach(var chamber in p["Chambers"]!.AsArray()) {
            chamber!["_parent"]=PortalIdentity.Template;
            chamber["_id"]="67360000000000000000a003";
            foreach(var filter in chamber["_props"]!["filters"]!.AsArray())filter!["Filter"]=new JsonArray();
        }
        return item;
    }
}
