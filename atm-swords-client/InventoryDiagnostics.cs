using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;
using System.Text.Json;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 float nextInventoryDiagnostics;IntPtr inventoryFactory;
 readonly List<object> inventoryTemplateDiagnostics=[];
 void ObserveInventoryPresentation(){
  if(Time.unscaledTime<nextInventoryDiagnostics)return;nextInventoryDiagnostics=Time.unscaledTime+10;
  var factory=Singleton<ItemFactory>.Instance;if(factory==null)return;
  try{
   if(inventoryFactory!=factory.Pointer){inventoryFactory=factory.Pointer;inventoryTemplateDiagnostics.Clear();
    foreach(var sword in Swords.Renderable.Prepend(new(Swords.BaseTemplate,"gladius","Gladius","Gladius",0,0,0))){try{
     var item=factory.CreateItem(factory.NextId.ToString(),sword.Id,null);var size=item.CalculateCellSize();var template=factory.ItemTemplates[new MongoID(sword.Id)];
     object? placement=null;if(!Swords.IsUtility(sword.Id)){
      // Detached native grid: verifies occupancy without touching player items.
      var parent=factory.CreateItem(factory.NextId.ToString(),"a7e000000000000000000104",null).TryCast<CompoundItem>();
      var grid=new EFT.InventoryLogic.Grid("icon-regression",1,4,false,false,Array.Empty<ItemFilter>(),parent,int.MaxValue);
      grid.AddItemWithoutRestrictions(item,new LocationInGrid(0,0,ItemRotation.Horizontal));
      var next=grid.FindFreeSpace(factory.CreateItem(factory.NextId.ToString(),sword.Id,null));
      placement=new{occupiedCells=Enumerable.Range(0,grid.Layout.Count).Count(i=>grid.Layout[i]),secondSwordFits=next!=null,secondX=next?.x,secondY=next?.y};
     }
     inventoryTemplateDiagnostics.Add(new{id=sword.Id,actual=item.StringTemplateId,width=item.Width,height=item.Height,cellWidth=size.X,cellHeight=size.Y,templateWidth=item.Template.Width,templateHeight=item.Template.Height,factoryWidth=template.Width,factoryHeight=template.Height,sameTemplate=item.Template.Pointer==template.Pointer,placement});
    }catch(Exception e){inventoryTemplateDiagnostics.Add(new{id=sword.Id,error=e.Message});}}
   }
   var views=new List<object>();foreach(var view in Resources.FindObjectsOfTypeAll<ItemView>())try{
    var item=view?.Item;if(item==null||Swords.Find(item.StringTemplateId)==null||view!.IsKilled||!view.gameObject.activeInHierarchy)continue;var image=view.MainImage;var size=item.CalculateCellSize();var rect=view.RectTransform;
    views.Add(new{id=item.StringTemplateId,view=view.GetIl2CppType().Name,cellWidth=size.X,cellHeight=size.Y,rotation=view.ItemRotation.ToString(),viewWidth=rect?.rect.width,viewHeight=rect?.rect.height,image=image==null?null:new{rectWidth=image.rectTransform.rect.width,rectHeight=image.rectTransform.rect.height,sprite=image.sprite?.name,spriteWidth=image.sprite?.rect.width,spriteHeight=image.sprite?.rect.height,scaleX=image.transform.localScale.x,scaleY=image.transform.localScale.y}});
   }catch{}
   File.WriteAllText(Path.Combine(context!.DataDirectory,"inventory-presentation.json"),JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,templates=inventoryTemplateDiagnostics,views}));
  }catch(Exception e){context!.Log(Local.ModHost.LogLevel.Warning,"Inventory presentation diagnostics: "+e.Message);}
 }
}
