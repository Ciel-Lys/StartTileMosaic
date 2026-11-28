using System;
using System.IO;
using System.Linq;
using System.Text;

namespace TileMosaic
{
    public static class TileRuntime
    {
        internal const string RotationServiceName = "TileGenie.Rotation";
        internal static void WriteBackgroundFiles(string directory, Project project)
        {
            string config = Path.Combine(directory, "config.js");
            if (!File.Exists(config))
            {
                long anchor = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
                string widths = string.Join(",", Enumerable.Range(0, project.TileCount)
                    .Select(index => project.ShapeAt(project.TileKeyAt(index)) == TileShape.Medium ? "false" : "true"));
                File.WriteAllText(config, "var tileConfig={frames:" + project.Frames.Count +
                    ",minutes:" + project.CycleMinutes + ",columns:" + project.Columns +
                    ",count:" + project.TileCount + ",names:" + (project.ShowNames ? "true" : "false") +
                    ",wideTiles:[" + widths + "],anchor:" + anchor + "};", Encoding.UTF8);
            }

            File.WriteAllText(Path.Combine(directory, "tiles.js"), TilesJs, Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "background.js"),
                "importScripts('config.js','tiles.js'); try { refreshTiles(); } catch(e) { " +
                "Windows.Storage.ApplicationData.current.localSettings.values['rotationError']=String(e); } close();", Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "rotation-service.js"), RotationServiceJs, Encoding.UTF8);
        }

        const string RotationServiceJs = @"
importScripts('config.js','tiles.js');
var task=Windows.UI.WebUI.WebUIBackgroundTaskInstance.current;
var connection=task.triggerDetails.appServiceConnection;
var lifetime=task.getDeferral(), finished=false;
function finish(){
 if(!finished){finished=true;lifetime.complete();close();}
}
task.addEventListener('canceled',finish);
connection.addEventListener('requestreceived',function(e){
 var request=e.request, deferral=e.getDeferral();
 function reply(state,error){
  var values=new Windows.Foundation.Collections.ValueSet();
  values.insert('state',state);
  if(error)values.insert('error',String(error));
  function complete(){deferral.complete();finish();}
  try{request.sendResponseAsync(values).done(complete,complete);}catch(e){complete();}
 }
 if(request.message.hasKey('renewOnly') && request.message.lookup('renewOnly')===true){
  var settings=Windows.Storage.ApplicationData.current.localSettings.values;
  var scheduled=Date.parse(settings['lastScheduled']);
  if(settings['rotationTimerReady']===true && Date.now()-scheduled<3600000){reply('ready');return;}
 }
 try{refreshTiles();}catch(e){reply('failed',e);return;}
 function ready(state,error){
  Windows.Storage.ApplicationData.current.localSettings.values['rotationTimerReady']=state==='ready';
  reply(state,error);
 }
 registerRotation().done(function(){ready('ready');},function(e){ready('scheduled',e);});
});
";
        internal const string TilesJs=@"
function pad(n){return ('000'+n).slice(-3);}
function currentFrame(time){return Math.max(0,Math.floor((time-tileConfig.anchor)/(tileConfig.minutes*60000)))%tileConfig.frames;}
function tileXml(frame,index){
 var stem='ms-appx:///Assets/F'+pad(frame)+'-Tile'+pad(index);
 var xml='<tile><visual branding=""'+(tileConfig.names?'name':'none')+'"">';
 xml+='<binding template=""TileSquare150x150Image""><image id=""1"" src=""'+stem+'-150.png""/></binding>';
 xml+='<binding template=""TileSquare310x310Image""><image id=""1"" src=""'+stem+'-310.png""/></binding>';
 var wide=tileConfig.wideTiles?tileConfig.wideTiles[index-1]:tileConfig.wide[(index-1)%tileConfig.columns];
 if(wide)xml+='<binding template=""TileWide310x150Image""><image id=""1"" src=""'+stem+'-Wide.png""/></binding>';
 var doc=new Windows.Data.Xml.Dom.XmlDocument();doc.loadXml(xml+'</visual></tile>');return doc;
}
function refreshTiles(){
 var N=Windows.UI.Notifications,now=Date.now(),period=tileConfig.minutes*60000;
 var next=tileConfig.anchor+(Math.floor((now-tileConfig.anchor)/period)+1)*period;
 for(var index=1;index<=tileConfig.count;index++){
  var key='Picture'+pad(index);
  if(!Windows.UI.StartScreen.SecondaryTile.exists(key))continue;
  var updater=N.TileUpdateManager.createTileUpdaterForSecondaryTile(key);
  updater.enableNotificationQueue(false);
  var old=updater.getScheduledTileNotifications();
  if(typeof old.length==='number'){
   for(var j=0;j<old.length;j++)updater.removeFromSchedule(old[j]);
  }else{
   for(var j=0;j<old.size;j++)updater.removeFromSchedule(old.getAt(j));
  }
  updater.update(new N.TileNotification(tileXml(currentFrame(now),index)));
  if(tileConfig.frames>1)for(var n=0;n<96;n++){
   var when=next+n*period,notification=new N.ScheduledTileNotification(tileXml(currentFrame(when),index),new Date(when));
   notification.id='frame'+n;updater.addToSchedule(notification);
  }
 }
 Windows.Storage.ApplicationData.current.localSettings.values['lastScheduled']=new Date().toISOString();
}
function registerRotation(){
 var B=Windows.ApplicationModel.Background;
 return B.BackgroundExecutionManager.requestAccessAsync().then(function(access){
  var status=B.BackgroundAccessStatus;
  if(access===status.denied||access===status.deniedByUser||access===status.deniedBySystem||access===status.unspecified)
   throw new Error('Windows denied background access.');
  var iterator=B.BackgroundTaskRegistration.allTasks.first();
  while(iterator.hasCurrent){
   if(iterator.current.value.name==='TileMosaicRotation')return;
   iterator.moveNext();
  }
  var builder=new B.BackgroundTaskBuilder();
  builder.name='TileMosaicRotation';
  builder.taskEntryPoint='background.js';
  builder.setTrigger(new B.TimeTrigger(30,false));
  builder.register();
 });
}
";
    }
}
